using Smod2.API;
using Smod2.Commands;

using System.Linq;

namespace Blackout
{
    public class CommandHandler : ICommandHandler
    {
        public string[] OnCall(ICommandSender sender, string[] args)
        {
            bool valid = sender is Server;
            Player player = null;
            if (!valid)
            {
                player = sender as Player;
                if (player != null)
                {
                    valid = BlackoutPlugin.validRanks.Contains(player.GetRankName());
                }
            }

			if (valid)
			{
				if (args.Length > 0)
				{
					switch (args[0].ToLower())
					{
						case "toggle":
							BlackoutPlugin.toggled = !BlackoutPlugin.toggled;
							BlackoutPlugin.activeNextRound = BlackoutPlugin.toggled;

							return new[]
							{
								$"Blackout has been toggled {(BlackoutPlugin.toggled ? "on" : "off")}."
							};

                        default:
                            return new[]
                            {
                                "Invalid argument"
                            };
					}
				}

			    if (!BlackoutPlugin.toggled)
			    {
			        BlackoutPlugin.activeNextRound = !BlackoutPlugin.activeNextRound;

			        return new[]
			        {
			            $"Blackout has been {(BlackoutPlugin.activeNextRound ? "enabled" : "disabled")} for next round."
			        };
			    }

			    return new[]
			    {
			        "Blackout is already toggled on."
			    };
			}

			return new[]
			{
				$"You (rank {player?.GetRankName() ?? "Server"}) do not have permissions to that command."
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
