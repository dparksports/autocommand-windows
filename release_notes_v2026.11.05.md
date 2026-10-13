# AutoCommand v2026.11.05

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🕳 Block Proof is now Black Hole — and it answers the real question first.** A new **Protection status** card tells you what is actually standing between each app and its beacons: the opt-out flag state (opted out / on / **not honored**), the network pin state (none — *beacons still leave the machine* / silent / *this tab catches the attempts*), and — the case that fooled everyone — a count of **python processes started before the flag was applied**, derived from process start times versus the settings file's last write. Ultralytics decides telemetry at import, so yesterday's still-running script keeps beaconing under the old setting until restarted; the card says so, inline.
- **States that teach instead of shrug.** No pins? The tab explains that a flag only governs new processes and never produces rows here — and where to create a pin. Pins silent (`0.0.0.0`)? It says attempts *are* being denied but leave nothing observable to log — which is what Black Hole logging mode is for. Port 443 busy, app closed, tampered rules — every edge states itself.
- **Python Spawns backfill** (shipped in v2026.11.04) — the last 6 hours of Sysmon EID 1/5 replayed through the live handlers, so the tab no longer starts from zero when opened late.

> **Note:** run elevated as usual. The protection card reads the rules knowledge base at `Assets\telemetry_rules.json` next to the exe and the live hosts file; attempt logging additionally needs a pin in `127.0.0.1` (sinkhole) mode with the app running.
