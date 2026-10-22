# PZConfigurator

A cross-platform desktop GUI for editing **Project Zomboid** server settings.

Instead of hand-editing INI and Lua files, you get form fields: checkboxes for booleans, dropdowns for enum options (parsed from the file comments, e.g. `1 = Sprinters`), bounded numeric inputs using the `Min:`/`Max:` values from the comments, and tooltips with the full original descriptions. Sections in `*_SandboxVars.lua` (ZombieLore, Map, ...) become group headers.

## Which files it touches

The app edits **only** two files per server, found in your Zomboid server config folder (default `%USERPROFILE%\Zomboid\Server`):

- `name.ini` — general server settings
- `name_SandboxVars.lua` — sandbox/game settings

Both are required to exist with the same `name` for a server to be listed. It never creates, renames or copies files — changes apply in place so the server picks them up directly (some settings are only re-read on server restart).

## Safety

- **Nothing is written until you press Save.**
- Only values you changed are rewritten; every comment, blank line and untouched setting stays **byte-identical** (verified in tests).
- Writing goes to a temp file and is atomically swapped in, and the previous version of each changed file is kept as `name.ini.bak` / `name_SandboxVars.lua.bak` — delete the changed file and remove `.bak` to roll back.
- Unsaved changes are flagged in the title (`*`) and a confirm dialog appears on close.

## Build & run

Requires the .NET SDK (10.x):

```sh
dotnet run --project PZConfigurator
```

Or from the IDE (Visual Studio / Rider / VS Code with C# Dev Kit) via `PZConfigurator.slnx`.

Run the tests:

```sh
dotnet test
```

Tests round-trip real captured config files (`PZConfigurator.Tests/TestData/`) to guarantee the parsers preserve files exactly.

## Project layout

```
PZConfigurator/
  Config/ConfigParser.cs    ini + lua parsers (comment-driven widgets: enums, min/max, bool/int/float/text)
  Config/Setting.cs         data model: Setting, SettingGroup, ConfigFile
  ViewModels/               server picker, search filtering, per-setting editor VMs
  Views/                    Avalonia UI (MainWindow, unsaved-changes confirm dialog)
PZConfigurator.Tests/       parser round-trip + save/safety tests
```

## Not (yet) supported

- Creating new servers or editing `mod`s list as a structured editor (`Mods=` is a plain text field)
- Settings the parser can't classify appear as plain text fields — still written correctly
- Starting/stopping the server
