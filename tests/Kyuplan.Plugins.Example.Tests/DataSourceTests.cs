using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kyuplan.Plugins.Example.Tests;

/// <summary>The contract of a data source (<see cref="IDataSource"/>) and what the example makes of its settings. Tests need no network and no account.</summary>
public class DataSourceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly DateTimeOffset Start = new(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(1)); // a Monday

    private static (ExampleDataSource Source, FakeTimeProvider Time) Make(ExampleOptions? options = null, TimeZoneInfo? zone = null)
    {
        var time = new FakeTimeProvider(ExampleHost.Now);
        return (new ExampleDataSource(options ?? new ExampleOptions(), time, zone ?? Berlin, NullLogger<ExampleDataSource>.Instance), time);
    }

    private static async Task<GroupInfo> GroupAsync(ExampleOptions? options = null) => Assert.Single(await Make(options).Source.GetGroupsAsync(default));

    private static async Task<IReadOnlyList<EventInfo>> EventsAsync(ExampleOptions? options, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo? zone = null) =>
        await Make(options, zone).Source.GetEventsAsync(ExampleDataSource.GroupId, from, to, default);

    // ---- the group ----

    [Fact]
    public async Task The_source_is_the_one_of_the_manifest_and_not_demo_data()
    {
        var (source, _) = Make();

        Assert.Equal("example", source.Kind);
        Assert.Equal(ExamplePlugin.DataSourceId, source.Kind);
        Assert.False(source.IsDemo);
        Assert.Equal(ExampleDataSource.GroupId, (await source.GetGroupsAsync(default))[0].Id);
    }

    [Fact]
    public async Task The_group_has_the_name_the_subgroups_and_as_many_members_as_the_settings_say()
    {
        var group = await GroupAsync(new ExampleOptions { GroupName = "  Dojo  ", MemberCount = 5 });

        Assert.Equal("Dojo", group.Name);
        Assert.Equal([ExampleDataSource.KidsId, ExampleDataSource.AdultsId], group.SubGroups.Select(s => s.Id));
        Assert.Equal(["m1", "m2", "m3", "m4", "m5"], group.Members.Select(m => m.Id));
        Assert.All(group.Members, m => Assert.Contains(Assert.Single(m.SubGroupIds), group.SubGroups.Select(s => s.Id)));
        Assert.All(group.Members, m => Assert.NotNull(m.BirthDate));
    }

    [Fact]
    public async Task A_group_without_a_name_gets_one()
    {
        Assert.Equal("Example group", (await GroupAsync(new ExampleOptions { GroupName = " " })).Name);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public async Task The_number_of_members_is_kept_within_its_limits_whatever_the_configuration_says(int asked, int made)
    {
        Assert.Equal(made, (await GroupAsync(new ExampleOptions { MemberCount = asked })).Members.Count);
    }

    [Fact]
    public async Task Members_have_unique_names_and_ids_even_beyond_the_list_of_first_names()
    {
        var members = (await GroupAsync(new ExampleOptions { MemberCount = 100 })).Members;

        Assert.Equal(100, members.Select(m => m.Id).Distinct().Count());
        Assert.Equal(100, members.Select(m => m.Name).Distinct().Count());
    }

    [Fact]
    public async Task The_ids_are_the_same_every_time_and_in_every_instance()
    {
        var first = (await GroupAsync()).Members.Select(m => (m.Id, m.Name, m.BirthDate));
        var again = (await GroupAsync()).Members.Select(m => (m.Id, m.Name, m.BirthDate));

        Assert.Equal(first, again);
    }

    [Fact]
    public async Task Extra_members_follow_the_made_up_ones_and_rows_without_a_name_are_skipped()
    {
        var options = new ExampleOptions
        {
            MemberCount = 2,
            ExtraMembers =
            [
                new ExtraMember { Name = "  Zed ", BirthYear = 1999 },
                new ExtraMember { Name = " ", BirthYear = 2000 },
                new ExtraMember { Name = "Yan" },
                new ExtraMember { Name = "Old", BirthYear = 1800 },
                new ExtraMember { Name = "Future", BirthYear = 2101 },
            ],
        };

        var members = (await GroupAsync(options)).Members;

        Assert.Equal(["m1", "m2", "x1", "x3", "x4", "x5"], members.Select(m => m.Id));
        Assert.Equal(["Zed", "Yan", "Old", "Future"], members.Skip(2).Select(m => m.Name));
        Assert.Equal([new DateOnly(1999, 1, 1), (DateOnly?)null, null, null], members.Skip(2).Select(m => m.BirthDate));
        Assert.All(members.Skip(2), m => Assert.Equal([ExampleDataSource.AdultsId], m.SubGroupIds));
    }

    // ---- the events ----

    [Fact]
    public async Task There_is_one_training_a_week_on_the_training_day_at_18_00_in_the_club_time_zone()
    {
        var events = await EventsAsync(new ExampleOptions { TrainingDay = DayOfWeek.Wednesday }, Start, Start.AddDays(28));

        Assert.Equal(["training-20260107", "training-20260114", "training-20260121", "training-20260128"], events.Select(e => e.Id));
        Assert.All(events, e => Assert.Equal(DayOfWeek.Wednesday, e.Start.DayOfWeek));
        Assert.Equal(new DateTimeOffset(2026, 1, 7, 18, 0, 0, TimeSpan.FromHours(1)), events[0].Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 7, 19, 30, 0, TimeSpan.FromHours(1)), events[0].End);
        Assert.Equal(new DateTimeOffset(2026, 1, 7, 17, 0, 0, TimeSpan.Zero), events[0].Start.ToUniversalTime()); // 17:00 UTC in winter
    }

    [Fact]
    public async Task The_time_follows_summer_time_not_the_machine()
    {
        var from = new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.FromHours(1));
        var events = await EventsAsync(new ExampleOptions { TrainingDay = DayOfWeek.Sunday }, from, from.AddDays(14));

        Assert.Equal(["training-20260329", "training-20260405"], events.Select(e => e.Id));
        Assert.Equal(TimeSpan.FromHours(2), events[0].Start.Offset); // clocks went forward in the night of 29 March
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 16, 0, 0, TimeSpan.Zero), events[0].Start.ToUniversalTime());
    }

    [Fact]
    public async Task Another_time_zone_moves_the_training_not_the_day_on_the_clock()
    {
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(9));

        var events = await EventsAsync(new ExampleOptions(), from, from.AddDays(7), tokyo);

        Assert.Equal(new DateTimeOffset(2026, 1, 7, 18, 0, 0, TimeSpan.FromHours(9)), Assert.Single(events).Start);
    }

    [Fact]
    public async Task Only_events_that_end_within_the_range_are_returned()
    {
        var training = new DateTimeOffset(2026, 1, 7, 19, 30, 0, TimeSpan.FromHours(1)); // the end of the first Wednesday

        Assert.Single(await EventsAsync(null, training, training)); // inclusive on both ends
        Assert.Empty(await EventsAsync(null, training.AddMinutes(1), training.AddDays(6)));
        Assert.Empty(await EventsAsync(null, training.AddDays(-6), training.AddMinutes(-1)));
    }

    [Fact]
    public async Task The_days_of_the_range_are_the_clubs_days_not_those_of_UTC()
    {
        // The training of Wednesday 7 January ends at 19:30 in Los Angeles, which is 03:30 UTC on Thursday: a range of UTC's Thursday morning has to find it.
        var losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var from = new DateTimeOffset(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);

        var events = await EventsAsync(null, from, from.AddHours(4), losAngeles);

        Assert.Equal("training-20260107", Assert.Single(events).Id);
        Assert.Equal(new DateTimeOffset(2026, 1, 8, 2, 0, 0, TimeSpan.Zero), events[0].Start.ToUniversalTime());
    }

    [Fact]
    public async Task An_unknown_group_and_a_backwards_range_have_no_events()
    {
        var (source, _) = Make();

        Assert.Empty(await source.GetEventsAsync("nobody", Start, Start.AddDays(28), default));
        Assert.Empty(await source.GetEventsAsync(ExampleDataSource.GroupId, Start.AddDays(28), Start, default));
    }

    [Fact]
    public async Task A_huge_range_is_cut_short_instead_of_keeping_the_server_busy()
    {
        var events = await EventsAsync(null, new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(9000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.InRange(events.Count, 1, ExampleDataSource.MaxDays / 7 + 1);
    }

    [Fact]
    public async Task Every_member_is_invited_and_the_answers_are_the_same_every_time()
    {
        var group = await GroupAsync();
        var events = await EventsAsync(null, Start, Start.AddDays(56));
        var again = await EventsAsync(null, Start, Start.AddDays(56));

        Assert.All(events, e => Assert.Equal(group.Members.Select(m => m.Id).Order(), e.Responses.Keys.Order()));
        Assert.Equal(events.Select(e => e.Responses.Values.ToArray()), again.Select(e => e.Responses.Values.ToArray()));
        var answers = events.SelectMany(e => e.Responses.Values).ToHashSet();
        Assert.Equal(Enum.GetValues<Rsvp>().Order(), answers.Order()); // the sample shows every kind of answer
    }

    [Fact]
    public async Task The_first_member_hosts_the_training_and_takes_no_part()
    {
        var events = await EventsAsync(new ExampleOptions { FirstMemberTrains = true }, Start, Start.AddDays(14));

        Assert.All(events, e =>
        {
            Assert.Equal(["m1"], e.Trainers);
            Assert.True(e.IsTrainer("m1"));
            Assert.Contains("m1", e.Responses.Keys);
            Assert.DoesNotContain("m1", e.Participants.Select(p => p.Key));
        });
    }

    [Fact]
    public async Task Without_a_host_everybody_takes_part()
    {
        var events = await EventsAsync(new ExampleOptions { FirstMemberTrains = false }, Start, Start.AddDays(14));

        Assert.All(events, e =>
        {
            Assert.Null(e.Trainers);
            Assert.Equal(e.Responses.Count, e.Participants.Count());
        });
    }

    [Fact]
    public async Task Every_sixth_week_is_cancelled()
    {
        var events = await EventsAsync(null, Start, Start.AddDays(7 * 12));

        Assert.Equal(12, events.Count);
        Assert.Equal(2, events.Count(e => e.Cancelled));
    }

    // ---- when it cannot deliver ----

    [Fact]
    public async Task A_service_that_is_down_says_what_to_do()
    {
        var (source, _) = Make(new ExampleOptions { SimulateOutage = true });

        var groups = await Assert.ThrowsAsync<DataSourceUnavailableException>(() => source.GetGroupsAsync(default));
        var events = await Assert.ThrowsAsync<DataSourceUnavailableException>(() => source.GetEventsAsync(ExampleDataSource.GroupId, Start, Start.AddDays(7), default));

        Assert.Contains("Untick", groups.Message);
        Assert.Equal(groups.Message, events.Message);
    }

    // ---- the cache ----

    [Fact]
    public async Task What_was_made_up_is_reused_until_it_is_too_old()
    {
        var (source, time) = Make(new ExampleOptions { CacheDuration = TimeSpan.FromMinutes(10) });

        await source.GetGroupsAsync(default);
        await source.GetEventsAsync(ExampleDataSource.GroupId, Start, Start.AddDays(7), default);
        time.Advance(TimeSpan.FromMinutes(9));
        await source.GetGroupsAsync(default);
        Assert.Equal(1, source.Loads);

        time.Advance(TimeSpan.FromMinutes(1));
        await source.GetGroupsAsync(default);
        Assert.Equal(2, source.Loads);
    }

    [Fact]
    public async Task The_refresh_button_forgets_what_was_fetched()
    {
        var (source, _) = Make();

        await source.GetGroupsAsync(default);
        source.InvalidateCache();
        source.InvalidateCache(); // twice is the same as once
        await source.GetGroupsAsync(default);
        await source.GetGroupsAsync(default);

        Assert.Equal(2, source.Loads);
    }

    [Fact]
    public async Task Calls_at_the_same_time_make_the_group_up_once()
    {
        var (source, _) = Make();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => source.GetGroupsAsync(default))));

        Assert.Equal(1, source.Loads);
    }

    // ---- built by the plugin ----

    private static PluginDataSourceContext Context(params (string Key, string Value)[] settings) => new(
        new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => $"{ExampleOptions.Section}:{s.Key}", s => (string?)s.Value)).Build().GetSection(ExampleOptions.Section),
        TimeProvider.System, Berlin, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());

    [Fact]
    public async Task The_plugin_builds_the_data_source_from_its_section_of_the_configuration()
    {
        var builder = new FakeBuilder();
        new ExamplePlugin().Initialize(builder);

        var source = builder.DataSources["example"](Context(("GroupName", "Dojo"), ("MemberCount", "3"), ("TrainingDay", "Friday")));

        Assert.Equal("example", source.Kind);
        var group = Assert.Single(await source.GetGroupsAsync(default));
        Assert.Equal(("Dojo", 3), (group.Name, group.Members.Count));
        Assert.All(await source.GetEventsAsync(group.Id, Start, Start.AddDays(14), default), e => Assert.Equal(DayOfWeek.Friday, e.Start.DayOfWeek));
    }

    [Fact]
    public async Task Without_any_settings_the_plugin_builds_the_data_source_with_the_defaults()
    {
        var builder = new FakeBuilder();
        new ExamplePlugin().Initialize(builder);

        var group = Assert.Single(await builder.DataSources["example"](Context()).GetGroupsAsync(default));

        Assert.Equal(("Example karate group", 12), (group.Name, group.Members.Count));
    }
}
