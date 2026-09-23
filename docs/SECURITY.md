# Security

Muse for Windows is a thin wrapper: all content comes from [muse.ai](https://muse.ai), rendered by Microsoft Edge WebView2. The wrapper's job is to make sure the page gets no more power than it would have in a browser tab. Here's how.

## Isolation

- **Dedicated WebView2 profile** under `%LOCALAPPDATA%\Muse`. Cookies, storage and cache are separate from Edge and other apps.
- **No browser extensions**, and **no OS single sign-on** (your Windows account is never used to sign in to websites automatically).
- **DevTools are off** in release builds (on only with a debugger attached or `MUSE_DEVTOOLS=1`). "Inspect" and "View source" are removed from the context menu.
- Password autosave is off. SmartScreen reputation checks are on.

## Navigation and popups

- **Trusted hosts** (including subdomains): muse.ai, meta.ai, meta.com, facebook.com, fb.com, instagram.com, fbcdn.net, fbsbx.com, the Muse content domains (metaaiusercontent.com, ecto1usercontent.com, meta-agents-apps.workers.dev) and a few sign-in/payment providers (Google, Microsoft, Apple, Stripe/Link).
- **Main window**: only `https:` (and `about:blank`) is allowed. `http:`, `file:`, `data:`, `javascript:`, `blob:` and every other scheme are blocked. Leaving the trusted hosts shows a "You've left Muse" bar with a **Back to Muse** button.
- **Popups**: blank popups and trusted hosts open in an in-app window (so sign-in popups work). Other `https:` links open in your default browser, but only when you clicked something (script-only popups to them are dropped). Every other scheme is dropped. In-app popups have no address bar, so their title bar always starts with the real host (in punycode), before the page's own title.
- **Only `http`/`https` links are ever handed to the Windows shell.** `mailto:` and `tel:` go through WebView2's own confirmation prompt. Every other external URI scheme (`file:`, `ms-*:`, `search-ms:`, custom protocol handlers …) is cancelled.
- TLS certificate errors are never bypassed. HTTP basic-auth prompts are cancelled.

## Permissions

| Origin | Microphone, notifications | Clipboard read, autoplay | Camera, location, screen capture | Everything else |
|---|---|---|---|---|
| `muse.ai` / `*.muse.ai` | Allowed | Allowed | WebView2 asks you (remembered) | Denied |
| Any other origin | Denied | WebView2 default | Denied | Denied |

## Notifications

Only muse.ai may send notifications. While the window is hidden or in the background they are shown by the tray icon as Windows notifications; clicking one opens Muse. Their text is never logged.

## Tray, hotkey and startup

- Closing the window hides Muse to the tray by default; the browser keeps running (with reduced memory use) so the session and notifications stay live. **Quit Muse** in the tray menu exits.
- A system-wide hotkey (default `Ctrl+Alt+M`) only shows or hides the Muse window. Change it with `"Hotkey"` in `%LOCALAPPDATA%\Muse\settings.json` (e.g. `"Ctrl+Shift+F9"`), or set it to `""` to register no hotkey at all. Only modifier + letter/digit/F-key combinations are accepted, and at least one of Ctrl, Alt or Win is required, so a bare key can never be taken from other apps.
- **Start with Windows** writes one per-user value, `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Muse` = `"<path to MuseApp.exe>" --minimized`. Nothing is written for all users and no admin rights are needed. Turning it off deletes the value.
- A second launch only signals the running copy to show its window; nothing else crosses between instances.

## Logs

Logs are at `%LOCALAPPDATA%\Muse\logs\muse.log` (capped at about 1 MB, two files kept). They record the app and WebView2 versions, crashes and WebView2 process failures, navigation failures, blocked navigations/popups/permissions, and Start with Windows changes. URLs are logged as **scheme + host only, never paths or query strings**, which can contain tokens.

## Reset

To start completely fresh (this signs you out and clears settings and cache), quit Muse from the tray, then run `MuseApp.exe --reset`. It deletes the browser profile and `settings.json` under `%LOCALAPPDATA%\Muse` and keeps the logs. Deleting `%LOCALAPPDATA%\Muse` by hand does the same. Neither removes the Start with Windows entry; turn that off from the tray menu first if you want it gone.

## Reporting a vulnerability

Please use GitHub's **Report a vulnerability** button (Security tab) on this repository rather than a public issue. Problems in muse.ai itself should go to Meta.
