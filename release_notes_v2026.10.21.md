# v2026.10.21 — Why is this site slow?

A new WATCH tab that answers the question every monitoring app dodges:
**"this one site feels slow — is it me?"** Born from a real diagnosis on
this codebase's own development machine (a site whose refreshes stalled
for seconds while every other site was fine), it automates the method
that cracked that case — and fixes the honest-UX trap that nearly
produced a false verdict along the way.

## New: 🩺 Why is this site slow?

- **Type a site (or pick one from your recorded traffic) → Run 60-second
  check.** The check reads the Transmissions archive (edge dispersion,
  retry bursts, connection churn), sends a small strictly-capped set of
  connections — about 21 operations, every one listed in the log — and
  compares against a control site you actually use.
- **One plain-English verdict, one reversible next step, the math one
  click away.** Verdicts: Blocked · Local TLS stack (every handshake
  fails, controls included — a post-hardening condition observed in the
  wild) · Server drops some handshakes (the works-in-one-app signature)
  · Server roulette (short-TTL DNS steering) · Far-away servers · No
  network problem found — with an honest "fast now, but history shows
  bursts" split.
- **Dual-shape TLS probe**: each server address gets two handshakes —
  the Windows-default hello and a plain TLS 1.2 hello. A server that
  answers one and silently drops the other is exactly the "slow
  refresh" signature, and now it's measurable.
- **AI guests get the same tool**: read-only `diagnose_site` on the Be
  My Guest bridge runs the whole pipeline in ~30 seconds.

## Fixed: the Down column can no longer lie

The Transmissions archive recorded `server_bytes: 0` on every row for
every site — the capture socket was seeing outbound only (RCVALL_ON
depends on the NIC honoring promiscuous mode). It now requests
`SIO_RCVALL_IPLEVEL` first (both directions at the IP layer, no
promiscuous dependency), falls back on older stacks, tallies inbound
vs outbound per session, and when a session is one-way the tab says so
in a banner: a 0 in Down means "not observed", not "nothing came
back". This is the readout that nearly produced a false "replies are
blocked" verdict during the original diagnosis.

## For developers

`SiteDoctorSelfTest\` — all pass (one loud SKIP by design on machines
whose schannel cannot host TLS). Full subsystem write-up in
`docs/TECHNICAL_NOTES.md` → *Why is this site slow?*
