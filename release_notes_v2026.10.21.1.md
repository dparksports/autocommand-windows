# v2026.10.21.1 — Your sites, your browsers, one-click unblock

Two answers to the two questions real users ask after their first
"Why is this site slow?" run.

## The picker is now user territory

- **"Sites from" filter**: pick from *All apps*, your ★ pinned sites, or
  the sites one specific browser actually visits — Chrome, Brave,
  Firefox, Edge, Opera, Vivaldi and friends are detected from your own
  recorded traffic. Every user's list is their own browsing; there is no
  built-in site roster anywhere in the flow (and never was — but now
  it's obvious).
- **★ Pin**: pin any typed site to the top of the picker. Pins live on
  this PC (`%LocalAppData%\AutoCommand\site-doctor-pins.json`), survive
  restarts, and are yours alone.
- Control sites (the comparison baseline) always come from the
  all-traffic pool, so filtering or pinning can never skew a verdict.

## Unblock right from the verdict

When the check finds addresses that fail **and** are on AutoCommand's
block list (via the v2026.10.21 live blocked-inventory cache):

- the evidence line says so outright — `142.250.141.84: TCP FAILED …
  ← ON YOUR BLOCK LIST`
- a **"🚫 Unblock N blocked address(es) for this site"** button appears
  under the verdict: confirmation lists each address with the date it
  was blocked, then removes **only AutoCommand's own AC-BLOCK rules**
  for them — nothing else in Windows Firewall is touched, and every
  address can be re-blocked from the Blocks tab. Then re-run the check
  to confirm the site is back.

No more "open wf.msc and hope you find it" runarounds.
