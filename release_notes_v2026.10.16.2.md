# AutoCommand v2026.10.16.2

Second same-day follow-up to v2026.10.16. The Process Monitor gets the structural fix for "no traces of svchost", plus a leaner status bar.

## The Process Monitor now seeds existing connections at start

- **The trap.** The monitor is event-driven: a row appears only when Sysmon logs a *new* network connection (Event ID 3) after you press Start. svchost's dominant channels are long-lived — the WNS push channel dates from boot and rarely reconnects, Delivery Optimization only connects during update fetches — so minutes could pass with zero new svchost events while browsers constantly open fresh ones. The list filled with everything *except* svchost, reading as broken detection. (Verified live: at any moment this machine had exactly one open svchost connection, the boot-time WNS channel to Azure.)
- **The fix.** Pressing Start now snapshots the machine's established TCP connections (`netstat -ano`) and seeds one row per connection before live events take over. Same filters as the event path — ignore list, ignored IPs, cloud filter, one row per remote IP — and seeded rows travel the exact same pipeline as event rows: task attribution, svchost service-identity enrichment, DNS resolution, stale-dimming. The status line reports how many were seeded ("Monitor active · N existing connection(s) seeded").
- Seeded rows carry the PID's executable path and start time, so their identity survives process exit exactly like Sysmon-sourced rows. Seeding runs on a background thread (a PID/path lookup per connection); IPv6 remotes are left to the live event path.
- The tab subtitle now reads "…+ startup connection baseline".

## Leaner status bar

- The **Ignored: N** and **Blocked: N** counts moved from the far end of the controls row to sit right after the monitor state they belong to.
- The **Inquire PID** combo and **Add Process** box are gone from the bar. PID tracing remains fully available on the row context menu ("🔎 Trace: how was this PID spawned?") and by row double-click; Add Process was vestigial (the monitor already watches all processes).
- The bar now reads: monitor status → Ignored → Blocked → High-TX threshold → DNS cache → cloud filter → ignore-process controls.
