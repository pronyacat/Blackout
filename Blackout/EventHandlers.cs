﻿using System;
using System.Collections.Generic;
using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.EventSystem.Events;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Blackout
{
    public class EventHandlers : IEventHandlerWaitingForPlayers, IEventHandlerRoundStart, 
        IEventHandlerDoorAccess, IEventHandlerTeamRespawn, IEventHandlerPlayerHurt, 
        IEventHandlerSummonVehicle, IEventHandlerRoundRestart, IEventHandlerCheckRoundEnd,
        IEventHandlerPlayerTriggerTesla, IEventHandlerElevatorUse, IEventHandlerWarheadStartCountdown,
		IEventHandlerSetRole
    {
        private readonly List<int> timers;

        private bool isRoundStarted;
        private bool escapeReady;

        private Broadcast broadcast;
        private MTFRespawn cassie;

        private string[] activeGenerators;
        private List<Smod2.API.TeslaGate> teslas;

        #region Config
        public bool giveFlashlights;
		public bool giveFlashbangs;

        public float percentLarrys;

        public float startDelay;
		public float slendyDelay;
        public float maxTime;
        public float uspTime;
        public int[] minuteAnnouncements;

        public bool teslaFlicker;
        #endregion

        public EventHandlers()
        {
            timers = new List<int>();
        }

		public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
		{
			Plugin.validRanks = Plugin.instance.GetConfigList("bo_ranks");

			giveFlashlights = Plugin.instance.GetConfigBool("bo_flashlights");
			giveFlashbangs = Plugin.instance.GetConfigBool("bo_flashbangs");

			percentLarrys = Plugin.instance.GetConfigFloat("bo_slendy_percent");

		    startDelay = Plugin.instance.GetConfigFloat("bo_start_delay");
			slendyDelay = Plugin.instance.GetConfigFloat("bo_slendy_delay");
            maxTime = Plugin.instance.GetConfigFloat("bo_max_time");
			uspTime = Plugin.instance.GetConfigFloat("bo_usp_time");
		    minuteAnnouncements = Plugin.instance.GetConfigIntList("bo_announce_times");

            teslaFlicker = Plugin.instance.GetConfigBool("bo_tesla_flicker");
		}

        public void OnRoundStart(RoundStartEvent ev)
        {
            #region Check and set if active
            if (!Plugin.activeNextRound)
            {
                return;
            }

            Plugin.active = true;
			if (!Plugin.toggled)
				Plugin.activeNextRound = false;
		    isRoundStarted = false;
            #endregion

            // Get announcement variables
            broadcast = Object.FindObjectOfType<Broadcast>();
            cassie = PlayerManager.localPlayer.GetComponent<MTFRespawn>();

            #region Map boundaries
            // Lock LCZ elevators
            foreach (Elevator elevator in ev.Server.Map.GetElevators())
            {
                switch (elevator.ElevatorType)
                {
                    case ElevatorType.LiftA:
                    case ElevatorType.LiftB:
                        if (elevator.ElevatorStatus == ElevatorStatus.Down)
                        {
                            elevator.Use();
                        }
						break;
					case ElevatorType.SCP049Chamber when elevator.ElevatorStatus == ElevatorStatus.Up:
						elevator.Use();
						break;
                }
            }

            List<Smod2.API.Door> doors = ev.Server.Map.GetDoors();
            doors.First(x => x.Name == "CHECKPOINT_ENT").Locked = true;
            doors.First(x => x.Name == "HCZ_ARMORY").Locked = true;

            AlphaWarheadController.host.SetLocked(true);
            #endregion

            #region Items
            // Spawn commander cards
            foreach (Smod2.API.Item item in ev.Server.Map
                .GetItems(ItemType.GUARD_KEYCARD, true)
                .Concat(ev.Server.Map.GetItems(ItemType.SENIOR_GUARD_KEYCARD, true)))
            {
                ev.Server.Map.SpawnItem(ItemType.MTF_COMMANDER_KEYCARD, item.GetPosition(), Vector.Zero);
                item.Remove();
            }

            // Delete all HIDs
            ev.Server.Map.GetItems(ItemType.MICROHID, true).First().Remove();
            
            // Delete rifles and make them into USP spawns
            List<Smod2.API.Item> rifles = ev.Server.Map.GetItems(ItemType.E11_STANDARD_RIFLE, true);
            Vector[] uspSpawns = rifles.Select(x => x.GetPosition()).ToArray();
            foreach (Smod2.API.Item weapon in rifles.Concat(ev.Server.Map.GetItems(ItemType.USP, true)))
            {
                weapon.Remove();
            }

            // Spawn all USPs in defined time
            timers.Add(Timing.In(x =>
            {
                cassie.CallRpcPlayCustomAnnouncement("WEAPON NOW AVAILABLE", false);

                foreach (Vector spawn in uspSpawns)
                {
                    ev.Server.Map.SpawnItem(ItemType.USP, spawn, Vector.Zero);
                }
            }, uspTime));
            #endregion

            List<Player> players = ev.Server.GetPlayers();
            List<Player> possibleSlendies = players.ToList();
		    // Get percentage of 049s based on players
            int larryCount = Mathf.FloorToInt(players.Count * percentLarrys);
            if (larryCount == 0)
            {
                larryCount = 1;
            }

		    // Get random 049s
            Player[] slendies = new Player[larryCount];
            for (int i = 0; i < larryCount; i++)
            {
                slendies[i] = possibleSlendies[Random.Range(0, possibleSlendies.Count)];
                possibleSlendies.Remove(slendies[i]);
            }

		    // Set every class to scientist
		    foreach (Player player in players)
		    {
				SpawnScientist(player, false, false);
				foreach (Smod2.API.Item item in player.GetInventory())
					item.Remove();
				if (giveFlashbangs)
					player.GiveItem(ItemType.FLASHBANG);	
			}

		    // Set 049 spawn points
            List<Role> availableSpawns = Plugin.larrySpawnPoints.ToList();
            Vector[] spawnPoints = new Vector[slendies.Length];
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                // Get role and remove it from pool
                Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                availableSpawns.Remove(spawnRole);

                // Set point to random point from role
                spawnPoints[i] = ev.Server.Map.GetRandomSpawnPoint(spawnRole);

                // Fill pool if it overflows
                if (availableSpawns.Count == 0)
                {
                    availableSpawns.AddRange(Plugin.larrySpawnPoints);
                }
            }

            activeGenerators = new string[0];
            teslas = ev.Server.Map.GetTeslaGates();

            // Inform players
            broadcast.CallRpcAddElement("This is Blackout, a custom gamemode. If you have never played it, press [`] or [~] for more info.", 10, false);
            foreach (Player player in players)
            {
                player.SendConsoleMessage("\nWelcome to Blackout!\n" +
                                          "In Blackout, you're either a scientist or 049. All the lights will turn off and exits have been locked. " +
                                          "The only way to get out is by activating all the 079 generators, then going to the Heavy Containment Zone armory (that 3 way intersection with the chasm beneath it). " +
                                          "Commander keycards will spawn in 096 (like usual) and nuke. When you escape, you will be given weapons to kill all 049s. " +
										  "Eliminate all of them before the nuke detonates for a scientist win.");
            }

            const float cassieDelay = 8.6f;
            const float flickerDelay = 0.4f;
            
            timers.Add(Timing.In(x => // Announcements
            {
                cassie.CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);

                timers.Add(Timing.In(y => // Blackout
                {
                    TenSecondBlackoutLoop(y);
                    RefreshGeneratorsLoop(y);

                    int maxTimeMinutes = Mathf.FloorToInt(maxTime / 60);
                    float remainder = maxTime - maxTimeMinutes * 60;
                    timers.Add(Timing.In(z => AnnounceTimeLoops(maxTimeMinutes - 1, z), remainder)); // Time announcements and nuke end

                    timers.Add(Timing.In(z => // Change role and teleport players
                    {
                        // Spawn 049s
                        for (int i = 0; i < slendies.Length; i++)
                        {
                            slendies[i].ChangeRole(Role.SCP_049, false, false);
							slendies[i].Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_106));
                        }

						foreach (Player player in possibleSlendies)
							ScientistInitInv(player);

						timers.Add(Timing.In(a =>
						{
							for (int i = 0; i < slendies.Length; i++)
								slendies[i].Teleport(spawnPoints[i]);
							cassie.CallRpcPlayCustomAnnouncement("CAUTION . SCP 0 4 9 CONTAINMENT BREACH IN PROGRESS", false);
						}, slendyDelay));

                        timers.Add(Timing.InTicks(() => // Unlock round
                        {
                            isRoundStarted = true;
                        }, 4));
                    }, flickerDelay + y)); // 0.4 IS VERY SPECIFIC. DO NOT CHANGE, MAY CAUSE LIGHT FLICKER TO NOT CORRESPOND WITH ROLE CHANGE
                }, cassieDelay + x)); // 8.6 IS VERY SPECIFIC. DO NOT CHANGE, MAY CAUSE BLACKOUT TO BE UNCOORDINATED WITH CASSIE
            }, startDelay - (cassieDelay + flickerDelay))); // Cassie and flicker delay is subtracted in order to start the round by that time
        }

		private void SpawnScientist(Player player, bool isScientist, bool initInv)
		{
		    if (!isScientist)
		        player.ChangeRole(Role.SCIENTIST);

			player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));

			if (initInv)
				ScientistInitInv(player);
		}

		private void ScientistInitInv(Player player)
		{
			foreach (Smod2.API.Item item in player.GetInventory())
				item.Remove();

			player.GiveItem(ItemType.SCIENTIST_KEYCARD);
			player.GiveItem(ItemType.RADIO);
			player.GiveItem(ItemType.WEAPON_MANAGER_TABLET);

			if (giveFlashlights)
				player.GiveItem(ItemType.FLASHLIGHT);
		}

        private void EscapeScientist(Player player)
        {
            string rank = player.GetRankName();
            player.SetRank("silver", $"[ESCAPED]{(string.IsNullOrWhiteSpace(rank) ? "" : $" {rank}")}");

            foreach (Smod2.API.Item item in player.GetInventory()) //drop items before converting
            {
                item.Drop();
            }

            player.ChangeRole(Role.NTF_SCIENTIST, false, false, false); //convert empty, ready to give items

            foreach (Smod2.API.Item item in player.GetInventory()) //drop items before converting
            {
                item.Remove();
            }

            GameObject playerObj = (GameObject) player.GetGameObject();
            Inventory inv = playerObj.GetComponent<Inventory>();
            WeaponManager manager = playerObj.GetComponent<WeaponManager>();

            int weapon = -1;
            for (int i = 0; i < manager.weapons.Length; i++)
            {
                if (manager.weapons[i].inventoryID == (int) ItemType.E11_STANDARD_RIFLE)
                {
                    weapon = i;
                }
            }

            if (weapon == -1)
            {
                throw new IndexOutOfRangeException("Weapon not found");
            }

            inv.AddNewItem((int)ItemType.E11_STANDARD_RIFLE, 40, manager.modPreferences[weapon, 0], manager.modPreferences[weapon, 1], 4); // Flashlight attachment forced
			player.GiveItem(ItemType.RADIO);
			player.GiveItem(ItemType.FRAG_GRENADE);

			// todo: add grenade launcher and turn off ff for nade launcher

			PluginManager.Manager.Server.Round.Stats.ScientistsEscaped++;
        }

        private void AnnounceTimeLoops(int minutes, float inaccuracy = 0)
        {
            if (minutes == 0)
            {
                return;
            }

            string cassieLine = minuteAnnouncements.Contains(minutes) ? $"{minutes} MINUTE{(minutes == 1 ? "" : "S")} REMAINING" : "";

            if (minutes == 1)
            {
                if (!string.IsNullOrWhiteSpace(cassieLine))
                {
                    cassieLine += " . ";
                }
                cassieLine += "ALPHA WARHEAD AUTOMATIC REACTIVATION SYSTEM ENGAGED";
                const float cassieDelay = 9f;

                timers.Add(Timing.In(x =>
                {
                    AlphaWarheadController.host.StartDetonation();
                    AlphaWarheadController.host.NetworktimeToDetonation = 60 - cassieDelay + inaccuracy;
                }, cassieDelay));
            }
            else
            {
                timers.Add(Timing.In(x => AnnounceTimeLoops(--minutes, x), 60 + inaccuracy));
            }

            if (!string.IsNullOrWhiteSpace(cassieLine))
            {
                cassie.CallRpcPlayCustomAnnouncement(cassieLine, false);
            }
        }

        private string GetGeneratorName(string rootName)
        {
            rootName = rootName.Substring(5).Trim().ToUpper();

            if (rootName.Length > 0 && (rootName[0] == '$' || rootName[0] == '!'))
            {
                rootName = rootName.Substring(1);
            }

            switch (rootName)
            {
                case "457":
                    return "096";

                case "ROOM3AR":
                    return "ARMORY";

                case "TESTROOM":
                    return "939";

                default:
                    return rootName;
            }
        }
        
        private void RefreshGeneratorsLoop(float inaccuracy = 0)
        {
            timers.Add(Timing.In(RefreshGeneratorsLoop, 1 + inaccuracy));

            string[] newActiveGenerators =
                Generator079.generators.Where(x => x.isTabletConnected).Select(x => x.curRoom).ToArray();

            if (!activeGenerators.SequenceEqual(newActiveGenerators))
            {
                foreach (string generator in newActiveGenerators.Except(activeGenerators))
                {
                    broadcast.CallRpcAddElement($"Generator {GetGeneratorName(generator)} powering up...", 5, false);
                }

                activeGenerators = newActiveGenerators;
            }
        }

        private void TenSecondBlackoutLoop(float inaccuracy = 0)
        {
            timers.Add(Timing.In(TenSecondBlackoutLoop, 11 + inaccuracy));

            Generator079.generators[0].CallRpcOvercharge();
            if (teslaFlicker)
            {
                foreach (Smod2.API.TeslaGate tesla in teslas)
                {
                    tesla.Activate();
                }
            }
        }

        public void OnDoorAccess(PlayerDoorAccessEvent ev)
        {
            if (Plugin.active)
            {
				if (isRoundStarted)
				{
					switch (ev.Door.Name)
					{
						case "CHECKPOINT_ENT":
							ev.Destroy = false;
							break;

						case "HCZ_ARMORY":
							if ((escapeReady || (escapeReady = Generator079.generators.All(x => x.remainingPowerup <= 0))) && //if escape is known to be ready, and if not check if it is
								ev.Player.TeamRole.Role == Role.SCIENTIST)
							{
								EscapeScientist(ev.Player);
							}
							goto case "CHECKPOINT_ENT";
					}
				}
				else
				{
					ev.Door.Open = false;
				}
            }
        }

		public void OnSetRole(PlayerSetRoleEvent ev)
		{
			if (Plugin.active && ev.Player.TeamRole.Role == Role.SCIENTIST)
			{
				SpawnScientist(ev.Player, true, true);
			}
		}

        public void OnTeamRespawn(TeamRespawnEvent ev)
        {
            if (Plugin.active)
            {
                ev.SpawnChaos = true;
                ev.PlayerList.Clear();
            }
        }

        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            if (Plugin.active && ev.DamageType == DamageType.NUKE && ev.Player.TeamRole.Team == Smod2.API.Team.SCP)
            {
                ev.Damage = 0;
            }
        }

        public void OnSummonVehicle(SummonVehicleEvent ev)
        {
            if (Plugin.active)
            {
                ev.AllowSummon = false;
            }
        }

        public void OnRoundRestart(RoundRestartEvent ev)
        {
			Plugin.active = false;

            // Prevent timers from rolling into next round
            foreach (int timer in timers)
            {
                Timing.Remove(timer);
            }
        }
        
        // Teslas not activated if PRANK MOED is on
        public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
        {
            if (Plugin.active && teslaFlicker)
            {
                ev.Triggerable = false;
            }
        }

        // Lock elevators
        public void OnElevatorUse(PlayerElevatorUseEvent ev)
        {
            if (Plugin.active)
            {
                switch (ev.Elevator.ElevatorType)
                {
                    case ElevatorType.LiftA:
                    case ElevatorType.LiftB:
                        ev.AllowUse = false;
                        break;
                }
            }
        }

        // Lock round when everyone is in room
		public void OnCheckRoundEnd(CheckRoundEndEvent ev)
		{
			if (!isRoundStarted && Plugin.active)
				ev.Status = ROUND_END_STATUS.ON_GOING;
		}

        public void OnStartCountdown(WarheadStartEvent ev)
        {
            if (Plugin.active)
            {
                ev.OpenDoorsAfter = false;
            }
        }
    }
}
