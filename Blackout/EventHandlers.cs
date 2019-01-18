using scp4aiur;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.EventSystem.Events;
using Object = UnityEngine.Object;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackout
{
    public partial class EventHandlers : IEventHandlerWaitingForPlayers, IEventHandlerRoundStart, 
        IEventHandlerDoorAccess, IEventHandlerTeamRespawn, IEventHandlerPlayerHurt, 
        IEventHandlerSummonVehicle, IEventHandlerRoundRestart, IEventHandlerCheckRoundEnd,
        IEventHandlerPlayerTriggerTesla, IEventHandlerElevatorUse, IEventHandlerWarheadStartCountdown,
		IEventHandlerSetRole, IEventHandlerRecallZombie, IEventHandlerInfected, IEventHandlerCallCommand,
        IEventHandlerIntercom
    {
        private readonly BlackoutPlugin plugin;

        #region Config
        public int[] waitingItems;
        public int[] gameItems;
        public int[] escapeItems;

        public float percentSlendies;
        public float percentFc;

        public float startDelay;
		public float slendyDelay;
        public float maxTime;
        public float uspTime;
        public float generatorRefreshRate;
        public float flickerlightDuration;
        public int[] minuteAnnouncements;

        public bool teslaFlicker;
        #endregion

        public EventHandlers(BlackoutPlugin plugin)
        {
            this.plugin = plugin;
            fcScripts = new List<Scp079PlayerScript>();
        }

        /// <summary>
        /// Loads all the configs
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
		{
			BlackoutPlugin.validRanks = plugin.GetConfigList("bo_ranks");

			waitingItems = plugin.GetConfigIntList("bo_items_wait");
		    gameItems = plugin.GetConfigIntList("bo_items_start");
		    escapeItems = plugin.GetConfigIntList("bo_items_escape");

            percentSlendies = plugin.GetConfigFloat("bo_slendy_percent");
		    percentFc = plugin.GetConfigFloat("bo_fc_percent");

            startDelay = plugin.GetConfigFloat("bo_start_delay");
			slendyDelay = plugin.GetConfigFloat("bo_slendy_delay");
            maxTime = plugin.GetConfigFloat("bo_max_time");
			uspTime = plugin.GetConfigFloat("bo_usp_time");
		    generatorRefreshRate = plugin.GetConfigFloat("bo_generator_refresh");
		    flickerlightDuration = plugin.GetConfigFloat("bo_flickerlight_duration");
            minuteAnnouncements = plugin.GetConfigIntList("bo_announce_times");

            teslaFlicker = plugin.GetConfigBool("bo_tesla_flicker");
		}

        /// <summary>
        /// Pretty much times and hooks all the GameHandler stuff.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnRoundStart(RoundStartEvent ev)
        {
            #region Check and set if active
            if (!BlackoutPlugin.activeNextRound && !BlackoutPlugin.toggled)
                return;

            BlackoutPlugin.active = true;
			BlackoutPlugin.activeNextRound = false;
		    roundStarted = false;
            slendiesFree = false;
            escapeReady = false;
            #endregion
            
            List<Player> allPlayers = plugin.Server.GetPlayers();
            GamePrep(allPlayers);

            // Inform players
            plugin.Server.Map.Broadcast(10, BroadcastExplanation, false);
            foreach (Player player in allPlayers)
                player.SendConsoleMessage(ConsoleExplaination);

            const float cassieDelay = 8.6f;
            const float flickerDelay = 0.4f;

            // Announcements
            Timing.In(x =>
            {
                plugin.Server.Map.AnnounceCustomMessage("LIGHT SYSTEM SCP079RECON6");

                // Blackout
                Timing.In(y =>
                {
                    BlackoutLoop(y);

                    // Change role and teleport players
                    Timing.In(StartGame, flickerDelay + y); // 0.4 IS VERY SPECIFIC. DO NOT CHANGE, MAY CAUSE LIGHT FLICKER TO NOT CORRESPOND WITH ROLE CHANGE
                }, cassieDelay + x); // 8.6 IS VERY SPECIFIC. DO NOT CHANGE, MAY CAUSE BLACKOUT TO BE UNCOORDINATED WITH CASSIE
            }, startDelay - (cassieDelay + flickerDelay)); // Cassie and flicker delay is subtracted in order to start the round by that time
        }

        /// <summary>
        /// Locks 049 room during start delay.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnCheckRoundEnd(CheckRoundEndEvent ev)
        {
            if (BlackoutPlugin.active)
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
                    bool scp = players.Count(x => x == Smod2.API.Team.SCP) - randomizedPlayers.fc.Count > 0;
                    bool scientist = players.Count(x => x == Smod2.API.Team.SCIENTIST) > 0;

                    if (!scientist)
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

        /// <summary>
        /// Resets active bool to false on restart.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnRoundRestart(RoundRestartEvent ev)
        {
            BlackoutPlugin.active = false;
        }

        /// <summary>
        /// Locks HCZ armory and entrance checkpoint, also has escape handler on HCZ.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnDoorAccess(PlayerDoorAccessEvent ev)
        {
            if (BlackoutPlugin.active)
            {
				if (roundStarted)
				{
					switch (ev.Door.Name)
					{
                        case "HCZ_ARMORY":
                        case "049_ARMORY":
						    if (ev.Player.TeamRole.Role == Role.SCIENTIST &&
						        (escapeReady || (escapeReady = Generator079.generators.All(x => x.remainingPowerup <= 0)))) //if escape is known to be ready, and if not check if it is
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

        /// <summary>
        /// Locks LCZ elevators.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnElevatorUse(PlayerElevatorUseEvent ev)
        {
            if (BlackoutPlugin.active && (ev.Elevator.ElevatorType == ElevatorType.LiftA || ev.Elevator.ElevatorType == ElevatorType.LiftB))
            {
                ev.AllowUse = false;
            }
        }

        /// <summary>
        /// Disables all teslas for all players of tesla flicker is enabled.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
        {
            if (BlackoutPlugin.active && teslaFlicker)
            {
                ev.Triggerable = false;
            }
        }

        /// <summary>
        /// Prevents SCPs dying from nuke, so its considered an SCP win.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            if (BlackoutPlugin.active && ev.DamageType == DamageType.NUKE && ev.Player.TeamRole.Team == Smod2.API.Team.SCP)
            {
                ev.Damage = 0;
            }
        }

        /// <summary>
        /// Handles scientist inventory and if the role set is a scientist.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnSetRole(PlayerSetRoleEvent ev)
		{
			if (BlackoutPlugin.active)
			{
				SpawnRole(ev.Player);
			}
		}

        /// <summary>
        /// Prevents team respawns from happening outside.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnTeamRespawn(TeamRespawnEvent ev)
        {
            if (BlackoutPlugin.active)
            {
                ev.SpawnChaos = true;
                ev.PlayerList = new List<Player>();
            }
        }

        /// <summary>
        /// Prevents heli/van from appearing, just in case some perm-abuser spawns people outside and gets excited for a team respawn B).
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnSummonVehicle(SummonVehicleEvent ev)
        {
            if (BlackoutPlugin.active)
            {
                ev.AllowSummon = false;
            }
        }
        
        /// <summary>
        /// Prevents doors from locking themselves open.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnStartCountdown(WarheadStartEvent ev)
        {
            if (BlackoutPlugin.active)
            {
                ev.OpenDoorsAfter = false;
            }
        }

        /// <summary>
        /// Prevents 049 from ressurecting.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnRecallZombie(PlayerRecallZombieEvent ev)
        {
            if (BlackoutPlugin.active)
            {
                ev.AllowRecall = false;
            }
        }

        /// <summary>
        /// Makes 049 have no recall time so they don't realize that they can't ressurect until they look at the body for 10 seconds.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnPlayerInfected(PlayerInfectedEvent ev)
        {
            if (BlackoutPlugin.active)
            {
                ev.InfectTime = 0;
            }
        }

        [Obsolete("Recommended for Smod use only.")]
        public void OnCallCommand(PlayerCallCommandEvent ev)
        {
            if (!BlackoutPlugin.active)
            {
                ev.ReturnMessage = "Blackout is not running this round.";
                return;
            }

            if (ev.Player.TeamRole.Role != Role.SCIENTIST)
            {
                ev.ReturnMessage = "You cannot talk to Facility Control: you are not a scientist.";
                return;
            }

            if (ev.Command.StartsWith("fc ") && ev.Command.Length > 3)
            {
                ev.ReturnMessage = SendFcMessage(ev.Player, ev.Command.Substring(3));
            }
        }


        public void OnIntercom(PlayerIntercomEvent ev)
        {
            Intercom.host.speechRemainingTime = 15f;
            ev.CooldownTime = 10f;
        }
    }
}
