using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Kyuplan.Plugins.Example.Tests;

/// <summary>
/// The endpoints end to end: in a web server in memory, behind the same "signed in" rule as in Kyuplan, with users who have exactly the permissions a test gives them.
/// The permissions are the ones of the manifest: <c>example.view</c>, <c>example.edit</c> and <c>example.manage</c>.
/// </summary>
public class EndpointTests
{
    private const string View = "example.view";
    private const string Edit = "example.edit";
    private const string Manage = "example.manage";

    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static async Task<string?> DetailOf(HttpResponseMessage response) => (await JsonOf(response)).GetProperty("detail").GetString();

    private static async Task<string[]> TitlesAsync(HttpClient client) =>
        [.. (await JsonOf(await client.GetAsync("/api/plugins/example/notes"))).EnumerateArray().Select(n => n.GetProperty("title").GetString()!)];

    // ---- who may call what ----

    /// <summary>Every endpoint: how it is called, the permission it asks for (none = every signed-in user), and a body that is fine.</summary>
    public static TheoryData<string, string, string?, string?> Endpoints() => new()
    {
        { "GET", "notes", View, null },
        { "POST", "notes/add", Edit, """{"title":"A note"}""" },
        { "PUT", "notes/pin", Edit, """{"id":1}""" },
        { "DELETE", "notes/clear", Manage, null },
        { "GET", "about", null, null },
        { "POST", "about/hello", null, null },
    };

    private static HttpRequestMessage Request(string method, string path, string? body) =>
        new(new HttpMethod(method), $"/api/plugins/example/{path}") { Content = body is null ? null : Body(body) };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Nobody_who_is_not_signed_in_gets_anything(string method, string path, string? permission, string? body)
    {
        await using var host = await ExampleHost.StartAsync();

        var response = await host.Anonymous().SendAsync(Request(method, path, body));

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path} (asks for {permission ?? "nothing"}): {response.StatusCode}");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_user_without_the_permission_is_refused_and_the_other_permissions_do_not_help(string method, string path, string? permission, string? body)
    {
        await using var host = await ExampleHost.StartAsync();
        var others = new[] { View, Edit, Manage }.Where(p => p != permission).Append("other.view").ToArray();

        var response = await host.SignedIn(permissions: others).SendAsync(Request(method, path, body));

        if (permission is null) Assert.True(response.IsSuccessStatusCode); // no permission asked: every signed-in user may
        else Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_user_with_the_permission_is_served(string method, string path, string? permission, string? body)
    {
        await using var host = await ExampleHost.StartAsync();

        var response = await host.SignedIn(permissions: permission is null ? [] : [permission]).SendAsync(Request(method, path, body));

        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {response.StatusCode}");
    }

    [Fact]
    public async Task A_user_with_all_permissions_can_use_every_endpoint()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit, Manage]);

        foreach (var (method, path, _, body) in Endpoints().Select(e => ((string)e[0], (string)e[1], (string?)e[2], (string?)e[3])))
            Assert.True((await client.SendAsync(Request(method, path, body))).IsSuccessStatusCode, $"{method} {path}");
    }

    // ---- the notes ----

    [Fact]
    public async Task The_notes_start_with_two_samples_pinned_first_in_the_shape_the_table_reads()
    {
        await using var host = await ExampleHost.StartAsync();

        var rows = (await JsonOf(await host.SignedIn(permissions: View).GetAsync("/api/plugins/example/notes"))).EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Read the page tour", rows[0].GetProperty("title").GetString());
        Assert.True(rows[0].GetProperty("pinned").GetBoolean());
        Assert.Equal(["id", "title", "category", "priority", "pinned", "created"], rows[0].EnumerateObject().Select(p => p.Name)); // the keys of the columns in ui/notes.json
        Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d UTC$", rows[0].GetProperty("created").GetString());
    }

    [Fact]
    public async Task A_note_is_added_trimmed_and_with_the_defaults_for_what_the_form_leaves_out()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit]);

        var response = await client.PostAsync("/api/plugins/example/notes/add", Body("""{"title":"  Buy belts  "}"""));
        var note = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("Buy belts", "general", 3, false), (note.GetProperty("title").GetString(), note.GetProperty("category").GetString(), note.GetProperty("priority").GetInt32(), note.GetProperty("pinned").GetBoolean()));
        Assert.Equal(3, note.GetProperty("id").GetInt32()); // after the two samples
        Assert.Contains("Buy belts", await TitlesAsync(client));
    }

    [Fact]
    public async Task A_note_takes_every_field_of_the_form_and_a_number_may_be_text()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit]);

        var response = await client.PostAsync("/api/plugins/example/notes/add",
            Body("""{"title":"Plan the exam","category":"todo","priority":"1","pinned":true,"details":"Book the hall.\nTell the parents."}"""));
        var note = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("todo", 1, true), (note.GetProperty("category").GetString(), note.GetProperty("priority").GetInt32(), note.GetProperty("pinned").GetBoolean()));
        Assert.Equal("Plan the exam", (await TitlesAsync(client))[0]); // pinned and most urgent: first
    }

    [Theory]
    [InlineData("""{}""", "A note needs a title.")]
    [InlineData("""{"title":null}""", "A note needs a title.")]
    [InlineData("""{"title":"   "}""", "A note needs a title.")]
    [InlineData("""{"title":"a\nb"}""", "The title must be one line of text.")]
    [InlineData("""{"title":"x","category":"secret"}""", "The category must be one of: general, todo, idea.")]
    [InlineData("""{"title":"x","priority":0}""", "The priority must be between 1 (urgent) and 5.")]
    [InlineData("""{"title":"x","priority":6}""", "The priority must be between 1 (urgent) and 5.")]
    [InlineData("""{"title":"x","priority":-1}""", "The priority must be between 1 (urgent) and 5.")]
    public async Task A_note_that_does_not_fit_is_refused_with_a_reason_the_user_can_read(string json, string reason)
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit]);

        var response = await client.PostAsync("/api/plugins/example/notes/add", Body(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(reason, await DetailOf(response));
        Assert.Equal(2, (await TitlesAsync(client)).Length); // nothing was added
    }

    [Fact]
    public async Task The_limits_of_a_note_hold_exactly_at_the_limit()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: Edit);

        var atLimit = JsonSerializer.Serialize(new { title = new string('t', ExampleEndpoints.MaxTitleLength), priority = 5, details = new string('d', ExampleEndpoints.MaxDetailsLength) });
        var tooLongTitle = JsonSerializer.Serialize(new { title = new string('t', ExampleEndpoints.MaxTitleLength + 1) });
        var tooLongDetails = JsonSerializer.Serialize(new { title = "x", details = new string('d', ExampleEndpoints.MaxDetailsLength + 1) });

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/plugins/example/notes/add", Body(atLimit))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/plugins/example/notes/add", Body(tooLongTitle))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/plugins/example/notes/add", Body(tooLongDetails))).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("""{"title":["a"]}""")]
    [InlineData("""{"title":"x","priority":"high"}""")]
    [InlineData("""{"title":"x","pinned":"maybe"}""")]
    public async Task A_body_that_is_no_note_at_all_is_refused_before_the_code_sees_it(string json)
    {
        await using var host = await ExampleHost.StartAsync();

        var response = await host.SignedIn(permissions: Edit).PostAsync("/api/plugins/example/notes/add", Body(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_full_store_says_so_instead_of_growing()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: Edit);

        for (var i = 0; i < NoteStore.MaxNotes - 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/plugins/example/notes/add", Body($$"""{"title":"Note {{i}}"}"""))).StatusCode);
        var response = await client.PostAsync("/api/plugins/example/notes/add", Body("""{"title":"One too many"}"""));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("delete some first", await DetailOf(response));
    }

    [Fact]
    public async Task A_note_is_pinned_and_unpinned_by_its_number()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit]);

        var pinned = await JsonOf(await client.PutAsync("/api/plugins/example/notes/pin", Body("""{"id":2}""")));
        var unpinned = await JsonOf(await client.PutAsync("/api/plugins/example/notes/pin", Body("""{"id":"2"}"""))); // a number may be text

        Assert.True(pinned.GetProperty("pinned").GetBoolean());
        Assert.False(unpinned.GetProperty("pinned").GetBoolean());
    }

    [Fact]
    public async Task Pinning_a_note_that_is_not_there_or_without_a_number_is_refused_with_a_reason()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: Edit);

        var missing = await client.PutAsync("/api/plugins/example/notes/pin", Body("""{"id":99}"""));
        var none = await client.PutAsync("/api/plugins/example/notes/pin", Body("""{}"""));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("There is no note with the number 99.", await DetailOf(missing));
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal("Which note? Enter its number.", await DetailOf(none));
    }

    [Fact]
    public async Task Deleting_all_notes_needs_the_permission_for_it_and_leaves_the_store_usable()
    {
        await using var host = await ExampleHost.StartAsync();
        var editor = host.SignedIn(permissions: [View, Edit]);
        var manager = host.SignedIn(permissions: [View, Edit, Manage]);

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync("/api/plugins/example/notes/clear")).StatusCode);
        Assert.Equal(2, (await TitlesAsync(editor)).Length); // refused: still there

        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync("/api/plugins/example/notes/clear")).StatusCode);
        Assert.Empty(await TitlesAsync(manager));

        await editor.PostAsync("/api/plugins/example/notes/add", Body("""{"title":"After the clear"}"""));
        Assert.Equal(["After the clear"], await TitlesAsync(manager));
    }

    [Fact]
    public async Task Adding_notes_at_the_same_time_loses_none()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit]);

        var responses = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => client.PostAsync("/api/plugins/example/notes/add", Body($$"""{"title":"Parallel {{i}}"}"""))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(42, (await TitlesAsync(client)).Length);
    }

    // ---- the page every signed-in user may open ----

    [Fact]
    public async Task The_about_page_shows_the_plugin_the_settings_the_clock_and_who_said_hello()
    {
        await using var host = await ExampleHost.StartAsync(("DataSource:Kind", "example"), ($"{ExampleOptions.Section}:Remark", "Trainings are paused in August."));
        var client = host.SignedIn("Aiko");

        async Task<Dictionary<string, string>> RowsAsync() =>
            (await JsonOf(await client.GetAsync("/api/plugins/example/about"))).EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("value").GetString()!);

        var before = await RowsAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/plugins/example/about/hello", null)).StatusCode);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        var after = await RowsAsync();

        Assert.Equal(["Plugin", "Chosen data source", "Remark of the administrator", "Server time", "Greetings so far", "Last greeting from"], before.Keys);
        Assert.Equal("example 1.2.3", before["Plugin"]);
        Assert.Equal("example", before["Chosen data source"]);
        Assert.Equal("Trainings are paused in August.", before["Remark of the administrator"]);
        Assert.Equal("2026-10-27 12:00 UTC", before["Server time"]);
        Assert.Equal(("0", "-"), (before["Greetings so far"], before["Last greeting from"]));
        Assert.Equal(("1", "Aiko"), (after["Greetings so far"], after["Last greeting from"]));
        Assert.Equal("2026-10-27 12:05 UTC", after["Server time"]);
    }

    [Fact]
    public async Task The_about_page_copes_with_settings_that_are_not_there()
    {
        await using var host = await ExampleHost.StartAsync();

        var rows = (await JsonOf(await host.SignedIn().GetAsync("/api/plugins/example/about"))).EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("value").GetString()!);

        Assert.Equal(("-", ""), (rows["Chosen data source"], rows["Remark of the administrator"]));
    }

    [Fact]
    public async Task Greetings_are_counted_per_greeting_even_when_they_come_at_the_same_time()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn();

        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => client.PostAsync("/api/plugins/example/about/hello", null)));
        var rows = (await JsonOf(await client.GetAsync("/api/plugins/example/about"))).EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("value").GetString()!);

        Assert.Equal("30", rows["Greetings so far"]);
        Assert.Equal("tester", rows["Last greeting from"]);
    }

    // ---- an endpoint that is not there ----

    [Fact]
    public async Task Other_addresses_and_methods_are_not_answered()
    {
        await using var host = await ExampleHost.StartAsync();
        var client = host.SignedIn(permissions: [View, Edit, Manage]);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/plugins/example/secrets")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync("/api/plugins/example/notes")).StatusCode);
    }
}
