using Smod2.API;
using Smod2.Commands;

using System.Linq;

namespace Blackout
{
	public class CommandHandler : ICommandHandler
	{
		private BlackoutPlugin plugin;

		public CommandHandler(BlackoutPlugin plugin)
		{
			this.plugin = plugin;
		}

		public string[] OnCall(ICommandSender sender, string[] args)
		{
			bool valid = sender is Server;
			Player player = null;
			if (!valid)
			{
				player = sender as Player;
				if (player != null)
				{
					valid = plugin.ValidRanks.Contains(player.GetRankName());
				}
			}

			if (valid)
			{
				if (args.Length > 0)
				{
					switch (args[0].ToLower())
					{
						case "toggle":
							plugin.Toggled = !plugin.Toggled;
							plugin.ActiveNextRound = plugin.Toggled;

							return new[]
							{
								$"Blackout has been toggled {(plugin.Toggled ? "on" : "off")}."
							};

						default:
							return new[]
							{
								"Invalid argument"
							};
					}
				}

				if (!plugin.Toggled)
				{
					plugin.ActiveNextRound = !plugin.ActiveNextRound;

					return new[]
					{
						$"Blackout has been {(plugin.ActiveNextRound ? "enabled" : "disabled")} for next round."
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
