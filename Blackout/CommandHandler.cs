using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.Commands;

namespace Blackout
{
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
                        foreach (Player p in PluginManager.Manager.Server.GetPlayers().Where(x => x.HasItem(ItemType.FLASHLIGHT)))
                        {
                            p.GiveItem(ItemType.FLASHLIGHT);
                        }
                    }

                    PlayerManager.localPlayer.GetComponent<MTFRespawn>().CallRpcPlayCustomAnnouncement("LIGHT SYSTEM SCP079RECON6", false);

                    Timing.In(TenSecondBlackout, 8.7f);
                }

                return new[]
                {
                    "Toggled blackout"
                };
            }

            return new[]
            {
                $"You (rank {player?.GetRankName() ?? "NULL"}) do not have permissions to that command."
            };
        }

        private void TenSecondBlackout(float inaccuracy = 0)
        {
            Generator079.generators[0].CallRpcOvercharge();

            if (run)
            {
                Timing.In(TenSecondBlackout, 11 + inaccuracy);
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
