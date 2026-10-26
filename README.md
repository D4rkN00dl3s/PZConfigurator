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

On a dedicated Linux server it's standard practice to run the PZ server under a separate user (e.g. `pz`), so the configs live in `/home/pz/Zomboid`. That home directory is private (`chmod 700` by default), and so is `Zomboid` itself — which is why your desktop user — sudoers or not — gets "permission denied": sudo rights are irrelevant here, only file permissions matter.

Grant your desktop user access (replace `pz` and `usr` with your users):

```sh
# Option B — recommended: affects only your user, not "others"
sudo setfacl -m u:usr:x /home/pz              # traverse, but not list
sudo setfacl -m u:usr:x /home/pz/Zomboid      # traverse, but not list
sudo setfacl -R -m u:usr:rwX /home/pz/Zomboid/Server # read + write + traverse
sudo setfacl -R -d -m u:usr:rwX /home/pz/Zomboid/Server # default ACL: files the server creates later stay accessible
```

```sh
# Option A — simplest, but grants access to "others" (every local user and
# service account). The configs may contain admin, RCON and server passwords,
# so only use this on a single-user machine:
sudo chmod o+x /home/pz                     # traverse, but not list
sudo chmod o+x /home/pz/Zomboid             # traverse, but not list
sudo chmod o+rx /home/pz/Zomboid/Server     # read + traverse
sudo chmod -R o+rwX /home/pz/Zomboid/Server # the app must also be able to WRITE (Save)
```

Option A (plain `chmod` on existing files) is applied once and is enough; **Option B needs the `-d` default ACL line above** — without it, files the server creates *after* you run `setfacl` would again be inaccessible to you. If the server ever recreates a config file as `600` anyway, re-run the command or fall back to a **shared group** (`sudo groupadd srvshare; sudo usermod -aG srvshare pz usr; sudo chgrp -R srvshare /home/pz/Zomboid/Server` + `chmod -R g+rwX` — see your distro docs). After that, pick the folder via **Browse…** in the app; `PZConfigurator` never needs to run as root or as the server user.

When saving, the app preserves the file permission bits of each config file (a `640` file stays `640`); file ownership is not preserved — a saved file ends up owned by the user running the app.

Note that execute-only (`--x`) on `/home/pz` and `/home/pz/Zomboid` allows *traversal but not listing*: file managers (Dolphin etc.) and the **Browse…** dialog cannot click through those directories even though the app itself can. Workarounds:

- **Symlink (recommended):** `ln -s /home/pz/Zomboid/Server ~/pz-server`, then Browse to `~/pz-server`.
- Type the full path directly (`Ctrl+L` in most file dialogs).
- Or allow listing: `sudo setfacl -m u:usr:rx /home/pz` (and the same for `/home/pz/Zomboid` if needed).

### Troubleshooting

- `namei -l /home/pz/Zomboid/Server` shows exactly where access stops along the path.
- `getfacl <dir>` verifies the ACL entries — expect `user:usr:` lines.
- If `setfacl` is missing, install the `acl` package (name varies by distro).
- Undo everything: `sudo setfacl -R -b /home/pz/Zomboid/Server` and `sudo setfacl -x u:usr /home/pz /home/pz/Zomboid`.

## License

[MIT](LICENSE)
