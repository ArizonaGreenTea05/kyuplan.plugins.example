using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kyuplan.Plugins.Example;

/// <summary>The names of the permissions in <c>plugin.json</c> (<c>permissions[].name</c>). The full id of one is <c>example.view</c>: <c>IPluginBuilder.Permission("view")</c>.</summary>
public static class ExamplePermissions
{
    public const string View = "view";

    public const string Edit = "edit";

    public const string Manage = "manage";
}

/// <summary>What a form of the page "Example notes" sends: field id → value. Numbers may arrive as numbers or as text, and every field may be missing.</summary>
public sealed record NewNote(string? Title, string? Category, int? Priority, bool? Pinned, string? Details);

/// <summary>What the form "Pin or unpin" sends.</summary>
public sealed record PinRequest(int? Id);

/// <summary>A row of the table "All notes": the columns of the layout pick these properties (<c>columns[].key</c>), written the way JSON writes them (<c>id</c>, <c>title</c>, ...).</summary>
public sealed record NoteRow(int Id, string Title, string Category, int Priority, bool Pinned, string Created)
{
    public static NoteRow From(Note note) =>
        new(note.Id, note.Title, note.Category, note.Priority, note.Pinned, note.Created.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
}

/// <summary>A row of the table on the page "About the example".</summary>
public sealed record AboutRow(string Name, string Value);

/// <summary>
/// The endpoints of the plugin, below <c>/api/plugins/example</c>. Every one of them needs a login (Kyuplan puts the group behind it); <c>RequirePermission</c> narrows
/// it to the people who have the permission, and an endpoint without it is open to <b>every signed-in user</b>. A page that is hidden from somebody is no protection:
/// the endpoints the page uses must ask for the permission themselves.
/// </summary>
public static class ExampleEndpoints
{
    public static readonly string[] Categories = ["general", "todo", "idea"];

    public const int MaxTitleLength = 100;

    public const int MaxDetailsLength = 500;

    public static void Map(RouteGroupBuilder routes, IPluginBuilder plugin, NoteStore notes)
    {
        var view = plugin.Permission(ExamplePermissions.View);
        var edit = plugin.Permission(ExamplePermissions.Edit);
        var manage = plugin.Permission(ExamplePermissions.Manage);
        var log = plugin.Loggers.CreateLogger("Kyuplan.Plugins.Example");

        // ---- the page "Example notes" ----

        // A table reads a GET and shows the array of objects it answers.
        routes.MapGet("notes", () => notes.All().Select(NoteRow.From)).RequirePermission(view);

        // A form sends a POST, PUT or DELETE with its values as a JSON object. The path of a page's action is fixed (no {id} in it): the form asks for the id.
        // Never trust a body: it comes from a browser. Answer with a problem (400/404/409): the client shows its detail to the user.
        routes.MapPost("notes/add", (NewNote body, TimeProvider time) =>
            {
                if (Check(body) is { } problem) return Results.Problem(problem, statusCode: StatusCodes.Status400BadRequest);

                var note = notes.Add(body.Title!.Trim(), Category(body), body.Priority ?? 3, body.Pinned ?? false, string.IsNullOrWhiteSpace(body.Details) ? null : body.Details.Trim(), time.GetUtcNow());
                return note is null
                    ? Results.Problem($"There are already {NoteStore.MaxNotes} notes: delete some first.", statusCode: StatusCodes.Status409Conflict)
                    : Results.Ok(NoteRow.From(note));
            })
            .RequirePermission(edit); // any one of several permissions works too: RequirePermission(edit, manage)

        routes.MapPut("notes/pin", (PinRequest body) =>
            {
                if (body.Id is not { } id) return Results.Problem("Which note? Enter its number.", statusCode: StatusCodes.Status400BadRequest);

                return notes.TogglePin(id) is { } note
                    ? Results.Ok(NoteRow.From(note))
                    : Results.Problem($"There is no note with the number {id}.", statusCode: StatusCodes.Status404NotFound);
            })
            .RequirePermission(edit);

        // A button sends no body. The page shows it to everybody who may open it; only people with the permission get past this check (403 for the others).
        routes.MapDelete("notes/clear", () =>
            {
                notes.Clear();
                return Results.NoContent();
            })
            .RequirePermission(manage);

        // ---- the page "About the example": no permission, so every signed-in user may call these ----

        var gate = new Lock(); // the endpoints run on several requests at once
        var greetings = 0;
        string? lastGreeter = null;

        // An endpoint can ask for the services of the API (IConfiguration, TimeProvider, ...) like any minimal API.
        routes.MapGet("about", (IConfiguration configuration, TimeProvider time) =>
        {
            var settings = configuration.GetSection(ExampleOptions.Section); // the same section the data source reads
            int count;
            string? last;
            lock (gate) (count, last) = (greetings, lastGreeter);

            return new[]
            {
                new AboutRow("Plugin", $"{plugin.Id} {plugin.Version}"),
                new AboutRow("Chosen data source", configuration["DataSource:Kind"] ?? "-"),
                new AboutRow("Remark of the administrator", settings["Remark"] ?? ""),
                new AboutRow("Server time", time.GetUtcNow().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)),
                new AboutRow("Greetings so far", count.ToString(CultureInfo.InvariantCulture)),
                new AboutRow("Last greeting from", last ?? "-"),
            };
        });

        // The signed-in user is available as a parameter. After a button or a form succeeded the tables are read again, which is how the counter above changes.
        routes.MapPost("about/hello", (ClaimsPrincipal user) =>
        {
            int count;
            lock (gate)
            {
                count = ++greetings;
                lastGreeter = user.Identity?.Name ?? "someone";
            }

            log.LogInformation("The example plugin was greeted {Count} times.", count); // no names and no secrets in a log
            return Results.NoContent();
        });
    }

    private static string Category(NewNote note) => string.IsNullOrWhiteSpace(note.Category) ? Categories[0] : note.Category;

    /// <summary>What is wrong with the body, in words for the user, or <c>null</c>. The settings form is checked the same way by <c>PluginRules</c> in Kyuplan; the body of your own endpoints is yours to check.</summary>
    internal static string? Check(NewNote? note)
    {
        if (note is null) return "Send a note.";
        if (string.IsNullOrWhiteSpace(note.Title)) return "A note needs a title.";
        if (note.Title.Trim().Length > MaxTitleLength) return $"The title can have at most {MaxTitleLength} characters.";
        if (note.Title.Any(char.IsControl)) return "The title must be one line of text.";
        if (!string.IsNullOrWhiteSpace(note.Category) && !Categories.Contains(note.Category)) return $"The category must be one of: {string.Join(", ", Categories)}.";
        if (note.Priority is < 1 or > 5) return "The priority must be between 1 (urgent) and 5.";
        if (note.Details?.Length > MaxDetailsLength) return $"The details can have at most {MaxDetailsLength} characters.";

        return null;
    }
}
