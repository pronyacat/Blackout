using System;
using System.Linq;
using Smod2.API;
using Smod2.Commands;

namespace Blackout
{
    public class RespawnCommandHandler : ICommandHandler
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
                Plugin.respawnActive = !Plugin.respawnActive;

                if (!Plugin.active)
                {
                    return new[]
                    {
                        "Blackout isn't even running, but respawn is enabled if you say so."
                    };
                }

                return new[]
                {
                    $"Toggled Blackout respawn {(Plugin.respawnActive ? "on" : "off")}. It will reset to off next round."
                };
            }

            return new[]
            {
                $"You (rank {player?.GetRankName() ?? "NULL"}) do not have permissions to that command."
            };
        }

        public string GetUsage()
        {
            return "blackoutrespawn";
        }

        public string GetCommandDescription()
        {
            return "Toggles respawn for scientists during Blackout on or off.";
        }
    }
}
