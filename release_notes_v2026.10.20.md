# AutoCommand v2026.10.20

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🔬 Svchost capture engine now built into the app — no more bundled child exe.** The capture/analysis engine was extracted into a class library (`SvchostAnalyzer.Core`) that AutoCommand links directly. **Start/Stop Capture** and **Analyze File** in the Svchost Trace tab now run the engine in-process instead of spawning `tools\SvchostAnalyzer.exe`. That child binary — the one Windows SmartScreen hard-blocked with *"Part of this app has been blocked"* (no "Run anyway" available, because an app-spawned downloaded binary has no consent path) — **no longer ships inside the app zip at all**. The only executable in the download is `AutoCommand.exe` itself, so the SmartScreen experience is a single, dismissible first-run prompt.
- **Boot auto-start remains, self-installed and clean.** For 24/7 recording independent of the app, the standalone `SvchostAnalyzer.exe` is still published as its own release asset and installs on demand (SHA-256-verified) into `%LOCALAPPDATA%\AutoCommand\tools` when you enable boot auto-start — files written by the app's own downloader carry no Mark-of-the-Web, so SmartScreen never sees them as downloads.
- **🐛 Fix: the standalone analyzer's `analyze` command never worked** — a long-standing off-by-one in its argument parsing dropped the first trace file in every invocation, so `analyze <file>` printed usage instead of the report. Fixed, and the exe now carries proper product metadata (dialogs name it "SvchostAnalyzer" instead of "UnkwownApp").
- **Same engine, same schema.** The JSONL trace format is unchanged — existing captures analyze identically, the cross-process single-instance guard still prevents double captures, and app-run captures stop gracefully with a proper stop event.

> **Note for Smart App Control machines:** Windows 11 SAC (auto-enabled after clean installs based on app-reputation evaluation) blocks unknown unsigned exes even without the Mark-of-the-Web. AutoCommand's in-app capture is immune (it's library code inside the already-running app), but the optional standalone `SvchostAnalyzer.exe` may be blocked by SAC until the releases are code-signed.

## Assets in this release

- **`AutoCommand-v2026.10.20-win-x64.zip`** — the app with the capture engine built in. Unzip, run `AutoCommand.exe` as Administrator.
- **`SvchostAnalyzer-win-x64.zip` + `SHA256SUMS.txt`** — the optional standalone capture engine (headless/boot-autostart host; CLI: `capture` / `analyze`), plus checksums for both zips.
