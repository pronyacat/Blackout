using System.Collections.Generic;
using scp4aiur;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Config;
using Smod2.Events;

namespace Blackout
{
    [PluginDetails(
        author = "4aiur, Cyanox",
		name = "Blackout",
        description = "Custom gamemode that uses 079's blackout feature.",
        id = "4aiur.cyanox.blackout",
        version = "1.0.0",
        SmodMajor = 3,
        SmodMinor = 2,
        SmodRevision = 2)]
    public class BlackoutPlugin : Plugin
    {
        public static Role[] slendySpawnPoints;

        public static bool active;
		public static bool toggled;
        public static bool activeNextRound;

        public static bool roundLock;

        public static string[] validRanks;

        public override void Register()
        {
            slendySpawnPoints = new[]
            {
                Role.SCP_096,
                Role.SCP_939_53,
                Role.SCP_939_89
            };

            AddConfig(new ConfigSetting("bo_ranks", new[]
            {
                "owner",
                "admin"
            }, SettingType.LIST, true, "Valid ranks for the BLACKOUT command."));

            AddConfig(new ConfigSetting("bo_items_wait", new[]
            {
                (int)ItemType.FLASHBANG
            }, SettingType.NUMERIC_LIST, true, "Items everyone should get while in 049s chamber. All items are removed when the lights go out."));
            AddConfig(new ConfigSetting("bo_items_start", new[]
			{
                (int)ItemType.SCIENTIST_KEYCARD,
                (int)ItemType.WEAPON_MANAGER_TABLET,
			    (int)ItemType.RADIO,
                (int)ItemType.FLASHLIGHT
			}, SettingType.NUMERIC_LIST, true, "Items all scientists should get when the game starts."));
            AddConfig(new ConfigSetting("bo_items_escape", new[]
            {
                (int)ItemType.E11_STANDARD_RIFLE,
                (int)ItemType.FRAG_GRENADE,
                (int)ItemType.FRAG_GRENADE
            }, SettingType.NUMERIC_LIST, true, "Items everyone should get while in 049s chamber. All items are removed when the lights go out."));

            AddConfig(new ConfigSetting("bo_slendy_percent", 0.06667f, SettingType.FLOAT, true, "Percentage of players that should be slendies."));
            AddConfig(new ConfigSetting("bo_fc_percent", 0.05556f, SettingType.FLOAT, true, "Percentage of players that should be slendies."));

            AddConfig(new ConfigSetting("bo_start_delay", 30f, SettingType.FLOAT, true, "Time until the round starts."));
			AddConfig(new ConfigSetting("bo_slendy_delay", 30f, SettingType.FLOAT, true, "Time until slendies are released."));
            AddConfig(new ConfigSetting("bo_max_time", 720f, SettingType.FLOAT, true, "Time before the round ends."));
			AddConfig(new ConfigSetting("bo_usp_time", 300f, SettingType.FLOAT, true, "Time until a USP spawns in nuke armory."));
            AddConfig(new ConfigSetting("bo_generator_time", 60f, SettingType.FLOAT, true, "Time required to engage a generator."));
            AddConfig(new ConfigSetting("bo_generator_refresh", 1f, SettingType.FLOAT, true, "Refresh rate of generator resuming and broadcasts."));
            AddConfig(new ConfigSetting("bo_flickerlight_duration", 0f, SettingType.FLOAT, true, "Amount of time between light flickers."));
            AddConfig(new ConfigSetting("bo_announce_times", new[]
            {
                9,
                6,
                3,
                2,
                1
            }, SettingType.NUMERIC_LIST, true, "Minutes remaining that should be announced"));

            AddConfig(new ConfigSetting("bo_tesla_flicker", true, SettingType.BOOL, true, "If teslas should activate on light flicker."));
            
            Timing.Init(this);

            AddEventHandlers(new EventHandlers(this), Priority.High); 
            // LASER: HIGH PRIORITY IS NECESSARY
            // Say Serpents Hand was going to spawn in and it got ran before ours. Bam. Now people are serpents hand and hold up the round.
            // Say someone has a door bypass plugin. Bam. Someones out of heavy containment.
            // PLEASE DO NOT ANGRYLASERBOI US

            AddCommand("blackout", new CommandHandler());
        }

        public override void OnEnable() { }

        public override void OnDisable() { }
    }
}
