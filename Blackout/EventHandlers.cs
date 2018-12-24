using System.Collections.Generic;
using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.EventSystem.Events;
using UnityEngine;

namespace Blackout
{
    public class EventHandlers : IEventHandlerRoundStart, IEventHandlerDoorAccess, IEventHandlerTeamRespawn, IEventHandlerCheckRoundEnd, IEventHandlerPlayerHurt, IEventHandlerSummonVehicle, IEventHandlerWarheadStopCountdown, IEventHandlerRoundRestart, IEventHandlerPlayerTriggerTesla, IEventHandlerDisconnect
    {
        private bool escapeReady;
        private Broadcast broadcast;

        public void OnRoundStart(RoundStartEvent ev)
        {
            Plugin.validRanks = Plugin.instance.GetConfigList("blackout_ranks");
            Plugin.giveFlashlights = Plugin.instance.GetConfigBool("blackout_flashlights");
            Plugin.percentLarrys = Plugin.instance.GetConfigFloat("blackout_larry_percent");

            if (Plugin.activeNextRound)
            {
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
                            elevator.Locked = true;
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
                List<Player> possibleLarrys = players.ToList();
                int larryCount = Mathf.FloorToInt(players.Count * Plugin.percentLarrys);
                if (larryCount == 0)
                {
                    larryCount = 1;
                }

                Player[] larrys = new Player[larryCount];
                for (int i = 0; i < larryCount; i++)
                {
                    larrys[i] = possibleLarrys[Random.Range(0, possibleLarrys.Count)];
                    possibleLarrys.Remove(larrys[i]);
                }

                foreach (Player player in possibleLarrys)
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
                Plugin.players = possibleLarrys.Count;

                if (Plugin.giveFlashlights)
                {
                    foreach (Player player in possibleLarrys)
                    {
                        player.GiveItem(ItemType.FLASHLIGHT);
                    }
                }

                List<Role> availableSpawns = Plugin.larrySpawnPoints.ToList();
                foreach (Player player in larrys)
                {
                    Role spawnRole = availableSpawns[Random.Range(0, availableSpawns.Count)];
                    availableSpawns.Remove(spawnRole);
                    if (availableSpawns.Count == 0)
                    {
                        availableSpawns = Plugin.larrySpawnPoints.ToList();
                    }

                    player.ChangeRole(Role.SCP_106);
                    player.Teleport(ev.Server.Map.GetRandomSpawnPoint(spawnRole));
                }

                PlayerManager.localPlayer.GetComponent<MTFRespawn>().CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);
                Plugin.active = true;
                Plugin.activeNextRound = false;

                broadcast = Object.FindObjectOfType<Broadcast>();
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


                Timing.In(TenSecondBlackout, 8.7f);
                //Timing.In(RefreshGenerators, 1f);
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

        private static void TenSecondBlackout(float inaccuracy = 0)
        {
            if (Plugin.active)
            {
                Timing.In(TenSecondBlackout, 11 + inaccuracy);
            }

            Generator079.generators[0].CallRpcOvercharge();
        }

        public void OnDoorAccess(PlayerDoorAccessEvent ev)
        {
            if (Plugin.active)
            {
                switch (ev.Door.Name)
                {
                    case "GATE_A":
                    case "GATE_B":
                    case "CHECKPOINT_ENT":
                        ev.Allow = false;
                        ev.Destroy = false;
                        break;

                    case "HCZ_ARMORY":
                        if ((escapeReady || (escapeReady = Generator079.generators.All(x => x.remainingPowerup <= 0))) && //if escape is known to be ready, and if not check if it is
                            ev.Player.TeamRole.Role == Role.SCIENTIST)
                        {
                            ev.Player.SetRank("ESCAPED", "silver");

                            if (Plugin.escaped++ == 0) //first escapee
                            {
                                ev.Player.ChangeRole(Role.SCP_079);

                                Scp079PlayerScript scp = ((GameObject)ev.Player.GetGameObject()).GetComponent<Scp079PlayerScript>();
                                scp.NetworkcurLvl = 5;
                                scp.NetworkmaxMana = 49;

                                bool isSpeaking = false;
                                void SpeakerRefresh(float inaccuracy)
                                {
                                    if (scp.Speaker != null)
                                    {
                                        isSpeaking = true;
                                        PluginManager.Manager.Server.Map.SetIntercomSpeaker(ev.Player);
                                    }
                                    else if (isSpeaking)
                                    {
                                        PluginManager.Manager.Server.Map.SetIntercomSpeaker(null);
                                    }

                                    if (Plugin.active)
                                    {
                                        Timing.In(SpeakerRefresh, 1 + inaccuracy);
                                    }
                                };
                                Timing.In(SpeakerRefresh, 1);

                                AlphaWarheadController.host.NetworktimeToDetonation = 60f;
                                AlphaWarheadController.host.StartDetonation();
                            }
                            else
                            {
                                ev.Player.ChangeRole(Role.SPECTATOR);
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
            if (Plugin.active && !Plugin.roundLock && Plugin.escaped >= Plugin.players)
            {
                ev.Status = ROUND_END_STATUS.MTF_VICTORY;
            }
        }

        /*
         * updated it so now the command causes the next round to be a blackout round, and also added your ideas. very little testing for the command and i dont have enough people for it tho.

25% (by default) players are 106 and instakill

the rest are scientists and spawn in 049 chambers with card, radio, tablet, and if setting is on, flashlights. they have to escape 
         */

        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            if (Plugin.active && ev.DamageType == DamageType.SCP_106)
            {
                ev.Damage = 99999f;
                Plugin.players--;
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
        }

        public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
        {
            if (Plugin.active && ev.Player.TeamRole.Role == Role.SCP_106)
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
    }
}
