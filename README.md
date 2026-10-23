# PZConfigurator

Cross-platform desktop GUI for editing **Project Zomboid** server settings (Avalonia / .NET 10).

> **AI-assisted project:** this tool was built with an AI coding agent under human direction — architecture, code review and testing were done iteratively with AI assistance. The author is not responsible for damaged server configs; the `.bak` backups exist for that reason.

## What it does

- Lists servers found in `%USERPROFILE%\Zomboid\Server` (any folder selectable via Browse). A server = `name.ini` + `name_SandboxVars.lua`.
- Two tabs: **Server (ini)** and **Sandbox**, settings grouped into categories (grouping authored by this app since the files don't carry a natural one). Typing in the search box shows matches from every category of that tab.
- Bools → checkboxes, enums (from comments) → dropdowns, `Min:`/`Max:` from comments → bounded numeric fields, everything else → text field. Setting descriptions appear on hover.
- Press **Save**: only changed values are rewritten — comments and untouched lines stay byte-identical. Changes land in the game folder directly.

## Safety

- Nothing is written until Save; Save writes via temp file + atomic replace, keeping the previous version as `name.ini.bak` / `name_SandboxVars.lua.bak` for rollback.
- Some settings are only re-read by the server on restart.
- Unsaved changes are flagged in the title; a confirm dialog appears on close and before switching servers.

## Downloads

Ready-to-run builds (no .NET install needed) for **win-x64, linux-x64, osx-x64, osx-arm64** are attached to the [latest release](https://github.com/D4rkN00dl3s/PZConfigurator/releases/latest). macOS builds are unsigned — right-click → Open to bypass Gatekeeper.

## Building from source

```sh
dotnet run --project PZConfigurator   # run
dotnet test                           # tests
.\publish.ps1                         # self-contained single-file builds, all 4 targets
```

## Linux: opening another user's Zomboid folder

On a dedicated Linux server it's standard practice to run the PZ server under a separate user (e.g. `pz`), so the configs live in `/home/pz/Zomboid`. That home directory is private (`chmod 700` by default), which is why your desktop user — sudoers or not — gets "permission denied": sudo rights are irrelevant here, only file permissions matter.

Grant your desktop user access (replace `pz` and `usr` with your users):

```sh
# Option A — simplest: make the paths reachable
sudo chmod o+x /home/pz                     # traverse, but not list
sudo chmod o+rx /home/pz/Zomboid/Server     # read + traverse
sudo chmod -R o+rwX /home/pz/Zomboid/Server # the app must also be able to WRITE (Save)
```

```sh
# Option B — more precise, affects only your user:
sudo setfacl -m u:usr:x /home/pz
sudo setfacl -R -m u:usr:rwX /home/pz/Zomboid/Server
```

New files created by the server later inherit default permissions, so Option A/B applied once is normally enough; if the server ever recreates a config file as `600`, re-run the command or fall back to a **shared group** (`sudo groupadd srvshare; sudo usermod -aG srvshare pz usr; sudo chgrp -R srvshare /home/pz/Zomboid/Server` + `chmod -R g+rwX` — see your distro docs). After that, pick the folder via **Browse…** in the app; `PZConfigurator` never needs to run as root or as the server user.

## License

[MIT](LICENSE)
