using System.Linq;
using Smod2.API;
using Smod2.Commands;

namespace Blackout
{
    public class ActivatorCommandHandler : ICommandHandler
    {
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
                if (!Plugin.active)
                {
                    Plugin.activeNextRound = true;
                    Plugin.roundLock = args.Length > 0 && bool.TryParse(args[0], out bool roundLock) && roundLock;
                }
                else
                {
                    Plugin.roundLock = false;
                }

                return new[]
                {
                    $"Toggled blackout {(Plugin.activeNextRound ? "on" : "off")} for next round."
                };
            }

            return new[]
            {
                $"You (rank {player?.GetRankName() ?? "NULL"}) do not have permissions to that command."
            };
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
