# AutoCommand v2026.11.01

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **Taskhostw Spawns now answers "which scheduled task launched this?"** Every taskhostw.exe spawn is attributed to its parent scheduled task by correlating Sysmon Event ID 1 (process create) with Task Scheduler operational history (event 129 "launching task X, instance taskhostw.exe, with process ID N", plus 100/102 instance start/stop and 200/201/203 action launch/completion/return-code). Instances stay linked across PID churn via the task's instance GUID, and a **historical backfill** reconstructs the same rows from the Sysmon log when you open the tab cold — the live watcher and the backfill share one parser, so both paths build identical rows.
- **Plain-English explanations for well-known tasks.** The Taskhostw Spawns tab enriches attributed spawns with a shipped, user-extensible catalog (`Assets/known-tasks.json`): add a path-keyed entry, restart, and every future spawn of that task carries its explanation — no rebuild. Unknown tasks are not errors; they fall back to the task XML's own Description/Author in the summary pane. Handler-DLL company and version are shown for registered tasks, and a DLL that no longer exists is flagged as the classic leftover of uninstalled software.
- **Double-click a Process Monitor row to trace it.** Double-clicking anywhere on a row opens the same PID inquiry spawn-chain window as the right-click "Trace" item — verified parent chain, spawn mechanism, Sysmon create-events and shell history. Clicks on column headers or empty space do nothing.
- **Connections tab removed.** It was redundant with the Process Monitor (same underlying Sysmon/sniffer feeds, less context); its polling view and the now-unused network-enumeration helper were dropped.
- **Tab order regrouped.** The three spawn-analysis tabs — Svchost Trace, Taskhostw Spawns, Python Spawns — now sit together right after Firewall and before Sysmon Audit, so the monitoring and forensics workflow reads top to bottom.

> **Note:** run elevated as usual — Sysmon event reading and Task Scheduler history queries need administrator rights. The known-tasks catalog ships at `Assets\known-tasks.json` next to the exe; edit it to grow the corpus.
