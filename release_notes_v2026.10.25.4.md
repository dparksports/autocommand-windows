# AutoCommand v2026.10.25.4

## RDAP attribution that respects the registries

IP owner lookups were quietly hammering rdap.org — roughly one query every three minutes, 500+ connections a day — and getting throttled for it (the 403s were landing in the cache as failures). Four things were wrong at once, and all four are fixed:

**Queries now go straight to the authoritative registry.** The IANA bootstrap registry (RFC 9224) is fetched once a day and cached beside the block cache, so an IP resolves to its registry locally — ARIN, APNIC, RIPE — and the query goes there directly. rdap.org, the shared redirector everyone rate-limits, is now only a fallback when bootstrap resolution fails.

**The cache can't be defeated anymore.** Failed lookups are negative-cached per /24 (IPv4) or /48 (IPv6) prefix instead of per exact IP — Windows telemetry rotates through sibling IPs, and each sibling was a fresh miss, a fresh rdap.org hit, a fresh throttle. A cached positive block always outranks a covering negative. And a server answer of `max-age=0` no longer evaporates a block entry on contact; TTLs are clamped to 1-30 days.

**The client is polite by construction.** A global pacer spaces request starts at least 10 seconds apart with a bounded queue; a 429/503 puts that host on a cooldown honoring `Retry-After`; and requests now carry a User-Agent identifying AutoCommand (registries, ARIN especially, reject anonymous clients — that was the 403s). Private and reserved ranges (10.x, 169.254.x, link-local IPv6…) are answered locally with no network at all — those were spending real queries.

**The Blocked tab's "RDAP-verify" button stays snappy** — user-initiated lookups skip the inter-request pacing while still obeying cooldowns and queue limits.

Net effect: rdap.org drops to effectively zero, new address blocks are answered by the right registry in one query, and the whole block (not one IP) is cached from that single answer — verified live against ARIN and APNIC, where one Microsoft lookup now covers their entire 20.180.0.0/12 telemetry range for a week.
