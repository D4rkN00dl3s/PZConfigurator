# PZConfigurator

Cross-platform desktop GUI for editing **Project Zomboid** server settings (Avalonia / .NET 10).

> **AI-assisted project:** this tool was built with an AI coding agent under human direction — architecture, code review and testing were done iteratively with AI assistance. The author is not responsible for damaged server configs; the `.bak` backups exist for that reason.

## What it does

- Lists servers found in `%USERPROFILE%\Zomboid\Server` (any folder selectable via Browse). A server = `name.ini` + `name_SandboxVars.lua`.
- Two tabs: **Server (ini)** and **Sandbox**. Each tab has a category list on the left (Details, Map, PVP, Zombie, World, Loot, ... — grouping authored by this app since the files don't carry it); clicking a category shows only its settings on the right, inputs aligned to the right edge. Typing in the search box instead shows a flat list of matches from every category of that tab (key names only).
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

## Building & releases

Self-contained single-file binaries (no .NET install needed for users):

```sh
.\publish.ps1                     # win-x64, linux-x64, osx-x64, osx-arm64
.\publish.ps1 -Runtimes win-x64   # just one target
```

Output: `PZConfigurator/bin/Release/net10.0/<rid>/publish/` (~48 MB per target). Pushing a tag like `v1.0.0` triggers the GitHub Actions workflow, which runs tests and attaches all four platform builds to a GitHub Release.

Known limits: `Mods=` is a plain text field; settings the parsers can't classify appear as text boxes or under "Other".

## Linux: opening another user's Zomboid folder

On a dedicated Linux server it's standard practice to run the PZ server under a separate user (e.g. `pz`), so the configs live in `/home/pz/Zomboid`. That home directory is private (`chmod 700` by default), which is why your desktop user — sudoers or not — gets "permission denied": sudo rights are irrelevant here, only file permissions matter.

Grant your desktop user access (replace `pz` and `martin` with your users):

```sh
# Option A — simplest: make the paths reachable
sudo chmod o+x /home/pz                     # traverse, but not list
sudo chmod o+rx /home/pz/Zomboid/Server     # read + traverse
sudo chmod -R o+rwX /home/pz/Zomboid/Server # the app must also be able to WRITE (Save)
```

```sh
# Option B — more precise, affects only your user:
sudo setfacl -m u:martin:x /home/pz
sudo setfacl -R -m u:martin:rwX /home/pz/Zomboid/Server
```

New files created by the server later inherit default permissions, so Option A/B applied once is normally enough; if the server ever recreates a config file as `600`, re-run the command or fall back to a **shared group** (`sudo groupadd srvshare; sudo usermod -aG srvshare pz martin; sudo chgrp -R srvshare /home/pz/Zomboid/Server` + `chmod -R g+rwX`, new files need a setgid bit or a matching umask — see your distro docs). After that, pick the folder via **Browse…** in the app; `PZConfigurator` never needs to run as root or as the server user.

## License

[MIT](LICENSE)
