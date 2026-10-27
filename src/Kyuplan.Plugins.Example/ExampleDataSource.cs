using Microsoft.Extensions.Logging;

namespace Kyuplan.Plugins.Example;

/// <summary>
/// A data source with made-up data: one group whose members train once a week. It shows the contract every data source keeps (see <see cref="IDataSource"/>):
/// read only, stable ids, times in the club's time zone, a message that says what to do when it cannot deliver, and a cache that the Refresh button empties.
/// A real data source fetches the same shapes (<see cref="GroupInfo"/>, <see cref="MemberInfo"/>, <see cref="EventInfo"/>) from a service.
/// </summary>
public sealed class ExampleDataSource(ExampleOptions options, TimeProvider time, TimeZoneInfo clubTimeZone, ILogger<ExampleDataSource> log) : IDataSource
{
    public const string GroupId = "example-group";

    public const string KidsId = "kids";

    public const string AdultsId = "adults";

    /// <summary>The most days one call looks at: the API asks for months, so a larger range is a mistake that must not keep the server busy.</summary>
    internal const int MaxDays = 3660;

    private static readonly string[] FirstNames =
    [
        "Anna", "Ben", "Clara", "David", "Emma", "Felix", "Greta", "Hannes", "Ida", "Jonas", "Klara", "Leon",
        "Mia", "Noah", "Olivia", "Paul", "Quinn", "Rosa", "Samuel", "Tilda", "Uwe", "Vera", "Wim", "Yara",
    ];

    private readonly Lock _lock = new();
    private IReadOnlyList<GroupInfo>? _groups;
    private DateTimeOffset _loadedAt;

    /// <summary>The id of the data source, as in <c>plugin.json</c>.</summary>
    public string Kind => ExamplePlugin.DataSourceId;

    // Sample data, yet false: the flag is for the generated demo data that comes with Kyuplan, and the data source of a plugin answers false.
    public bool IsDemo => false;

    /// <summary>How often the group was made up (the tests watch the cache with it).</summary>
    internal int Loads { get; private set; }

    public Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CancellationToken cancellationToken)
    {
        EnsureAvailable();
        return Task.FromResult(Groups());
    }

    public Task<IReadOnlyList<EventInfo>> GetEventsAsync(string groupId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (groupId != GroupId || to < from) return Task.FromResult<IReadOnlyList<EventInfo>>([]);

        var members = Groups()[0].Members;
        var trainers = options.FirstMemberTrains ? new[] { members[0].Id } : null;
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, clubTimeZone).Date);
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(to, clubTimeZone).Date);
        if (last.DayNumber - first.DayNumber > MaxDays) last = first.AddDays(MaxDays);

        var events = new List<EventInfo>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (day.DayOfWeek != options.TrainingDay) continue;

            var start = At(day, 18, 0);
            var end = At(day, 19, 30);
            if (end < from || end > to) continue; // the contract is "ending within [from, to]"; a superset would be fine too

            events.Add(new EventInfo(
                $"training-{day:yyyyMMdd}", "Training", start, end,
                Cancelled: day.DayNumber / 7 % 6 == 5, // every sixth week is off, to have cancelled events
                Responses: members.Select((member, index) => (member.Id, Answer: AnswerOf(index, day))).ToDictionary(r => r.Id, r => r.Answer),
                Trainers: trainers));
        }

        return Task.FromResult<IReadOnlyList<EventInfo>>(events);
    }

    /// <summary>The Refresh button: forget what was fetched so the next call fetches it again.</summary>
    public void InvalidateCache()
    {
        lock (_lock) _groups = null;
    }

    /// <summary>
    /// When a service cannot be reached, refuses the login or is not configured, throw this with a message that says what to do: the API answers 503
    /// and shows the message. Here the administrator asked for it.
    /// </summary>
    private void EnsureAvailable()
    {
        if (options.SimulateOutage)
            throw new DataSourceUnavailableException("The example service is pretending to be down. Untick \"Pretend the service is down\" in the settings of the data source.");
    }

    private IReadOnlyList<GroupInfo> Groups()
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            if (_groups is null || now - _loadedAt >= options.CacheDuration)
            {
                _groups = [Build()];
                _loadedAt = now;
                Loads++;
            }

            return _groups;
        }
    }

    private GroupInfo Build()
    {
        var count = Math.Clamp(options.MemberCount, 1, ExampleOptions.MaxMembers); // the configuration can say anything, an environment variable included
        var members = new List<MemberInfo>();
        for (var i = 0; i < count; i++)
        {
            var kid = i % 3 == 0;
            var birth = new DateOnly(kid ? 2014 + i % 4 : 1980 + i * 3 % 30, 1 + i % 12, 1 + i * 5 % 28);

            // The ids must not change between calls or restarts: members, exams and filters of Kyuplan refer to them. A real source uses the ids of its service.
            members.Add(new MemberInfo($"m{i + 1}", NameOf(i), [kid ? KidsId : AdultsId], birth));
        }

        var rows = options.ExtraMembers;
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i].Name)) continue;

            var year = rows[i].BirthYear;
            members.Add(new MemberInfo($"x{i + 1}", rows[i].Name!.Trim(), [AdultsId], year is >= 1900 and <= 2100 ? new DateOnly(year.Value, 1, 1) : null));
        }

        log.LogDebug("Made up the group {Group} with {Members} members.", options.GroupName, members.Count);
        return new GroupInfo(GroupId, string.IsNullOrWhiteSpace(options.GroupName) ? "Example group" : options.GroupName.Trim(), [new SubGroupInfo(KidsId, "Kids"), new SubGroupInfo(AdultsId, "Adults")], members);
    }

    private static string NameOf(int index) => index < FirstNames.Length ? FirstNames[index] : $"{FirstNames[index % FirstNames.Length]} {index / FirstNames.Length + 1}";

    /// <summary>Made up, but the same every time: roughly six in ten say yes.</summary>
    private static Rsvp AnswerOf(int member, DateOnly day)
    {
        var roll = (member * 7 + day.DayNumber) % 10;
        return roll switch
        {
            < 6 => Rsvp.Accepted,
            < 8 => Rsvp.Declined,
            8 => Rsvp.Unanswered,
            _ => Rsvp.Other,
        };
    }

    /// <summary>A time of a day in the club's time zone, not the machine's: a training at 18:00 in Germany is 17:00 UTC in winter and 16:00 in summer.</summary>
    private DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, clubTimeZone.GetUtcOffset(local));
    }
}
