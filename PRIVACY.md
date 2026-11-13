# Privacy Policy — MonitorBridge – Cursor Fix

Last updated: 2026-11-13

MonitorBridge does not collect, store, share or transmit personal information. It has no network access and contains no analytics, telemetry, advertising or third-party services.

## What the app reads on your PC (locally only)
- **Mouse movement:** to correct the cursor when it crosses the boundary between monitors, the app observes mouse movement through a Windows mouse hook and Raw Input. This data is processed in memory only for that moment. It is never recorded, saved or sent anywhere. The hook is active only while correction is turned on and is removed when you turn it off or exit.
- **Monitor information:** resolution, scaling, orientation, and the monitor's physical size and model/serial identifiers (EDID), read from Windows to build the layout.

## What the app saves (locally only)
A settings file in your own user profile (`%APPDATA%\MonitorBridge\settings.json`, or the app's private storage when installed from the Microsoft Store). It contains your monitor layout (sizes, positions, monitor identifiers used to recognize the same monitors again), your language, and your on/off and start-with-Windows choices. It is never uploaded.

## Sharing and third parties
None. No data is shared with the developer or any third party.

## Your control
Turn correction off or exit the app at any time to remove the mouse hook. Uninstall the app and delete the settings file to remove all stored data.

## Contact
Questions or concerns: open an issue at https://github.com/Lee-D-Sen/MonitorBridge/issues

Changes to this policy will be published in this file in the repository.
