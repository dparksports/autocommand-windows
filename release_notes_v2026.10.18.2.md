# AutoCommand v2026.10.18.2

Two features for people who stare at the Process Monitor and ask "what IS
that?": the real name behind a row now surfaces (and corrects itself), and
the app can start simple — the deep-dive tabs wait behind an opt-in toggle.

## The real name behind the row — the x-rayed process name

You know the moment: a row shows a name that means nothing — or worse, a
name a recycled PID lent it — and only the PID inquiry reveals the truth
("it was `git-remote-https.exe` all along"). Until now that finding died
with the trace window. Four changes close the loop:

- **Rows are named by the event, not the live PID.** A row's name now comes
  from the Sysmon event's own image field — the one source that is
  definitionally correct for that connection. The live lookup only runs
  when Sysmon gave no image. A recycled PID can no longer put the wrong
  tenant's name on a row at birth.
- **The trace corrects the row.** Whatever the PID inquiry establishes
  about identity flows back onto the row that opened it — guarded by the
  start-time anchor, so a report about a different tenant of the number
  never overwrites anything. Corrections are visible, never silent: the
  tooltip says "real name revealed from the Sysmon record (row previously
  showed '…')".
- **Reveal without the trace.** A new context-menu item, "🏷 Reveal real
  process name (Sysmon record)", runs the same anchored archive recovery
  on demand. Rows whose name never resolved get one automatic attempt on
  the refresh tick — strictly one-shot per row, so the disk cost stays
  bounded.
- **The name gets translated.** The row tooltip now shows "spawned by
  git.exe" (from the create-record's parent image) plus a plain-English
  gloss for the top offenders — git-remote-https.exe is "Git's network
  helper — spawned by git.exe for every fetch/push over HTTPS" — and the
  PID inquiry gained the same "ℹ" gloss line under its identity card.
  The generic parent line covers every helper without a glossary entry.

## Advanced investigation — the deep-dive tabs are opt-in

New Settings card, **off by default**: "Show advanced investigation tabs".
Off, the sidebar is a plain watch-and-block tool — Process Monitor,
Firewall, Be My Guest, Settings. On, eight deep-dive tabs appear in place:
Spawns, BITS Transfers and Traffic Patterns under WATCH; Black Hole and
Windows Defender under CONTAIN; Data & Privacy, OneSettings and Sysmon
Audit under SYSTEM. Applies immediately (no restart), is remembered across
launches, and the group-header tooltips say where the hidden tabs went.
Hidden tabs also never start their background collectors — off really is
lighter, not just quieter.

## Known deferred

Name/hash watch-rules (block the exe *wherever* it lands next) and a
`block_process` agent tool; the engines are untouched and the offline
self-tests still cover them.
