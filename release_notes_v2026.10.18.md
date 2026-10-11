# AutoCommand v2026.10.18

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🔎 PID inquiry tracing in the Process Monitor** — for any process you see, you can now ask "how was this PID spawned?" and get a verified answer. Two entry points:
  - **Toolbar dropdown**: "🔎 Inquire PID" lists every PID currently in the monitor (most recently active first); any other PID can be typed. Hit **Trace** (or Enter).
  - **Row right-click**: "🔎 Trace PID nnn — how was it spawned?" at the top of the context menu.

  The inquiry window shows a color-coded verdict (user-launched from a terminal / interactive shell / scheduled task / Windows service / logon autostart), the live identity (image, command line, owner, session, memory), the **spawn chain from child to root**, and corroborating records. Everything is read-only — collection never kills, blocks or modifies anything.

- **PID-reuse guard built in** — Windows recycles PIDs aggressively, so a parent is only trusted if its creation time predates the child's. Recycled parents are flagged red in the chain with an explicit warning instead of silently blaming the wrong process.

- **Evidence that survives process exit** — when Sysmon is installed, the inquiry correlates the recorded create-event (Event ID 1: exact spawn time, image, parent image and parent command line, user). Security audit 4688 is used as a fallback. Exited processes whose history wasn't recorded degrade gracefully with guidance to enable Sysmon (🛠 Setup Sysmon).

- **Interactive-launch proof** — the inquiry scans your PSReadLine PowerShell history for the exact command line, so shells like the venv-launcher pattern (a `python.exe` that legitimately re-spawns the real interpreter as a child) are attributed to the typed command rather than looking like an unexplained duplicate process.

- **Autostart & service surface** — Run keys / Startup folder entries matching the image, services hosted by the target, and taskhostw scheduled-task attribution (task path, user context, COM-handler DLL) all appear as evidence when relevant.

- **📋 Copy report** — one click puts the full plain-text report (verdict, chain, evidence, warnings) on the clipboard for pasting into tickets or an AI assistant; **↻ Re-collect** re-runs the inquiry on a live PID.

## Assets in this release

- **`AutoCommand-v2026.10.18-win-x64.zip`** — the app with the Svchost Analyzer bundled at `tools\SvchostAnalyzer.exe`. Unzip, run `AutoCommand.exe` as Administrator.
- **`SvchostAnalyzer-win-x64.zip` + `SHA256SUMS.txt`** — the capture engine standalone (SHA-256 verified against `SHA256SUMS.txt` before anything runs), plus checksums for both zips.
