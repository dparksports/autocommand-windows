# AutoCommand v2026.10.26

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **New tab: 📊 Traffic Patterns.** Per-process network patterns — every process name → destination IP pair with cumulative sent/received bytes, flow counts, ports, PIDs, and background reverse-DNS hostnames, grouped by process. A 24-hour ring of 5-minute buckets per pair powers a sparkline column, windowed totals/rates (5 min / 1 h / 24 h / all time), and a conservative **beacon-like hint** (regular cadence + similar upload sizes). Regex filter, CSV export, one-click IP block (with the same Microsoft-infra warnings as Connections), and history that persists across app restarts (`%LOCALAPPDATA%\AutoCommand\traffic\patterns.json`, budget-capped).
- **One shared capture socket for the whole app.** `TcpStreamCaptureService` is now an app-level instance (`Shared`) with reference counting: the Python Spawns and Traffic Patterns tabs hold it with `AddRef`/`Release`, so at most one SIO_RCVALL raw socket runs regardless of how many capturing tabs you visit, and Python Spawns flows also feed the Traffic Patterns aggregation.
- **All-process flow attribution.** A new `SysmonConnectWatcherService` watches Sysmon EID 3 for every process (not just python) and tags captured TCP flows with their pid/image at connect time. Flows Sysmon never saw land under "(unattributed)" so totals stay honest.
- **Known gaps (unchanged, by design):** IPv4 TCP only — loopback, IPv6, and UDP/QUIC never cross the capture; bytes accumulate while a capturing tab (Traffic Patterns or Python Spawns) is open.

> **Note:** run elevated as usual — the shared capture needs the same raw-socket privileges as before.
