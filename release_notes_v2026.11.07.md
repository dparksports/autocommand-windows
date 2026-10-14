# AutoCommand v2026.11.07

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **Fix: "Last Update" and "Tx Pkts" stayed empty forever on one-shot connections.** Rows are created by Sysmon's connect event, but the packet counters come from the raw-socket sniffer — and for telemetry beacons (OneDS collectors, Azure CDN pings) the packets had already crossed the wire before the row existed, so short-lived sends from svchost and exited processes showed "—" and 0 forever, never dimmed, and never updated again. Three changes close that hole:
  - **Rows are stamped at birth.** "Last Update" is seeded from the Sysmon event's own UTC time, so a row always has a real timestamp (and the 5-minute staleness dimming works from the start).
  - **Received traffic counts too.** Last-activity is now refreshed on matched packets in either direction, not just transmit — receive-only rows no longer sit at "—".
  - **Dead processes are labeled.** A 10-second PID check marks rows whose process has exited ("3840 (svchost) — exited"), so a ghost connection stops looking live; trace/block still work off the Sysmon image path.
- **Rows follow new tenants.** When the process behind a tracked remote IP exits and a *different* PID later connects to the same IP, the row is re-attributed to the live process instead of accruing counters under a dead PID forever.
- **IPv6 connections get packet counts.** A second best-effort raw socket (SIO_RCVALL over IPv6) now runs alongside the IPv4 one; previously IPv6 rows could never accumulate counters at all. Where the platform refuses the v6 socket the IPv4 capture continues undisturbed, and IPv6 rows keep their event-seeded timestamp.

> **Note:** run elevated as usual — the sniffer's raw sockets require Administrator, same as before.
