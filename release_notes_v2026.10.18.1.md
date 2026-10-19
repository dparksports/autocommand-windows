# AutoCommand v2026.10.18.1

A one-feature patch release for the Process Monitor's block flow: what
happens when you go to block a process and it has already run away. Short
version — it's blockable now, honestly, safely, and from the right identity.

## Blocking a suspect that already ran away

Processes you want to block have a habit of being gone by the time you decide
to. That used to be two bad outcomes: if the row had a Sysmon path the block
succeeded *silently* — you'd think you stopped something that had already
finished talking — and if it didn't, you got a dead-end "could not determine
process path" error. Both are gone. The block was never really about the live
process anyway: the firewall rule matches the exe's **path** and arms its next
run, so an exited suspect is fully blockable as long as the path can be
recovered. Now it always is:

- **The row's Sysmon capture** is the first source — it recorded the image
  path when the row was created, precisely so dead rows stay actionable.
- **The 30-day Sysmon archive** is the fallback when even that is missing:
  the suspect's create-event *is* its identity (path, command line, parent,
  hashes), and when several recycled PIDs share the number, the record that
  matches the row's start time and image wins.

And the flow is honest about what happened. The confirmation says so plainly
— "Process already exited — block its executable for next time?" — with the
limitation spelled out: the rule covers that path; a copy of the exe
elsewhere is not blocked. The success toast repeats it when the prompt was
suppressed, and the rule's Description carries an `exited` (or
`path-from-archive`) marker for wf.msc audits later. When nothing can be
recovered, the error is replaced by guidance: block the remote IP it talked
to, or open the PID inquiry for everything recorded about it.

One hazard fixed on the way: the old flow queried the live PID *first*, so a
recycled number handed the block to whatever unrelated process now owns it —
silently. Exited rows no longer consult the live PID at all, and live rows
cross-check the name first (the same guard the kill flow has); a mismatch
blocks the row's own process, not the PID's new tenant. The unblock path uses
the row's identity too, so unblocking can't target a recycled PID's path
either.

## Known deferred

Name/hash watch-rules (block the exe *wherever* it lands next, not just the
recovered path) and a `block_process` agent tool; the engines are untouched
and the offline self-tests still cover them.
