# Blackout

A custom gamemode for SCP:SL.

# Backstory

Dr. Bright woke up on a Saturday morning, today was his day to make the company's morning tea. As he was bringing cups to his co-workers, he tripped on a wire and spilled some tea all over a loose outlet. The circuit breaker tripped and the facility suddenly lost power. In a craze for their morning tea, all of the employees grabbed flashlights and went out in search for the generators to get the power back up. They passed by SCP-049s containment chamber and saw that the backup generator for his door malfunctioned.
*The door was wide open.*

# Gameplay

When the round starts, all players will be in SCP-049s chamber, the door will be locked. After 30 seconds, the lights will flicker and shut off. A few players will spawn as 049 in various places around heavy containment at thsi time, SCP-049s door will open, and the scientists will be let free with nothing but a keycard, weapon manager tablet, and flashlight. Their goal is to reactivate all 5 generators in heavy containment and escape before SCP-049 kills them. After doing so they can open the heavy containment armory door to escape, in which they will spawn as a Nine Tailed Fox Scientist with weapons. They must now eliminate all SCP-049s before time is up.

# Installation

**[Smod2](https://github.com/Grover-c13/Smod2) must be installed for this to work.**

Place the "Blackout.dll" file in your sm_plugins folder.
**Plugin is still being made, and as such no builds are publicly available.**

# Commands

| Command        | Description |
| :-------------: | :------ |
| BLACKOUT | Enables Blackout for the next round only. |
| BLACKOUT TOGGLE | Toggles Blackout on or off. |

# Configs

| Config        | Value Type | Default | Description |
| :-------------: | :---------: | :---------: |:------ |
| bo_ranks | String List | owner, admin | Ranks allowed to run the `BLACKOUT` command. |
| bo_items_wait | Integer List | 26 | Items scientists should be given in the waiting room. These are removed when the gamemode starts. |
| bo_items_start | Integer List | 1,19,12,15 | Items scientists should be given when the gamemode start. |
| bo_items_escape | Integer List | 20,25,25 | Items scientists should be given when they escape and turn into NTF scientists. |
| bo_slendy_percent | Float | 0.15 | Percentage of players that should be slendies (SCP-049). |
| bo_start_delay | Float | 30 | Time in seconds until the round starts. |
| bo_slendy_delay | Float | 30 | Time in seconds until 049s are released. |
| bo_slendy_delay | Float | 30 | Time in seconds before the slendies (SCP-049) are released. |
| bo_max_time | Float | 720 | Time in seconds before the round ends. |
| bo_usp_time | Float | 300 | Time in seconds before a USP spawns in nuke armory. |
| bo_generator_refresh | Float | 1 | Refresh rate of generator resuming and broadcasts. |
| bo_announce_times | Integer List | 10,7,4,2,1 | Minute times for CASSIE to announce how many minutes are remaining. |
| bo_tesla_flicker | Boolean | True | If teslas should activate on light flicker. |
| bo_multithreaded | Boolean | True | If multithreading should be enabled. This may cause crashes on Linux, disable if so. Refreshed on server restart. |
