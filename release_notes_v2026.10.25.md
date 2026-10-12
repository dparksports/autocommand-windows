# AutoCommand v2026.10.25

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **The standalone SvchostAnalyzer.exe is no longer distributed.** It is not built, zipped, or uploaded with releases anymore, and it has been removed from the two posted releases that carried it (v2026.10.23, v2026.10.24 — their SHA256SUMS.txt files were updated to match). Every release from here on ships exactly three assets: the app zip, the unblock script, and SHA256SUMS.txt.
- **Svchost Trace is unaffected.** The capture engine is compiled into the app (`SvchostAnalyzer.Core`, in-process) — tracing, the timeline, and the pattern detector work with no separate binary, as they have since v2026.10.20.
- **Boot auto-start (headless 24/7 recording) now requires a previously installed exe.** The one-click SHA-256-verified download was removed along with the asset; enabling auto-start without the exe present explains where the app looks for an existing copy (next to `AutoCommand.exe`, in a `tools\` folder beside it, or `%LOCALAPPDATA%\AutoCommand\tools`). Already-installed copies keep working unchanged.
- **README updated** to say the same. No other behavior changed; no new dependencies.

> **Note:** run elevated as usual — nothing about the capture engine's privileges changed.
