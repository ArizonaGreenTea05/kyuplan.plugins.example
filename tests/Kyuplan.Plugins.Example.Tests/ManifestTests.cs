using System.Reflection;
using System.Text.Json;
using Kyuplan.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Kyuplan.Plugins.Example.Tests;

/// <summary>
/// What makes the code a plugin: Kyuplan refuses to install a package whose manifest or JSON breaks its rules (<see cref="PluginRules"/>), and a page that points at an
/// endpoint that is not there only fails when somebody opens it. These tests are the cheapest place to find out. Keep them when you copy the template.
/// </summary>
public class ManifestTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string PathOf(string file) => Path.Combine(AppContext.BaseDirectory, file);

    private static T Read<T>(string file) => JsonSerializer.Deserialize<T>(File.ReadAllText(PathOf(file)), Json)!;

    private static JsonElement ReadJson(string file) => JsonDocument.Parse(File.ReadAllText(PathOf(file))).RootElement;

    private static PluginManifest Manifest() => Read<PluginManifest>("plugin.json");

    public static TheoryData<string> PageFiles() => new(Manifest().Pages!.Select(p => p.Layout));

    private static IEnumerable<PluginUiElement> Flatten(IEnumerable<PluginUiElement> elements) =>
        elements.SelectMany(e => new[] { e }.Concat(Flatten(e.Elements ?? [])));

    [Fact]
    public void The_manifest_passes_the_rules_of_the_host()
    {
        Assert.Null(PluginRules.CheckManifest(Manifest()));
    }

    [Fact]
    public void The_manifest_names_the_assembly_the_plugin_and_the_data_source_of_the_code()
    {
        var manifest = Manifest();

        Assert.Equal(ExamplePlugin.PluginId, manifest.Id);
        Assert.Equal(typeof(ExamplePlugin).Assembly.GetName().Name + ".dll", manifest.Assembly);
        Assert.Equal(ExamplePlugin.DataSourceId, manifest.DataSources!.Single().Id);
        Assert.True(File.Exists(PathOf(manifest.DataSources!.Single().Settings!)));
    }

    [Fact]
    public void The_data_source_keeps_its_settings_in_the_default_section_which_the_options_read()
    {
        var source = Manifest().DataSources!.Single();

        Assert.Null(source.ConfigSection); // left out: the default
        Assert.Equal("Plugins:example", ExampleOptions.Section);
        Assert.Equal(ExampleOptions.Section, PluginRules.DefaultSection(ExamplePlugin.PluginId));
        Assert.True(PluginRules.IsSection(ExampleOptions.Section));
    }

    [Fact]
    public void The_permissions_the_code_asks_for_are_the_ones_of_the_manifest()
    {
        var names = typeof(ExamplePermissions).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!);

        Assert.Equal(names.Order(), Manifest().Permissions!.Select(p => p.Name).Order());
    }

    [Fact]
    public void The_plugin_class_is_the_only_implementation_of_the_interface_in_the_assembly()
    {
        var plugins = typeof(ExamplePlugin).Assembly.GetExportedTypes().Where(t => !t.IsAbstract && typeof(IKyuplanPlugin).IsAssignableFrom(t)).ToList();

        Assert.Equal([typeof(ExamplePlugin)], plugins);
        Assert.NotNull(typeof(ExamplePlugin).GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void The_plugin_registers_the_data_source_of_the_manifest_and_its_endpoints()
    {
        var builder = new FakeBuilder();

        new ExamplePlugin().Initialize(builder);

        Assert.Equal([ExamplePlugin.DataSourceId], builder.DataSources.Keys);
        Assert.Single(builder.Endpoints);
    }

    [Theory]
    [MemberData(nameof(PageFiles))]
    public void Every_page_passes_the_rules_of_the_host(string file)
    {
        Assert.Null(PluginRules.CheckLayout(Read<PluginUiLayout>(file), PluginUiUse.Page));
    }

    [Fact]
    public void The_settings_form_passes_the_rules_of_the_host()
    {
        Assert.Null(PluginRules.CheckLayout(Read<PluginUiLayout>("ui/settings.json"), PluginUiUse.DataSourceSettings));
    }

    [Fact]
    public void Every_json_file_of_the_plugin_is_part_of_the_manifest()
    {
        var manifest = Manifest();
        var listed = manifest.Pages!.Select(p => p.Layout).Concat(manifest.DataSources!.Select(s => s.Settings!)).Select(f => f.Replace('/', Path.DirectorySeparatorChar)).Order();
        var present = Directory.EnumerateFiles(PathOf("ui"), "*.json").Select(f => Path.GetRelativePath(AppContext.BaseDirectory, f)).Order();

        Assert.Equal(listed, present);
    }

    // ---- the pages and the endpoints they use ----

    private static IReadOnlyList<(string Method, string Path, RouteEndpoint Endpoint)> EndpointsOf(ExampleHost host) =>
        [.. host.Endpoints.OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (m, RelativePath(e.RoutePattern), e)))];

    private static string RelativePath(RoutePattern pattern) => pattern.RawText![$"/api/plugins/{ExamplePlugin.PluginId}/".Length..];

    /// <summary>What a page calls: a table reads (GET) its source, a button or form sends its action.</summary>
    private static IEnumerable<(string Method, string Path)> CallsOf(PluginUiLayout page) =>
        Flatten(page.Elements).SelectMany<PluginUiElement, (string Method, string Path)>(e => e.Type switch
        {
            "table" => [("GET", e.Source!)],
            "button" or "form" => [(e.Action!.Method, e.Action.Path)],
            _ => [],
        });

    [Fact]
    public async Task Every_endpoint_a_page_uses_is_an_endpoint_of_the_plugin_with_that_method()
    {
        await using var host = await ExampleHost.StartAsync();
        var endpoints = EndpointsOf(host).Select(e => (e.Method, e.Path)).ToList();

        foreach (var page in Manifest().Pages!)
        {
            var calls = CallsOf(Read<PluginUiLayout>(page.Layout)).ToList();
            Assert.NotEmpty(calls);
            foreach (var call in calls) Assert.Contains(call, endpoints);
        }
    }

    [Fact]
    public async Task Every_endpoint_of_a_page_with_a_permission_asks_for_a_permission_itself()
    {
        await using var host = await ExampleHost.StartAsync();
        var endpoints = EndpointsOf(host);

        foreach (var page in Manifest().Pages!.Where(p => !string.IsNullOrEmpty(p.Permission)))
        {
            foreach (var call in CallsOf(Read<PluginUiLayout>(page.Layout)))
            {
                var endpoint = endpoints.Single(e => (e.Method, e.Path) == call).Endpoint;
                // RequirePermission adds the policy that tests for the claim; a group only adds "signed in".
                Assert.True(endpoint.Metadata.GetMetadata<AuthorizationPolicy>() is not null, $"{call.Method} {call.Path} is open to every signed-in user, but the page {page.Id} needs {page.Permission}.");
            }
        }
    }

    // ---- German ----

    /// <summary>The texts that need a German twin (<c>textDe</c>, <c>labelDe</c>, ...) in a layout.</summary>
    private static readonly string[] Texts = ["text", "label", "hint", "message", "confirm"];

    private static void RequireGerman(JsonElement element, string path, string[] keys, List<string> missing)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (keys.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.String && !element.TryGetProperty(property.Name + "De", out _))
                        missing.Add($"{path}.{property.Name}");
                    RequireGerman(property.Value, $"{path}.{property.Name}", keys, missing);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray()) RequireGerman(item, $"{path}[{index++}]", keys, missing);
                break;
        }
    }

    [Theory]
    [InlineData("ui/notes.json")]
    [InlineData("ui/about.json")]
    [InlineData("ui/settings.json")]
    public void Every_text_of_a_page_and_of_the_settings_form_has_a_german_twin(string file)
    {
        var missing = new List<string>();

        RequireGerman(ReadJson(file), file, Texts, missing);

        Assert.True(missing.Count == 0, $"No German text for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_text_of_the_manifest_has_a_german_twin()
    {
        var manifest = ReadJson("plugin.json");
        var missing = new List<string>();

        foreach (var key in new[] { "name", "description" }) if (!manifest.TryGetProperty(key + "De", out _)) missing.Add(key);
        foreach (var permission in manifest.GetProperty("permissions").EnumerateArray()) RequireGerman(permission, "permission", ["label"], missing);
        foreach (var page in manifest.GetProperty("pages").EnumerateArray()) RequireGerman(page, "page", ["title"], missing);
        foreach (var source in manifest.GetProperty("dataSources").EnumerateArray()) RequireGerman(source, "dataSource", ["name", "description"], missing);

        Assert.True(missing.Count == 0, $"No German text for: {string.Join(", ", missing)}");
    }
}
