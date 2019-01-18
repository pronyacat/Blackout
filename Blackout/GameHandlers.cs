using scp4aiur;
using Smod2;
using Smod2.API;
using Console = GameConsole.Console;

using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

using System.Collections.Generic;
using System.Linq;

namespace Blackout
{
    public partial class EventHandlers
    {
        private const string BroadcastExplanation = "<b><color=#f22>This is Blackout, a custom gamemode. If you have never played it, press [`] or [~] for more info.</color></b>";
        private const string ConsoleExplaination =
            "\nWelcome to Blackout!\n" +
            "In Blackout, you're either a scientist or 049. All the lights will turn off and exits have been locked. " +
            "The only way to get out is by activating all the 079 generators, then going to the Heavy Containment Zone armory " +
            "(that 3 way intersection with the chasm beneath it). " +
            "O5 keycards will replace all existing keycards. When you escape, you will be given weapons to kill all 049s. " +
            "Eliminate all of them before the nuke detonates for a scientist win.";

        private const float Cassie049BreachDelay = 8.25f;

        private bool roundStarted;
        private bool slendiesFree;
        private bool escapeReady;

        private readonly List<Scp079PlayerScript> fcScripts;

        private Dictionary<Player, Vector> slendySpawns;
        private (List<Player> slendies, List<Player> fc, List<Player> scientists) randomizedPlayers;
        private Scp079PlayerScript intercomFc;
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
            fcScripts.Clear();

            // Set every class to scientist
            foreach (Player player in players)
            {
                player.ChangeRole(Role.SCIENTIST);
                SpawnRole(player);
                SetItems(player, waitingItems);
            }

            // Set 049 spawn points
            slendySpawns = GenerateSpawnPoints(randomizedPlayers.slendies);

            activeGenerators = new Generator079[0];
            float powerUp = plugin.GetConfigFloat("bo_generator_time");
            generatorTimes = Generator079.generators.ToDictionary(x => x, x => powerUp);
            teslas = plugin.Server.Map.GetTeslaGates();
        }

        /// <summary>
        /// Begins the game.
        /// </summary>
        /// <param name="inaccuracy">Timing offset.</param>
        private void StartGame(float inaccuracy)
        {
            // Begins looping to display active generators
            Refresh079Loop(inaccuracy);

            int maxTimeMinutes = Mathf.FloorToInt(maxTime / 60);
            float remainder = maxTime - maxTimeMinutes * 60;
            Timing.In(x => AnnounceTimeLoops(maxTimeMinutes - 1, x), remainder);

            ImprisonSlendies(randomizedPlayers.slendies);

            foreach (Player player in randomizedPlayers.scientists)
            {
                SetItems(player, gameItems);
            }

            foreach (Player player in randomizedPlayers.fc)
            {
                player.ChangeRole(Role.SCP_079);
            }
            
            Timing.In(x =>
                {
                    slendiesFree = true;
                    FreeSlendies(slendySpawns);
                }, slendyDelay - Cassie049BreachDelay);
            UpdateUspRespawns(uspRespawns);

            Timing.In(x => // Unlock round
            {
                roundStarted = true;
            }, 3f);
        }

        /// <summary>
        /// Locks entrance checkpoint, LCZ elevators, and nuke button. Also sends the 049 elevator down.
        /// </summary>
        private void SetMapBoundaries()
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
                info.itemId = (int) ItemType.O5_LEVEL_KEYCARD;
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
                plugin.Server.Map.AnnounceCustomMessage("U S P NOW AVAILABLE");

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
            List<Role> availableSpawns = BlackoutPlugin.slendySpawnPoints.ToList();
            return slendies.ToDictionary(x => x, x =>
            {
                // Get role and remove it from pool
                Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                availableSpawns.Remove(spawnRole);

                // Fill pool if it overflows
                if (availableSpawns.Count == 0)
                {
                    availableSpawns.AddRange(BlackoutPlugin.slendySpawnPoints);
                }

                // Set point to random point from role
                return plugin.Server.Map.GetRandomSpawnPoint(spawnRole);
            });
        }

        /// <summary>
        /// Randomizes the slendy players and scientist players.
        /// </summary>
        /// <param name="players">All the players that are playing the game.</param>
        private (List<Player> slendies, List<Player> fc, List<Player> scientists) RandomizePlayers(IEnumerable<Player> players)
        {
            List<Player> possibleSpecials = players.ToList();

            if (possibleSpecials.Count < 1)
            {
                return (new List<Player>(), new List<Player>(), possibleSpecials);
            }

            // Get percentage of 049s based on players
            int slendyCount = Mathf.CeilToInt(possibleSpecials.Count * percentSlendies);

            // Get random 049s
            List<Player> slendies = new List<Player>(slendyCount);
            for (int i = 0; i < slendyCount; i++)
            {
                Player slendy = possibleSpecials[Random.Range(0, possibleSpecials.Count)];

                slendies.Add(slendy);
                possibleSpecials.Remove(slendy);
            }

            if (possibleSpecials.Count < 2)
            {
                return (slendies, new List<Player>(), possibleSpecials);
            }

            // Get percentage of FCs based on players
            int fcCount = Mathf.CeilToInt(possibleSpecials.Count * percentFc);

            // Get random FCs
            List<Player> fcs = new List<Player>(fcCount);
            for (int i = 0; i < fcCount; i++)
            {
                Player fc = possibleSpecials[Random.Range(0, possibleSpecials.Count)];

                fcs.Add(fc);
                possibleSpecials.Remove(fc);
            }

            return (slendies, fcs, possibleSpecials);
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
                slendy.Teleport(plugin.Server.Map.GetRandomSpawnPoint(Role.SCP_106));
            }
        }

        /// <summary>
        /// Teleports slendies to their spawn points.
        /// </summary>
        /// <param name="slendies">Slendies and their corresponding spawn points.</param>
        private void FreeSlendies(Dictionary<Player, Vector> slendies)
        {
            plugin.Server.Map.AnnounceCustomMessage("CAUTION . SCP 0 4 9 CONTAINMENT BREACH IN PROGRESS");

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
        private void SpawnRole(Player player)
        {
            switch (player.TeamRole.Role)
            {
                case Role.SCIENTIST:
                    player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));

                    if (roundStarted)
                    {
                        player.PersonalBroadcast(10, "You are a <color=#FFFF7C>scientist</color>.\nDodge <color=#f00>SCP-049</color> and escape by\nworking with <color=#0096FF>Facility Control</color>.", false);

                        Timing.Next(() => SetItems(player, gameItems));
                    }
                    else
                    {
                        Timing.Next(() => SetItems(player, waitingItems));
                    }
                    break;

                case Role.SCP_049:
                    player.PersonalBroadcast(10, "You are <color=#f00>SCP-049</color>.\nPrevent <color=#FFFF7C>scientists</color> from escaping\nand hide from <color=#0096FF>Facility Control</color>.", false);

                    player.Teleport(slendiesFree
                        ? PluginManager.Manager.Server.Map.GetRandomSpawnPoint(BlackoutPlugin.slendySpawnPoints[Random.Range(0, BlackoutPlugin.slendySpawnPoints.Length)])
                        : PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_106));
                    break;

                case Role.SCP_079:
                    player.PersonalBroadcast(10, "You are <color=#0096FF>Facility Control</color>.\nWork with <color=#FFFF7C>scientists</color> to help them\nescape and kill <color=#f00>SCP-049</color>.", false);

                    Scp079PlayerScript fcScript = ((GameObject)player.GetGameObject()).GetComponent<Scp079PlayerScript>();

                    if (!randomizedPlayers.fc.Contains(player))
                    {
                        randomizedPlayers.slendies.Remove(player);
                        randomizedPlayers.scientists.Remove(player);

                        randomizedPlayers.fc.Add(player);
                        fcScripts.Add(fcScript);
                    }
                    
                    fcScript.NetworkmaxMana = 49.9f;
                    fcScript.NetworkcurLvl = 4;
                    break;
            }
        }

        /// <summary>
        /// Gives an array of items to players.
        /// </summary>
        /// <param name="player">Player to give items to.</param>
        /// <param name="items">Items to give the player.</param>
        private void GiveItems(Player player, IEnumerable<int> items)
        {
            GameObject playerObj = (GameObject)player.GetGameObject();
            Inventory inv = playerObj.GetComponent<Inventory>();
            WeaponManager manager = playerObj.GetComponent<WeaponManager>();

            Console console = Object.FindObjectOfType<Console>();
            foreach (int item in items)
            {
                int i = WeaponManagerIndex(manager, item);

                if (item < 31)
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
        /// Clears a players inventory.
        /// </summary>
        /// <param name="player">Player whose inventory would be cleared</param>
        private void RemoveItems(Player player)
        {
            foreach (Smod2.API.Item item in player.GetInventory())
                item.Remove();
        }

        /// <summary>
        /// Overwrites the players inventory with an array of items.
        /// </summary>
        /// <param name="player">Player whose inventory should be set.</param>
        /// <param name="items">Items the players should have.</param>
        private void SetItems(Player player, IEnumerable<int> items)
        {
            RemoveItems(player);
            GiveItems(player, items);
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

            SetItems(player, escapeItems);

            plugin.Server.Round.Stats.ScientistsEscaped++;
        }

        private string SendFcMessage(Player sender, string message)
        {
            foreach (Player fc in randomizedPlayers.fc)
            {
                fc.PersonalBroadcast(5, $"<color=#FFFF7C>{sender.Name}</color>: {message}", false);
            }

            return $"<color=#0f0>Sent message to {randomizedPlayers.fc.Count} Facility Control member{(randomizedPlayers.fc.Count == 1 ? "s" : "")}.</color>";
        }

        /// <summary>
        /// Gets the index of an item in WeaponManager from a weapon ItemType.
        /// </summary>
        /// <param name="manager">A player's weapon manager.</param>
        /// <param name="item">The ItemType of the weapon.</param>
        private static int WeaponManagerIndex(WeaponManager manager, int item)
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
                plugin.Server.Map.AnnounceCustomMessage(cassieLine);
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
        private void Refresh079Loop(float inaccuracy = 0)
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

                    foreach (Player player in randomizedPlayers.slendies.Concat(randomizedPlayers.scientists))
                    {
                        player.PersonalBroadcast(5, $"<b><color=#ccc>Generator {GetGeneratorName(generator.curRoom)} powering up...</color></b>", false);
                    }
                }

                foreach (Generator079 generator in newlyShutdown)
                {
                    generatorTimes[generator] = generator.NetworkremainingPowerup;

                    foreach (Player player in randomizedPlayers.slendies.Concat(randomizedPlayers.scientists))
                    {
                        player.PersonalBroadcast(5, generator.remainingPowerup > 0
                                ? $"<b><color=#ccc>Generator {GetGeneratorName(generator.curRoom)} was shut down.</color></b>"
                                : $"<b><color=#ccc>Generator {GetGeneratorName(generator.curRoom)} has successfully powered up.</color></b>",
                            false);
                    }
                }
            }

            if (intercomFc != null && intercomFc.Speaker == null)
            {
                Intercom.host.RequestTransmission(null);
                intercomFc = null;
            }

            foreach (Scp079PlayerScript fc in fcScripts)
            {
                fc.NetworkcurMana = Mathf.Min(fc.NetworkcurMana, fc.maxMana);

                plugin.Info($"Speaker: {fc.Speaker ?? "NULL (null default)"}");

                // If the Facility Control member is talking
                if (!string.IsNullOrWhiteSpace(fc.Speaker))
                {
                    // If no one is talking, route them to intercom
                    if (intercomFc == null)
                    {
                        plugin.Info("Routing through to intercom.");

                        intercomFc = fc;
                        fc.GetComponent<CharacterClassManager>().smAllowIntercom = true;
                        Intercom.host.RequestTransmission(fc.gameObject);
                    }
                    // If they are not routed through to intercom, prevent them from talking
                    else if (fc != intercomFc)
                    {
                        plugin.Info("Forcing them to stop talking.");

                        fc.NetworkcurSpeaker = null;
                    }
                }
                // If they are not talking and they are the current intercom speaker, stop talking
                else if (fc == intercomFc)
                {
                    plugin.Info("Stopping intercom.");

                    fc.GetComponent<CharacterClassManager>().smAllowIntercom = false;
                    intercomFc = null;
                }
            }

            Timing.In(Refresh079Loop, generatorRefreshRate + inaccuracy);
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
