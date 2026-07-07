# ACT Logs Uploader (FF Logs)

An [Advanced Combat Tracker](https://advancedcombattracker.com/) plugin that uploads FFXIV
combat logs to FF Logs — both `www.fflogs.com` (Global) and `cn.fflogs.com` (CN) — from
inside ACT, without the Archon App.

It speaks the FF Logs desktop-client protocol directly: email/password login, session cookie
+ XSRF, then create-report → parse → upload segments → terminate. Parsing runs FF Logs' own
JavaScript parser (downloaded from `/desktop-client/parser`) in an embedded V8 engine
(ClearScript), which converts raw `Network_*.log` lines into the event/master-table format the
upload endpoints expect.

Upload logic is derived from [Robert5204/FFLogsUploader](https://github.com/Robert5204/FFLogsUploader)
(MIT), ported to ACT/.NET Framework and made host-configurable.

## Build

Requires the .NET SDK and a local ACT install. Output targets net48 x64.

```
dotnet build -c Release -p:ACTPath="C:\path\to\Advanced Combat Tracker"
```

- The Diemoe repack keeps the real ACT assembly at `DLibs\Advanced Combat Tracker.dll`; the
  project auto-detects it.
- Building requires the ILRepack tool (`dotnet tool install -g dotnet-ilrepack`). The build
  IL-merges the managed dependencies and embeds the native V8 + ICU data, producing a single
  `bin\Release\ACTLogsUploader.dll` (~40 MB).

## Install

Copy `ACTLogsUploader.dll` anywhere, then in ACT: Plugins → Plugin Listing → Browse → select
the DLL → Add/Enable. No other files are needed. On first load the embedded native V8 is
extracted to `%TEMP%\ACTLogsUploader\native\`.

## Use

On the FFLogs Uploader tab (UI language English or Chinese, switchable at the top):

1. Target — Global (`fflogs.com`) or China (`cn.fflogs.com`).
2. Email / Password — password is stored DPAPI-encrypted only if "Remember credentials" is on.
3. Region — NA/EU/JP/OC or CN.
4. Visibility — Public / Private / Unlisted.
5. Login — populates the guild list.
6. Upload to — Personal Logs or a guild.
7. Upload:
   - **Upload latest log** — the newest `Network_*.log` in the log folder.
   - **Upload file...** — pick a specific `.log` file.
   - **Upload specific fights...** — pick a file, then choose which fights to upload.
   - **Start live / Stop live** — upload fights as they finish. "Enable real-time uploading"
     uploads each fight immediately; "Include existing fights" also uploads what's already
     in the current file.
   - **Split log...** — split a large log into ~40 MB parts (each keeps the setup lines).

The log folder auto-detects from ACT's current log path; override it if needed.

## Log archive / deletion

Under the maintenance section:

- **Automatically archive logs** — zips logs untouched for 3+ days into an `Archive` subfolder.
- **Auto-delete archived after** — removes archived zips older than the selected range.
- **Archive logs** / **Delete all archived** — run either action manually.

Auto actions run on a 6-hour timer and are off by default.

## Notes

- Login is gated on client version; the server rejects old versions. `CLIENT_VERSION` in
  `FFLogsClient.cs` tracks a current Archon App version and must be bumped when the server
  starts rejecting it.
- CN support: `serverOrRegion` for create-report defaults to `1` for CN (undocumented); the
  parser region code is `CN`. Adjust `PluginSettings.Region` if reports land in the wrong region.

## Disclaimer

Unofficial. Reimplements an undocumented protocol and may break when FF Logs changes it, and
may conflict with the FF Logs Terms of Service. Use at your own risk.
