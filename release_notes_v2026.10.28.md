# AutoCommand v2026.10.28

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **New tab: 🦠 Windows Defender.** The Defender answer to "why is it eating my machine", promoted from a Settings card so it can carry real monitoring:
  - **Live resources** — MsMpEng CPU (as a share of total machine capacity), machine CPU, and private working set, sampled every 2 s with a 5-minute sparkline. Real-time protection shows up as short spikes when files change; sustained CPU during builds usually means the engine is scanning compiler output.
  - **Engine status** — real-time + tamper protection, running mode, platform/engine/security-intelligence versions, and the last quick/full scan with durations.
  - **Activity feed (live)** — straight from Defender's own operational event log: scans started/finished/stopped, detections (amber), definition updates, cloud-protection lookups/uploads, configuration changes. Backfills the last 60 events so it is never empty; scan durations are paired by Scan ID.
  - **One-click triggers** — Quick Scan, Full Scan, and Update Definitions launch detached so the tab never blocks; watch them appear in Scans/Activity while the resource strip reacts. That is the reproduce loop: scan → watch MsMpEng spike → adjust the cap → scan again.
  - **Performance knobs** — the scan CPU-cap slider (5–100%, presets Gentle/Balanced/Fast) and idle-only scheduled scans, with Tamper Protection / group-policy refusals surfaced instead of swallowed.
  - **Exclusions stay read-only by design** — AutoCommand shows what's excluded (paths/processes/extensions, so you can spot gaps) but never adds exclusions, since that is a classic malware technique; the card explains how to add them via Windows Security and why narrow build-output exclusions beat broad ones.
- **Settings returns to its pre-Defender layout** — the card moved wholesale into the new tab.
- Event IDs, WMI counters, and every preference round-trip were verified against a live machine before shipping.

> **Note:** run elevated as usual — exclusions listing and preference writes need the same privileges the app already has.
