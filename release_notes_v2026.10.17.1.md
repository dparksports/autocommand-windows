# AutoCommand v2026.10.17.1

A same-day patch, born from this machine's own morning: a PID that briefly
belonged to a short-lived `ZaiChatRecovery.exe` was reported under that name
while the PID's current holder was the monitor's own `netstat` child — an
attribution bug, a retention bug, and a README written for the wrong reader,
all found in one investigation.

## Process-name attribution: the netstat era ends

Root cause of the mislabel: the monitor snapshot joined a WMI PID→name table
against netstat output sampled at a different instant — a PID reused in
between inherits the previous owner's name.

- **One syscall instead of a child process.** New `TcpConnectionTable`
  (IP Helper `GetExtendedTcpTable`) returns every established IPv4 TCP row
  with its owning PID at a single kernel instant — no spawned netstat.exe,
  no text to parse, microseconds of skew. Every former call site uses it:
  the MCP `get_monitor_rows` snapshot, the Process Monitor's startup seed,
  and the Svchost Trace tab's baseline + 1s poll (which had been spawning
  netstat.exe once per second — the monitor no longer appears in its own
  process landscape). Runtime-validated against netstat output on the
  development machine: identical row sets, zero differences either direction.
- **(pid, start time) is the identity; a bare PID is not.** The name
  snapshot now carries `CreationDate` from WMI and stamps
  `process_started_utc` on every row — the same rule the PID Inquiry
  enforces against PID recycling.
- **Never guess a name.** A row whose owner exited between the kernel table
  and the name pass is labeled `attribution: exited` ("(exited during
  snapshot)") or `recovered-from-sysmon` — the image Sysmon recorded in the
  PID's most recent EID 1 create event, which PID reuse cannot corrupt. A
  PID whose name differs from the previous snapshot gets
  `pid_reused_since_last_snapshot: true` instead of a silent overwrite.
- **tail_sysmon printed `PID -1` on every line** — the property index read
  ProcessGuid (index 2) where ProcessId lives at index 3. Fixed.

## Sysmon retention: 64 MB → 192 MB

Measured on the development box: ~15,000 events/hour during ordinary
dev/agent activity, and the whole 64 MB default channel held ~64k events —
roughly four hours of history. Overnight incidents wrap out before morning
triage (the motivating investigation found zero Sysmon evidence for exactly
this reason). Install, repair and the startup health pass now raise the
Operational channel to 192 MB (`EventLogConfiguration`, grown in place —
existing events are kept). Best-effort: without elevation the default
retention stands and nothing else changes.

## README rewritten for the person it was for

The README now speaks to the non-technical reader — the person whose
antivirus says "No threats found" while their PC still feels wrong — in the
same plain-language style as the Z.ai Chat Recovery README: what to do in
three steps, a common-questions table where every answer is a sentence, and
honest framing of what AutoCommand is (the visibility your antivirus never
gives you, not a second guard dog). Two new infographics ship with it, built
at larger type than the existing set (body 17-19pt) and QA'd for overlap,
overflow and jargon:

- `Assets/oss_alternative_infographic.png` — what your antivirus can and
  can't tell you, side by side with what AutoCommand adds.
- `Assets/plain_english_infographic.png` — see it, ask about it, stop it.

Generator: `Assets/generate_infographics_oss.py` (house matplotlib style).

## Known gaps (honest accounting)

- The kernel-table snapshot is IPv4-only (AF_INET), inherited from the
  netstat parsing it replaces; the Sysmon event path covers IPv6.
- Machines that never run the app elevated keep the 64 MB channel default.
