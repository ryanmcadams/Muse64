# Muse for Windows

A lightweight desktop app for [Muse](https://muse.ai) — Meta's personal AI assistant — built with .NET and WebView2. One download, no installer: it wraps muse.ai in a native window.

*Created by **Zucker** — [@ZuckerMuse](https://x.com/ZuckerMuse) on X.*

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![WebView2](https://img.shields.io/badge/WebView2-latest-00A4EF) ![Windows](https://img.shields.io/badge/Windows-x64-0078D6)

## Features

- **Native window** around muse.ai — feels like a real app, not a browser tab
- **Single-file executable** — download and run, no installer
- **Lives in the tray** — closing the window keeps Muse running in the notification area, so you stay signed in and notifications still arrive
- **Single instance** — launching it again brings the open (or hidden) window to the front
- **Remembers your window** — size, position, and maximized state persist, and it always reopens on-screen
- **Dark/light theme** — follows Windows, title bar included, no white flash on startup
- **Safe popups and links** — sign-in popups stay in the app; other links you click open in your browser; nothing but `http`/`https` ever reaches the Windows shell
- **Mic and clipboard just work** — voice dictation and copy/paste are allowed for muse.ai only; other sites can't get your mic
- **Offline-friendly** — a "Can't reach Muse" panel with Retry, and it reconnects on its own when the network comes back
- **Crash-resilient** — recovers automatically if WebView2 crashes or updates underneath it
- **Downloads just work** — files land in your Downloads folder
- **Notifications** — when the window is hidden or in the background, Muse notifications appear as Windows notifications from the tray icon
- **Keyboard shortcuts** — `Ctrl+R` / `F5` reload, `Ctrl+Plus` / `Ctrl+Minus` / `Ctrl+0` zoom (zoom level persists); `Ctrl+Alt+M` anywhere in Windows shows or hides Muse (configurable)
- **Dedicated browser profile** — stored under `%LOCALAPPDATA%\Muse`, separate from Edge

## Requirements

- Windows 10/11 (x64)
- [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) (free, ~2 MB bootstrapper; preinstalled on most Windows 11 machines). If it's missing, the app tells you and links the download.

## Tray, startup and command line

Right-click the tray icon for **Open Muse**, **Reload**, **Start with Windows**, **Close to tray** (on by default), **Minimize to tray** (off by default) and **Quit Muse**. Left-click the icon to show or hide the window. With **Close to tray** on, the window's X button only hides Muse; use **Quit Muse** to exit.

**Start with Windows** adds a per-user entry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Muse`) that starts `MuseApp.exe --minimized`. It points at the exe's current location, so turn it off and on again if you move the exe. You can also disable it from Task Manager's Startup apps page.

| Switch | Effect |
|---|---|
| `--minimized` or `--tray` | Start hidden in the tray |
| `--reset` | Sign out and start fresh: deletes the browser profile and settings (logs are kept). Quit Muse first, or the running copy just comes to the front. |

### Show/hide hotkey

`Ctrl+Alt+M` is a system-wide hotkey. To change or turn it off, quit Muse and edit `%LOCALAPPDATA%\Muse\settings.json`:

```json
"Hotkey": "Ctrl+Shift+F9"
```

Use `Ctrl`, `Alt`, `Shift` and/or `Win` plus one key from `A`–`Z`, `0`–`9` or `F1`–`F24`, joined with `+`. At least one of `Ctrl`, `Alt` or `Win` is required. Set `"Hotkey": ""` (or `null`) to disable it. An invalid value falls back to `Ctrl+Alt+M`. If another app already owns the combination, Muse skips it. Both cases are noted in the log.

## Download

Grab the zip from [Releases](../../releases), unzip, run `MuseApp.exe` (about 70 MB, self-contained: no .NET install needed). `SHA256SUMS.txt` is attached to each release if you want to verify it.

The exe isn't code-signed yet, so the first run shows a SmartScreen **"Unknown publisher"** warning. Click **More info → Run anyway**. See [docs/RELEASING.md](docs/RELEASING.md#smartscreen-and-code-signing).

What changed in each version is in [CHANGELOG.md](CHANGELOG.md).

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`).

```powershell
dotnet build Muse64.sln -c Release
dotnet test Muse64.sln -c Release
```

To produce the single-file exe:

```powershell
./scripts/publish.ps1
```

That builds, tests, and publishes `MuseApp.exe` to `artifacts/publish/`, then prints its size and SHA256. Or directly:

```powershell
dotnet publish src/MuseApp/MuseApp.csproj -c Release -o publish
```

## Safety

The wrapper locks down what the page can do: a trusted-host list, https-only navigation, per-site permissions, no extensions, DevTools off in release, and logs that never contain query strings. Details in [docs/SECURITY.md](docs/SECURITY.md).

## Project layout

```
Muse64/
├── src/
│   ├── MuseApp/              # WPF app (.NET 10 + WebView2): windows, WebView2 host, tray, theme, single instance
│   └── MuseApp.Core/         # UI-free logic: URL & permission policy, window placement, settings, logging, startup entry
├── tests/
│   └── MuseApp.Core.Tests/   # xUnit tests for MuseApp.Core
├── docs/                     # SECURITY.md, RELEASING.md
├── scripts/                  # publish.ps1
├── .github/                  # CI + release workflows, Dependabot
├── assets/                   # app icon
├── Muse64.sln
└── README.md
```

## Tech

- [.NET 10](https://dotnet.microsoft.com/) (WPF)
- [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) — Chromium-based embedded browser
- C# (latest), nullable reference types, warnings as errors, single-file publish

## License

MIT — see [LICENSE](LICENSE).

## Credits

Built by **Zucker** — [@ZuckerMuse](https://x.com/ZuckerMuse) on X — for Ryan McAdams.
