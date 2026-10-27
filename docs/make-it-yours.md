# Make it yours

The example plugin does a bit of everything so that you can see each option. Yours will do one or two of those things. This is the order that keeps the tests useful while you change it.

## 1. Start from the template

On GitHub press **Use this template** (the repository owner switches that on under *Settings → Template repository*), or clone it and delete `.git`. Keep kyuplan checked out next to it ([Development](development.md#requirements)) and run `dotnet test Kyuplan.Plugins.Example.sln`: everything is green before you change a line.

## 2. Choose the names

| What | Rule |
|---|---|
| **Plugin id** | Lower case letters, digits and hyphens, 2 to 32 characters, starting with a letter. It starts your permissions (`acme.view`) and your addresses (`/api/plugins/acme/…`), and must not be the name of one of kyuplan's own permission domains (`members`, `settings`, …) nor `api` or `plugins`. |
| **Data source id** | Lower case letters, digits and hyphens; unique among all installed plugins; not `demo`, `custom` or `manual`. It is the value of `DataSource:Kind`. |
| **Assembly name** | Any `.dll` name in the root of the package. Keep it equal to the project's name. |

## 3. Rename

Do it with your IDE's rename so that the code follows, then search for what is left (`example` is in file names, namespaces, strings and the tests):

```bash
grep -rIli example . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=artifacts
```

| Rename | Where |
|---|---|
| the repository, the solution, the two project folders and `.csproj` files | `Kyuplan.Plugins.Example.sln`, `src/`, `tests/` |
| namespace `Kyuplan.Plugins.Example`, `RootNamespace`, `InternalsVisibleTo` | the `.cs` files, `src/…/*.csproj` |
| `ExamplePlugin`, `ExamplePlugin.PluginId`, `ExamplePlugin.DataSourceId` | the entry point and everything that names it |
| `id`, `name`/`nameDe`, `description`/`descriptionDe`, `author`, `homepage`, `assembly`, the data source's `id` | `src/…/plugin.json` |
| the section `Plugins:example` in the tests and in `ExampleOptions.Section` | `ExampleOptions.cs`, `tests/` (`ExampleHost`, `EndpointTests`) |
| `Product`, `Authors` | `Directory.Build.props` |
| the file names and the paths in the workflows and scripts | `.github/workflows/`, `scripts/` |
| the texts | `README.md`, `docs/`, `SECURITY.md`, `CONTRIBUTING.md` |

The tests that read `plugin.json` ([`ManifestTests`](../tests/Kyuplan.Plugins.Example.Tests/ManifestTests.cs)) fail when the id, the assembly or the data source of the code and the manifest differ, which is the quickest way to find what you missed.

## 4. Keep what you need, delete what you do not

Each row is a unit that stands on its own. Delete its code, its entry in `plugin.json` and its tests together.

| You do not need … | Delete |
|---|---|
| a data source | `ExampleDataSource.cs`, `ExampleOptions.cs`, `ui/settings.json`, `AddDataSource` in `ExamplePlugin`, the `dataSources` entry, `DataSourceTests`, `OptionsTests` |
| pages and endpoints | `ExampleEndpoints.cs`, `NoteStore.cs`, `ui/notes.json`, `ui/about.json`, `MapEndpoints` in `ExamplePlugin`, the `pages` and `permissions` entries, `EndpointTests`, `NoteStoreTests`, `ExampleHost.cs` |
| only the second page | `ui/about.json`, its `pages` entry, `GET about`, `POST about/hello`, and the tests that name them |
| a permission | its entry in `permissions`, its constant in `ExamplePermissions`, the `RequirePermission` calls |

A plugin that registers nothing does nothing, so keep at least one of the three: a data source, pages, or endpoints.

## 5. Put your logic in

- **A data source.** Replace the made-up data in `ExampleDataSource` with a call to your service. Keep the contract: read only, stable ids, club time zone, `DataSourceUnavailableException` with a message that says what to do, a cache that `InvalidateCache` empties. Add the NuGet packages your service needs to `Directory.Packages.props` and the `.csproj`; they are packed with the plugin.
- **Settings.** Add the property to your options class, the field to `ui/settings.json` (English and German text) and a line to [Configuration](configuration.md). `OptionsTests` fails when form and options differ. Keep the keys of secrets in `password` fields.
- **Pages.** A page is data: copy an element from `ui/notes.json`, point it at an endpoint, and `ManifestTests` checks that the endpoint exists with that method and (for a page behind a permission) asks for a permission itself.
- **Endpoints.** Every endpoint that returns or changes club data gets `RequirePermission(…)`. Validate every body. Put your data in a database or a file of your own: `NoteStore` is in memory on purpose.

## 6. Test, package, try

```bash
dotnet test Kyuplan.Plugins.Example.sln
bash scripts/pack.sh
```

[Development](development.md#trying-it-in-kyuplan) shows how to run the package in a kyuplan of your own.

## 7. Release

Tag `v1.0.0` and push the tag: the [release workflow](releasing.md) attaches the package. Administrators install your plugin with the address of your repository on kyuplan's **Plugins** page, so the repository must be **public**.

Before you do: replace the texts of `README.md`, remove what is only true of the example, and read the [Security policy](../SECURITY.md) and [kyuplan's safety notes](https://github.com/ArizonaGreenTea05/kyuplan/blob/main/docs/plugin-development.md#safety): a plugin runs with the rights of the API.
