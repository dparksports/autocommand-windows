# AutoCommand v2026.10.14

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

This release re-anchors versioning to the calendar date. Earlier builds had drifted into future-dated version numbers; those versions have been removed and everything shipped since v2026.10.9 is consolidated here under today's date.

## Highlights

### The analyst toolkit refocused

- **The consumer setup tabs moved to Shields Up** (same author, same undo-first design, [releases page](https://github.com/dparksports/shields-up/releases/latest)). AutoCommand is now purely the analyst toolkit — debloat, firewall presets, privacy opt-outs and OS hardening live in Shields Up, and **Process Monitor opens first**.
- **Connections tab removed** — redundant with the Process Monitor (same Sysmon/sniffer feeds, less context). In its place: **double-click any Process Monitor row** to open the PID spawn-chain trace.

### New tabs

- **🦠 Windows Defender** — live MsMpEng CPU/memory with a 5-minute sparkline, engine status (real-time + tamper protection, versions, last scans with durations), a **live activity feed** from Defender's own event log (scans paired by Scan ID, detections, definition updates, cloud activity), one-click quick/full scans and definition updates. Performance knobs: scan **CPU cap** and **idle-only scans** that apply instantly on Tamper-Protected builds by writing the Defender Scan policy values directly — no more 30–60 s `Set-MpPreference` stalls. The **exclusions view is read-only by design** (adding exclusions is a classic malware technique) with a one-click deep link to Windows Security.
- **📊 Traffic Patterns** — every process → destination pair with cumulative bytes, flow counts, ports and reverse-DNS, a **24-hour per-5-minute activity ring** as a sparkline, a conservative **beacon-like hint** (regular cadence + similar sizes), time-window selector, regex filter, CSV export and one-click IP block. Honors the Process Monitor's ignore list; history persists across restarts. Built on one app-level shared TCP capture.
- **🕳 Black Hole** (launched as Block Proof) — *proof that your telemetry blocks work.* A **Protection status** card answers what actually stands between each app and its beacons (flag state, pin state, still-running processes started before the flag), and the whole ladder runs from the tab: **Opt out** (typed write, verified, restart-aware) → **Block & log** (hosts pin into sinkhole mode; the loopback 443 listener reads each attempt's TLS ClientHello for the hostname, captures the PID, then closes with no reply ever) → **Remove block** / **Revert**. Cross-app pins: markers match Shields Up both ways. Denials land in a live feed with per-domain/per-process counters, persisted as plain JSONL, exportable to CSV. Telemetry **opt-outs that hold**: typed JSON writes re-read to verify, a quoted `"false"` reported as **not honored** in amber, never a fake green.
- **🧵 Taskhostw Spawns** — every `taskhostw.exe` attributed to the **scheduled task** that launched it (Task Scheduler event correlation + Sysmon EID 1 backfill, instance GUIDs surviving PID churn), with a user-extensible known-tasks catalog (`Assets\known-tasks.json`), handler-DLL details, and no more false **Orphaned** verdicts on inbox tasks whose CLSID resolves through packaged COM. An offline **TaskAttributionSelfTest** harness replays hours of history to verify the engine.
- **🔬 Svchost Trace** — timeline over the 70+ service hosts: spawns with parents, connection lifetimes, byte flows, and a pattern detector (bad parents, respawn loops, beacon-regular connections, upload-dominant flows). The capture engine is built into the app — no separate analyzer binary to install or unblock.
- **🔐 Data & Privacy** — your data-sharing settings in plain English (MAPS, sample submission, diagnostic data level, SmartScreen, Smart App Control), a managed-by-organization banner, a curated who-receives catalog with sources, and a **US rights request letter builder** (CCPA/CPRA state statutes and FOIA/Privacy Act kept legally distinct) with a local-only request tracker and settings-snapshot export. Read-only by design; AutoCommand never sends anything.

### Process Monitor hardened

- **One-shot connections no longer show empty forever.** "Last Update" is seeded from the Sysmon event's own time (so telemetry beacons get a real timestamp and dim like everything else), activity refreshes on received packets too, rows whose process died are labeled **— exited**, a later process inheriting a tracked destination takes over the row, and packet counters now cover **IPv4 and IPv6**.
- **High transmitters pin themselves**: rows above a user-set transmit threshold (1 KB default) sort to the top tinted red.
- **Firewall Quick Profiles** state what they allow — including the new **Default Lockdown** — and the standalone `unblock-autocommand.ps1` release asset plus the in-app Unblock card clear Mark-of-the-Web SmartScreen blocks.

### Also fixed

- Python Spawns: **auto-discovery of python installs** (PEP 514, PATH, Store, conda, uv, pyenv-win, scoop/choco, venv scans), a startup crash, and a **6-hour Sysmon backfill** so the tab no longer starts from zero when opened late.
- Traffic Patterns: groups no longer collapse seconds after opening; the Process Monitor ignore list is honored in aggregation.
- In-app usage telemetry, fully removed — the app collects nothing, leftovers included.

> **Note:** run elevated as usual — the raw-socket capture, Sysmon event reading and the sinkhole listener need administrator rights.
