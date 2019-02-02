using System;
using scp4aiur;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.EventSystem.Events;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Linq;
using Smod2;
using UnityEngine;
using Console = GameConsole.Console;
using Random = UnityEngine.Random;

namespace Blackout
{
	public class EventHandlers : IEventHandlerWaitingForPlayers, IEventHandlerRoundStart,
		IEventHandlerDoorAccess, IEventHandlerTeamRespawn, IEventHandlerPlayerHurt,
		IEventHandlerSummonVehicle, IEventHandlerRoundRestart, IEventHandlerCheckRoundEnd,
		IEventHandlerPlayerTriggerTesla, IEventHandlerElevatorUse, IEventHandlerWarheadStartCountdown,
		IEventHandlerSetRole, IEventHandlerRecallZombie, IEventHandlerInfected, IEventHandlerCallCommand,
		IEventHandlerGeneratorInsertTablet, IEventHandlerGeneratorEjectTablet, IEventHandlerGeneratorFinish,
		IEventHandler079TeslaGate, IEventHandler079Elevator, IEventHandler079AddExp
	{
		private const string BroadcastExplanation = "<b><color=#f22>Раунд:Blackout.\n Для просмотра информации нажмите [ Ё ] или [~].</color></b>";
		private const string ConsoleExplaination =
		  "\nЭто Blackout!\n" +
		  "В Blackout вы или учёный, чумной доктор (049), или Диспетчер (SCP-079). Свет отключён и выходы заблокированы." +
		  "Единственный путь сбежать - активировать все генераторы, после чего следуйте к оружейной в камере содержания 049, оружейной на тройной развилке, или же КПП." +
		  "Все карточки заменены на О5 карточки. Когда вы сбежите, вы получите снаряжение, чтобы убить всех чумных докторов (049). " +
		  "У вас есть 10 минут до того, как взорвётся боеголовка.";

		private const float Cassie049BreachDelay = 8.25f;

		private readonly BlackoutPlugin plugin;

		private bool roundStarted;
		private bool slendiesFree;
		private int generatorPowerups;

		private Dictionary<Player, Vector> slendySpawns;
		private readonly Dictionary<int, Player> scientists;
		private readonly Dictionary<int, Player> slendies;
		private readonly Dictionary<int, Player> fcs;
		private Vector3[] uspRespawns;

		private List<Smod2.API.TeslaGate> teslas;

		public EventHandlers(BlackoutPlugin plugin)
		{
			this.plugin = plugin;

			scientists = new Dictionary<int, Player>();
			slendies = new Dictionary<int, Player>();
			fcs = new Dictionary<int, Player>();
		}

		private IEnumerable<float> TimingSetItems(float delay, Player player, IEnumerable<int> items)
		{
			yield return delay;

			SetItems(player, items);
		}

		private IEnumerable<float> TimingSpawnUsps(float delay, IEnumerable<Vector3> spawns, Inventory inventory, WeaponManager.Weapon usp)
		{
			yield return delay;

			plugin.Server.Map.AnnounceCustomMessage("U S P NOW AVAILABLE");

			// Spawn USPs with random sight, heavy barrel, and flashlight :ok_hand:
			foreach (Vector3 spawn in spawns)
			{
				inventory.SetPickup((int)ItemType.USP, usp.maxAmmo, spawn, Quaternion.Euler(0, 0, 0), Random.Range(0, usp.mod_sights.Length), 2, 1);
			}
		}

		public IEnumerable<float> TimingRoundStart()
		{
			const float cassieDelay = 8.6f;
			const float flickerDelay = 0.4f;

			// Cassie and flicker delay is subtracted in order to start the round by that time
			yield return plugin.StartDelay - (cassieDelay + flickerDelay);

			plugin.Server.Map.AnnounceCustomMessage("LIGHT SYSTEM SCP079RECON6");
			yield return cassieDelay;

			Timing.Run(TimingBlackoutFlicker());

			int maxTimeMinutes = Mathf.FloorToInt(plugin.ScpVictoryTime / 60);
			float remainder = plugin.ScpVictoryTime - maxTimeMinutes * 60;
			Timing.Run(TimingTimeAnnouncements(remainder, maxTimeMinutes));

			foreach (Player slendy in slendies.Values)
			{
				slendy.ChangeRole(Role.SCP_049, false, false);

				//Teleport to 106 as a prison
				slendy.Teleport(plugin.Server.Map.GetRandomSpawnPoint(Role.SCP_106));

				slendy.PersonalBroadcast(5, $"<color=#ccc>Вас освободят через</color> {plugin.SlendyReleaseDelay} <color=#ccc>секунд.</color>", false);
			}

			foreach (Player player in scientists.Values)
			{
				SetItems(player, plugin.GameItems);
			}

			foreach (Player player in fcs.Values)
			{
				player.ChangeRole(Role.SCP_079);
			}

			Timing.Run(TimingReleaseSlendies(plugin.SlendyReleaseDelay - Cassie049BreachDelay));
			UpdateUspRespawns(uspRespawns);

			yield return 2f;

			roundStarted = true;
		}

		private IEnumerable<float> TimingReleaseSlendies(float delay)
		{
			yield return delay;

			plugin.Server.Map.AnnounceCustomMessage("CAUTION . SCP 0 4 9 CONTAINMENT BREACH IN PROGRESS");
			yield return Cassie049BreachDelay;

			slendiesFree = true;
			foreach (KeyValuePair<Player, Vector> slendy in slendySpawns)
			{
				slendy.Key.Teleport(slendy.Value);
			}
		}

		/// <summary>
		/// Causes a blackout to happen in all of HCZ.
		/// </summary>
		public IEnumerable<float> TimingBlackoutFlicker()
		{
			while (true)
			{
				Generator079.mainGenerator.CallRpcOvercharge();

				if (plugin.TeslaFlicker)
				{
					foreach (Smod2.API.TeslaGate tesla in teslas)
					{
						tesla.Activate(true);
					}
				}

				yield return 10 + plugin.FlickerlightDuration;
			}
		}

		/// <summary>
		/// Announcements for how much time is left and nuke at the last minute of the game
		/// </summary>
		/// <param name="waitTime">Amount of time before the countdown begins.</param>
		/// <param name="minutes">Minutes remaining</param>
		public IEnumerable<float> TimingTimeAnnouncements(float waitTime, int minutes)
		{
			yield return waitTime;

			for (int i = minutes; i > 0; i--)
			{
				string cassieLine = plugin.MinuteAnnouncements.Contains(minutes) ? $"{minutes} MINUTE{(minutes == 1 ? "" : "S")} REMAINING" : "";

				if (minutes == 1)
				{
					if (!string.IsNullOrWhiteSpace(cassieLine))
					{
						cassieLine += " . ";
					}

					const float nukeStart = 50f; // Makes sure that the nuke starts when the siren is almost silent so it sounds like it just started

					plugin.Server.Map.AnnounceCustomMessage(cassieLine + "ALPHA WARHEAD AUTOMATIC REACTIVATION SYSTEM ENGAGED");
					yield return 60f - nukeStart;

					AlphaWarheadController.host.StartDetonation();
					AlphaWarheadController.host.NetworktimeToDetonation = nukeStart;

					yield return nukeStart;
				}
				else if (cassieLine != string.Empty)
				{
					plugin.Server.Map.AnnounceCustomMessage(cassieLine);
					yield return 60f;
				}
			}
		}

		public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
		{
			plugin.RefreshConfig();
		}

		public void OnRoundStart(RoundStartEvent ev)
		{
			if (!plugin.ActiveNextRound && !plugin.Toggled)
			{
				return;
			}

			plugin.Active = true;
			plugin.ActiveNextRound = false;
			roundStarted = false;
			slendiesFree = false;
			generatorPowerups = 0;

			GamePrep(plugin.Server.GetPlayers());

			Timing.Run(TimingRoundStart());
		}

		public void OnCheckRoundEnd(CheckRoundEndEvent ev)
		{
			if (plugin.Active)
			{
				if (!roundStarted)
				{
					ev.Status = ROUND_END_STATUS.ON_GOING;
				}
				else
				{
					Smod2.API.Team[] players = PlayerManager.singleton.players.Select(x =>
					{
						CharacterClassManager ccm = x.GetComponent<CharacterClassManager>();
						return (Smod2.API.Team) ccm.klasy[ccm.curClass].team;
					}).ToArray();

					bool mtf = players.Count(x => x == Smod2.API.Team.NINETAILFOX) > 0;
					bool scp = players.Count(x => x == Smod2.API.Team.SCP) - fcs.Count > 0;
					bool scientist = players.Count(x => x == Smod2.API.Team.SCIENTIST) > 0;

					if (scientist)
					{
						if (!scp)
						{
							ev.Status = ROUND_END_STATUS.MTF_VICTORY;
						}
					}
					else
					{
						if (mtf)
						{
							if (!scp)
							{
								ev.Status = ROUND_END_STATUS.MTF_VICTORY;
							}
						}
						else
						{
							ev.Status = ROUND_END_STATUS.SCP_VICTORY;
						}
					}
				}
			}
		}

		public void OnRoundRestart(RoundRestartEvent ev)
		{
			plugin.Active = false;

			scientists.Clear();
			slendies.Clear();
			fcs.Clear();
		}

		public void OnDoorAccess(PlayerDoorAccessEvent ev)
		{
			if (plugin.Active)
			{
				if (roundStarted)
				{
					switch (ev.Door.Name)
					{
						case "HCZ_ARMORY":
						case "049_ARMORY":
						case "ENTRANCE_CHECKPOINT":
							if (ev.Player.TeamRole.Role == Role.SCIENTIST && generatorPowerups >= 5) //if escape is known to be ready, and if not check if it is
							{
								EscapeScientist(ev.Player);
							}

							ev.Destroy = false;
							break;
					}
				}
				else
				{
					ev.Door.Open = false;
				}
			}
		}

		public void OnElevatorUse(PlayerElevatorUseEvent ev)
		{
			if (plugin.Active && (ev.Elevator.ElevatorType == ElevatorType.LiftA || ev.Elevator.ElevatorType == ElevatorType.LiftB))
			{
				ev.AllowUse = false;
			}
		}

		public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
		{
			if (plugin.Active && plugin.TeslaFlicker)
			{
				ev.Triggerable = false;
			}
		}

		public void OnPlayerHurt(PlayerHurtEvent ev)
		{
			if (plugin.Active && ev.DamageType == DamageType.NUKE && ev.Player.TeamRole.Team == Smod2.API.Team.SCP)
			{
				ev.Damage = 0;
			}
		}

		public void OnSetRole(PlayerSetRoleEvent ev)
		{
			if (plugin.Active)
			{
				switch (ev.Role)
				{
					case Role.SCIENTIST:
						ev.Player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));

						int[] items;
						if (roundStarted)
						{
							ev.Player.PersonalBroadcast(10, "Вы <color=#FFFF7C>Ученый</color>.\n Избегайте <color=#f00>SCP-049</color>. Запустите генераторы и сбегите.\nКооперируйтесь с <color=#0096FF>Диспечетром</color>.", false);

							if (!scientists.ContainsKey(ev.Player.PlayerId))
							{
								fcs.Remove(ev.Player.PlayerId);
								slendies.Remove(ev.Player.PlayerId);

								scientists.Add(ev.Player.PlayerId, ev.Player);
							}

							items = plugin.GameItems;
						}
						else
						{
							items = plugin.WaitingItems;
						}

						ev.Items = items.Cast<ItemType>().ToList();
						break;

					case Role.SCP_049:
						ev.Player.PersonalBroadcast(10, "Вы <color=#f00>SCP-049</color>.\nНе дайте <color=#FFFF7C>Ученым</color> сбежать.\nИзбегайте <color=#0096FF>Диспетчера</color>.", false);

						if (!slendies.ContainsKey(ev.Player.PlayerId))
						{
							fcs.Remove(ev.Player.PlayerId);
							scientists.Remove(ev.Player.PlayerId);

							slendies.Add(ev.Player.PlayerId, ev.Player);
						}

						ev.Player.Teleport(slendiesFree
							? PluginManager.Manager.Server.Map.GetRandomSpawnPoint(plugin.SlendySpawnPoints[Random.Range(0, plugin.SlendySpawnPoints.Length)])
							: PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_106));
						break;

					case Role.SCP_079:
						ev.Player.PersonalBroadcast(10, "Вы <color=#0096FF>Диспетчер</color>.\nПомогите <color=#FFFF7C>Ученым</color> запустить генераторы,\n а также сбежать и убить <color=#f00>SCP-049</color>.", false);

						if (!fcs.ContainsKey(ev.Player.PlayerId))
						{
							scientists.Remove(ev.Player.PlayerId);
							slendies.Remove(ev.Player.PlayerId);

							fcs.Add(ev.Player.PlayerId, ev.Player);
						}

						ev.Player.Scp079Data.Level = 1;
						ev.Player.Scp079Data.APPerSecond = 4f;
						ev.Player.Scp079Data.MaxAP = 80f;
						ev.Player.Scp079Data.ExpToLevelUp = 999;
						break;

					default:
						fcs.Remove(ev.Player.PlayerId);
						scientists.Remove(ev.Player.PlayerId);
						slendies.Remove(ev.Player.PlayerId);
						break;
				}
			}
		}

		public void OnTeamRespawn(TeamRespawnEvent ev)
		{
			if (plugin.Active)
			{
				ev.SpawnChaos = true;
				ev.PlayerList = new List<Player>();
			}
		}

		public void OnSummonVehicle(SummonVehicleEvent ev)
		{
			if (plugin.Active)
			{
				ev.AllowSummon = false;
			}
		}

		public void OnStartCountdown(WarheadStartEvent ev)
		{
			if (plugin.Active)
			{
				ev.OpenDoorsAfter = false;
			}
		}

		public void OnRecallZombie(PlayerRecallZombieEvent ev)
		{
			if (plugin.Active)
			{
				ev.AllowRecall = false;
			}
		}

		public void OnPlayerInfected(PlayerInfectedEvent ev)
		{
			if (plugin.Active)
			{
				ev.InfectTime = 0;
			}
		}

		public void OnCallCommand(PlayerCallCommandEvent ev)
		{
			if (ev.Command.StartsWith("fc") && (ev.Command.Length == 2 || ev.Command[2] == ' '))
			{
				if (!plugin.Active)
				{
					ev.ReturnMessage = "Blackout is not running this round.";
					return;
				}

				if (ev.Command.Length < 4)
				{
					ev.ReturnMessage = "Please specify your message.";
					return;
				}

				ev.ReturnMessage = SendFcMessage(ev.Player, ev.Command.Substring(3));
			}
		}

		/// <summary>
		/// Spawns everyone in waiting room and caches spawns and players.
		/// </summary>
		/// <param name="players">All the players involved in the </param>
		public void GamePrep(IReadOnlyCollection<Player> players)
		{
			Pickup[] pickups = Object.FindObjectsOfType<Pickup>();

			uspRespawns = pickups.Where(x => x.info.itemId == (int)ItemType.E11_STANDARD_RIFLE).Select(x => x.info.position).ToArray();
			UpdateItems(pickups);
			SetMapBoundaries();
			RandomizePlayers(players);

			// Set every class to scientist
			foreach (Player player in players)
			{
				player.ChangeRole(Role.SCIENTIST);
			}

			// Set 049 spawn points
			slendySpawns = GenerateSpawnPoints(slendies.Values);
			teslas = plugin.Server.Map.GetTeslaGates();

			// Inform players
			plugin.Server.Map.Broadcast(10, BroadcastExplanation, false);
			foreach (Player player in players)
			{
				player.SendConsoleMessage(ConsoleExplaination);
			}
		}

		/// <summary>
		/// Locks entrance checkpoint, LCZ elevators, and nuke button. Also sends the 049 elevator down.
		/// </summary>
		public void SetMapBoundaries()
		{
			// Lock LCZ elevators
			foreach (Elevator elevator in plugin.Server.Map.GetElevators())
			{
				switch (elevator.ElevatorType)
				{
					case ElevatorType.SCP049Chamber when elevator.ElevatorStatus == ElevatorStatus.Up:
					case ElevatorType.LiftA when elevator.ElevatorStatus == ElevatorStatus.Down:
					case ElevatorType.LiftB when elevator.ElevatorStatus == ElevatorStatus.Down:
						elevator.Use();
						break;
				}
			}

			List<Smod2.API.Door> doors = plugin.Server.Map.GetDoors();
			doors.First(x => x.Name == "CHECKPOINT_ENT").Locked = true;
			doors.First(x => x.Name == "HCZ_ARMORY").Locked = true;
			doors.First(x => x.Name == "049_ARMORY").Locked = true;

			AlphaWarheadController.host.SetLocked(true);
		}

		/// <summary>
		/// Removes HIDs, replaces keycards
		/// </summary>
		public static void UpdateItems(Pickup[] pickups)
		{
			// Delete all micro HIDs or USPs
			foreach (Pickup gun in pickups.Where(x =>
				x.info.itemId == (int)ItemType.MICROHID ||
				x.info.itemId == (int)ItemType.USP ||
				x.info.itemId == (int)ItemType.E11_STANDARD_RIFLE
				))
				gun.Delete();

			foreach (Pickup keycard in pickups.Where(x => -1 < x.info.itemId && x.info.itemId < 12)) // All keycard items
			{
				Pickup.PickupInfo info = keycard.info;
				info.itemId = (int)ItemType.O5_LEVEL_KEYCARD;
				keycard.Networkinfo = info;
			}
		}

		/// <summary>
		/// Gets all the possible timed-USP spawn points.
		/// </summary>
		/// <param name="allPickups">Every pickup on the map.</param>
		public static IEnumerable<Vector3> UspSpawnPoints(IEnumerable<Pickup> allPickups)
		{
			return allPickups.Where(x => x.info.itemId == (int)ItemType.E11_STANDARD_RIFLE).Select(x => x.info.position);
		}

		/// <summary>
		/// Starts the timer on USP respawning.
		/// </summary>
		/// <param name="spawns">Spawn positions for USPs.</param>
		public void UpdateUspRespawns(IEnumerable<Vector3> spawns)
		{
			GameObject host = GameObject.Find("Host");

			Timing.Run(TimingSpawnUsps(plugin.UspTime, spawns, host.GetComponent<Inventory>(), host.GetComponent<WeaponManager>().weapons.First(x => x.inventoryID == (int)ItemType.USP)));
		}

		/// <summary>
		/// Evenly distributes spawnpoints randomly to each slendy.
		/// </summary>
		/// <param name="slendies">Slendies that are going to spawn.</param>
		public Dictionary<Player, Vector> GenerateSpawnPoints(IEnumerable<Player> slendies)
		{
			List<Role> availableSpawns = plugin.SlendySpawnPoints.ToList();
			return slendies.ToDictionary(x => x, x =>
			{
				// Get role and remove it from pool
				Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
				availableSpawns.Remove(spawnRole);

				// Fill pool if it overflows
				if (availableSpawns.Count == 0)
				{
					availableSpawns.AddRange(plugin.SlendySpawnPoints);
				}

				// Set point to random point from role
				return plugin.Server.Map.GetRandomSpawnPoint(spawnRole);
			});
		}

		/// <summary>
		/// Randomizes the slendy players and scientist players.
		/// </summary>
		/// <param name="players">All the players that are playing the </param>
		public void RandomizePlayers(IEnumerable<Player> players)
		{
			foreach (Player player in players)
			{
				scientists.Add(player.PlayerId, player);
			}

			if (scientists.Count == 1)
			{
				return;
			}

			// Get percentage of 049s based on players
			int slendyCount = Mathf.CeilToInt(scientists.Count * plugin.PercentSlendies);

			// Get random 049s
			for (int i = 0; i < slendyCount; i++)
			{
				KeyValuePair<int, Player> slendy = scientists.ElementAt(Random.Range(0, scientists.Count));

				slendies.Add(slendy.Key, slendy.Value);
				scientists.Remove(slendy.Key);
			}

			if (scientists.Count == 1)
			{
				return;
			}

			// Get percentage of FCs based on players
			int fcCount = Mathf.CeilToInt(scientists.Count * plugin.PercentFacilityControl);

			// Get random FCs
			for (int i = 0; i < fcCount; i++)
			{
				KeyValuePair<int, Player> fc = scientists.ElementAt(Random.Range(0, scientists.Count));

				fcs.Add(fc.Key, fc.Value);
				scientists.Remove(fc.Key);
			}
		}

		/// <summary>
		/// Teleports all slendies to 106 to keep them from doing anything.
		/// </summary>
		/// <param name="slendies">Slendies to imprison.</param>
		public void ImprisonSlendies(IEnumerable<Player> slendies)
		{
			foreach (Player slendy in slendies)
			{
				slendy.ChangeRole(Role.SCP_049, false, false);

				//Teleport to 106 as a prison
				slendy.Teleport(plugin.Server.Map.GetRandomSpawnPoint(Role.SCP_106));

				slendy.PersonalBroadcast(5, $"<color=#ccc>Вас освободят через</color> {plugin.SlendyReleaseDelay} <color=#ccc>секунд.</color>", false);
			}
		}

		/// <summary>
		/// Spawns a scientist with gamemode spawn and items.
		/// </summary>
		/// <param name="player">Player to spawn.</param>
		public void SpawnRole(Player player, Role role)
		{
			switch (role)
			{
				case Role.SCIENTIST:
					player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));

					int[] items;
					if (roundStarted)
					{
						player.PersonalBroadcast(10, "Вы <color=#FFFF7C>Ученый</color>.\n Избегайте <color=#f00>SCP-049</color>. Запустите генераторы и сбегите.\nКооперируйтесь с <color=#0096FF>Диспечетром</color>.", false);

						if (!scientists.ContainsKey(player.PlayerId))
						{
							fcs.Remove(player.PlayerId);
							slendies.Remove(player.PlayerId);

							scientists.Add(player.PlayerId, player);
						}

						items = plugin.GameItems;
					}
					else
					{
						items = plugin.WaitingItems;
					}

					Timing.Run(TimingSetItems(0, player, items));
					break;

				case Role.SCP_049:
					player.PersonalBroadcast(10, "Вы <color=#f00>SCP-049</color>.\nНе дайте <color=#FFFF7C>Ученым</color> сбежать.\nИзбегайте <color=#0096FF>Диспетчера</color>.", false);

					if (!slendies.ContainsKey(player.PlayerId))
					{
						fcs.Remove(player.PlayerId);
						scientists.Remove(player.PlayerId);

						slendies.Add(player.PlayerId, player);
					}

					player.Teleport(slendiesFree
						? PluginManager.Manager.Server.Map.GetRandomSpawnPoint(plugin.SlendySpawnPoints[Random.Range(0, plugin.SlendySpawnPoints.Length)])
						: PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_106));
					break;

				case Role.SCP_079:
					player.PersonalBroadcast(10, "Вы <color=#0096FF>Диспетчер</color>.\nПомогите <color=#FFFF7C>Ученым</color> запустить генераторы,\n а также сбежать и убить <color=#f00>SCP-049</color>.", false);

					if (!fcs.ContainsKey(player.PlayerId))
					{
						scientists.Remove(player.PlayerId);
						slendies.Remove(player.PlayerId);

						fcs.Add(player.PlayerId, player);
					}

					player.Scp079Data.Level = 1;
					player.Scp079Data.APPerSecond = 4f;
					player.Scp079Data.MaxAP = 80f;
					player.Scp079Data.ExpToLevelUp = 999;
					break;
			}
		}

		/// <summary>
		/// Overwrites the players inventory with an array of items.
		/// </summary>
		/// <param name="player">Player whose inventory should be set.</param>
		/// <param name="items">Items the players should have.</param>
		public void SetItems(Player player, IEnumerable<int> items)
		{
			foreach (Smod2.API.Item item in player.GetInventory())
			{
				item.Remove();
			}

			GameObject playerObj = (GameObject)player.GetGameObject();
			Inventory inv = playerObj.GetComponent<Inventory>();
			WeaponManager manager = playerObj.GetComponent<WeaponManager>();

			Console console = Object.FindObjectOfType<Console>();
			int vanillaItems = Enum.GetValues(typeof(ItemType)).Length;
			foreach (int item in items)
			{
				int i = WeaponManagerIndex(manager, item);

				if (item <= vanillaItems)
				{
					int flashlight;

					switch (item)
					{
						case (int)ItemType.E11_STANDARD_RIFLE:
							flashlight = 4;
							break;

						case (int)ItemType.P90:
						case (int)ItemType.USP:
						case (int)ItemType.COM15:
							flashlight = 1;
							break;

						case (int)ItemType.RADIO:
							player.RadioStatus = RadioStatus.SHORT_RANGE;
							goto default;

						default:
							player.GiveItem((ItemType)item);
							continue;
					}

					inv.AddNewItem(item, manager.weapons[i].maxAmmo, manager.modPreferences[i, 0], manager.modPreferences[i, 1], flashlight);
				}
				else
				{
					// Support for ItemManager items
					console.TypeCommand($"imgive {player.PlayerId} {item}");
				}
			}
		}

		/// <summary>
		/// Sets role, handles items, and handles round logic of an escaped scientist.
		/// </summary>
		/// <param name="player">Scientist that escaped</param>
		public void EscapeScientist(Player player)
		{
			// Drop items before converting
			foreach (Smod2.API.Item item in player.GetInventory())
				item.Drop();

			// Convert only class, no inventory or spawn point
			player.ChangeRole(Role.NTF_SCIENTIST, false, false, false);

			SetItems(player, plugin.EscapeItems);

			plugin.Server.Round.Stats.ScientistsEscaped++;
		}

		public string SendFcMessage(Player sender, string message)
		{
			switch (sender.TeamRole.Role)
			{
				case Role.SCIENTIST:
				{
					foreach (Player fc in fcs.Values)
					{
						fc.PersonalBroadcast(7, $"<color=#FFFF7C>{sender.Name}</color>: {message}", false);
					}

					return $"<color=#0f0>Sent message to {fcs.Count} Facility Control member{(fcs.Count == 1 ? "s" : "")}.</color>";
				}

				case Role.SCP_079:
				{
					foreach (Player scientist in scientists.Values)
					{
						scientist.PersonalBroadcast(7, $"<color=#0096FF>{sender.Name}</color>: {message}", false);
					}

					return $"<color=#0f0>Sent message to {scientists.Count} scientist{(scientists.Count == 1 ? "s" : "")}.</color>";
				}

				default:
					return "You do not have access to the Facility Control Messaging System.";
			}
		}

		/// <summary>
		/// Gets the index of an item in WeaponManager from a weapon ItemType.
		/// </summary>
		/// <param name="manager">A player's weapon manager.</param>
		/// <param name="item">The ItemType of the weapon.</param>
		public static int WeaponManagerIndex(WeaponManager manager, int item)
		{
			// Get weapon index in WeaponManager
			int weapon = -1;
			for (int i = 0; i < manager.weapons.Length; i++)
			{
				if (manager.weapons[i].inventoryID == item)
				{
					weapon = i;
				}
			}

			return weapon;
		}

		public void OnGeneratorInsertTablet(PlayerGeneratorInsertTabletEvent ev)
		{
			if (plugin.Active)
			{
				ev.Generator.TimeLeft = plugin.GeneratorTime;

				foreach (Player player in scientists.Values.Concat(slendies.Values))
				{
					player.PersonalBroadcast(5, $"<b><color=#ccc>Генератор запускается в {ev.Generator.Room.RoomType.ToString().Replace('_', ' ')}.</color></b>", false);
				}
			}
		}

		public void OnGeneratorEjectTablet(PlayerGeneratorEjectTabletEvent ev)
		{
			if (plugin.Active && !ev.Generator.Engaged)
			{
				foreach (Player player in scientists.Values.Concat(slendies.Values))
				{
					player.PersonalBroadcast(5, $"<b><color=#ccc>Генератор был отключен в {ev.Generator.Room.RoomType.ToString().Replace('_', ' ')}.</color></b>", false);
				}
			}
		}

		public void OnGeneratorFinish(GeneratorFinishEvent ev)
		{
			if (plugin.Active)
			{
				generatorPowerups++;

				foreach (Player player in scientists.Values.Concat(slendies.Values))
				{
					player.PersonalBroadcast(5, $"<b><color=#ccc>Генератор был запущен в {ev.Generator.Room.RoomType.ToString().Replace('_', ' ')}.</color></b>", false);
				}
			}
		}

		public void On079TeslaGate(Player079TeslaGateEvent ev)
		{
			if (plugin.Active)
			{
				ev.Allow = false;
			}
		}

		public void On079Elevator(Player079ElevatorEvent ev)
		{
			if (plugin.Active && ev.Elevator.ElevatorType != ElevatorType.SCP049Chamber && ev.Elevator.ElevatorType != ElevatorType.WarheadRoom)
			{
				ev.Allow = false;
			}
		}

		public void On079AddExp(Player079AddExpEvent ev)
		{
			if (plugin.Active)
			{
				ev.ExpToAdd = 0;
			}
		}
	}
}
