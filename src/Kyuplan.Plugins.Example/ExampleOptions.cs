namespace Kyuplan.Plugins.Example;

/// <summary>
/// The settings of the data source: the section <see cref="Section"/> of the configuration. The settings form of the data source (<c>ui/settings.json</c>) fills it
/// on the Settings page of Kyuplan; an administrator can also set the keys by hand (<c>Plugins:example:MemberCount</c>, environment variable
/// <c>Plugins__example__MemberCount</c>). The id of a field of the form is the name of a property here, and a <c>default</c> of the form is the default of the
/// property (a test keeps them equal).
/// </summary>
public sealed class ExampleOptions
{
    /// <summary>Where <c>plugin.json</c> keeps the settings when a data source names no <c>configSection</c>: <c>Plugins:&lt;plugin id&gt;</c>.</summary>
    public const string Section = "Plugins:" + ExamplePlugin.PluginId;

    /// <summary>The most members the data source makes up, whatever the settings say.</summary>
    public const int MaxMembers = 100;

    // ---- a text field, a number, a select, a checkbox and a multiline field ----

    public string GroupName { get; set; } = "Example karate group";

    public int MemberCount { get; set; } = 12;

    /// <summary>A <c>select</c>: the values of its options are the names of <see cref="DayOfWeek"/>, which the configuration binds by name.</summary>
    public DayOfWeek TrainingDay { get; set; } = DayOfWeek.Wednesday;

    /// <summary>A <c>checkbox</c>: the first member hosts the training (an event's <c>Trainers</c>), so it counts for none of their statistics.</summary>
    public bool FirstMemberTrains { get; set; } = true;

    /// <summary>A <c>multiline</c> field. Shown on the "About the example" page, which reads the setting from an endpoint.</summary>
    public string? Remark { get; set; }

    // ---- a list: the user adds and removes rows of fields ----

    public List<ExtraMember> ExtraMembers { get; set; } = [];

    // ---- a login: an address and a secret that belong together (a "together" rule); a password field is stored encrypted and arrives here decrypted ----

    public string? ServiceUrl { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Tick to see how Kyuplan shows a data source that cannot be reached (<see cref="DataSourceUnavailableException"/>).</summary>
    public bool SimulateOutage { get; set; }

    // ---- not in the form: settings an administrator may still set by hand ----

    /// <summary>How long the made-up group is reused before it is made up again, as a real data source reuses what it fetched.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Whether a service address and its key are set. The example data source does not call a service; a real one would.</summary>
    public bool HasService => !string.IsNullOrWhiteSpace(ServiceUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>One row of the list <see cref="ExampleOptions.ExtraMembers"/>: the fields of a row are the <c>elements</c> of the list in <c>ui/settings.json</c>.</summary>
public sealed class ExtraMember
{
    public string? Name { get; set; }

    public int? BirthYear { get; set; }
}
