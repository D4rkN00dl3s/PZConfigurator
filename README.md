# PZConfigurator

Cross-platform desktop GUI for editing **Project Zomboid** server settings (Avalonia / .NET 10).

## What it does

- Lists servers found in `%USERPROFILE%\Zomboid\Server` (any folder selectable via Browse). A server = `name.ini` + `name_SandboxVars.lua`.
- Two tabs: **Server (ini)** and **Sandbox**. Each tab has a category list on the left (Details, Map, PVP, Zombie, World, Loot, ... — grouping authored by this app since the files don't carry it); clicking a category shows only its settings on the right, inputs aligned to the right edge.
- Bools → checkboxes, enums (from comments) → dropdowns, `Min:`/`Max:` from comments → bounded numeric fields, everything else → text field. Setting descriptions appear on hover.
- Edit freely, press **Save**: only changed values are rewritten — comments, untouched lines and their order stay byte-identical (round-trip tested). Changes land in the game folder directly, so no manual copying.

## Safety

- Nothing is written until Save; Save writes via temp file + atomic replace, keeping the previous version as `name.ini.bak` / `name_SandboxVars.lua.bak` for rollback.
- Some settings are only re-read by the server on restart.
- Unsaved changes are flagged in the title and a confirm dialog appears on close.

## Usage

```sh
dotnet run --project PZConfigurator   # run
dotnet test                           # tests (round-trip + save safety on captured configs)
```

Layout: `PZConfigurator/Config` (parsers, category map), `ViewModels/` (picker, filtering, editors), `Views/` (UI), `PZConfigurator.Tests/`.

Known limits: `Mods=` is a plain text field; settings the parsers can't classify appear as text boxes or under "Other".
