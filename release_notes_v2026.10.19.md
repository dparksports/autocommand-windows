# AutoCommand v2026.10.19

The Blocks tab. A real block list told the story: 50 of 64 blocks labeled
"svchost.exe", 66% of them on Microsoft infrastructure, made in overnight
bursts — a list that could not answer its own questions. "What did I even
block?" "Which of these is breaking the thing that just stopped working?"
This release answers both, and keeps answering them for every block made
from now on.

## The Blocks tab — blocks grouped by the service that made the connection

A new ⛔ Blocks tab in the CONTAIN group lists every AutoCommand firewall
block, folded under the **Windows service that actually made the
connection** — "Delivery Optimization (DoSvc)", "Cryptographic Services",
"Defender Antivirus Engine" — never a wall of identical "svchost.exe"
rows. Each group header carries a plain-language description of what the
service does and an impact rating: red groups (Defender, lsass,
SmartScreen, Windows Update) break security, sign-in or updates when
blocked; orange degrade features. Rows show what was blocked, when, the
classification at block time, and **last transmitted packets** — live from
the monitor, else the newest archived connection attempt (Sysmon logs
attempts even when the firewall drops them, so you can see a block is
still being probed — or is dead weight on a rotated CDN edge).

**Toggles at both levels.** Pause/resume any single block, or a whole
service group with one impact-aware confirmation. Pausing disables the
rules and lets traffic flow immediately, but deletes nothing — every
record stays in the list to re-enable, and every action lands in an
append-only history log.

## blocks.txt v3 — identity captured at block time

The block record format grows up. v3 rows carry the service identity,
display name, classification snapshot, origin, attribution source and
pause state — captured the moment the block is made, while the PID (and
its identity) still exists. The file moves to
`%LOCALAPPDATA%\AutoCommand\blocks.txt`: the old CWD-relative file was
orphaned by every versioned install folder. The loader is
version-tolerant — v2 rows and the older myfirewall 3-field rows (both
column orders) migrate in place, the original file is preserved as
`blocked.txt.imported-<timestamp>`, and anything unrecognized is kept
verbatim rather than silently dropped.

**Attribution repair recovers the past.** "🔎 Repair attribution"
back-fills identity for legacy rows in one pass: the 30-day Sysmon
archive supplies network event → PID → process create → `-s` service, and
when the PID predates the archive window but still runs (svchost hosts
live for days), the SCM's live service map — guarded against PID reuse by
process start time. On the motivating machine this turned 47 anonymous
"svchost" rows into Network List Service ×18, DiagTrack ×13, CryptSvc
×11, in seconds; the 6 the archive genuinely cannot answer stay honestly
unresolved instead of guessed.

## The Restore guide — "it broke; which rule do I lift?"

When a network feature fails, the top of the Blocks tab answers the only
question that matters: pick the broken feature — internet connectivity,
Windows Update & Store, Defender, sign-in, Start menu & search,
notifications, Edge, certificates, telemetry, clock sync — and the guide
lists exactly the active blocks in that feature's blast radius, ranked by
freshest connection attempt (the block still swallowing traffic sorts
first). One confirmation pauses all of them and traffic resumes;
un-pausing any of them later is a click. Symptom matching reaches both
directions: service-group blocks by name, and infrastructure blocks by
classification — an unresolved Akamai edge still stops update delivery,
so "Windows Update broken" finds it anyway.
