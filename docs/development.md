# Development

## Requirements

- .NET SDK as pinned in `global.json` (.NET 10).
- The kyuplan repository checked out **next to this one**: the plugin compiles against its `Kyuplan.Plugins.Abstractions` project, and the tests use the rules in `Kyuplan.Contracts` that kyuplan applies to a package.

```bash
git clone https://github.com/ArizonaGreenTea05/kyuplan.git
git clone https://github.com/ArizonaGreenTea05/kyuplan.plugins.example.git
cd kyuplan.plugins.example
```

If kyuplan lives elsewhere, pass `-p:KyuplanRoot=<path>/` to every `dotnet` command (default `../Kyuplan/`, see `Directory.Build.props`).

## Build and test

```bash
dotnet test Kyuplan.Plugins.Example.sln
```

Narrow it: `dotnet test --filter "FullyQualifiedName~DataSourceTests"`. The tests need no network and no account. Package versions are central in `Directory.Packages.props`; the `.csproj` files carry none.

## Package

```bash
bash scripts/pack.sh                 # Linux, macOS, Git Bash
```

```powershell
./scripts/pack.ps1                   # Windows PowerShell
```

Both publish the plugin and zip the result as `artifacts/kyuplan-plugin-example.kyuplugin` (`artifacts/` is ignored by git; pass another folder to put it elsewhere). By hand it is `dotnet publish src/Kyuplan.Plugins.Example -c Release -o publish` and a zip of the **contents** of `publish`: `plugin.json`, the assembly and `ui/` have to be at its root, and the names of the files in the zip should use `/`. `Compress-Archive` and `ZipFile.CreateFromDirectory` of Windows PowerShell 5.1 write `\` instead, and want a `.zip` extension; that is why the script writes the zip entry by entry.

## Trying it in kyuplan

Kyuplan installs the packages in `Plugins:BundledDirectory` when the plugin folder of an installation is **new**, so a throw-away installation is the quickest way to see your plugin. From the kyuplan repository (a fresh folder for the settings, the databases and the plugins; `<try>` is any empty folder and `<example>` this repository):

```bash
export Plugins__BundledDirectory=<example>/artifacts Plugins__Directory=<try>/plugins Setup__SettingsFile=<try>/settings.json
export Database__ConnectionString="Data Source=<try>/kyuplan.db" Accounts__ConnectionString="Data Source=<try>/accounts.db"
export Accounts__InitialAdminPassword=<a password for the test account> Sync__Enabled=false
dotnet run --project src/Kyuplan.Api --launch-profile http
```

```powershell
$env:Plugins__BundledDirectory = '<example>\artifacts'; $env:Plugins__Directory = '<try>\plugins'; $env:Setup__SettingsFile = '<try>\settings.json'
$env:Database__ConnectionString = 'Data Source=<try>\kyuplan.db'; $env:Accounts__ConnectionString = 'Data Source=<try>\accounts.db'
$env:Accounts__InitialAdminPassword = '<a password for the test account>'; $env:Sync__Enabled = 'false'
dotnet run --project src/Kyuplan.Api --launch-profile http
```

Open <http://localhost:5100/> and sign in as `admin`. Then:

1. **Settings → Data source**: choose *Example (sample data)*, change the form, save. The server restarts; the **Members** and **Statistics** pages show the made-up group. (Or start with the environment variable `DataSource__Kind=example`.)
2. **Plugins** (Administration) lists the plugin and whether it runs. The two pages are in the navigation: *Example notes* and *About the example*.
3. **Roles** (Administration): the permissions `example.view`, `example.edit` and `example.manage` are listed under the plugin's name. Make a user with only `example.view` and try *Delete all notes*: the answer is *forbidden*. The administrator has every permission.

A change to the plugin needs the API to restart (plugins are loaded once at start), and the bundled package is only read when the plugin folder is new: delete `<try>` and start again with the new package. An installation that already has plugins takes your plugin from a release instead: *Plugins → Install* with the address of your repository (see [Releasing](releasing.md)).

You can also call the endpoints directly, with HTTP Basic login:

```bash
curl -u admin:<password> http://localhost:5100/api/plugins/example/notes
```

## Changing things

| I want to … | Do |
|---|---|
| add a setting | add the property to `ExampleOptions`, the field to `ui/settings.json` (English and German text) and a line to [Configuration](configuration.md). `OptionsTests` fails if form and options differ. |
| add an endpoint | map it in `ExampleEndpoints.Map` with `RequirePermission(…)` and add it to `Endpoints()` in `EndpointTests`: the 401/403/success checks then cover it. |
| add a page | a file in `ui/`, an entry in `pages` of `plugin.json`, and (for a permission) an entry in `permissions`. `ManifestTests` checks the layout, the endpoints it calls and the German texts. |
| add a permission | `plugin.json`, a constant in `ExamplePermissions`, the `RequirePermission` calls. |
| change what the data source delivers | `ExampleDataSource`, and a test in `DataSourceTests`. |

## Conventions

- Tests are named as sentences (`The_first_member_hosts_the_training_and_takes_no_part`). Every change ships with tests, including the edge cases: empty and duplicate data, limits, summer time, unusual strings, requests at the same time.
- Files use LF line endings (`.gitattributes`); C# latest, nullable on.
- Every text a user sees has an English and a German version.
- Documentation changes with the code. Keep [Configuration](configuration.md) and `ui/settings.json` in step.
- Never log or put a secret in a message; never commit real credentials or real club data (test data is made up).

The kyuplan side of plugins (manifest, JSON of forms, safety) is in kyuplan's [Plugin development](https://github.com/ArizonaGreenTea05/kyuplan/blob/main/docs/plugin-development.md).
