<p align="center">
  <b>kyuplan example plugin</b><br>
  A small, complete plugin for <a href="https://github.com/ArizonaGreenTea05/kyuplan">kyuplan</a>, the exam planner for karate clubs. Read it, run it, copy it as the start of your own.
</p>

<p align="center">
<a href="https://github.com/ArizonaGreenTea05/kyuplan.plugins.example/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/ArizonaGreenTea05/kyuplan.plugins.example?include_prereleases&sort=semver"></a> <a href="https://github.com/ArizonaGreenTea05/kyuplan.plugins.example/actions/workflows/ci.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/ArizonaGreenTea05/kyuplan.plugins.example/ci.yml?branch=main&label=CI"></a> <a href="https://github.com/ArizonaGreenTea05/kyuplan.plugins.example/issues"><img alt="Open issues" src="https://img.shields.io/github/issues/ArizonaGreenTea05/kyuplan.plugins.example"></a> <a href="LICENSE"><img alt="MIT licence" src="https://img.shields.io/github/license/ArizonaGreenTea05/kyuplan.plugins.example"></a> <a href="https://github.com/ArizonaGreenTea05/kyuplan"><img alt="Made for kyuplan" src="https://img.shields.io/badge/for-kyuplan-blue"></a>
</p>

<p align="center"><a href="docs/README.md">Documentation</a> · <a href="docs/anatomy.md">Anatomy of a plugin</a> · <a href="docs/make-it-yours.md">Make it yours</a> · <a href="docs/development.md">Development</a></p>

A kyuplan **plugin** is a package (`.kyuplugin`) that adds things to a kyuplan installation without touching kyuplan itself. This repository is a plugin that uses **every option** the plugin interface has, so you can see how each one looks in code, in JSON and on screen, and then keep what you need.

| A plugin can add … | The example shows … | Where |
|---|---|---|
| a **data source** (where groups, members and events come from) | one group of made-up members who train once a week, with hosts, answers, cancelled trainings, a cache and a refresh | [`ExampleDataSource.cs`](src/Kyuplan.Plugins.Example/ExampleDataSource.cs) |
| a **settings form** for it, drawn by every client | text, number, select, checkbox, multiline, a list of rows, a password, a rule that two fields belong together, English and German | [`ui/settings.json`](src/Kyuplan.Plugins.Example/ui/settings.json), [`ExampleOptions.cs`](src/Kyuplan.Plugins.Example/ExampleOptions.cs) |
| **endpoints** below `/api/plugins/example/` | GET, POST, PUT and DELETE; open to every signed-in user or narrowed to a permission; errors with a reason | [`ExampleEndpoints.cs`](src/Kyuplan.Plugins.Example/ExampleEndpoints.cs) |
| **permissions** for roles and users | `view`, `edit` and `manage`, checked by the endpoints (not only by hiding a page) | [`plugin.json`](src/Kyuplan.Plugins.Example/plugin.json) |
| **pages** in the navigation | a page with a table, two forms and a button behind a permission; a page for everybody | [`ui/notes.json`](src/Kyuplan.Plugins.Example/ui/notes.json), [`ui/about.json`](src/Kyuplan.Plugins.Example/ui/about.json) |

It also shows what surrounds the code: tests that check the plugin the way kyuplan will (the manifest, the JSON, the permissions of every endpoint), a CI workflow, and a release workflow that builds the package from a tag.

## Use it as a template

1. Click **Use this template** on GitHub (or clone and delete `.git`).
2. Follow [Make it yours](docs/make-it-yours.md): rename, delete what you do not need, keep the tests green.
3. Tag `v0.1.0`: the release workflow attaches your `.kyuplugin`, and an administrator installs it on kyuplan's **Plugins** page with your repository's address.

## Quick start

You need the .NET 10 SDK and the kyuplan repository checked out **next to this one** (the plugin is built against its `Kyuplan.Plugins.Abstractions`):

```bash
git clone https://github.com/ArizonaGreenTea05/kyuplan.git
git clone https://github.com/ArizonaGreenTea05/kyuplan.plugins.example.git
cd kyuplan.plugins.example
dotnet test Kyuplan.Plugins.Example.sln     # no network, no account
bash scripts/pack.sh                        # Windows: scripts/pack.ps1 → artifacts/kyuplan-plugin-example.kyuplugin
```

Then put the package into a **new** kyuplan installation (before its first start) or install it from a release, and open the pages: [Development](docs/development.md#trying-it-in-kyuplan).

## Documentation

| Page | For | What it covers |
|---|---|---|
| [Anatomy of a plugin](docs/anatomy.md) | Developers | Every file, and which option of the plugin interface it shows |
| [Make it yours](docs/make-it-yours.md) | Developers | Turning the template into your plugin, step by step |
| [Development](docs/development.md) | Developers | Building, testing, packaging, trying it in kyuplan |
| [Configuration](docs/configuration.md) | Administrators | The settings of the example data source, also by environment variable |
| [Releasing](docs/releasing.md) | Maintainers | Tags, the workflows, versions |
| [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) | Everyone | How to help; what a plugin must keep safe |

How plugins work in general: [Plugin development](https://github.com/ArizonaGreenTea05/kyuplan/blob/main/docs/plugin-development.md) in the kyuplan repository. A plugin that does real work: the [Spond plugin](https://github.com/ArizonaGreenTea05/kyuplan.plugins.spond).

## Licence

[MIT](LICENSE).
