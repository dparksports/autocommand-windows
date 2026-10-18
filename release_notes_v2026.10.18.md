# AutoCommand v2026.10.18

A workflow release for people who trace and block all day: the confirmation
popup now has a "don't ask again" checkbox that actually respects what you
meant, and the Process Monitor finally shows which rows are blocked — in the
grid, not just in a popup.

## "I know what I'm doing" — said once, remembered per risk class

Blocking an IP or a process used to show the same confirmation every single
time. Trace ten suspicious connections, block ten things, click through ten
identical warnings. The confirmation is now a proper dialog with a checkbox —
and the checkbox is **per warning class, not a global off switch**:

- **Don't ask again before blocking remote IPs** — the routine confirm.
- **Don't ask again before blocking processes** — same, for executables.
- **Don't warn me about Microsoft-infrastructure IPs** — the "think twice"
  warning for Microsoft ranges.
- **Don't warn me about blocking Windows service hosts / components** — the
  svchost/taskhostw "this is how you lose your internet" warning.

Silencing the routine IP confirm never silences the svchost warning. A prompt
is skipped only when the generic confirm *and* its applicable warning class
are both suppressed — so even with everything else quieted, blocking a
Microsoft IP still gets its warning until you've silenced that class too.
Choices persist across launches (`confirm_prefs.txt`) and are reversible: the
Blocked popup has a **↩ Reset "don't ask again" choices** button with an
"N of 4 suppressed" indicator, so the state is visible, not a hidden cookie.

Blocks made under a suppressed prompt say so — the firewall rule's
Description carries a `confirm-suppressed` marker, auditable in wf.msc later.

## No more "Blocked!" popups — the row tells you

Every per-block success MessageBox is gone. Blocks now report through a
small toast in the corner of the grid with a **↩ Undo** button, and it
auto-hides after a few seconds. The real confirmation is the row visibly
flipping to blocked in the list.

## Blocked vs allowed, at a glance

A new **Block** column leads the grid:

- **⛔** — this row's traffic is denied (an IP and/or process block rule is
  active).
- **⏸** — block rules exist but are disabled (the fleet pause) — traffic
  actually flows, so it's not shown as blocked.
- empty — allowed, which is the outbound default; unmarked means nothing to
  worry about.

The affected cell itself turns red with strikethrough — the Remote IP cell
when the IP is blocked, the Process cell when the executable is — so you can
see *which half* is blocked, not just that the row is. Hovering a marked cell
or the ⛔ shows the rule's provenance (which process contacted it, when it
was blocked, how much traffic had flowed).

The state comes from the **live firewall inventory**, not from the monitor's
own records — blocks created by your AI assistant through the Be My Guest
bridge, or by hand in wf.msc, mark rows the same as ones created here. The
grid re-syncs after every block or unblock, when the Blocked-rules window
closes, and once a minute.

The right-click menu tells the truth too: on a blocked row, "⛔ Block remote
IP…" becomes "**✅ Unblock remote IP … — traffic resumes**" (same for
processes). And when a block is only paused, the menu says so and declines to
"re-block" — re-adding a paused rule would silently do nothing.

## Known deferred

Per-IP suppression memory (deliberately not built — a repeat IP contacted by
a *different* process deserves the prompt), a green marker for explicit
allow-rules on the svchost allowlist, and a confirmation on *Kill process*.
The engines are untouched; the offline self-tests still cover them.
