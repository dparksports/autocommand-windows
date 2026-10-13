# AutoCommand v2026.11.04

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🐍 Python Spawns now backfills history instead of starting from zero.** The tab was live-only: its Sysmon watchers run while the tab is selected and stop when you leave it, so pythons that ran while you were on another tab never appeared — even though Process Monitor (fed by the persistent shared capture) kept showing them. Opening the tab now replays the **last 6 hours** of Sysmon EID 1/5 records through the same handlers the live path uses, so historical rows arrive with full parentage, venv pairing and Exited stamps intact — and a status line reports how many were loaded. Replay is pid-deduped against the live feed, so events racing the backfill never double-add a row. The same pattern the taskhostw tab has always had, now wired for python.

> **Note:** run elevated as usual — the Sysmon channel the backfill reads is admin-only. Requires Sysmon (installable from the Sysmon Audit tab); without it, the tab reports that spawn tracing needs it.
