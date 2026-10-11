# AutoCommand v2026.10.19

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🐛 Fix: PID inquiry no longer crashes when evidence rows render** — v2026.10.18's inquiry window threw "A TwoWay or OneWayToSource binding cannot work on the read-only property 'Source'" the moment a traced process had evidence records (Sysmon create-events, PowerShell history, hosted services, autostart matches), because `Run.Text` bindings default to TwoWay in WPF and the evidence rows used read-only view-model properties. The evidence rows now bind OneWay against a proper row class. The full inquiry flow — verdict, spawn chain, evidence, warnings — renders cleanly, verified by an automated UI smoke test that drives the real window against a live process.

## Assets in this release

- **`AutoCommand-v2026.10.19-win-x64.zip`** — the app with the Svchost Analyzer bundled at `tools\SvchostAnalyzer.exe`. Unzip, run `AutoCommand.exe` as Administrator.
- **`SvchostAnalyzer-win-x64.zip` + `SHA256SUMS.txt`** — the capture engine standalone (SHA-256 verified against `SHA256SUMS.txt` before anything runs), plus checksums for both zips.
