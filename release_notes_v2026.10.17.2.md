# AutoCommand v2026.10.17.2

The two gaps named in v2026.10.17.1's "honest accounting," closed — plus the
Sysmon archive that makes the forensic promise durable.

## IPv6: the snapshot sees both families now

`TcpConnectionTable` enumerates `GetExtendedTcpTable` twice — AF_INET and
AF_INET6 (`MIB_TCP6ROW_OWNER_PID`) — so every sampled view (the MCP
`get_monitor_rows` snapshot, the Process Monitor startup seed, the Svchost
Trace baseline + 1s poll) sees established TCP in **both address families**.
IPv6 endpoints render as `[addr]:port`, netstat-style. A held `[::1]`
connection — invisible to the v2026.10.17.1 table, and, as it turned out, to
`netstat -ano -p tcp` itself on some builds — is now a first-class row, proven
live by the self-test.

- **One destination predicate, both families.** New shared
  `TcpConnectionTable.IsLocalOrNonRoutable`: loopback, link-local (fe80::/10),
  ULA (fc00::/7 — IPv6's RFC1918), multicast, broadcast. `IsIgnoredIp`
  delegates to it, which also fixes the **event path**: IPv6 EID 3
  destinations used to slip past a literal `"::1"` check and could seed
  link-local/ULA addresses as if they were internet rows.
- **Trace tab loopback filter made exact** — `Contains("::1")` also matched
  `fe80::1`; now `ip == "::1"` (the tab traces LAN traffic by design).
- `Classify` and the known-cloud CIDR list are IPv4 by construction; IPv6
  destinations classify as "IPv6/other" honestly rather than being silently
  matched. Extending the cloud catalog to v6 ranges is future work, noted
  below.

## Sysmon archive: weeks of history, readable on disk

The live channel is a rolling window (192 MB ≈ half a busy day). New
`SysmonArchiveService` makes "every spawn retroactively traceable" true for
weeks:

- **Backfill at every app start** — the entire retained channel is read and
  appended, skipping what's already archived via a (recordId, utc) watermark
  (record id alone lies when the log is cleared; time leads, id breaks exact
  ties). Cheap precheck: the channel's newest record vs the watermark.
- **Live append while the app runs** — an `EventLogWatcher` on the whole
  channel archives every event as it arrives. All EventData fields are
  preserved verbatim, so an archived EID 1 is a complete substitute for the
  live one: command line, parent image, hashes.
- **Storage is house-style JSONL** — one readable, greppable file per day
  under `%LOCALAPPDATA%\AutoCommand\sysmon-archive\`, pruned after 30 days
  (disclosed on the Sysmon Audit card, which now shows an Archive line:
  events, day files, retention).
- **Readers merged**: the PID Inquiry falls back to the archive when the live
  channel no longer holds a PID's spawns — the verdict that used to read
  "ancestry is unrecoverable" now produces evidence labeled *"Sysmon archive
  create-event (EID 1) — wrapped out of the live log."* The MCP `tail_sysmon`
  tool gains `archive=true` to query the archive instead of the live log.
- Unelevated sessions degrade silently: no channel access → no archive
  activity, nothing crashes, nothing is guessed. Events from before the
  archive's first backfill that already wrapped out of the channel are, by
  definition, gone.

## NetWatchSelfTest

New offline + live self-test (linked app sources, byte-identical to what
ships, house pattern): 42 checks — destination classification across both
families, **a live held `[::1]` connection asserted visible** in the
dual-family enumeration, endpoint formatting, archive watermark dedupe
(duplicates, older-than-watermark, same-ts-higher-rid boundary), day-file
rotation, newest-first search with eid/contains/since/max filters, streamed
PID create-event lookup, corrupt-line tolerance, and prune. All pass — and
the test caught two real Search bugs (an unapplied PID predicate and
within-file ordering) before they shipped.

## README + infographics, round two: the try-it framing

The README now speaks to the person who is simply *tired of antivirus
software* and wants to try something: opener validates the fatigue
("popups, subscriptions, 'no threats found' — and still no answer"), the
lead section is **"Try it in five minutes"** (download → run as admin →
look/ask, with "deleting the folder uninstalls it"), and a
**"What am I looking at?"** section teaches the main screen before the
first launch. Two new infographics ship with it, house style at larger
type (body 17–19pt), layout QA'd by a visual pass (which caught and fixed
a circle-grazes-heading collision on all three cards):

- `Assets/first_five_minutes_infographic.png` — the three getting-started
  steps, each with what you'll actually see, plus the no-installer /
  no-account / delete-it-anytime reassurance strip.
- `Assets/row_anatomy_infographic.png` — "one row, five things to know"
  (who, where, how much, how long, the Block/Undo buttons) — how to read
  the main screen before you ever open it.

The two v2026.10.17.1 infographics carry the new version chip.

## Known gaps (honest accounting)

- IPv6 destinations are visible but not owner-classified — "IPv6/other"
  until the cloud/CDN catalog learns v6 ranges (and RDAP lookups extend).
- The archive covers only what the app has seen: from its first elevated
  start onward. Pre-archive history that wrapped out of the live channel
  before the first backfill is unrecoverable.
- Backfill scans the whole retained channel on first run; subsequent runs
  are incremental (precheck + watermark), but a very large first backfill
  takes seconds on the background thread.
