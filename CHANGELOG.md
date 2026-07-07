# Changelog

Versioning follows `MAJOR.MINOR.PATCH`. The release version is set by `<Version>` in
`ACTLogsUploader.csproj` and tagged `vX.Y.Z`; each tag has a GitHub release with the built DLL.

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
