# Offline SQLite + Local Hub

Offline mode uses a **local SQLite file** (no SQL Express). Central/online data stays on SQL Server.

## Why https://pos.coditium.com dies when Wi‑Fi is cut

That URL is on the **internet**. With no DNS/network the browser shows `DNS_PROBE_FINISHED_NO_INTERNET` and never reaches your app. No Offline button on the cloud site can fix that.

**Automatic offline** means: shop PCs always use the **Local Hub** URL (`http://localhost:5227` or `http://<hub-pc-ip>:5227`). The hub stays running on the shop PC; **Auto** uses central SQL when online and SQLite when not.

### One-time setup (automatic at logon)

```powershell
dotnet publish -c Release -o .\build
.\build\POS_MT.exe offline pull
powershell -ExecutionPolicy Bypass -File .\scripts\install-local-hub-autostart.ps1
```

- Puts a shortcut in the Windows **Startup** folder (hub starts at logon).
- Adds a desktop shortcut: **Coditium POS (Local Hub)**.
- Open that shortcut (or `http://localhost:5227`) - **not** [pos.coditium.com](https://pos.coditium.com).

Optional firewall (LAN PCs → hub PC): `.\scripts\setup-local-hub-firewall.ps1`

## Work modes

| Mode | Behavior |
|------|----------|
| **Online** | Use central SQL Server (`ConnectionStrings:POS_MT`) |
| **Offline** | Use local SQLite (`App_Data/pos-offline.db`) |
| **Auto** | Online when reachable; otherwise SQLite if `offline pull` was done |

## Command line (configure + sync)

From the project or published folder:

```bat
dotnet run -- offline help
dotnet run -- offline pull
dotnet run -- offline push
dotnet run -- offline sync
dotnet run -- offline status
```

Published:

```bat
POS_MT.exe offline pull
POS_MT.exe offline push
POS_MT.exe offline status
```

| Command | What it does |
|---------|----------------|
| `offline init` | Create empty SQLite schema |
| `offline pull` / `configure` | Download products, inventory, customers, users, etc. from central SQL into SQLite |
| `offline push` | Upload pending offline sales (`SyncOutbox`) to central SQL |
| `offline sync` | `pull` then `push` |
| `offline status` | Show SQLite path and counts |

## One-time setup

1. Set `ConnectionStrings:POS_MT` (and optionally `POS_MT_Central`) to your online SQL Server.
2. Keep `POS_MT_Offline` / `PosHub:OfflineSqlitePath` as `App_Data/pos-offline.db` (default).
3. While online, run:

```bat
dotnet run -- offline pull
```

4. Start the app, click **Offline** in the UI (or leave **Auto**).
5. When back online, run `offline push` (or wait for the background sync worker).

## LocalHub (shop LAN PC)

Copy values from `appsettings.LocalHub.Example.json`:

- `PosHub:Mode` = `LocalHub` (always SQLite for POS; sync to central when reachable)
- Central SQL in `POS_MT` / `POS_MT_Central`
- Run `offline pull` once on that PC
- Bind LAN: `Urls` = `http://0.0.0.0:5227`

Other PCs open `http://<hub-ip>:5227`.

## Notes

- SQLite file lives under `App_Data/` (gitignored).
- Offline sales get a `ClientTransactionId` and `SyncOutbox` row for safe push.
- Cloud-only hosts still need a SQLite file on **that** machine for Offline — run `offline pull` there, or use a shop PC as LocalHub.
