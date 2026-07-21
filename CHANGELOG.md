# Changelog

Versioning follows `MAJOR.MINOR.PATCH`. The release version is set by `<Version>` in
`ACTLogsUploader.csproj` and tagged `vX.Y.Z`; each tag has a GitHub release with the build output.

## 0.3.2

- Offer both downloads: the loose-files zip (antivirus-safe, default) and the single DLL
  (`-p:SingleFile=true`; antivirus may flag it, add an exclusion to use it).

## 0.3.1

- Revert to loose dependency files. The single DLL from 0.3.0 embedded the native V8, which
  antivirus flagged as a virus (HRESULT 0x800700E1), blocking the plugin. Loose, unmodified,
  signed files avoid that.

## 0.3.0

- Back to a single DLL: managed dependencies are IL-merged (so ACT's type discovery works),
  and the native V8 + ICU data are embedded and loaded at runtime.

## 0.2.1

- Fix plugin failing to enable on some ACT versions ("could not load System.Text.Json"):
  ship dependencies as loose files next to the DLL instead of embedding them.
- Fix live-logging buttons not updating after auto-start.

## 0.2.0

- Add auto-login and auto-upload (auto-start live logging on load).
- Show the version on the plugin tab.

## 0.1.1

- Remove the non-functional real-time upload option; clarify labels.

## 0.1.0

- Upload FFXIV combat logs to FF Logs (Global `fflogs.com` and CN `cn.fflogs.com`).
- Email/password login with session cookie + XSRF; create-report, master-table and segment
  uploads, terminate.
- In-process parsing via FF Logs' own JS parser in an embedded V8 engine (ClearScript).
- Upload modes: latest log, specific file, selected fights, live logging (with real-time and
  include-existing-fights options).
- Log maintenance: auto/manual archive to zip, auto/manual delete of archived logs, log split.
- English/Chinese UI.
- Single self-contained DLL (dependencies and native V8 embedded).
