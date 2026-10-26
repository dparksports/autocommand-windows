# AutoCommand v2026.10.25.2

## Phone Home: at a glance and in depth

The Phone Home tab is now two panes: every Windows talker as one compact
row on the left, the full story of the selected one on the right.

- **One row per service**, sorted by measured connects per hour: a
  colored verdict dot (green = inside the authored normal band, amber =
  above it, gray = no baseline), the name, and the live numbers —
  `2.6/hr · 63 in 24h · 0 live`. The whole curated set reads in one
  screenful with no scrolling; arrow keys walk the list.
- Two quiet signals ride on the row itself: **🔇** when you've quieted a
  service (endpoints pinned, attempts denied), and **⚠** when the beacon
  analyzer sees regular-interval, beacon-like timing.
- **The detail pane renders instantly** — selection binds to facts that
  were already computed, and what you're reading stays on screen through
  the 60-second refresh and re-sorting.
- **New in the detail pane**: a 24-hour connect histogram (hover a bar
  for "5h ago · 3 connect(s)"), the full destination list instead of the
  top five, and a per-destination **block button** — block exactly the IP
  you're suspicious of, with the same confirmation, impact warning and
  one-click undo as every other AutoCommand block.
- Everything from the card view is still there: the plain-English story,
  the is-that-normal verdict, TLS volume, identity corroboration, and
  receipts when a quieted service has knocked and been denied.

Same rule as always: every number comes from the local Sysmon archive
and the live connection table on this PC — nothing new leaves the
machine.
