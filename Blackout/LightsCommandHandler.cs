using System.Collections.Generic;
using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.Commands;

namespace Blackout
{
	public class LightsCommandHandler : ICommandHandler
	{
		private readonly BlackoutPlugin plugin;
		private bool running;

		public LightsCommandHandler(BlackoutPlugin plugin)
		{
			this.plugin = plugin;
		}

		public string[] OnCall(ICommandSender sender, string[] args)
		{
			if (!(sender is Server) && 
			    sender is Player player && 
			    !plugin.ValidLightsOutRanks.Contains(player.GetRankName()))
			{
				return new[]
				{
					$"You (rank {player.GetRankName() ?? "Server"}) do not have permissions to that command."
				};
			}

			running = !running;

			if (running)
			{
				Timing.Run(TimingRunLights(PluginManager.Manager.Server.Map.Get079InteractionRooms(Scp079InteractionType.CAMERA).Where(x => x.ZoneType != ZoneType.ENTRANCE).ToArray()));
			}

			return new[]
			{
				$"Facility lights have been turned {(running ? "off" : "on")}."
			};
		}

		private IEnumerable<float> TimingRunLights(IReadOnlyList<Room> rooms)
		{
			while (running)
			{
				foreach (Room room in rooms)
				{
					room.FlickerLights();
				}

				yield return 8.25f;
			}
		}

		public string GetUsage()
		{
			return "lightsout";
		}

		public string GetCommandDescription()
		{
			return "Turns off all facility lights";
		}
	}
}
