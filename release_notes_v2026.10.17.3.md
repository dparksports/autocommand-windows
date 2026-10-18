# AutoCommand v2026.10.17.3

A navigation release: the three Sysmon trace tabs become one, the sidebar gets
grouped, and — the part that actually mattered — watching no longer stops just
because you clicked somewhere else.

## One Spawns tab, three feeds

**Svchost Trace, Taskhostw Spawns and Python Spawns were three tabs running
the same machinery** (Sysmon spawn tracing + attribution) sliced by process
name — which forced you to guess the right tab before you knew what you were
looking for. They are now one **Spawns** tab with a segmented selector:

- **Scheduled Tasks** (the old taskhostw feed) — every taskhostw launch with
  the scheduled task behind it, verdict chips and the attribution pane.
- **Python** — launches, call stacks for instrumented environments, TLS flows.
- **svchost Timeline** — spawn genealogy, connections, destination
  classification, beacon detection and the pattern report.

The feeds stay in the visual tree and switch by visibility, so **browsing
between them never unloads or interrupts one that's recording**. Each segment
carries a live dot — green while that feed is recording, grey when stopped —
so the tab's state is readable without visiting each feed.

## The silent-coverage bug

The taskhostw and Python feeds used to **stop watching when their tab was
left** (`Unloaded → StopWatching`) — start a watch, check the firewall tab,
come back, and everything in between was gone with no indication. Both feeds
now follow the lifetime policy the svchost timeline already documented ("a
recorder must not stop because another tab was viewed"): a feed records from
its first activation until you press *Stop Watch* or close the app. The 6-hour
Sysmon backfill still seeds each feed on first open, so the window before you
arrived is covered too.

## Grouped sidebar

Fourteen tall tabs are now twelve single-line entries in three groups:
**Watch** (Process Monitor, Spawns, BITS Transfers, Traffic Patterns),
**Contain** (Firewall, Black Hole, Windows Defender) and **System** (Be My
Guest, Data & Privacy, OneSettings, Sysmon Audit, Settings). The group
headers are non-selectable; all twelve entries fit a default-height window
without scrolling. BITS stays its own tab deliberately — its rows are
transfer jobs, not process launches, and merging them would make one grid
that's mostly empty columns per feed.

## Smaller

- **Verdict chips are colored now** — Inbox green, Third-party amber,
  Orphaned red — instead of all sharing amber text; the three states read
  apart at a glance.
- README gains a "Where everything lives" section with a new house-style
  infographic (`app_map_infographic.png`) showing the grouped sidebar and the
  Spawns merge. All five OSS infographics regenerated under the same light
  theme and version stamp.

## Known deferred

A true single grid unifying the taskhostw + Python rows (they share the
`SpawnItem` model), substring-search filters with opt-in regex, a
confirmation on *Kill process*, and cross-tab row linking (PID → connections
→ timeline). The engines are untouched; the offline self-tests still cover
them.
