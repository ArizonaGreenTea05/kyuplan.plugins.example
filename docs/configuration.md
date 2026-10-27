# Configuration

The example plugin has one data source, **Example (sample data)**. Choose it on kyuplan's **Settings** page (*Data source*); its form appears below the choice. Saving restarts the server, which is how a change of the settings takes effect: the data source reads them when it is created.

The pages and endpoints of the plugin have no settings. (The page *About the example* shows the **Remark** below, to show that an endpoint can read the same section.)

## Settings

The keys are in the section `Plugins:example`, the default section of a plugin's data source.

| Key (form field) | Default | What it does |
|---|---|---|
| `GroupName` | `Example karate group` | The name of the made-up group. Required, at most 60 characters. |
| `MemberCount` | `12` | How many members the group has, 1 to 100 (the data source clamps any other value). They are called Anna, Ben, Clara, …; ids `m1`, `m2`, …. |
| `TrainingDay` | `Wednesday` | The weekday of the weekly training, 18:00 to 19:30 in the club's time zone. |
| `FirstMemberTrains` | `true` | The first member hosts the training: it counts for none of their statistics. |
| `Remark` | empty | A text of up to 500 characters (several lines) that the *About the example* page shows. |
| `ExtraMembers` | none | Up to 20 more members, each a row with `Name` (up to 60 characters) and `BirthYear` (1900 to 2100). Rows without a name are skipped. Ids `x1`, `x2`, … (the position of the row). |
| `ServiceUrl`, `ApiKey` | empty | A login that the sample data does not use; it shows the `password` field and the rule that both are set or neither. The key is stored encrypted. |
| `SimulateOutage` | `false` | Makes the data source answer that it is unavailable, to see how kyuplan shows that message. |
| `CacheDuration` | `00:15:00` | How long the made-up group is reused. **Not in the form**; set it by hand. |

## Setting them by hand

The section is ordinary configuration, so an administrator can set the keys without the form, in the settings file of kyuplan or with environment variables (`__` separates the levels):

```bash
Plugins__example__MemberCount=30
Plugins__example__TrainingDay=Friday
Plugins__example__ExtraMembers__0__Name="Aiko Tanaka"
Plugins__example__ExtraMembers__0__BirthYear=1998
Plugins__example__CacheDuration=00:05:00
```

A key that is not set keeps the default above. A value from an environment variable is in plain text; the form is the way to store `ApiKey` encrypted.

## When something is wrong

| What you see | Why |
|---|---|
| *The example service is pretending to be down* | `SimulateOutage` is ticked. Untick it. |
| The group has fewer members than `MemberCount` | The count is clamped to 1 to 100. |
| The plugin is listed but not running | The Plugins page names the reason: a broken assembly, or an exception while the plugin started. |
| The data source *Example* is missing on the Settings page | The plugin is not installed, or does not run. |
