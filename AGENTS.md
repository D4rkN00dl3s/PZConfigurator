# AGENTS.md — PZConfigurator

Desktop GUI (Avalonia 12 / .NET 10) for editing Project Zomboid dedicated-server configs:
`name.ini` + `name_SandboxVars.lua` inside the server's `Zomboid\Server` folder.

## Commands

```sh
dotnet test -v q --nologo                  # all tests (26)
dotnet test --filter <TestName>            # single test
dotnet build PZConfigurator -v q --nologo  # zero-warning is the bar
dotnet run --project PZConfigurator        # app loads real configs from %USERPROFILE%\Zomboid\Server
.\publish.ps1                              # self-contained builds, all 4 RIDs (not needed for small fixes)
powershell -ExecutionPolicy Bypass -File tools\uicheck.ps1   # UIA smoke: toggle→dirty→Discard→close
powershell -ExecutionPolicy Bypass -File tools\templateapply.ps1  # template apply smoke (Ctrl+Tab + UIA patterns)
```

Tests are hermetic: `MainViewModel(confirmDiscard, folder, userTemplateFolder)` lets tests inject temp dirs, so nothing under the real `%USERPROFILE%\Zomboid` is ever read (the ctor assigns `folder` before `DiscoverAsync` starts, and passing `userTemplateFolder` also skips the real `Zomboid\Sandbox Presets` scan).

## Hard invariants (breaking these breaks user data)

- **Byte-preserving output:** untouched lines must stay byte-identical, including comments, order, spacing before values, trailing commas, BOM, newline style, and *count* of trailing newlines. Parsers keep every raw line; `Setting` stores `LinePrefix/RawValue/LineSuffix` so `Update` rewrites only its own line. `RoundTrip_Unchanged_IsByteIdentical` tests this — run tests after any parser change.
- Only ever write the two config files: `.bak` backup (latest pre-save content), temp file + atomic move, plus timestamped history backups `<file>.<yyyyMMdd-HHmmss-fff>.bak` (UTC; newest `ConfigParser.BackupHistoryCount` = 10 kept, older ones deleted). Never touch anything else in that folder (e.g. `spawnregions.lua`) — the history pattern is the only extra thing the app may create or delete. Before installing, every rendered temp file is verified (line count unchanged, untouched lines byte-identical, parsed setting sequence identical); `SaveVerificationException` refuses the whole save on the first mismatch — verification runs before any `.bak`/history is created, so a refused save leaves no backup debris either.
- `ConfigFile.OriginalText` = the text currently on disk *as seen by the app*. `Save` no-ops when `Render() == OriginalText` and refreshes `OriginalText` after writing; `.bak` must always hold the true pre-last-save content. `Save` refuses to write if disk differs from `OriginalText` (server rewrote it meanwhile) by throwing `ConfigChangedOnDiskException` from `ConfigParser.Save` — the guard lives in the parser so every caller gets it; the VM only surfaces its message.
- Dirty-confirm: `MainViewModel` takes `Func<Task<bool>> confirmDiscard` (injected in `App.axaml.cs` with the MainWindow as dialog owner). Server-switch denial reverts the ComboBox selection using the `_suppress` guard — keep the `_loading`/`_suppress` flags correct when touching selection logic.
- No releases, tags, or publishes without explicit user consent (plain `git push` of master is fine). Ask at milestones.

## Non-obvious architecture facts

- **Categories don't exist in the files.** They are authored in `Config/CategoryMap.cs` (ini keys → category; lua: top-level keys by name, nested sections via `LuaSections`, unknown nested sections → "Mods", unknown keys → "Other"). The order arrays in that file drive display order; `BuildGroups` drops categories not listed in the order array's content — every category must end in "Other".
- **Two panes per tab:** master-detail (selected category) plus a flat "matches" pane for search, toggled by `IsSearching`. Flat lists hold SettingVMs from *all* categories; row visibility in the detail pane is `SettingVM.IsVisible`, maintained by `CollectMatch` — keep both consistent.
- **Avalonia perf trap:** `ItemsControl` has no virtualization by default. All four right-hand panes wrap an explicit `ItemsPanel` of `VirtualizingStackPanel` inside a manual `ScrollViewer` — removing that caused a measured 316 ms first tab switch (75 ms with it). Left panes are `ListBox` (virtualized by default).
- **Control widths are a convention:** numeric 320px + `ButtonSpinnerLocation="Left"`, text 320px, enum Width 320, key label MinWidth 140 — every right-handed input ends at the same right edge. Keep consistent across the four row templates in `MainWindow.axaml`. Checkboxes need `Padding="0"` so the glyph aligns with the other fields' right edges (Fluent CheckBox reserves template padding to the right of the 20px glyph for its text content, which our empty checkboxes never use); boolean rows toggle on DoubleTapped anywhere in the row (`BoolRow_DoubleTapped`, skipped when the tap source is inside a CheckBox — glyph clicks are the checkmark's own toggles; the Source is the template's inner element, so check visual ancestry; the row Border needs `Background="Transparent"` — a null-Background Border isn't hit-testable over its fill).
- Editing side-effect chain: setting a VM property (`NumberValue`, `IsChecked`, `Text`…) *immediately* writes through `ConfigFile.Update` and marks dirty. Each VM holds an `_originalRaw` baseline (value at load/save); `IsModified`/yellow paint = current value differs from that baseline, so toggling a bool on and off clears its paint. `ClearModified` (called from `Save` for every item) re-baselines to the saved value. The `markDirty` closure recomputes `IsDirty`/Title from `Render() != OriginalText` — same check `Save` uses for its no-op. Separately, `IsNonDefault`/sky-blue paint (`TextBlock.nondefault`, `#66C2FF`) = current value differs from the game's vanilla default (values of the embedded Apocalypse.json via `TemplateLoader.VanillaDefaults()`); it survives Save — blue shows what was customized before this session, yellow what changed during it (yellow wins when both apply). Constructor assignments deliberately don't (initial `[ObservableProperty]` field sets don't raise `OnChanged`) — that's why an enum value outside its comment list shows an empty dropdown instead of rewriting a user's value. Tests pin this.
- `EnumSettingVM` options come from comments matching `N = label`; `Min:`/`Max:` in comments become numeric bounds.
- `TemplateLoader` builds the template list from three sources: embedded game-preset JSONs, user JSONs (`%USERPROFILE%\PZConfigurator\Templates\`), and PZ's own saved sandbox presets (`%USERPROFILE%\Zomboid\Sandbox Presets\*.cfg` via `LoadZomboidPresets` — `Key=value` lines, `#`/`--` comment lines skipped, `Version=` is the cfg header, not a setting). `MainViewModel.HasConfigLoaded` is false while no server config pair is loaded and hides the template row plus drives the pane's "Nothing to display" empty state.
- `ConfirmWindow` is hand-rolled (no Avalonia dialog dependency); `OnClosing` clears `IsDirty` before re-`Close()` to avoid a re-entrant cancel loop.

## Sandbox templates

- `Config/TemplateLoader.cs` loads preset JSONs (`PZConfigurator/Templates/*.json`) as embedded `AvaloniaResource` plus any user `*.json` from `%USERPROFILE%\PZConfigurator\Templates\` (same schema: `{ name, description, values: { key: "literal" } }`).
- The preset values are a **one-time snapshot of the current game build** (`GameRoot/media/lua/shared/Sandbox/*.lua`). A live extractor reading the game files directly could be tried sometime later — for now, presets only: to refresh values after a game update, hand-edit or regenerate the JSONs.
- Applying a template goes through the normal write-through SettingVM path (dirty flag, disk-change guard on Save). Bool/enum/number values map to VM properties — unknown keys or unparseable values are counted in Status, never crash.

## Testing quirks

- Fixtures in `PZConfigurator.Tests/TestData/` are copies of a real server's files. If Project Zomboid adds settings, refresh them from the real folder.
- VM tests build fake servers (`a`/`b` ini+lua) in a temp dir and use `SetFolder`; async selection switches use `WaitUntil` polling (≤2.5 s).
- `InternalsVisibleTo("PZConfigurator.Tests")` enables testing `MainViewModel.BuildGroups` directly.

## Windows-only dev checks

UI scripts in `tools/` drive the real window via UIA + SendInput. Avalonia TabItems
expose no `SelectionItemPattern` — click tab-header coordinates instead. Tests can't
catch UI bugs; run the relevant script after XAML changes.

**Docs first:** before hard-coding margins/insets against layout quirks discovered
via the ps1 screenshot/scanner tools, look up the control's actual theme template —
the Fluent control XAML sources (linked from Avalonia docs, "Control themes":
`github.com/AvaloniaUI/Avalonia/tree/main/src/Avalonia.Themes.Fluent/Controls`)
tell you exactly which template part (`Padding`, `MinWidth`, column definition)
causes the offset, and local properties/styles beat theme setters.

## UX batch 7–11 (medium)

- **#7 Remember last server + window geometry** — `%USERPROFILE%\PZConfigurator\settings.json` holds `Folder`, `Server`, `X`, `Y`, `Width`, `Height`, `Maximized` (best-effort try/catch, never blocks startup/exit). Restored in `MainWindow.OnOpened` (position only if it lands on a connected screen via `Screens.All` bounds test), saved on `OnClosing` via `PersistSettings()`. New module: `Config/AppSettings.cs`; wiring: `Views/MainWindow.axaml.cs`.
- **#9 Template preview before Apply** — on `TemplateSelected` change, `Status` shows `Ready to apply "<name>": <description> — N of M settings found. Press Apply, review, then Save`. No confirm dialog (2-step apply already). VM: `MainViewModel.OnTemplateSelectedChanged`.
- **#10 Save button accent when dirty** — `Classes.dirty="{Binding IsDirty}"` on the Save button; `.dirty` style = yellow background, bold font. XAML: `Views/MainWindow.axaml`.
- **#11 Search results show category** — `SettingVM.MatchCategory` set in `CollectMatch` as `"<Category>"` only while `IsSearching`, cleared otherwise; rendered as a dim `FontSize=11` line under the key in all four row templates. VM: `SettingsVM.MatchCategory`/`HasMatchCategory`, `MainViewModel.CollectMatch`; XAML: four row templates in `Views/MainWindow.axaml`.
- **Ctrl+Tab tab switching + search-box focus on open** — `TabControl SelectedIndex="{Binding SelectedTabIndex}"`; `MainWindow_KeyDown` handles Ctrl+Tab (cycles tabs), Ctrl+S (Save), Ctrl+F (focus search); handler attached with `AddHandler(..., handledEventsToo: true)` because Avalonia's keyboard navigation swallows Ctrl+Tab first; search box gets focus on `OnOpened`. UIA scripts use `SelectionItemPattern` on TabItems (no mouse clicks — DPI-safe).


