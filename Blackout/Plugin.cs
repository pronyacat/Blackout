using scp4aiur;
using Smod2.API;
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
        public static Role[] larrySpawnPoints;

        public static Plugin instance;

        public static bool active;
        public static bool respawnActive;
        public static bool activeNextRound;

        public static bool roundLock;
        public static int players;
        public static int escaped;

        public static string[] validRanks;
        public static bool giveFlashlights;
        public static float percentLarrys;
        public static int maxTime;
        public static float respawnTime;
        public static float uspTime;

        public override void Register()
        {
            larrySpawnPoints = new[]
            {
                Role.SCP_096,
                Role.SCP_106,
                Role.SCP_939_53,
                Role.SCP_939_89
            };

            instance = this;
            Timing.Init(this);

            AddConfig(new ConfigSetting("bo_ranks", new string[0], SettingType.LIST, true, "Valid ranks for the BLACKOUT command."));
            AddConfig(new ConfigSetting("bo_flashlights", true, SettingType.BOOL, true, "If everyone should get a flashlight on spawn."));
            AddConfig(new ConfigSetting("bo_slendy_percent", 0.2f, SettingType.FLOAT, true, "Percentage of players that should be slendies."));
            AddConfig(new ConfigSetting("bo_max_time", 7, SettingType.NUMERIC, true, "Time before the round ends"));
            AddConfig(new ConfigSetting("bo_respawn_time", 15f, SettingType.FLOAT, true, "Time before a dead scientist respawns with nothing in 049 (if respawn is enabled via command)."));
            AddConfig(new ConfigSetting("bo_usp_time", 5 * 60f, SettingType.FLOAT, true, "Time until a USP spawns in nuke armory."));

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
