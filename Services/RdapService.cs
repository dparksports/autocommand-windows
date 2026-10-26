using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoCommand.Services
{
    /// <summary>
    /// RDAP client with a persistent, block-keyed cache, IANA-bootstrap
    /// routing and throttle-aware pacing.
    ///
    /// Why cache by REGISTERED BLOCK, not per-IP: an RDAP response describes the
    /// whole allocated range (e.g. 20.0.0.0/8's sub-block), so one lookup answers
    /// every sibling IP in that range. That keeps the cache tiny (hundreds of
    /// entries cover effectively all recurring Windows traffic) and lookups
    /// rare — which also keeps us friendly to registry rate limits.
    ///
    /// Why route via the IANA bootstrap instead of rdap.org: rdap.org is a
    /// shared convenience redirector with real rate limits (it throttled this
    /// app at ~1 lookup/2.8 min sustained). The bootstrap registry
    /// (data.iana.org/rdap, RFC 9224) is fetched once a day and cached beside
    /// the cache file, letting us resolve IP → authoritative registry locally
    /// and query it directly. rdap.org remains only the fallback when the
    /// bootstrap can't resolve an address.
    ///
    /// Throttle-respect, three layers:
    ///   · a global pacer spaces request starts ≥ MinRequestInterval apart and
    ///     a bounded queue sheds bursts (user-initiated "urgent" lookups skip
    ///     the spacing, never the limits);
    ///   · a 429/503 answer puts that host on a cooldown honoring Retry-After;
    ///   · failed lookups are negative-cached per /24 (IPv4) or /48 (IPv6)
    ///     prefix, so the sibling-IP rotation telemetry favors can't retrigger
    ///     the fetch — and a cached positive block always outranks a covering
    ///     negative prefix.
    ///
    /// Private/reserved space (RFC 1918, link-local, loopback, multicast …)
    /// is answered locally without any network request.
    ///
    /// Cache file: C:\ProgramData\AutoCommand\rdap-cache.json (machine-wide, so
    /// future CLI tools — e.g. SvchostAnalyzer --rdap — can share it).
    /// TTL: honors the server's Cache-Control max-age clamped to
    /// [1 day, 30 days], else 7 days (registration data changes slowly, and a
    /// max-age=0 answer must not defeat the block-keyed cache).
    /// </summary>
    public class RdapService
    {
        public class CacheEntry
        {
            public string StartIp { get; set; }
            public string EndIp { get; set; }
            public string QueryIp { get; set; }
            public string Netname { get; set; }
            public string Org { get; set; }
            public string Country { get; set; }
            public string Type { get; set; }
            public string AbuseEmail { get; set; }
            public bool IsNegative { get; set; }
            public DateTime FetchedUtc { get; set; }
            public DateTime ExpiresUtc { get; set; }
            /// <summary>Cooldown/queue answer, returned to the caller but never
            /// cached (already expired, skipped on Save).</summary>
            [JsonIgnore] public bool IsTransient { get; set; }
            public bool IsCurrent => DateTime.UtcNow < ExpiresUtc;
        }

        public static RdapService Instance { get; } = new();

        // ── throttle policy ───────────────────────────────────────────────
        private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PositiveTtlFloor = TimeSpan.FromDays(1);
        private static readonly TimeSpan PositiveTtlCeiling = TimeSpan.FromDays(30);
        private static readonly TimeSpan DefaultPositiveTtl = TimeSpan.FromDays(7);
        private static readonly TimeSpan NegativeTtl = TimeSpan.FromHours(6);
        private static readonly TimeSpan NetworkErrorTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan BootstrapRefreshInterval = TimeSpan.FromHours(24);
        private static readonly TimeSpan BootstrapRetryInterval = TimeSpan.FromHours(1);
        private const int MaxQueuedLookups = 25;

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            try
            {
                // registries (ARIN especially) throttle anonymous clients
                var v = typeof(RdapService).Assembly.GetName().Version;
                http.DefaultRequestHeaders.UserAgent.ParseAdd(
                    $"AutoCommand/{(v != null ? v.ToString(3) : "1.0")} (Windows network-attribution client)");
            }
            catch { /* UA is politeness, not correctness */ }
            return http;
        }

        private readonly string _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AutoCommand", "rdap-cache.json");
        private readonly string _bootstrapPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AutoCommand", "rdap-bootstrap.json");
        private readonly object _lock = new();
        private List<CacheEntry> _entries = new();
        private readonly Dictionary<string, Task<CacheEntry>> _inflight = new();

        // request pacing / cooldown state (_nextSlotUtc is serialized by the
        // gate; everything else by _lock)
        private readonly SemaphoreSlim _netGate = new(1, 1);
        private DateTime _nextSlotUtc;
        private readonly Dictionary<string, DateTime> _hostCooldownUntil = new();
        private int _queued;

        // ── IANA bootstrap (RFC 9224) — registry endpoint per CIDR ────────

        private class BootstrapRule
        {
            public int Family { get; set; }              // 4 or 6
            public List<string> Cidrs { get; set; }
            public string Url { get; set; }
        }

        private class BootstrapFile
        {
            public DateTime FetchedUtc { get; set; }
            public List<BootstrapRule> Rules { get; set; }
        }

        private List<BootstrapRule> _bootstrap = new();
        private DateTime _bootstrapFetchedUtc = DateTime.MinValue;
        private DateTime _bootstrapRetryAtUtc;
        private Task _bootstrapRefreshTask;

        private RdapService()
        {
            Load();
            LoadBootstrap();
        }

        // ── cache persistence ──────────────────────────────────────────────

        private void Load()
        {
            try
            {
                if (File.Exists(_cachePath))
                    _entries = JsonSerializer.Deserialize<List<CacheEntry>>(
                        File.ReadAllText(_cachePath)) ?? new();
            }
            catch { _entries = new(); }
        }

        private void Save()
        {
            try
            {
                // trim: expired entries first, then oldest — the cache covers
                // whole blocks, so 500 entries is effectively unbounded reach
                lock (_lock)
                {
                    _entries = _entries.OrderByDescending(e => e.ExpiresUtc).Take(500).ToList();
                }
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
                File.WriteAllText(_cachePath, JsonSerializer.Serialize(_entries,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* cache is an optimization, never fatal */ }
        }

        // ── lookup ─────────────────────────────────────────────────────────

        /// <summary>Cached block containing this IP, if any (never hits network).
        /// A current positive block outranks a covering negative prefix.</summary>
        public CacheEntry Cached(string ip)
        {
            lock (_lock)
            {
                CacheEntry negative = null;
                foreach (var e in _entries)
                {
                    if (!e.IsCurrent || !Contains(e, ip)) continue;
                    if (!e.IsNegative) return e;
                    negative ??= e;
                }
                return negative;
            }
        }

        /// <summary>Lookup with cache + in-flight dedup. Network failures are
        /// returned as negative entries, not thrown. <paramref name="urgent"/>
        /// (user-initiated) skips the inter-request spacing, never the cooldown
        /// or queue limits.</summary>
        public Task<CacheEntry> LookupAsync(string ip, bool urgent = false)
        {
            lock (_lock)
            {
                var hit = Cached(ip);
                if (hit != null) return Task.FromResult(hit);
                if (_inflight.TryGetValue(ip, out Task<CacheEntry> task)) return task;
                _inflight[ip] = task = FetchAsync(ip, urgent);
                return task;
            }
        }

        private async Task<CacheEntry> FetchAsync(string ip, bool urgent)
        {
            try
            {
                var entry = await FetchCoreAsync(ip, urgent);
                if (!entry.IsTransient)
                {
                    lock (_lock)
                    {
                        // drop this entry's predecessor and any expired entries
                        // the new positive block covers — but never let a
                        // negative result delete a still-valid positive block
                        _entries.RemoveAll(e => ReferenceEquals(e, entry)
                                              || (!entry.IsNegative && !e.IsNegative && Contains(entry, e.QueryIp)));
                        _entries.Add(entry);
                    }
                    Save();
                }
                return entry;
            }
            finally
            {
                lock (_lock) { _inflight.Remove(ip); }
            }
        }

        private async Task<CacheEntry> FetchCoreAsync(string ip, bool urgent)
        {
            // Private/reserved space has no registry data — answer locally
            // instead of spending a request on it (10.x, 169.254.x … were live
            // offenders before this short-circuit).
            byte[] addr = ParseIp(ip);
            if (addr == null || IsNonPublic(addr)) return Negative(ip, "private/reserved range");

            string url = ResolveEndpoint(ip, addr);
            string host = new Uri(url).Host;

            if (CooldownActive(host, out string why)) return Transient(ip, why);

            if (Interlocked.Increment(ref _queued) > MaxQueuedLookups)
            {
                Interlocked.Decrement(ref _queued);
                return Transient(ip, "lookup queue full");
            }
            try
            {
                await _netGate.WaitAsync();
                try
                {
                    // another request may have been throttled while we waited
                    if (CooldownActive(host, out why)) return Transient(ip, why);

                    if (!urgent)
                    {
                        var wait = _nextSlotUtc - DateTime.UtcNow;
                        if (wait > TimeSpan.Zero) await Task.Delay(wait);
                    }
                    _nextSlotUtc = DateTime.UtcNow + MinRequestInterval;

                    using var resp = await Http.GetAsync(url);
                    if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
                    {
                        var cool = DefaultCooldown;
                        if (resp.Headers.RetryAfter?.Delta is TimeSpan d && d > TimeSpan.Zero)
                            cool = TimeSpan.FromMinutes(Math.Clamp(d.TotalMinutes, 1, 60));
                        lock (_lock) _hostCooldownUntil[host] = DateTime.UtcNow + cool;
                        return Transient(ip, $"HTTP {(int)resp.StatusCode} — {host} cooling down");
                    }
                    if (!resp.IsSuccessStatusCode)
                        return Negative(ip, $"HTTP {(int)resp.StatusCode}");

                    var entry = Parse(await resp.Content.ReadAsStringAsync(), ip)
                                ?? Negative(ip, "unparseable response");
                    if (!entry.IsNegative)
                    {
                        // clamp server max-age: a max-age=0 answer must not
                        // defeat the block-keyed cache (one lookup should
                        // cover the whole block)
                        var ttl = resp.Headers.CacheControl?.MaxAge ?? DefaultPositiveTtl;
                        if (ttl < PositiveTtlFloor) ttl = PositiveTtlFloor;
                        if (ttl > PositiveTtlCeiling) ttl = PositiveTtlCeiling;
                        entry.ExpiresUtc = DateTime.UtcNow + ttl;
                    }
                    return entry;
                }
                finally { _netGate.Release(); }
            }
            catch (Exception ex)
            {
                // network trouble — shorter negative so attribution recovers
                // quickly once the network does
                return Negative(ip, ex.Message, NetworkErrorTtl);
            }
            finally { Interlocked.Decrement(ref _queued); }
        }

        private bool CooldownActive(string host, out string why)
        {
            lock (_lock)
            {
                if (_hostCooldownUntil.TryGetValue(host, out DateTime until) && DateTime.UtcNow < until)
                {
                    why = $"{host} cooling down (throttled)";
                    return true;
                }
            }
            why = null;
            return false;
        }

        // ── negative / transient entries ──────────────────────────────────

        /// <summary>Negative answer covering the queried IP's /24 (IPv4) or
        /// /48 (IPv6) prefix, so sibling-IP rotation shares one failure.</summary>
        private static CacheEntry Negative(string ip, string why, TimeSpan? ttl = null)
        {
            string lo = null, hi = null;
            try
            {
                var b = IPAddress.Parse(ip).GetAddressBytes();
                if (b.Length == 4)
                {
                    lo = $"{b[0]}.{b[1]}.{b[2]}.0";
                    hi = $"{b[0]}.{b[1]}.{b[2]}.255";
                }
                else if (b.Length == 16)
                {
                    var start = new byte[16];
                    Array.Copy(b, start, 6);
                    var end = (byte[])start.Clone();
                    for (int i = 6; i < 16; i++) end[i] = 0xFF;
                    lo = new IPAddress(start).ToString();
                    hi = new IPAddress(end).ToString();
                }
            }
            catch { /* unparseable → exact-IP-only entry (Contains falls back) */ }
            return new CacheEntry
            {
                QueryIp = ip, IsNegative = true, Netname = why,
                StartIp = lo, EndIp = hi,
                FetchedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow + (ttl ?? NegativeTtl),
            };
        }

        /// <summary>Not an answer (queue full / host cooling down): returned so
        /// callers keep their static label, but never cached or persisted.</summary>
        private static CacheEntry Transient(string ip, string why) => new()
        {
            QueryIp = ip, IsNegative = true, IsTransient = true, Netname = why,
            FetchedUtc = DateTime.UtcNow,   // ExpiresUtc left at default → never current
        };

        // ── endpoint resolution: bootstrap first, rdap.org fallback ───────

        private string ResolveEndpoint(string ip, byte[] addr)
        {
            EnsureBootstrapFresh();
            int family = addr.Length == 16 ? 6 : 4;
            lock (_lock)
            {
                foreach (var r in _bootstrap)
                {
                    if (r.Family != family) continue;
                    foreach (var cidr in r.Cidrs)
                        if (CidrContains(cidr, addr))
                            return r.Url.TrimEnd('/') + "/ip/" + ip;
                }
            }
            return $"https://rdap.org/ip/{ip}";
        }

        private void EnsureBootstrapFresh()
        {
            lock (_lock)
            {
                bool empty = _bootstrap.Count == 0;
                bool stale = _bootstrapFetchedUtc + BootstrapRefreshInterval < DateTime.UtcNow;
                if (!(empty || stale)) return;
                if (DateTime.UtcNow < _bootstrapRetryAtUtc) return;
                _bootstrapRetryAtUtc = DateTime.UtcNow + BootstrapRetryInterval;
                _bootstrapRefreshTask ??= RefreshBootstrapAsync();
            }
        }

        private async Task RefreshBootstrapAsync()
        {
            try
            {
                var rules = new List<BootstrapRule>();
                foreach (int family in new[] { 4, 6 })
                {
                    using var resp = await Http.GetAsync($"https://data.iana.org/rdap/ipv{family}.json");
                    resp.EnsureSuccessStatusCode();
                    rules.AddRange(ParseBootstrap(await resp.Content.ReadAsStringAsync(), family));
                }
                if (rules.Count == 0) throw new InvalidOperationException("empty bootstrap");

                var file = new BootstrapFile { FetchedUtc = DateTime.UtcNow, Rules = rules };
                Directory.CreateDirectory(Path.GetDirectoryName(_bootstrapPath));
                File.WriteAllText(_bootstrapPath, JsonSerializer.Serialize(file,
                    new JsonSerializerOptions { WriteIndented = true }));
                lock (_lock)
                {
                    _bootstrap = rules;
                    _bootstrapFetchedUtc = file.FetchedUtc;
                }
            }
            catch
            {
                // keep whatever we already have (possibly nothing) and retry
                // later — lookups fall back to rdap.org meanwhile
                lock (_lock) { _bootstrapRetryAtUtc = DateTime.UtcNow + BootstrapRetryInterval; }
            }
            finally
            {
                lock (_lock) { _bootstrapRefreshTask = null; }
            }
        }

        private void LoadBootstrap()
        {
            try
            {
                if (File.Exists(_bootstrapPath))
                {
                    var file = JsonSerializer.Deserialize<BootstrapFile>(File.ReadAllText(_bootstrapPath));
                    if (file?.Rules is { Count: > 0 })
                    {
                        _bootstrap = file.Rules;
                        _bootstrapFetchedUtc = file.FetchedUtc;
                    }
                }
            }
            catch { _bootstrap = new(); }
        }

        private static List<BootstrapRule> ParseBootstrap(string json, int family)
        {
            var rules = new List<BootstrapRule>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var svc in doc.RootElement.GetProperty("services").EnumerateArray())
                {
                    // RFC 9224 bootstrap format: each service is a nested
                    // array [ [cidrs…], [urls…] ] — not keyed objects
                    if (svc.ValueKind != JsonValueKind.Array || svc.GetArrayLength() < 2) continue;
                    var cidrs = svc[0];
                    var urls = svc[1];
                    if (cidrs.ValueKind != JsonValueKind.Array || urls.ValueKind != JsonValueKind.Array) continue;

                    string url = null;
                    foreach (var u in urls.EnumerateArray())
                    {
                        var s = u.GetString();
                        if (string.IsNullOrEmpty(s)) continue;
                        url ??= s;
                        if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            url = s;   // prefer https when offered
                            break;
                        }
                    }
                    if (url == null) continue;

                    var list = new List<string>();
                    foreach (var c in cidrs.EnumerateArray())
                    {
                        var s = c.GetString();
                        if (!string.IsNullOrEmpty(s)) list.Add(s);
                    }
                    if (list.Count > 0)
                        rules.Add(new BootstrapRule { Family = family, Cidrs = list, Url = url });
                }
            }
            catch { /* malformed file → act as if absent (rdap.org fallback) */ }
            return rules;
        }

        // ── parsing / address math ─────────────────────────────────────────

        private static CacheEntry Parse(string json, string queryIp)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string org = null, abuse = null;
                if (root.TryGetProperty("entities", out var entities))
                    foreach (var ent in entities.EnumerateArray())
                    {
                        var roles = ent.TryGetProperty("roles", out var r)
                            ? r.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string>();
                        var fn = VcardValue(ent, "fn");
                        if (roles.Contains("registrant")) org ??= fn;
                        if (roles.Contains("abuse"))
                        {
                            abuse ??= VcardValue(ent, "email");
                            if (fn != null && abuse == null) abuse = fn;
                        }
                    }
                return new CacheEntry
                {
                    QueryIp = queryIp,
                    StartIp = GetString(root, "startAddress"),
                    EndIp = GetString(root, "endAddress"),
                    Netname = GetString(root, "name"),
                    Country = GetString(root, "country"),
                    Type = GetString(root, "type"),
                    Org = org,
                    AbuseEmail = abuse,
                    FetchedUtc = DateTime.UtcNow,
                };
            }
            catch { return null; }
        }

        private static string VcardValue(JsonElement entity, string key)
        {
            try
            {
                if (entity.TryGetProperty("vcardArray", out var va) && va.GetArrayLength() > 1)
                    foreach (var field in va[1].EnumerateArray())
                        if (field.GetArrayLength() > 3 && field[0].GetString() == key)
                            return field[3].GetString();
            }
            catch { }
            return null;
        }

        private static string GetString(JsonElement e, string prop) =>
            e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static byte[] ParseIp(string ip)
        {
            try { return IPAddress.Parse(ip).GetAddressBytes(); }
            catch { return null; }
        }

        /// <summary>True when the entry's start–end range contains the IP (v4
        /// and v6). Entries without a range (legacy negatives) match only their
        /// exact query IP.</summary>
        private static bool Contains(CacheEntry e, string ip)
        {
            if (string.IsNullOrEmpty(e.StartIp) || string.IsNullOrEmpty(e.EndIp))
                return e.IsNegative && string.Equals(e.QueryIp, ip, StringComparison.OrdinalIgnoreCase);
            try
            {
                byte[] lo = IPAddress.Parse(e.StartIp).GetAddressBytes();
                byte[] hi = IPAddress.Parse(e.EndIp).GetAddressBytes();
                byte[] v = IPAddress.Parse(ip).GetAddressBytes();
                if (lo.Length != v.Length) return false;
                int cmpLo = Compare(v, lo), cmpHi = Compare(v, hi);
                return cmpLo >= 0 && cmpHi <= 0;
            }
            catch { return false; }
        }

        private static bool CidrContains(string cidr, byte[] ip)
        {
            int slash = cidr.IndexOf('/');
            if (slash <= 0) return false;
            byte[] net;
            try { net = IPAddress.Parse(cidr.Substring(0, slash)).GetAddressBytes(); }
            catch { return false; }
            if (net.Length != ip.Length) return false;
            if (!int.TryParse(cidr.Substring(slash + 1), out int bits) ||
                bits < 0 || bits > net.Length * 8) return false;
            int full = bits / 8, rem = bits % 8;
            for (int i = 0; i < full; i++)
                if (net[i] != ip[i]) return false;
            if (rem > 0)
            {
                int mask = (0xFF << (8 - rem)) & 0xFF;
                if ((net[full] & mask) != (ip[full] & mask)) return false;
            }
            return true;
        }

        /// <summary>Private/reserved/loopback/link-local/multicast space — no
        /// registry has data for it, so never spend a request on it.</summary>
        private static bool IsNonPublic(byte[] b)
        {
            if (b.Length == 4)
                return b[0] == 0 || b[0] == 10 || b[0] == 127
                    || (b[0] == 169 && b[1] == 254)
                    || (b[0] == 172 && (b[1] & 0xF0) == 16)
                    || (b[0] == 192 && b[1] == 168)
                    || b[0] >= 224;
            if (b.Length == 16)
            {
                // IPv4-mapped (::ffff:a.b.c.d) — judge by the embedded v4 address
                bool mapped = b[10] == 0xFF && b[11] == 0xFF;
                for (int i = 0; mapped && i < 10; i++) if (b[i] != 0) { mapped = false; break; }
                if (mapped)
                {
                    var v4 = new byte[4];
                    Array.Copy(b, 12, v4, 0, 4);
                    return IsNonPublic(v4);
                }
                return b[0] == 0                                  // ::/8 — IETF reserved (::, ::1, …)
                    || (b[0] & 0xFE) == 0xFC                      // fc00::/7 unique-local
                    || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)    // fe80::/10 link-local
                    || b[0] == 0xFF;                              // ff00::/8 multicast
            }
            return true;   // unknown family — don't send it to a public registry
        }

        private static int Compare(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
            return 0;
        }

        public string CachePath => _cachePath;
        public int EntryCount { get { lock (_lock) { return _entries.Count; } } }
    }
}
