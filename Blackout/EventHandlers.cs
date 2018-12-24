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
    public class EventHandlers : IEventHandlerRoundStart, IEventHandlerDoorAccess, IEventHandlerTeamRespawn, IEventHandlerCheckRoundEnd, IEventHandlerPlayerHurt, IEventHandlerSummonVehicle, IEventHandlerWarheadStopCountdown, IEventHandlerRoundRestart, IEventHandlerPlayerTriggerTesla, IEventHandlerDisconnect, IEventHandlerPlayerDie, IEventHandlerElevatorUse, IEventHandlerWarheadStartCountdown
    {
        private static int curRound;

        private bool escapeReady;
        private Broadcast broadcast;
        private static MTFRespawn cassie;

        public EventHandlers()
        {
            curRound = int.MinValue;
        }

        public void OnRoundStart(RoundStartEvent ev)
        {
            Plugin.validRanks = Plugin.instance.GetConfigList("bo_ranks");
            Plugin.giveFlashlights = Plugin.instance.GetConfigBool("bo_flashlights");
            Plugin.percentLarrys = Plugin.instance.GetConfigFloat("bo_slendy_percent");
            Plugin.maxTime = Plugin.instance.GetConfigInt("bo_max_time");
            Plugin.respawnTime = Plugin.instance.GetConfigInt("bo_respawn_time");

            if (Plugin.activeNextRound)
            {
                Plugin.active = true;
                Plugin.activeNextRound = false;

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
                    }
                }

                Smod2.API.Item[] guardCards = ev.Server.Map.GetItems(ItemType.GUARD_KEYCARD, true)
                    .Concat(ev.Server.Map.GetItems(ItemType.SENIOR_GUARD_KEYCARD, true)).ToArray();

                foreach (Smod2.API.Item item in guardCards)
                {
                    ev.Server.Map.SpawnItem(ItemType.MTF_COMMANDER_KEYCARD, item.GetPosition(), Vector.Zero);
                    item.Remove();
                }

                List<Player> players = ev.Server.GetPlayers();
                List<Player> possibleSlendies = players.ToList();
                int larryCount = Mathf.FloorToInt(players.Count * Plugin.percentLarrys);
                if (larryCount == 0)
                {
                    larryCount = 1;
                }

                Player[] slendies = new Player[larryCount];
                for (int i = 0; i < larryCount; i++)
                {
                    slendies[i] = possibleSlendies[Random.Range(0, possibleSlendies.Count)];
                    possibleSlendies.Remove(slendies[i]);
                }

                foreach (Player player in possibleSlendies)
                {
                    player.ChangeRole(Role.SCIENTIST, false, false);
                    foreach (Smod2.API.Item item in player.GetInventory())
                    {
                        item.Remove();
                    }

                    player.GiveItem(ItemType.SCIENTIST_KEYCARD);
                    player.GiveItem(ItemType.RADIO);
                    player.GiveItem(ItemType.WEAPON_MANAGER_TABLET);

                    player.Teleport(ev.Server.Map.GetRandomSpawnPoint(Role.SCP_049));
                }
                Plugin.players = possibleSlendies.Count;

                if (Plugin.giveFlashlights)
                {
                    foreach (Player player in possibleSlendies)
                    {
                        player.GiveItem(ItemType.FLASHLIGHT);
                    }
                }

                List<Role> availableSpawns = Plugin.larrySpawnPoints.ToList();
                foreach (Player player in slendies)
                {
                    Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                    availableSpawns.Remove(spawnRole);
                    if (availableSpawns.Count == 0)
                    {
                        availableSpawns = Plugin.larrySpawnPoints.ToList();
                    }

                    player.ChangeRole(Role.SCP_049);
                    player.Teleport(ev.Server.Map.GetRandomSpawnPoint(spawnRole));
                }

                broadcast = Object.FindObjectOfType<Broadcast>();
                cassie = PlayerManager.localPlayer.GetComponent<MTFRespawn>();

                broadcast.CallRpcAddElement("This is Blackout, a custom gamemode. If you have never played it, please press [`] or [~] for more info.", 10, false);
                foreach (Player player in players)
                {
                    player.SendConsoleMessage("\nWelcome to Blackout!\n" +
                                              "In Blackout, you're either a scientist or 106. All the lights have been turned off and exits locked.\n" +
                                              "The only way to get out is by activating all the 079 generators, then going to the Heavy Containment Zone armory (that 3 way intersection with the chasm beneath it).\n" +
                                              "Commander keycards will spawn in 096 (like usual) and nuke.\n" +
                                              "If you are caught by a 106, you are instantly dead. However, if you manage to escape first you will become SCP-079.\n" +
                                              "079 is like the facility's surveilance. They has access tier 5 and their goal is to help the scientists. Using any speaker activates intercom and you have access to map, so feel free to guide others out.");
                }

                int timerRound = curRound;
                Timing.In(inaccuracy =>
                {
                    if (timerRound == curRound)
                    {
                        cassie.CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);

                        Timing.In(x => TenSecondBlackout(timerRound, x), 8.7f + inaccuracy);
                    }
                }, 30f);

                //Timing.In(RefreshGenerators, 1f);
                Timing.In(x => AnnounceTime(Plugin.maxTime, timerRound, x), 60);
            }
        }

        private static void AnnounceTime(int minutes, int timerRound, float inaccuracy = 0)
        {
            if (timerRound == curRound)
            {
                cassie.RpcPlayCustomAnnouncement($"{minutes} MINUTES REMAINING", true);

                if (minutes == 1)
                {
                    AlphaWarheadController.host.NetworktimeToDetonation = 60f;
                    AlphaWarheadController.host.StartDetonation();
                }
                else
                {
                    Timing.In(x => AnnounceTime(--minutes, timerRound, x), 60 + inaccuracy);
                }
            }
        }

        /*
        private void RefreshGenerators(float inaccuracy = 0)
        {
            if (Plugin.active)
            {
                Timing.In(RefreshGenerators, 1 + inaccuracy);
            }

            string[] newActiveGenerators =
                Generator079.generators.Where(x => x.remainingPowerup < 120).Select(x => x.curRoom).ToArray();

            if (!activeGenerators.SequenceEqual(newActiveGenerators))
            {
                activeGenerators = newActiveGenerators;
                foreach (string generator in newActiveGenerators.Except(activeGenerators))
                {
                    broadcast.CallRpcAddElement($"{generator} engaging", 5, false);
                }
            }
        }
        */

        private static void TenSecondBlackout(int timerRound, float inaccuracy = 0)
        {
            if (timerRound == curRound)
            {
                Timing.In(x => TenSecondBlackout(timerRound, x), 11 + inaccuracy);

                Generator079.generators[0].CallRpcOvercharge();
            }
        }

        public void OnDoorAccess(PlayerDoorAccessEvent ev)
        {
            if (Plugin.active)
            {
                switch (ev.Door.Name)
                {
                    case "CHECKPOINT_ENT":
                        ev.Allow = false;
                        ev.Destroy = false;
                        break;

                    case "HCZ_ARMORY":
                        if ((escapeReady || (escapeReady = Generator079.generators.All(x => x.remainingPowerup <= 0))) && //if escape is known to be ready, and if not check if it is
                            ev.Player.TeamRole.Role == Role.SCIENTIST)
                        {
                            ev.Player.SetRank("silver", "ESCAPED");

                            if (Plugin.escaped++ == 0) //first escapee
                            {
                                ev.Player.ChangeRole(Role.SCP_079);

                                Scp079PlayerScript scp = ((GameObject)ev.Player.GetGameObject()).GetComponent<Scp079PlayerScript>();
                                scp.NetworkcurLvl = 5;
                                scp.NetworkmaxMana = 49;

                                bool isSpeaking = false;
                                int timerRound = curRound;
                                void SpeakerRefresh(float inaccuracy)
                                {
                                    if (Plugin.active && timerRound == curRound)
                                    {
                                        Timing.In(SpeakerRefresh, 1 + inaccuracy);

                                        if (scp.Speaker != null)
                                        {
                                            isSpeaking = true;
                                            PluginManager.Manager.Server.Map.SetIntercomSpeaker(ev.Player);
                                        }
                                        else if (isSpeaking)
                                        {
                                            PluginManager.Manager.Server.Map.SetIntercomSpeaker(null);
                                        }
                                    }
                                };
                                Timing.In(SpeakerRefresh, 1);

                                if (AlphaWarheadController.host.inProgress)
                                {
                                    AlphaWarheadController.host.NetworktimeToDetonation = 60f;
                                    AlphaWarheadController.host.StartDetonation();
                                }

                                broadcast.RpcAddElement($"The first ({Plugin.escaped}/{Plugin.players}) scientist has escaped and become 079. Cooperate with them to exit the facility.", 5, false);
                            }
                            else
                            {
                                ev.Player.ChangeRole(Role.SPECTATOR);
                                broadcast.RpcAddElement($"Another ({Plugin.escaped}/{Plugin.players}) scientist has escaped.", 5, false);
                            }
                        }
                        goto case "CHECKPOINT_ENT";
                }
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

        public void OnCheckRoundEnd(CheckRoundEndEvent ev)
        {
            if (Plugin.active && !Plugin.roundLock)
            {
                if (Plugin.escaped >= Plugin.players)
                {
                    ev.Status = ROUND_END_STATUS.MTF_VICTORY;
                }
                else if (ev.Server.GetPlayers().Count(x => x.TeamRole.Role != Role.SPECTATOR) == 0)
                {
                    ev.Status = ROUND_END_STATUS.OTHER_VICTORY;
                }
                else if (Plugin.players == 0)
                {
                    ev.Status = ROUND_END_STATUS.SCP_VICTORY;
                }
            }
        }

        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            if (Plugin.active && ev.DamageType == DamageType.SCP_049)
            {
                ev.Damage = 99999f;
            }
        }

        public void OnSummonVehicle(SummonVehicleEvent ev)
        {
            if (Plugin.active)
            {
                ev.AllowSummon = false;
            }
        }

        public void OnStopCountdown(WarheadStopEvent ev)
        {
            if (Plugin.active)
            {
                ev.Cancel = true;
            }
        }

        public void OnRoundRestart(RoundRestartEvent ev)
        {
            Plugin.active = false;
            Plugin.respawnActive = false;

            curRound++;
        }

        public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
        {
            if (Plugin.active && ev.Player.TeamRole.Role == Role.SCIENTIST)
            {
                ev.Triggerable = false;
            }
        }

        public void OnDisconnect(DisconnectEvent ev)
        {
            if (Plugin.active)
            {
                UpdatePlayers();
            }
        }

        private void UpdatePlayers()
        {
            Plugin.players = PluginManager.Manager.Server.GetPlayers().Count(x => x.TeamRole.Role == Role.SCIENTIST);
        }

        public void OnPlayerDie(PlayerDeathEvent ev)
        {
            if (Plugin.active && ev.Player.TeamRole.Role == Role.SCIENTIST)
            {
                Plugin.players--;
                
                Timing.In(inaccuracy =>
                {
                    if (Plugin.active && Plugin.respawnActive)
                    {
                        ev.Player.ChangeRole(Role.SCIENTIST, false, false, false);
                        ev.Player.Teleport(PluginManager.Manager.Server.Map.GetRandomSpawnPoint(Role.SCP_049));
                        Plugin.players++;
                    }
                }, Plugin.respawnTime);
            }
        }

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

        public void OnStartCountdown(WarheadStartEvent ev)
        {
            if (Plugin.active)
            {
                ev.OpenDoorsAfter = false;
            }
        }
    }
}
