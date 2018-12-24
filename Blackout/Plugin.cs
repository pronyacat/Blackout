using scp4aiur;
using Smod2.Attributes;
using Smod2.Config;

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

            Timing.Init(this);
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
}
