# Anatomy of a plugin

A plugin is **C# code** (one class that implements `IKyuplanPlugin`) plus **JSON files** (the manifest, the pages, the settings form). The JSON describes what the user sees and **kyuplan's clients draw it** (web, Windows, Linux), so a plugin has no UI code and nothing platform specific.
This page walks through the repository and says which option of the plugin interface each file shows. The rules behind it all are in kyuplan's [Plugin development](https://github.com/ArizonaGreenTea05/kyuplan/blob/main/docs/plugin-development.md).

```
src/Kyuplan.Plugins.Example/
  plugin.json            the manifest: who the plugin is and what it adds
  ExamplePlugin.cs       the entry point: registers the data source and the endpoints
  ExampleOptions.cs      the settings of the data source, read from the configuration
  ExampleDataSource.cs   the data source (IDataSource)
  ExampleEndpoints.cs    the endpoints the pages use, and their permissions
  NoteStore.cs           the state behind the notes page
  ui/settings.json       the settings form of the data source
  ui/notes.json          a page behind a permission
  ui/about.json          a page for every signed-in user
tests/Kyuplan.Plugins.Example.Tests/   the tests (see below)
scripts/pack.sh, pack.ps1              build the .kyuplugin package
.github/workflows/                     CI and release
```

## What is in the package

`dotnet publish` puts into one folder what the zip holds in its root: `plugin.json`, the assembly (`Kyuplan.Plugins.Example.dll`) and the `ui/` files. Not in it: the framework and `Kyuplan.Plugins.Abstractions`, which kyuplan provides (the project reference in the `.csproj` has `Private="false"`). Kyuplan loads the package **once when it starts**, into a load context of its own; installing, updating or removing a plugin restarts the API.

## The manifest: `plugin.json`

| Property | What it does here |
|---|---|
| `id` (`example`) | Starts the plugin's permissions (`example.view`) and endpoint addresses (`/api/plugins/example/…`). Lower case letters, digits and hyphens. |
| `name`/`nameDe`, `description`/`descriptionDe` | English and German texts; without a German one the English one is shown. |
| `version` | Dotted numbers. The release workflow writes the version of the tag into it. |
| `assembly` | The `.dll` that holds the `IKyuplanPlugin`. A test keeps it equal to the project's name. |
| `permissions[]` | Three permissions: `view`, `edit`, `manage`. Roles and users get them on kyuplan's role and user forms. |
| `pages[]` | Two pages. `notes` has `"permission": "view"`, so only people with `example.view` see it in the navigation. `about` has none: every signed-in user sees it. `icon` is one of the names the clients know (`tag`, `puzzle`, `chart`, …). |
| `dataSources[]` | One data source. `settings` is the form; `configSection` is left out, so the settings live in `Plugins:example`. Name a section of your own (`"configSection": "Acme"`) if your settings had a home before they were a plugin. |

## The entry point: `ExamplePlugin.cs`

`Initialize(IPluginBuilder)` is called once. It registers

- **a data source**: `builder.AddDataSource("example", create)`. The id is the one in `plugin.json`. `create` runs when the API starts with this data source *chosen* on the Settings page, and gets the settings section, the clock (`TimeProvider`), the club's time zone and a logger factory;
- **endpoints**: `builder.MapEndpoints(routes => …)` gives a route group `/api/plugins/example` that needs a login.

The builder also knows the plugin's `Id`, `Version`, `Directory` (its unpacked folder), `Loggers` and `Permission(name)`, the full id of one of the manifest's permissions. A plugin that throws in `Initialize` does not run; the API starts anyway and the Plugins page says why.

## The data source: `ExampleDataSource.cs`

`IDataSource` is what every part of kyuplan analyses. The example keeps its contract:

- **Read only**; ids (`m1`, `training-20260107`) never change between calls or restarts, because members, exams and filters refer to them.
- **Times in the club's time zone** (`ClubTimeZone`), so a training at 18:00 is 17:00 UTC in winter and 16:00 in summer. The range of a call is read in that zone too.
- `GetEventsAsync` returns the events **ending within** the range, and protects itself from absurd ranges (`MaxDays`).
- **Hosts**: `Trainers` lists the members who run the event; they count for none of their statistics (`EventInfo.Participants`). **Answers**: `Responses` has every invited member with an `Rsvp`.
- **Cache and refresh**: the group is reused for `CacheDuration`; the Refresh button calls `InvalidateCache()`. A real data source caches what it fetched from a service.
- **Failing with a message**: when the service cannot be reached, is not configured or refuses the login, throw `DataSourceUnavailableException` with a message that says what to do. The API answers `503` and shows it. Tick *Pretend the service is down* in the settings to see it.
- `IsDemo` stays `false`: the flag is for kyuplan's own generated demo data.

## The settings: `ui/settings.json` and `ExampleOptions.cs`

The form is a list of `elements`. The id of a `field` is the key in the configuration section and the name of a property in `ExampleOptions`:

| Field | `input` | Shows |
|---|---|---|
| `GroupName` | `text` | `required`, `default`, `maxLength` |
| `MemberCount` | `number` | `min`, `max`, a `hint` |
| `TrainingDay` | `select` | `options` with `labelDe` |
| `FirstMemberTrains` | `checkbox` | a `default` of `"true"` |
| `Remark` | `multiline` | read again by an endpoint (the About page) |
| `ExtraMembers` | `list` | rows of fields the user adds and removes (no multiline inside) |
| `ServiceUrl` + `ApiKey` | `text` + `password` | a `together` rule: both or neither. A `password` is stored **encrypted**, never sent back to a client (an empty field keeps it) and arrives in the options decrypted |
| `SimulateOutage` | `checkbox` | for trying the error path |

`heading` and `text` elements structure the form. Every text has a German twin (`textDe`, `labelDe`, `hintDe`, `messageDe`). `CacheDuration` is an option without a field: an administrator may set any key of the section by hand or by environment variable ([Configuration](configuration.md)). Kyuplan checks the form against its rules when the package is installed, and checks every value when somebody saves the form; the same rules are in the tests (below).

## The pages: `ui/notes.json`, `ui/about.json`

A page is a list of elements drawn one under the other. The client draws the page's title (`pages[].title`) itself, so a layout does not repeat it:

| Element | Shows |
|---|---|
| `heading`, `text` | Structure and explanation. |
| `table` | `source` is an endpoint of the plugin that answers a JSON array of objects (`GET notes`); `columns[].key` picks a property of each row; `text` is shown while it is empty. Read again after every successful action. |
| `form` | `elements` are `field`s (and `text`s) as on the settings form; the values go to the `action` as a **JSON object** (field id → value). The label is the button's text. |
| `button` | An `action` without a body. `confirm` makes the client ask first. |

An `action` is a `method` (`POST`, `PUT` or `DELETE`) and a `path` relative to `/api/plugins/example/`. The path is **fixed** (no `{id}` in it), so a form asks for the number of a note (*Pin or unpin*) instead of having it in the address.

## The endpoints: `ExampleEndpoints.cs`

Minimal API, as in any ASP.NET Core app, below `/api/plugins/example`:

| Endpoint | Permission | Shows |
|---|---|---|
| `GET notes` | `view` | what a table reads |
| `POST notes/add` | `edit` | a form; validating a body that comes from a browser; answering a problem (400, 409) with a reason the client shows |
| `PUT notes/pin` | `edit` | a form with a number; 404 with a reason |
| `DELETE notes/clear` | `manage` | a button with `confirm` |
| `GET about` | none | services from the API in the handler (`IConfiguration`, `TimeProvider`); reads the data source's settings |
| `POST about/hello` | none | the signed-in user as `ClaimsPrincipal`; logging |

**Every endpoint needs a login**; `RequirePermission(builder.Permission("view"))` narrows it to people with that permission (any one of several if you pass more: `RequirePermission(edit, manage)`). An endpoint without it is open to **every signed-in user**. Hiding a page protects nothing: the *Delete all notes* button on the Example notes page is shown to everybody who may open the page, and kyuplan's answer to somebody without `example.manage` is `403`. The administrator role has every permission of every plugin.

Never trust a body: it comes from a browser. `ExampleEndpoints.Check` validates, and the limits (`MaxTitleLength`, `NoteStore.MaxNotes`) keep a user from filling the server's memory. `NoteStore` keeps the notes in memory (gone at restart) behind a lock, because requests run at the same time. Put your data in a database or a file of your own.

## The tests

The tests need no network and no account. They are part of the template: copy them and keep them green.

| File | Checks |
|---|---|
| `ManifestTests` | The manifest and every page pass kyuplan's rules (`PluginRules`, from `Kyuplan.Contracts`), or kyuplan would refuse to install the package. Every endpoint a page calls exists with that method; every endpoint of a page with a permission asks for a permission; every text has a German twin; the permissions in the code are the ones in the manifest; there is exactly one `IKyuplanPlugin`. |
| `OptionsTests` | The form and `ExampleOptions` are the same keys, with the same defaults; the secrets are the password fields; what the form accepts and refuses. |
| `DataSourceTests` | The contract above: ids, time zones and summer time, ranges, hosts, answers, cache, the error path, the plugin building the source from the configuration. |
| `EndpointTests` | Every endpoint end to end in an in-memory web server behind the same "signed in" rule as in kyuplan: 401 without a login, 403 without the permission, success with it; validation; limits; concurrency. `ExampleHost` is the helper to copy. |
| `NoteStoreTests` | The state behind the page. |
