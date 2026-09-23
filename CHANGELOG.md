# Changelog

All notable changes to Muse for Windows are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed
- Dependencies: Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0; GitHub Actions moved to the Node 24 runtime (checkout v7, setup-dotnet v6, upload-artifact v7, action-gh-release v3).

## [2.1.0] - 2026-09-23

### Added
- **Tray mode.** Closing the window keeps Muse running in the notification area, so you stay signed in and live updates keep arriving. Tray menu: Open Muse, Reload, Start with Windows, Close to tray, Minimize to tray, Quit Muse. Left-click the icon to show or hide the window.
- **Start with Windows** via a per-user Run entry that launches Muse hidden.
- **Global show/hide hotkey**, `Ctrl+Alt+M` by default, configurable or disableable through `Hotkey` in `settings.json`.
- **Command-line switches:** `--minimized` / `--tray` start hidden; `--reset` deletes the browser profile and settings for a fresh start.
- **Notifications from the tray.** Web notifications raised while the window is hidden or in the background appear as Windows notifications; clicking one opens Muse.
- **In-app popup windows** for sign-in and connector OAuth flows, sharing the same browser profile so `window.opener` and cookies work.
- **"You've left Muse" info bar** with a Back to Muse button whenever the main window lands on a site outside the trusted set.
- **Offline panel** with Retry that also reconnects automatically when the network comes back.
- **Automatic crash recovery** when a WebView2 renderer or browser process dies (including runtime updates underneath a running app), with a retry budget and a clear message if it keeps failing.
- **Single instance.** Launching the exe again brings the existing window forward, or shows it if it is hidden in the tray.
- **Dark and light theme** that follows Windows, including the title bar and the first frame, so there is no white flash.
- **Video fullscreen** support.
- **Diagnostics log** at `%LOCALAPPDATA%\Muse\logs\muse.log` (rolling, size-capped) with global exception handling and a friendly error dialog instead of a silent exit.
- **Unit-tested policy core** (`MuseApp.Core`) covering URL, popup, permission, placement, settings, hotkey and startup logic.
- **CI and releases.** GitHub Actions build, test and publish on every push; tagged releases ship a zip plus SHA256 checksum. Dependabot keeps NuGet and Actions current. `scripts/publish.ps1` for local builds.
- Docs: `docs/SECURITY.md` and `docs/RELEASING.md`.

### Changed
- Window placement is validated against the current monitors before restoring, so the window always reopens on-screen after docking or display changes.
- Zoom level persists across restarts. Numpad `+`, `-` and `0` now work for zoom alongside the main keys.
- Keyboard shortcuts are handled in one place while the page has focus, so reloads and zoom steps are never applied twice.
- Window title mirrors the page title.
- Settings moved to `%LOCALAPPDATA%\Muse\settings.json` (migrated automatically from the old `%APPDATA%` location) and are written atomically.
- Publish is explicitly self-contained and compressed: the exe is about 70 MB, down from 141 MB.
- Per-monitor DPI awareness (PerMonitorV2), long path support and Windows 10/11 compatibility declared in the app manifest.
- Downloads use the real Downloads known folder instead of a hard-coded `%USERPROFILE%\Downloads`.
- Build treats warnings as errors with .NET analyzers enabled.

### Fixed
- The app failed to start because the window icon could not be loaded by WPF.
- The loading overlay disappeared before the page had painted, showing a blank window on slow connections.
- A dead renderer or browser process left a blank window with no way to recover.
- Dead zoom command code and compiler warnings removed.

### Security
- Only plain `http`/`https` URLs with no embedded credentials are ever handed to the Windows shell. Any other scheme from page content, including custom protocol handlers, is dropped and logged.
- Popups opened by scripts to untrusted sites are dropped; popups to untrusted sites open in the default browser only when you clicked; trusted sign-in and payment hosts open in-app.
- Top-level navigation to `http:`, `file:`, `data:`, `javascript:` and other non-https schemes is blocked.
- Microphone, clipboard read and notification permissions are granted to muse.ai only; other origins are denied without a prompt. Camera, location and screen capture still prompt.
- Certificate errors are never bypassed, HTTP basic-auth prompts are refused, browser extensions and Windows single sign-on are disabled, DevTools and the Inspect menu are off in release builds, and password autosave is off.
- In-app popup titles show the host first so a page cannot spoof where it comes from.
- Logs record scheme and host only, never paths, query strings or tokens.

## [2.0.0] - 2026-09-17

### Added
- Rewrite on .NET 10 and WebView2 with a single-file executable.
- Window size, position and maximized state persist between sessions.
- Popups and `target="_blank"` links open in the default browser.
- Downloads land in the Downloads folder.
- Keyboard shortcuts for reload and zoom.
- Dedicated browser profile under `%LOCALAPPDATA%\Muse`.

[2.1.0]: https://github.com/ryanmcadams/Muse64/releases/tag/v2.1.0
[2.0.0]: https://github.com/ryanmcadams/Muse64/commits/f4881a4
