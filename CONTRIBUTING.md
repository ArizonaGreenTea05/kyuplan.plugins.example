# Contributing

Thanks for helping. Small fixes and big ideas are both welcome.

## Before you start

- Look through the [open issues](https://github.com/ArizonaGreenTea05/kyuplan.plugins.example/issues). For a larger change, open an issue first so we agree on the direction.
- This repository is a **template**: it should stay small, readable and complete. A new option of the plugin interface belongs here when kyuplan gains it; a feature of one particular service does not.
- Problems with kyuplan itself (pages, statistics, how plugins are loaded) belong in the [kyuplan repository](https://github.com/ArizonaGreenTea05/kyuplan/issues).

## Workflow

1. Fork and create a branch from `main`.
2. Set up as in [Development](docs/development.md) (kyuplan checked out next to this repository).
3. Make the change **with tests** (bug fixes need a test that fails without the fix), and update the documentation in `docs/` if behaviour, settings or files change.
4. `dotnet test Kyuplan.Plugins.Example.sln` must pass.
5. Open a pull request. Say what changed and why; link the issue.

## Guidelines

- Comments explain *why* an option is used the way it is: readers copy this code.
- Every text a user sees has an English and a German version.
- Settings: every form field must be an option of `ExampleOptions`, with the same default.
- Never include real credentials, real names or club data in code, tests, issues or logs.
- Match the surrounding code: naming, comment density, LF line endings.
