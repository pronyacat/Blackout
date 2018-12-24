using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Commands;
using Smod2.Config;
using Smod2.EventHandlers;
using Smod2.Events;

namespace Blackout
{
    [PluginDetails(
        author = "4aiur",
        description = "Adds light blackout command.",
        id = "4aiur.custom.blackout",
        version = "1.0.0",
        SmodMajor = 3,
        SmodMinor = 2,
        SmodRevision = 0)]
    public class Plugin : Smod2.Plugin
    {
        public static Plugin instance;

        public static string[] validRanks;
        public static bool giveFlashlights;

        public override void Register()
        {
            instance = this;

            AddConfig(new ConfigSetting("blackout_ranks", new string[0], SettingType.LIST, true, "Valid ranks for the BLACKOUT command."));
            AddConfig(new ConfigSetting("blackout_flashlights", true, SettingType.BOOL, true, "If everyone should get a flashlight on spawn."));

            AddEventHandlers(new Timing(Info));
            AddEventHandlers(new EventHandlers());

            AddCommand("blackout", new CommandHandler());
        }

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }
    }

    public class EventHandlers : IEventHandlerRoundStart
    {
        public void OnRoundStart(RoundStartEvent ev)
        {
            Plugin.validRanks = Plugin.instance.GetConfigList("blackout_ranks");

            Plugin.giveFlashlights = Plugin.instance.GetConfigBool("blackout_flashlights");
        }
    }

    public class CommandHandler : ICommandHandler
    {
        private bool run;

        public string[] OnCall(ICommandSender sender, string[] args)
        {
            bool valid = sender is Server;

            Player player = sender as Player;
            if (!valid && player != null)
            {
                valid = Plugin.validRanks.Contains(player.GetRankName());
            }

            if (valid)
            {
                run = !run;

                if (run)
                {
                    if (Plugin.giveFlashlights)
                    {
                        foreach (Player p in PluginManager.Manager.Server.GetPlayers())
                        {
                            if (!p.HasItem(ItemType.FLASHLIGHT))
                            {
                                p.GiveItem(ItemType.FLASHLIGHT);
                            }
                        }
                    }

                    PlayerManager.localPlayer.GetComponent<MTFRespawn>().CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);

                    Timing.Timer(TenSecondBlackout, 8.7f);
                }

                return new[]
                {
                    "Toggled blackout"
                };
            }

            return new[]
            {
                $"You (rank {player.GetRankName()}) do not have permissions to that command."
            };
        }

        private void TenSecondBlackout(float inaccuracy = 0)
        {
            Generator079.generators[0].CallRpcOvercharge();

            if (run)
            {
                Timing.Timer(TenSecondBlackout, 11 + inaccuracy);
            }
        }

        public string GetUsage()
        {
            return "blackout";
        }

        public string GetCommandDescription()
        {
            return "Causes all the lights to flicker.";
        }
    }
}
