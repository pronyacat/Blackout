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
		IEventHandlerSetRole, IEventHandlerRecallZombie, IEventHandlerInfected
    {
        #region Config
        public int[] waitingItems;
        public int[] gameItems;
        public int[] escapeItems;

        public float percentSlendies;

        public float startDelay;
		public float slendyDelay;
        public float maxTime;
        public float uspTime;
        public float generatorRefreshRate;
        public float flickerlightDuration;
        public int[] minuteAnnouncements;

        public bool teslaFlicker;
        #endregion

        /// <summary>
        /// Loads all the configs
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
		{
			Plugin.validRanks = Plugin.instance.GetConfigList("bo_ranks");

			waitingItems = Plugin.instance.GetConfigIntList("bo_items_wait");
		    gameItems = Plugin.instance.GetConfigIntList("bo_items_start");
		    escapeItems = Plugin.instance.GetConfigIntList("bo_items_escape");

            percentSlendies = Plugin.instance.GetConfigFloat("bo_slendy_percent");

		    startDelay = Plugin.instance.GetConfigFloat("bo_start_delay");
			slendyDelay = Plugin.instance.GetConfigFloat("bo_slendy_delay");
            maxTime = Plugin.instance.GetConfigFloat("bo_max_time");
			uspTime = Plugin.instance.GetConfigFloat("bo_usp_time");
		    generatorRefreshRate = Plugin.instance.GetConfigFloat("bo_generator_refresh");
		    flickerlightDuration = Plugin.instance.GetConfigFloat("bo_flickerlight_duration");
            minuteAnnouncements = Plugin.instance.GetConfigIntList("bo_announce_times");

            teslaFlicker = Plugin.instance.GetConfigBool("bo_tesla_flicker");

		    server = ev.Server;
		}

        /// <summary>
        /// Pretty much times and hooks all the GameHandler stuff.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnRoundStart(RoundStartEvent ev)
        {
            #region Check and set if active
            if (!Plugin.activeNextRound && !Plugin.toggled)
                return;

            Plugin.active = true;
			Plugin.activeNextRound = false;
		    isRoundStarted = false;
            #endregion

            // Get announcement variables
            broadcast = Object.FindObjectOfType<Broadcast>();
            cassie = PlayerManager.localPlayer.GetComponent<MTFRespawn>();

            List<Player> allPlayers = server.GetPlayers();
            GamePrep(allPlayers);

            // Inform players
            broadcast.CallRpcAddElement(BroadcastExplanation, 10, false);
            foreach (Player player in allPlayers)
                player.SendConsoleMessage(ConsoleExplaination);

            const float cassieDelay = 8.6f;
            const float flickerDelay = 0.4f;

            // Announcements
            Timing.In(x =>
            {
                cassie.CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);

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
            if (Plugin.active && !isRoundStarted)
            {
                ev.Status = ROUND_END_STATUS.ON_GOING;
            }
        }

        /// <summary>
        /// Resets active bool to false on restart.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnRoundRestart(RoundRestartEvent ev)
        {
            Plugin.active = false;
        }

        /// <summary>
        /// Locks HCZ armory and entrance checkpoint, also has escape handler on HCZ.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
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

        /// <summary>
        /// Locks LCZ elevators.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
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

        /// <summary>
        /// Disables all teslas for all players of tesla flicker is enabled.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnPlayerTriggerTesla(PlayerTriggerTeslaEvent ev)
        {
            if (Plugin.active && teslaFlicker)
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
            if (Plugin.active && ev.DamageType == DamageType.NUKE && ev.Player.TeamRole.Team == Smod2.API.Team.SCP)
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
			if (Plugin.active && ev.Player.TeamRole.Role == Role.SCIENTIST)
			{
				SpawnScientist(ev.Player, true, true);
			}
		}

        /// <summary>
        /// Prevents team respawns from happening outside.
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnTeamRespawn(TeamRespawnEvent ev)
        {
            if (Plugin.active)
            {
                ev.SpawnChaos = true;
                ev.PlayerList.Clear();
            }
        }

        /// <summary>
        /// Prevents heli/van from appearing, just in case some perm-abuser spawns people outside and gets excited for a team respawn B).
        /// </summary>
        [Obsolete("Recommended for Smod use only.")]
        public void OnSummonVehicle(SummonVehicleEvent ev)
        {
            if (Plugin.active)
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
            if (Plugin.active)
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
            if (Plugin.active)
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
            if (Plugin.active)
            {
                ev.InfectTime = 0;
            }
        }
    }
}
