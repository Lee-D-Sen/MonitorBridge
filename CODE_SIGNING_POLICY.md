# Code Signing Policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).

## What is signed
- Only `MonitorBridge.exe`, the release build of this repository.
- The file is built from the source in this repository by GitHub Actions (`.github/workflows/release.yml`) on a GitHub-hosted runner. Local or manually uploaded builds are never signed.
- Release binaries are published only on the [GitHub Releases](https://github.com/Lee-D-Sen/MonitorBridge/releases) page.

## Roles
- Author / committer / approver: [Lee-D-Sen](https://github.com/Lee-D-Sen). Every signing request is approved manually by the approver in SignPath.

## Privacy
MonitorBridge does not send any data to the network. Settings are stored locally in `%APPDATA%\MonitorBridge\settings.json`. The app reads monitor information (resolution, scale, EDID physical size) from Windows only to compute the cursor layout.

## Uninstall
MonitorBridge is portable (no installer). To remove it: turn off "Start in tray with Windows" (or delete the `MonitorBridge` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), exit from the tray icon, then delete `MonitorBridge.exe` and the `%APPDATA%\MonitorBridge` folder.
