using System.Collections.Generic;
using System.Linq;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Config;
using UnityEngine;

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
        public static bool activeNextRound;

        public static bool roundLock;
        public static int initialPlayers;
        public static int escaped;

        public static string[] validRanks;
        public static bool giveFlashlights;
        public static float percentLarrys;

        public override void Register()
        {
            larrySpawnPoints = new[]
            {
                Role.SCP_096,
                Role.SCP_939_53,
                Role.SCP_939_89
            };

            instance = this;
            Timing.Init(this);

            AddConfig(new ConfigSetting("blackout_ranks", new string[0], SettingType.LIST, true, "Valid ranks for the BLACKOUT command."));
            AddConfig(new ConfigSetting("blackout_flashlights", true, SettingType.BOOL, true, "If everyone should get a flashlight on spawn."));
            AddConfig(new ConfigSetting("blackout_larry_percent", 0.2f, SettingType.FLOAT, true, "Percentage of players that should be Larry."));

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
