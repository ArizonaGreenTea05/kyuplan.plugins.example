using System.Globalization;
using System.Text.Json;
using Kyuplan.Contracts;
using Microsoft.Extensions.Configuration;

namespace Kyuplan.Plugins.Example.Tests;

/// <summary>The settings form and the options class are two descriptions of the same keys; these tests keep them one.</summary>
public class OptionsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static PluginUiLayout Form() =>
        JsonSerializer.Deserialize<PluginUiLayout>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ui/settings.json")), Json)!;

    private static IEnumerable<PluginUiElement> Fields() => Form().Elements.Where(e => e.Type == "field");

    private static IEnumerable<PluginUiElement> Lists() => Form().Elements.Where(e => e.Type == "list");

    /// <summary>The values of a form: the ids of the fields as they are, which is what a form sends. The name of the group is required, so it is always there unless a test sets it.</summary>
    private static Dictionary<string, JsonElement> Values(params (string Key, object? Value)[] values)
    {
        var form = new Dictionary<string, JsonElement> { ["GroupName"] = JsonSerializer.SerializeToElement("Dojo") };
        foreach (var (key, value) in values) form[key] = JsonSerializer.SerializeToElement(value);
        return form;
    }

    private static string? Problem(IReadOnlyDictionary<string, JsonElement> values, Func<string, bool>? hasSecret = null) =>
        PluginRules.CheckValues(Form(), values, hasSecret ?? (_ => false));

    private static ExampleOptions Bind(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => $"{ExampleOptions.Section}:{s.Key}", s => (string?)s.Value))
            .Build().GetSection(ExampleOptions.Section).Get<ExampleOptions>() ?? new ExampleOptions();

    // ---- the form and the options are the same keys ----

    /// <summary>Settings that the form does not offer: an administrator sets them by hand (docs/configuration.md).</summary>
    private static readonly string[] SetByHand = [nameof(ExampleOptions.CacheDuration)];

    [Fact]
    public void Every_field_of_the_form_is_an_option_and_every_option_is_a_field_or_set_by_hand()
    {
        var options = typeof(ExampleOptions).GetProperties().Where(p => p.CanWrite).Select(p => p.Name).ToHashSet();
        var ids = Form().Elements.Where(e => e.Id is not null).Select(e => e.Id!).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var id in ids) Assert.Contains(id, options);
        Assert.Equal(options.Except(SetByHand).Order(), ids.Order());
    }

    [Fact]
    public void Every_field_of_a_row_is_a_property_of_the_row_and_the_list_is_a_list_of_rows()
    {
        var list = Assert.Single(Lists());
        var row = typeof(ExampleOptions).GetProperty(list.Id!)!.PropertyType;
        var properties = typeof(ExtraMember).GetProperties().Select(p => p.Name).Order();

        Assert.Equal(typeof(List<ExtraMember>), row);
        Assert.Equal(properties, list.Elements!.Select(f => f.Id!).Order());
    }

    [Fact]
    public void The_defaults_of_the_form_are_the_defaults_of_the_options()
    {
        var defaults = new ExampleOptions();
        var withDefault = Fields().Where(f => f.Default is not null).ToList();

        Assert.NotEmpty(withDefault);
        foreach (var field in withDefault)
        {
            var value = typeof(ExampleOptions).GetProperty(field.Id!)!.GetValue(defaults);
            var text = value is bool flag ? (flag ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
            Assert.True(field.Default == text, $"{field.Id}: the form says {field.Default}, the options {text}.");
        }
    }

    [Fact]
    public void A_field_without_a_default_starts_empty_or_unticked()
    {
        var defaults = new ExampleOptions();

        foreach (var field in Fields().Where(f => f.Default is null))
        {
            var value = typeof(ExampleOptions).GetProperty(field.Id!)!.GetValue(defaults);
            Assert.True(value is null or false, $"{field.Id} has a default in the options but none in the form.");
        }
    }

    [Fact]
    public void The_options_of_a_select_are_the_values_of_its_property()
    {
        var days = Fields().Single(f => f.Id == nameof(ExampleOptions.TrainingDay)).Options!.Select(o => o.Value);

        Assert.Equal(Enum.GetNames<DayOfWeek>().Order(), days.Order());
    }

    [Fact]
    public void The_key_of_the_service_is_the_only_secret_and_a_password_field()
    {
        Assert.Equal([nameof(ExampleOptions.ApiKey)], PluginRules.SecretPaths(Form()));
        Assert.Equal("password", Fields().Single(f => f.Id == nameof(ExampleOptions.ApiKey)).Input);
    }

    [Fact]
    public void Every_input_a_settings_form_offers_is_used()
    {
        var used = Form().Elements.Concat(Form().Elements.SelectMany(e => e.Elements ?? [])).Where(e => e.Input is not null).Select(e => e.Input!).Distinct().Order();

        Assert.Equal(PluginRules.Inputs.Order(), used);
    }

    // ---- reading the section ----

    [Fact]
    public void Without_any_setting_the_options_are_the_defaults()
    {
        var options = Bind();

        Assert.Equal(("Example karate group", 12, DayOfWeek.Wednesday, true), (options.GroupName, options.MemberCount, options.TrainingDay, options.FirstMemberTrains));
        Assert.Empty(options.ExtraMembers);
        Assert.False(options.HasService);
        Assert.False(options.SimulateOutage);
    }

    [Fact]
    public void The_options_are_read_from_the_section_including_the_rows_of_the_list()
    {
        var options = Bind(
            ("GroupName", "Dojo"), ("MemberCount", "30"), ("TrainingDay", "friday"), ("FirstMemberTrains", "false"), ("Remark", "Line 1\nLine 2"), ("SimulateOutage", "true"),
            ("CacheDuration", "00:01:00"), ("ServiceUrl", "https://example.org"), ("ApiKey", "secret"),
            ("ExtraMembers:0:Name", "Zed"), ("ExtraMembers:0:BirthYear", "1999"), ("ExtraMembers:1:Name", "Yan"));

        Assert.Equal(("Dojo", 30, DayOfWeek.Friday, false), (options.GroupName, options.MemberCount, options.TrainingDay, options.FirstMemberTrains));
        Assert.Equal("Line 1\nLine 2", options.Remark);
        Assert.True(options.SimulateOutage);
        Assert.Equal(TimeSpan.FromMinutes(1), options.CacheDuration);
        Assert.True(options.HasService);
        Assert.Equal(["Zed", "Yan"], options.ExtraMembers.Select(m => m.Name));
        Assert.Equal([1999, (int?)null], options.ExtraMembers.Select(m => m.BirthYear));
    }

    [Theory]
    [InlineData("https://example.org", "", false)]
    [InlineData("", "key", false)]
    [InlineData("  ", "key", false)]
    [InlineData("https://example.org", "key", true)]
    public void A_service_needs_its_address_and_its_key(string url, string key, bool has)
    {
        Assert.Equal(has, new ExampleOptions { ServiceUrl = url, ApiKey = key }.HasService);
    }

    // ---- what the form accepts (the same check Kyuplan makes when somebody saves it) ----

    [Fact]
    public void The_defaults_are_fine()
    {
        // The clients fill a form with the defaults, so these are the values of a form nobody touched.
        Assert.Null(Problem(Values(("GroupName", "Example karate group"), ("MemberCount", 12), ("TrainingDay", "Wednesday"), ("FirstMemberTrains", true))));
        Assert.Equal("GroupName: is required.", Problem(new Dictionary<string, JsonElement>())); // a required field must be sent
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_name_of_the_group_is_required(string name)
    {
        Assert.Equal("GroupName: is required.", Problem(Values(("GroupName", name))));
    }

    [Fact]
    public void The_name_of_the_group_has_a_limit()
    {
        Assert.Null(Problem(Values(("GroupName", new string('x', 60)))));
        Assert.Equal("GroupName: must be at most 60 characters.", Problem(Values(("GroupName", new string('x', 61)))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData("25", true)] // a number may come as text
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(-3, false)]
    [InlineData("many", false)]
    public void The_number_of_members_is_between_1_and_100(object count, bool fine)
    {
        var problem = Problem(Values(("MemberCount", count)));

        if (fine) Assert.Null(problem);
        else Assert.StartsWith("MemberCount:", problem);
    }

    [Theory]
    [InlineData("Monday", true)]
    [InlineData("Sunday", true)]
    [InlineData("monday", false)] // an option is exactly one of the values
    [InlineData("Someday", false)]
    public void The_training_day_is_one_of_the_options(string day, bool fine)
    {
        Assert.Equal(fine, Problem(Values(("TrainingDay", day))) is null);
    }

    [Fact]
    public void A_remark_can_have_several_lines_but_has_a_limit()
    {
        Assert.Null(Problem(Values(("Remark", "one\ntwo"))));
        Assert.StartsWith("Remark:", Problem(Values(("Remark", new string('x', 501)))));
    }

    [Fact]
    public void The_address_and_the_key_of_the_service_come_together_or_not_at_all()
    {
        const string message = "ApiKey: The service needs its address and its key, or neither.";

        Assert.Null(Problem(Values(("ServiceUrl", ""), ("ApiKey", ""))));
        Assert.Null(Problem(Values(("ServiceUrl", "https://example.org"), ("ApiKey", "key"))));
        Assert.Equal(message, Problem(Values(("ServiceUrl", "https://example.org"), ("ApiKey", ""))));
        Assert.Equal(message, Problem(Values(("ServiceUrl", ""), ("ApiKey", "key"))));
    }

    [Fact]
    public void A_stored_key_is_enough_so_the_form_need_not_ask_for_it_again()
    {
        Assert.Null(Problem(Values(("ServiceUrl", "https://example.org"), ("ApiKey", "")), path => path == "ApiKey"));
        Assert.NotNull(Problem(Values(("ServiceUrl", ""), ("ApiKey", "")), path => path == "ApiKey")); // the key without the address
    }

    [Fact]
    public void The_extra_members_are_rows_with_limits_of_their_own()
    {
        Assert.Null(Problem(Values(("ExtraMembers", new object[] { new { Name = "Zed", BirthYear = 1999 }, new { Name = "Yan" } }))));
        Assert.StartsWith("ExtraMembers[1].BirthYear:", Problem(Values(("ExtraMembers", new object[] { new { Name = "Zed", BirthYear = 1800 } }))));
        Assert.StartsWith("ExtraMembers[1].Name:", Problem(Values(("ExtraMembers", new object[] { new { Name = new string('x', 61) } }))));
        Assert.StartsWith("ExtraMembers:", Problem(Values(("ExtraMembers", Enumerable.Repeat(new { Name = "Zed" }, 21).ToArray()))));
    }
}
