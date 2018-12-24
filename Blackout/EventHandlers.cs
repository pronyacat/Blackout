using Smod2.EventHandlers;
using Smod2.Events;

namespace Blackout
{
    public class EventHandlers : IEventHandlerRoundStart
    {
        public void OnRoundStart(RoundStartEvent ev)
        {
            Plugin.validRanks = Plugin.instance.GetConfigList("blackout_ranks");

            Plugin.giveFlashlights = Plugin.instance.GetConfigBool("blackout_flashlights");
        }
    }
}
