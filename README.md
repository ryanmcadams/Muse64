# Muse for Windows

A lightweight desktop app for [Muse](https://muse.ai) — Meta's personal AI assistant — built with .NET and WebView2. One download, no installer: it wraps muse.ai in a native window.

*Created by **Zucker** — [@ZuckerMuse](https://x.com/ZuckerMuse) on X.*

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![WebView2](https://img.shields.io/badge/WebView2-latest-00A4EF) ![Windows](https://img.shields.io/badge/Windows-x64-0078D6)

## Features

- **Native window** around muse.ai — feels like a real app, not a browser tab
- **Single-file executable** — download and run, no installer
- **Remembers your window** — size, position, and maximized state persist between sessions
- **Popups open in your browser** — OAuth flows and `target="_blank"` links never get trapped in a blank window
- **Downloads just work** — files land in your Downloads folder
- **Keyboard shortcuts** — `Ctrl+R` / `F5` reload, `Ctrl+Plus` / `Ctrl+Minus` / `Ctrl+0` zoom
- **Dedicated browser profile** — stored under `%LOCALAPPDATA%\Muse`, separate from Edge

## Requirements

- Windows 10/11 (x64)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (free; preinstalled on most Windows 11 machines). If it's missing, the app tells you and links the download.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
cd src/MuseApp
dotnet publish -c Release -o publish
```

The single-file `MuseApp.exe` lands in `src/MuseApp/publish/`. Run it.

## Project layout

```
muse-for-windows/
├── src/MuseApp/        # WPF app (.NET 10 + WebView2)
│   ├── MainWindow.xaml(.cs)  # WebView2 host, shortcuts, error handling
│   ├── WindowSettings.cs     # window placement persistence
│   └── App.xaml(.cs)
├── assets/             # app icon
└── README.md
```

## Tech

- [.NET 10](https://dotnet.microsoft.com/) (WPF)
- [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) — Chromium-based embedded browser
- C# (latest), nullable reference types, single-file publish

## License

MIT — see [LICENSE](LICENSE).

## Credits

Built by **Zucker** — [@ZuckerMuse](https://x.com/ZuckerMuse) on X — for Ryan McAdams.
