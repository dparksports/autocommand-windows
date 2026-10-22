# AutoCommand v2026.10.22

## Site Doctor: "the site is fine — its dependency is blocked here"

The tab formerly known as "Why is this site slow?" could not see one failure class at all: a site whose **own servers answer perfectly** while its sign-in or parts of its pages fail, because an address the *page depends on* (captcha, assets, APIs) is on this machine's own block list. It used to answer "Healthy — try a private window with extensions off", actively pointing away from the cause.

- **Why the old pipeline was blind:** a firewall-dropped connect dies before the TLS ClientHello, so it carries no hostname and never enters the transmissions archive — the probe measures the typed host only, and the block cross-check only looked at that host's failed edges. The only record of a blocked connect is the Sysmon EID 3 join against the block inventory.
- **New evidence pass** (`DependencyBlockService`): anchors on the archive's recorded page loads ("brave.exe fetched www.spectrum.net at 08:23"), pulls blocked connects from the same browser within ±2 minutes of each load, and attributes every blocked address by what it has **served on this machine** (recorded SNI history — "this address also serves mail.google.com").
- **New verdict, `DependencyBlocked`** — ranked above the remote-cause rules, red-panel, headline naming the browser, the blocked IP, what it serves and when: *"www.spectrum.net's own servers answer fine — but at 08:23, brave.exe made 7 blocked connection attempts toward 142.251.34.67 — an address this page depends on…"* The **one-click unblock** button now covers the dependency addresses too ("🚫 Unblock N address(es) this page depends on"), fully reversible from the Blocks tab.
- **Intermittency explained:** the page's dependency hosts are resolved live and crossed with the block list — "www.google.com currently resolves to 8 addresses; 2 of them are blocked here — that rotation is why it comes and goes."
- **Honesty guards:** a blocked connect is called causal only inside a recorded visit window; hits outside it surface as context and never hijack a Healthy verdict. Attribution never guesses past recorded history.
- **Signpost for users who don't know the tab is for them:** on load, a hint appears when a browser has hit blocked addresses in the last day — "Brave hit blocked addresses 4 time(s) in the last day — if a page or sign-in failed, that's usually why. Check the site."
- **The AI bridge gets the same answer:** `diagnose_site` runs the identical pass (its tool description no longer says "slow" only) and returns a `dependency_blocks` section — hits with process, attempts, timestamps, served-hosts and block provenance, plus the rotation facts.

## Rename: "Site Doctor"

A question-shaped header ("Why is this site slow?") self-selects for slowness — a user staring at an authentication error concludes the tab isn't about them and never opens it. The tab is now **🩺 Site Doctor**, with the scope stated where you type: *"Slow, failing, or can't sign in — one 60-second check that measures, compares and explains."*

## Self-test

New section 5b replays the login-failure shape end to end over synthetic fixtures (healthy probe edges + in-window blocked connects, the exact capture that motivated the feature): the join, attribution, block provenance, exclusions (other processes and other browsers), rotation math, the new verdict, and the honesty negatives (nearby-only hits keep `Healthy`; the join stands alone with no probe). 43 checks, all passing.
