# Documentation of the example plugin

The example plugin is a template for [kyuplan](https://github.com/ArizonaGreenTea05/kyuplan) plugins. Pick the page that fits what you want to do.

**I want to write a plugin**
- [Anatomy of a plugin](anatomy.md): every file of this repository and the option of the plugin interface it shows. Start here.
- [Make it yours](make-it-yours.md): the checklist for turning this template into your own plugin.
- [Development](development.md): build, test, package, and try the package in a running kyuplan.

**I run a kyuplan instance**
- [Configuration](configuration.md): the settings of the example data source.

**I maintain a plugin made from this**
- [Releasing](releasing.md): how a version gets out.
- [Contributing](../CONTRIBUTING.md), [Security policy](../SECURITY.md).

Words used throughout: a **plugin** is a package (`.kyuplugin`) that adds data sources, pages, permissions or endpoints to kyuplan; a **data source** is where kyuplan reads its groups, members and events from;
a **manifest** is the file `plugin.json` that says what a plugin adds; a **layout** is the JSON file that describes a page or a settings form, which every kyuplan client draws.
