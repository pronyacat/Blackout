using scp4aiur;
using Smod2;
using Smod2.API;

using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackout
{
    public partial class EventHandlers
    {
        private const string BroadcastExplanation = "This is Blackout, a custom gamemode. If you have never played it, press [`] or [~] for more info.";
        private const string ConsoleExplaination =
            "\nWelcome to Blackout!\n" +
            "In Blackout, you're either a scientist or 049. All the lights will turn off and exits have been locked. " +
            "The only way to get out is by activating all the 079 generators, then going to the Heavy Containment Zone armory " +
            "(that 3 way intersection with the chasm beneath it). " +
            "Commander keycards will spawn in 096 (like usual) and nuke. When you escape, you will be given weapons to kill all 049s. " +
            "Eliminate all of them before the nuke detonates for a scientist win.";

        private const float Cassie049BreachDelay = 8.25f;

        private bool isRoundStarted;
        private bool escapeReady;

        private Broadcast broadcast;
        private MTFRespawn cassie;
        private Server server;

        private Dictionary<Player, Vector> slendySpawns;
        private (Player[] slendies, List<Player> scientists) randomizedPlayers;
        private Vector3[] uspRespawns;

        private Generator079[] activeGenerators;
        private List<Smod2.API.TeslaGate> teslas;
        private Dictionary<Generator079, float> generatorTimes;

        /// <summary>
        /// Spawns everyone in waiting room and caches spawns and players.
        /// </summary>
        /// <param name="players">All the players involved in the game.</param>
        private void GamePrep(IReadOnlyCollection<Player> players)
        {
            Pickup[] pickups = Object.FindObjectsOfType<Pickup>();

            uspRespawns = UspSpawnPoints(pickups).ToArray();
            UpdateItems(pickups);
            SetMapBoundaries();
            
            randomizedPlayers = RandomizePlayers(players);

            // Set every class to scientist
            foreach (Player player in players)
            {
                SpawnScientist(player, false, false);
                GiveWaitingItems(player);
            }

            // Set 049 spawn points
            slendySpawns = GenerateSpawnPoints(randomizedPlayers.slendies);

            activeGenerators = new Generator079[0];
            generatorTimes = Generator079.generators.ToDictionary(x => x, x => x.remainingPowerup);
            teslas = server.Map.GetTeslaGates();
        }

        /// <summary>
        /// Begins the game.
        /// </summary>
        /// <param name="inaccuracy">Timing offset.</param>
        private void StartGame(float inaccuracy)
        {
            // Begins looping to display active generators
            RefreshGeneratorsLoop(inaccuracy);

            int maxTimeMinutes = Mathf.FloorToInt(maxTime / 60);
            float remainder = maxTime - maxTimeMinutes * 60;
            Timing.In(x => AnnounceTimeLoops(maxTimeMinutes - 1, x), remainder);

            ImprisonSlendies(randomizedPlayers.slendies);

            foreach (Player player in randomizedPlayers.scientists)
                GiveGamemodeItems(player);
            
            Timing.In(x => FreeSlendies(slendySpawns), slendyDelay - Cassie049BreachDelay);
            UpdateUspRespawns(uspRespawns);

            Timing.InTicks(() => // Unlock round
            {
                isRoundStarted = true;
            }, 4);
        }

        /// <summary>
        /// Locks entrance checkpoint, LCZ elevators, and nuke button. Also sends the 049 elevator down.
        /// </summary>
        private void SetMapBoundaries()
        {
            // Lock LCZ elevators
            foreach (Elevator elevator in server.Map.GetElevators())
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

            List<Smod2.API.Door> doors = server.Map.GetDoors();
            doors.First(x => x.Name == "CHECKPOINT_ENT").Locked = true;
            doors.First(x => x.Name == "HCZ_ARMORY").Locked = true;

            AlphaWarheadController.host.SetLocked(true);
        }

        /// <summary>
        /// Removes HIDs, replaces keycards
        /// </summary>
        private static void UpdateItems(Pickup[] pickups)
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
                info.itemId = (int) ItemType.MTF_COMMANDER_KEYCARD;
                keycard.Networkinfo = info;
            }
        }

        /// <summary>
        /// Gets all the possible timed-USP spawn points.
        /// </summary>
        /// <param name="allPickups">Every pickup on the map.</param>
        private static IEnumerable<Vector3> UspSpawnPoints(IEnumerable<Pickup> allPickups)
        {
            return allPickups.Where(x => x.info.itemId == (int)ItemType.E11_STANDARD_RIFLE).Select(x => x.info.position);
        }

        /// <summary>
        /// Starts the timer on USP respawning.
        /// </summary>
        /// <param name="spawns">Spawn positions for USPs.</param>
        private void UpdateUspRespawns(IEnumerable<Vector3> spawns)
        {
            GameObject host = GameObject.Find("Host");
            Inventory inventory = host.GetComponent<Inventory>();
            WeaponManager.Weapon usp = host.GetComponent<WeaponManager>().weapons.First(x => x.inventoryID == (int) ItemType.USP);

            Timing.In(x =>
            {
                cassie.CallRpcPlayCustomAnnouncement("U S P NOW AVAILABLE", false);

                // Spawn USPs with random sight, heavy barrel, and flashlight :ok_hand:
                foreach (Vector3 spawn in spawns)
                    inventory.SetPickup((int) ItemType.USP, usp.maxAmmo, spawn, Quaternion.Euler(0, 0, 0), Random.Range(0, usp.mod_sights.Length), 2, 1);
            }, uspTime);
        }

        /// <summary>
        /// Evenly distributes spawnpoints randomly to each slendy.
        /// </summary>
        /// <param name="slendies">Slendies that are going to spawn.</param>
        private Dictionary<Player, Vector> GenerateSpawnPoints(IEnumerable<Player> slendies)
        {
            List<Role> availableSpawns = Plugin.larrySpawnPoints.ToList();
            return slendies.ToDictionary(x => x, x =>
            {
                // Get role and remove it from pool
                Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                availableSpawns.Remove(spawnRole);

                // Fill pool if it overflows
                if (availableSpawns.Count == 0)
                {
                    availableSpawns.AddRange(Plugin.larrySpawnPoints);
                }

                // Set point to random point from role
                return server.Map.GetRandomSpawnPoint(spawnRole);
            });
        }

        /// <summary>
        /// Randomizes the slendy players and scientist players.
        /// </summary>
        /// <param name="players">All the players that are playing the game.</param>
        private (Player[] slendies, List<Player> scientists) RandomizePlayers(IEnumerable<Player> players)
        {
            List<Player> possibleSlendies = players.ToList();

            // Get percentage of 049s based on players
            int slendyCount = Mathf.FloorToInt(possibleSlendies.Count * percentSlendies);
            if (slendyCount == 0)
                slendyCount = 1;

            // Get random 049s
            Player[] slendies = new Player[slendyCount];
            for (int i = 0; i < slendyCount; i++)
            {
                slendies[i] = possibleSlendies[Random.Range(0, possibleSlendies.Count)];
                possibleSlendies.Remove(slendies[i]);
            }

            return (slendies, possibleSlendies);
        }

        /// <summary>
        /// Teleports all slendies to 106 to keep them from doing anything.
        /// </summary>
        /// <param name="slendies">Slendies to imprison.</param>
        private void ImprisonSlendies(IEnumerable<Player> slendies)
        {
            foreach (Player slendy in slendies)
            {
                slendy.ChangeRole(Role.SCP_049, false, false);

                //Teleport to 106 as a prison
                slendy.Teleport(server.Map.GetRandomSpawnPoint(Role.SCP_106));
            }
        }

        /// <summary>
        /// Teleports slendies to their spawn points.
        /// </summary>
        /// <param name="slendies">Slendies and their corresponding spawn points.</param>
        private void FreeSlendies(Dictionary<Player, Vector> slendies)
        {
            cassie.CallRpcPlayCustomAnnouncement("CAUTION . SCP 0 4 9 CONTAINMENT BREACH IN PROGRESS", false);

            Timing.In(x =>
            {
                foreach (KeyValuePair<Player, Vector> slendy in slendies)
                    slendy.Key.Teleport(slendy.Value);
            }, Cassie049BreachDelay);
        }

        /// <summary>
        /// Spawns a scientist with gamemode spawn and items.
        /// </summary>
        /// <param name="player">Player to spawn.</param>
        /// <param name="isScientist">If the player is already a scientist.</param>
        /// <param name="initInv">If items should be given to the player.</param>
        private void SpawnScientist(Player player, bool isScientist, bool initInv)
        {
            if (!isScientist)
                player.ChangeRole(Role.SCIENTIST);

            player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));

            Timing.Next(() =>
            {
                if (!isRoundStarted)
                {
                    GiveWaitingItems(player);
                }
                else if (initInv)
                {
                    GiveGamemodeItems(player);
                }
            });
        }

        /// <summary>
        /// Gives items to use mid-game to a scientist.
        /// </summary>
        /// <param name="player">Player to give items to. </param>
		private void GiveGamemodeItems(Player player)
        {
            // Remove all items
            foreach (Smod2.API.Item item in player.GetInventory())
                item.Remove();

            player.GiveItem(ItemType.SCIENTIST_KEYCARD);
            player.GiveItem(ItemType.RADIO);
            player.GiveItem(ItemType.WEAPON_MANAGER_TABLET);

            if (giveFlashlights)
                player.GiveItem(ItemType.FLASHLIGHT);
        }

        /// <summary>
        /// Gives items to use when stuck waiting in 049.
        /// </summary>
        /// <param name="player">Player to give items to.</param>
		private void GiveWaitingItems(Player player)
        {
            // Remove all items
            foreach (Smod2.API.Item item in player.GetInventory())
                item.Remove();

            if (giveFlashbangs)
                player.GiveItem(ItemType.FLASHBANG);
        }

        /// <summary>
        /// Sets role, handles items, and handles round logic of an escaped scientist.
        /// </summary>
        /// <param name="player">Scientist that escaped</param>
        private void EscapeScientist(Player player)
        {
            string rank = player.GetRankName();
            player.SetRank("silver", $"[ESCAPED]{(string.IsNullOrWhiteSpace(rank) ? "" : $" {rank}")}");

            // Drop items before converting
            foreach (Smod2.API.Item item in player.GetInventory())
                item.Drop();

            // Convert only class, no inventory or spawn point
            player.ChangeRole(Role.NTF_SCIENTIST, false, false, false);

            // Delete all items if set role gives them any
            foreach (Smod2.API.Item item in player.GetInventory())
                item.Remove();

            GameObject playerObj = (GameObject)player.GetGameObject();
            Inventory inv = playerObj.GetComponent<Inventory>();
            WeaponManager manager = playerObj.GetComponent<WeaponManager>();

            // Get weapon index in WeaponManager
            int weapon = -1;
            for (int i = 0; i < manager.weapons.Length; i++)
            {
                if (manager.weapons[i].inventoryID == (int)ItemType.E11_STANDARD_RIFLE)
                {
                    weapon = i;
                }
            }

            // Should never happen unless code above breaks
            if (weapon == -1)
            {
                throw new IndexOutOfRangeException("Weapon not found");
            }

            // Flashlight attachment forced
            inv.AddNewItem((int)ItemType.E11_STANDARD_RIFLE, 40, manager.modPreferences[weapon, 0], manager.modPreferences[weapon, 1], 4);
            player.GiveItem(ItemType.RADIO);
            player.GiveItem(ItemType.FRAG_GRENADE);

            // todo: add grenade launcher and turn off ff for nade launcher

            server.Round.Stats.ScientistsEscaped++;
        }

        /// <summary>
        /// Announcements for how much time is left and nuke at the last minute of the game
        /// </summary>
        /// <param name="minutes">Minutes remaining</param>
        /// <param name="inaccuracy">Timing offset</param>
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
                const float nukeStart = 50f; // Makes sure that the nuke starts when the siren is almost silent so it sounds like it just started

                Timing.In(x =>
                {
                    AlphaWarheadController.host.StartDetonation();
                    AlphaWarheadController.host.NetworktimeToDetonation = nukeStart;
                }, 60 - nukeStart);
            }
            else
            {
                Timing.In(x => AnnounceTimeLoops(--minutes, x), 60 + inaccuracy);
            }

            if (!string.IsNullOrWhiteSpace(cassieLine))
            {
                cassie.CallRpcPlayCustomAnnouncement(cassieLine, false);
            }
        }

        /// <summary>
        /// Gets a user-friendly generator name from the room name
        /// </summary>
        /// <param name="roomName">Room that the generator is in.</param>
        private static string GetGeneratorName(string roomName)
        {
            roomName = roomName.Substring(5).Trim().ToUpper();

            if (roomName.Length > 0 && (roomName[0] == '$' || roomName[0] == '!'))
                roomName = roomName.Substring(1);

            switch (roomName)
            {
                case "457":
                    return "096";

                case "ROOM3AR":
                    return "ARMORY";

                case "TESTROOM":
                    return "939";

                default:
                    return roomName;
            }
        }

        /// <summary>
        /// Broadcasts when a generator begins powering up
        /// </summary>
        /// <param name="inaccuracy">Timing offset</param>
        private void RefreshGeneratorsLoop(float inaccuracy = 0)
        {
            Generator079[] currentActiveGenerators = Generator079.generators.Where(x => x.isTabletConnected).ToArray();

            if (!activeGenerators.SequenceEqual(currentActiveGenerators))
            {
                Generator079[] newlyActivated = currentActiveGenerators.Except(activeGenerators).ToArray();
                Generator079[] newlyShutdown = activeGenerators.Except(currentActiveGenerators).ToArray();
                activeGenerators = currentActiveGenerators;

                foreach (Generator079 generator in newlyActivated)
                {
                    generator.NetworkremainingPowerup = generatorTimes[generator];

                    broadcast.CallRpcAddElement($"Generator {GetGeneratorName(generator.curRoom)} powering up...", 5, false);
                }

                foreach (Generator079 generator in newlyShutdown)
                {
                    generatorTimes[generator] = generator.NetworkremainingPowerup;

                    broadcast.CallRpcAddElement($"Generator {GetGeneratorName(generator.curRoom)} was shut down.", 5, false);
                }
            }

            Timing.In(RefreshGeneratorsLoop, generatorRefreshRate + inaccuracy);
        }

        /// <summary>
        /// Causes a blackout to happen in all of HCZ.
        /// </summary>
        /// <param name="inaccuracy">Timing offset.</param>
        private void BlackoutLoop(float inaccuracy = 0)
        {
            Timing.In(BlackoutLoop, 11 + flickerlightDuration + inaccuracy);

            Generator079.generators[0].CallRpcOvercharge();
            if (teslaFlicker)
            {
                foreach (Smod2.API.TeslaGate tesla in teslas)
                    tesla.Activate();
            }
        }
    }
}
