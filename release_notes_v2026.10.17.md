# AutoCommand v2026.10.17

One new tab, born from this machine's own investigation: on 2026-10-15 the
monitor flagged `svchost -k netsvcs -p -s BITS` uploading packets to
34.104.35.123. The triage was clean (Chrome Component Updater pulling from
Google's gvt1.com CDN via BITS — three jobs, all downloads, status 0x0), but
it took a manual session to prove it. Now it's a tab.

## New tab: 📥 BITS Transfers

Every Background Intelligent Transfer job, with the five questions answered
from two read-only sources stitched by job GUID — the Bits-Client operational
event log (history: EID 3 names the creating process + PID, 59/60 carry the
URL and status, 4 completes with file counts) and the BITS COM queue (live:
**direction**, state, progress, per-file local paths — all-users, read-only,
via the elevation the app already runs with).

- **Where they connect / what they connect to** — URL host → forward-resolved
  IPs (new cached forward-DNS layer in `DnsResolutionService`, 60 min TTL) →
  instant owner from the built-in Microsoft/Akamai/Gcore/Google CDN ranges,
  RDAP registry lookup for anything unknown.
- **How often** — the Patterns view rolls transfers up per (creating process
  → host): counts, uploads, per-day averages, and an interval-regularity
  coefficient (the Traffic Patterns beacon-CV math — low CV means
  machine-scheduled).
- **Why** — a reason catalog (`BitsReasonCatalog`) maps job name + creator +
  host to a human explanation and verdict, seeded from real triage: Chrome
  components on gvt1.com, Windows cloud fonts on fs.microsoft.com, Defender
  signatures, Office CDN, Windows Update metadata. No match → "Unattributed —
  review" with investigation pointers. Never a guess.
- **What they transfer** — direction, bytes, per-file remote + local names,
  and on completion an automatic size + SHA-256 per delivered file
  (metadata-only, Sysmon-style).
- **Their content** — size + hash are automatic; the bytes themselves appear
  only via the explicit per-file **Preview content** button (read-only
  text/hex window, in memory, never persisted or exported). PRIVACY.md spells
  the split out.
- **Does it bypass the firewall? — asked straight, answered honestly.** A
  banner on the tab and a per-job pane section, in three layers: the active
  profile's default outbound action (on this machine: Allow — meaning no rule
  is evaluated at all for BITS, or anything else outbound), explicit allow
  rules scoped to the BITS service/svchost (the exemptions under a
  default-deny), and the always-true structural fact that transfers execute
  inside trusted svchost — so a firewall rule blocking the *creating* process
  never matches this traffic (ATT&CK T1197). Read-only COM over
  `INetFwPolicy2`, 5-minute cache.
- **UPLOAD-type jobs are the security event.** Update delivery is
  download-only; a BITS upload is the documented exfiltration vector. Those
  rows get a Review verdict that no catalog match can downgrade, and the tab
  forwards them to the app-level threat status bar (new
  `MainWindow.RaiseExternalThreat` hook — the same path the Security
  Enforcer uses).

## Engineering notes

- BITS COM interop is hand-declared, read-only `ComImport` vtable interfaces
  (manager CLSID `{4991d34b-80a1-4291-83b6-3328366b9097}`, `CLSCTX_LOCAL_SERVER`)
  — GUIDs and slot order verified against the SDK `bits.idl` and validated
  live on-box before a line of the service was written. No PowerShell, no
  bitsadmin shelling, nothing in the zero-PowerShell core changed.
- The stitcher has a real problem to solve: EID 59/60 messages carry no Job
  ID, and the motivating log had *three simultaneous jobs sharing one name*.
  Name-only events adopt the oldest same-name row that still has no URL —
  which reconstructs all three Chrome jobs correctly on the real log.
- The tab auto-starts on first view and keeps tracing across tab switches
  (the v2026.10.16.1 lesson); only the UI refresh timer parks on Unloaded.
- Strictly read-only toward Windows: the tab never creates, cancels or
  modifies a BITS job — BITS delivers Windows Update.
- State: `%LOCALAPPDATA%\AutoCommand\bits\jobs.jsonl` (one line per completed
  job, metadata only) and `bits-stats.json` (pattern rollups, ≤1/min dirty
  flush). Both documented in TECHNICAL_NOTES.
- `BitsTraceSelfTest`: 53 offline checks against the *real* 2026-10-15 event
  shapes (parser, stitcher, stats/CV math, firewall verdict branches, reason
  verdicts, export purity — no content in exports), plus a read-only replay
  of the machine's actual Bits-Client channel asserting the Chrome/gvt1 and
  Font Download jobs reconstruct end-to-end. All pass.

## Known gaps (honest accounting)

- Direction/local paths exist only for jobs observed live; history-only rows
  show "—" for direction and say so in the pane.
- The Bits-Client log wraps at ~1 MB — history is days-to-weeks. The tracer
  backfills the entire retained channel at start.
- Files moved or deleted after delivery show "gone" (hash stands if it was
  captured at completion). Mid-transfer BIT*.TMP files are never read; the
  qmgr database is not parsed.
- The COM poll needs the BITS service to be startable; when it isn't, the tab
  says so in the status line and event-log tracing continues regardless.
