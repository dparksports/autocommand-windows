# AutoCommand v2026.10.19.2

One new lens for the Process Monitor, answering a question the triage
workflow kept asking by eye: *which programs are sending packets but never
hearing anything back?*

## Why that question deserves its own switch

A connection that only ever transmits is a distinctive shape — a firewall
block doing its job, a beacon calling a dead end — and until now the only
way to find one was to scan the Rx Pkts column row by row. The count sits
next to every row, but nothing grouped the zeros together.

## What's new

- **TX-only (no reply)** — a checkbox in the Process Monitor toolbar, off
  by default. Checked, the list narrows to rows that have transmitted
  packets and never received a single one. Unchecked, the full list is
  exactly where it was: it's a lens, not a filter that forgets.
- **The count lives on the checkbox.** While the lens is off, its label
  shows how many one-way transmitters are out there ("TX-only (no reply): 3"),
  refreshed on the same beat as the High TX chip — so you can see whether
  the question is worth asking before you ask it.
- **No flashing.** A brand-new row has sent its first packet before any
  reply can arrive, so for a moment every connection on the machine looks
  one-way. Rows only qualify after 30 seconds of life with zero received
  bytes, and live filtering drops a row out of the view the instant its
  first reply lands.
- **Nothing downstream changes.** The lens is view-only: ignored-process
  handling, the hourly CSV and the AI-assistant audit context all keep
  reading the full, unfiltered list.

README and TECHNICAL_NOTES document the lens in the same terms.
