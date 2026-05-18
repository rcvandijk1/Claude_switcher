# Claude Switcher

Windows 11 desktop app that swaps your Claude logins across every surface you
have open in one click:

| Surface | What gets swapped |
|---|---|
| Claude Code CLI (and the VS Code / Antigravity extensions that share its credentials) | `~/.claude/.credentials.json` |
| Claude Desktop (MSIX app from the Microsoft Store) | `%APPDATA%\Claude\` auth files; app is closed + relaunched via AUMID |
| Antigravity | `%APPDATA%\Antigravity\` auth files; app closed + relaunched |
| `claude.ai` tabs in Chrome | Cookies flipped via a companion MV3 extension; matching tabs reloaded in place |

Profiles live under `~/.claude_switcher/profiles/<id>/` with all credential
snapshots **DPAPI-wrapped (per-user)** so another user account on the same
machine cannot read them.

## Architecture

```
[Chrome tab]              [Chrome extension (MV3)]
     |                              |
     |                              | chrome.runtime.connectNative
     |                              v
     |               [claude-switcher-host.exe]   <-- stdio frames in/out
     |                              |
     |                              | named pipe \\.\pipe\ClaudeSwitcher
     |                              v
[Claude Desktop]   [Antigravity]  [ClaudeSwitcher.App]  --> swaps files,
[Claude Code CLI]                                            kills/relaunches apps,
                                                             pushes cookie commands
                                                             back to extension
```

* **ClaudeSwitcher.Core** — profile store + DPAPI vault + the four
  `ISwapProvider` implementations (CLI, Claude Desktop, Antigravity, Chrome).
* **ClaudeSwitcher.App** — WPF tray app. Hosts the named-pipe server, the
  Chrome bridge, the system tray icon, and the management window.
* **ClaudeSwitcher.NativeHost** — tiny console exe Chrome spawns on each
  native-messaging connection; forwards the byte stream to the tray app's pipe.
* **chrome-extension/** — Manifest V3 extension. Captures and applies
  claude.ai cookies via `chrome.cookies` and reloads matching tabs.

## Build

Requires .NET 9 SDK and Windows 10/11.

```powershell
dotnet build .\ClaudeSwitcher.sln
```

Run the tray app:

```powershell
dotnet run --project .\src\ClaudeSwitcher.App
```

On first launch the app migrates any legacy `~/.claude_switcher/profiles.json`
(from the previous Python switcher) into the new on-disk layout.

## Wire up Chrome

1. Build the solution so `claude-switcher-host.exe` exists under
   `src\ClaudeSwitcher.NativeHost\bin\Debug\net9.0-windows\`.
2. In Chrome, open `chrome://extensions/`, flip on **Developer mode**, click
   **Load unpacked**, and select the `chrome-extension/` folder. Copy the
   32-character extension ID Chrome assigns it.
3. Register the native-messaging host:

   ```powershell
   .\setup\Register-NativeHost.ps1 -ExtensionId <your-extension-id>
   ```

   This writes
   `%LOCALAPPDATA%\ClaudeSwitcher\com.operative.claudeswitcher.json` and the
   `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.operative.claudeswitcher`
   registry key.
4. Reload the extension. Its popup should now say **Connected to desktop app**,
   and the main window's "Browser bridge" line should turn green.

`setup\Unregister-NativeHost.ps1` reverses step 3.

## Using it

1. Log in to one account everywhere (Chrome tabs, Claude Desktop, Antigravity,
   `claude` CLI). In the tray app, click **+ Add Profile**, name it (e.g.
   "Personal"). The app snapshots all four targets into that profile.
2. Log out of everything and log into the other account. **+ Add Profile**
   again as "Work".
3. From now on, switch from the tray icon's menu (or from the main window).
   The app first captures the currently-live state back into the active
   profile (so any new logins are preserved), then applies the chosen
   profile to every target.

### What "switch" actually does, per target

* **CLI** — overwrites `~/.claude/.credentials.json` from the DPAPI-wrapped
  snapshot. No restart needed; the CLI re-reads creds on each invocation, and
  the VS Code / Antigravity Claude Code extensions read the same file.
* **Claude Desktop** — closes the running app, swaps `%APPDATA%\Claude\`
  auth files (`Local State`, `Network\Cookies*`, `Local Storage\`,
  `Session Storage\`, plus a few peers), and relaunches via
  `shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude`.
* **Antigravity** — same pattern against `%APPDATA%\Antigravity\` and
  `%LOCALAPPDATA%\Programs\Antigravity\Antigravity.exe`.
* **Chrome** — extension wipes existing `claude.ai` / `anthropic.com` cookies,
  applies the saved set, and reloads all matching tabs (`bypassCache: true`).

### Caveats

* Switching closes Claude Desktop and Antigravity. **Unsaved chat input is
  lost.** Toggle "Auto-relaunch" off in the main window if you'd rather
  reopen them manually.
* The Chrome leg only works while the extension is loaded and the desktop
  app is running. With the bridge disconnected, switches still happen for
  the other three targets — you'll see "skipped (browser extension not
  connected)" in the result toast.
* `Local State` holds the DPAPI-wrapped cookie encryption key. We always
  snapshot and apply it as a unit with `Network\Cookies` so cookies remain
  readable after the swap.

## Layout

```
.
├── ClaudeSwitcher.sln
├── README.md
├── chrome-extension/
│   ├── manifest.json
│   ├── service_worker.js
│   ├── popup.html
│   └── popup.js
├── setup/
│   ├── Register-NativeHost.ps1
│   └── Unregister-NativeHost.ps1
└── src/
    ├── ClaudeSwitcher.Core/         # profile model, DPAPI vault, providers
    ├── ClaudeSwitcher.App/          # WPF tray app, IPC server, UI
    └── ClaudeSwitcher.NativeHost/   # Chrome native-messaging stdio bridge
```
