# AutoCommand v2026.10.25.1

## One DNS resolver, one geo cache — ip-api.com trips collapse

AutoCommand was hitting ip-api.com far more than its answers justified. Not because caching was missing — a per-IP cache existed — but because of how it was used:

- **Four resolvers, four caches.** The Svchost monitor, Traffic Patterns and BITS each constructed their own `DnsResolutionService`: the same IP was resolved once per tab, and each instance kept its own 1400 ms throttle. Four throttles together ran at 4× the free-tier limit (45 req/min), so ip-api answered with rate-limit failures.
- **The shared cache file was being clobbered.** All instances persisted to the same `dns_cache.json`, each writing only its own in-memory dict — last writer wins. Every restart reloaded a gutted file, found everything "unresolved", and re-queried.
- **Failures were cached as facts.** A rate-limited or timed-out geo lookup stored the raw IP as the "hostname" for 24 hours: a wasted trip that also displayed wrong data, then repeated forever.
- **Per-IP keys missed the block.** org/country is constant across an operator's whole address range, but DNS-rotating fleets (Azure, Google, Akamai) present hundreds of distinct addresses — each one its own API call.

**The fix, in layers:**

- **`DnsResolutionService` is now a singleton** (`Instance`) shared by every tab and watcher: one cache, one throttle, one file writer, one background worker. `Start()` is idempotent, and BITS no longer disposes the shared resolver when its tab stops.
- **Geo answers are cached by /24 prefix** (`150.171.27.0/24`), not per address — one answer serves an entire rotating fleet block for **14 days**, persisted in `geo_prefix_cache.json` across restarts. The PTR-hostname cache stays per-IP, since reverse DNS really is per-address.
- **Local answers never reach the API.** Locally classified ranges (Microsoft/Azure, Akamai, Gcore — the most frequent talkers on a Windows machine) are answered offline from the same range table the Blocks tab uses; RFC1918, loopback, link-local, CGNAT and multicast addresses never spend a call.
- **Transient failures back off instead of lying.** A rate limit or timeout caches nothing and doubles the inter-call delay (1.4 s → up to 5 min, reset on success); a definitive "no data" answer is negative-cached for 24 h. No more day-long wrong "unresolved" rows born from one busy minute.
- **Cache files survive crashes**: all three (`dns_cache.json`, `dns_forward_cache.json`, `geo_prefix_cache.json`) now write atomically (temp + replace), with a 60-second save timer backing the existing checkpoints.

Steady-state effect: locally-classified talkers cost zero trips, each new operator block costs one trip per two weeks, and the 4× throttle overrun is gone. One visible change: addresses in locally-classified ranges now show as `Microsoft/Azure` / `Akamai (MS CDN)` instead of ip-api's `Microsoft Corporation · US` — same knowledge, no trip.

Not included (available if the throttle ever still binds): batching queued lookups into ip-api's 100-IP POST endpoint.
