using System.Linq;
using Smod2;
using Smod2.API;
using Smod2.Commands;

namespace Blackout
{
    public class CommandHandler : ICommandHandler
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
                if (args.Length > 0)
                {
                    switch (args[0])
                    {
                        case "enable":
                            if (!Plugin.active)
                            {
                                Plugin.activeNextRound = true;
                                Plugin.roundLock = args.Length > 1 && bool.TryParse(args[1], out bool roundLock) && roundLock;
                            }
                            else
                            {
                                Plugin.roundLock = false;
                            }

                            return new[]
                            {
                                $"Toggled blackout {(Plugin.activeNextRound ? "on" : "off")} for next round."
                            };

                        case "respawn":
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
                }
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
