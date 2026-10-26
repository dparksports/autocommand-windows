using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AutoCommand.Models;

namespace AutoCommand.Services
{
    /// <summary>
    /// Resolves IP → hostname in the background, with:
    ///   • A persistent JSON cache on disk (survives app restarts)
    ///   • An in-memory LRU/TTL layer for fast repeated lookups
    ///   • Rate-limited queue so we never spam any API
    ///   • Live update of SvchostMonitorItem.Hostname via INotifyPropertyChanged
    ///
    /// SINGLETON — every tab and watcher shares DnsResolutionService.Instance.
    /// Four separate instances once meant four in-memory caches (duplicate
    /// lookups for the same IP), four independent 1400ms throttles (4x the
    /// ip-api.com free-tier limit, so answers came back rate-limited), and
    /// four writers clobbering the same dns_cache.json (last save wins, so a
    /// restart re-resolved everything and re-hit the API).
    ///
    /// The geo-IP fallback is cached BY /24 PREFIX, not per address: the
    /// org/country answer is constant across an operator's whole address
    /// block, and DNS-rotating fleets (Azure, Google, Akamai) present
    /// hundreds of distinct addresses from a handful of blocks. Locally
    /// classified ranges (IpClassifier: Microsoft/Azure, Akamai, Gcore) and
    /// non-routable addresses (RFC1918, loopback, link-local) never touch
    /// the API at all. Transient API failures back off instead of caching
    /// a wrong "unresolved" answer for the TTL.
    /// </summary>
    public sealed class DnsResolutionService : IDisposable
    {
        public static DnsResolutionService Instance => _instance ??= new DnsResolutionService();
        private static DnsResolutionService _instance;

        private DnsResolutionService()
        {
            LoadCacheFromDisk();
            LoadForwardCacheFromDisk();
            LoadGeoCacheFromDisk();
        }

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private static readonly TimeSpan DnsCacheTtl   = TimeSpan.FromHours(24);

        /// <summary>Geo org/country per /24 prefix — ownership changes rarely.</summary>
        private static readonly TimeSpan GeoCacheTtl   = TimeSpan.FromDays(14);

        /// <summary>A definitive "no org data" answer — retry the prefix next day.</summary>
        private static readonly TimeSpan GeoNegativeTtl = TimeSpan.FromHours(24);

        /// <summary>Minimum delay between successive ip-api.com calls (45 req/min free tier).</summary>
        private static readonly TimeSpan GeoApiThrottle = TimeSpan.FromMilliseconds(1_400);

        /// <summary>Cap for the transient-failure backoff doubling.</summary>
        private static readonly TimeSpan GeoBackoffMax = TimeSpan.FromMinutes(5);

        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(60);

        private static readonly string CacheFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dns_cache.json");

        private static readonly string ForwardCacheFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dns_forward_cache.json");

        private static readonly string GeoCacheFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "geo_prefix_cache.json");

        /// <summary>Forward (hostname→IPs) entries are short-lived: CDN names rotate.</summary>
        private static readonly TimeSpan ForwardCacheTtl = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan ForwardNegativeTtl = TimeSpan.FromMinutes(10);

        // -----------------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------------

        // ip → (hostname, resolvedAt)
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        // "a.b.c.0/24" (IPv4; full address for IPv6) → (org·code, resolvedAt, negative)
        private readonly ConcurrentDictionary<string, GeoCacheEntry> _geoCache = new();

        // hostname → (ips, resolvedAt) — the BITS tab's "where do they connect"
        private readonly ConcurrentDictionary<string, ForwardCacheEntry> _forwardCache = new();
        private readonly SemaphoreSlim _forwardGate = new(1, 1);

        // Queue of items waiting for resolution; bounded to avoid runaway growth
        private readonly Channel<IResolvableHost> _queue =
            Channel.CreateBounded<IResolvableHost>(new BoundedChannelOptions(512)
            {
                FullMode    = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

        private readonly CancellationTokenSource _cts = new();
        private Task _workerTask;
        private Timer _saveTimer;
        private int _started;

        private DateTime _lastGeoCall = DateTime.MinValue;

        /// <summary>Grows on transient ip-api failures (rate limit, timeouts), resets on success.</summary>
        private TimeSpan _geoBackoff = GeoApiThrottle;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Starts the background resolution worker. Idempotent — every consumer
        /// (Svchost monitor, Traffic Patterns, BITS) may call it on the shared
        /// instance; only the first spawns the single reader.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1) return;
            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
            _saveTimer = new Timer(_ => { SaveCacheToDisk(); SaveGeoCacheToDisk(); }, null,
                SaveInterval, SaveInterval);
        }

        /// <summary>
        /// Returns the cached hostname for <paramref name="ip"/> synchronously,
        /// or null if not yet resolved.
        /// </summary>
        public string GetCachedHostname(string ip)
        {
            if (_cache.TryGetValue(ip, out var entry) && !entry.IsStale(DnsCacheTtl))
                return entry.Hostname;
            return null;
        }

        /// <summary>
        /// Resolves <paramref name="item"/>.RemoteIp → Hostname asynchronously.
        /// If a fresh cache entry exists, the item is updated immediately on the
        /// calling thread; otherwise it is queued for background resolution and
        /// the DataGrid row will update itself via INotifyPropertyChanged.
        /// </summary>
        public void EnqueueForResolution(IResolvableHost item)
        {
            if (item == null || string.IsNullOrEmpty(item.RemoteIp)) return;

            // Already has a real hostname
            if (!IsUnresolvedHostname(item.Hostname)) return;

            // Check cache first — update immediately if fresh
            if (_cache.TryGetValue(item.RemoteIp, out var entry) && !entry.IsStale(DnsCacheTtl))
            {
                item.Hostname = entry.Hostname;
                return;
            }

            // Otherwise push to background queue (drops silently if full)
            _queue.Writer.TryWrite(item);
        }

        /// <summary>
        /// Pre-seeds the cache with a Sysmon-provided hostname (already resolved at event time).
        /// </summary>
        public void SeedFromSysmon(string ip, string hostname)
        {
            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(hostname) || hostname == "-") return;
            _cache[ip] = new CacheEntry(hostname, DateTime.UtcNow);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _started, 2) == 2) return;   // already disposed
            _cts.Cancel();
            _queue.Writer.TryComplete();
            _workerTask?.Wait(TimeSpan.FromSeconds(3));
            _saveTimer?.Dispose(); _saveTimer = null;
            SaveCacheToDisk();
            SaveForwardCacheToDisk();
            SaveGeoCacheToDisk();
            _cts.Dispose();
        }

        // -----------------------------------------------------------------------
        // Forward resolution (hostname → IPs) — used by the BITS tab to turn
        // a job's URL host into the IPs it actually connects from. Cached
        // with a short TTL because CDN names rotate; failed lookups are
        // negative-cached briefly so a dead resolver can't spin the loop.
        // -----------------------------------------------------------------------

        public async Task<string[]> ResolveForwardAsync(string host)
        {
            if (string.IsNullOrEmpty(host) || IPAddress.TryParse(host, out _)) return new[] { host };

            if (_forwardCache.TryGetValue(host, out var cached) && !cached.IsStale)
                return cached.Ips;

            await _forwardGate.WaitAsync();
            try
            {
                if (_forwardCache.TryGetValue(host, out var second) && !second.IsStale)
                    return second.Ips;   // resolved while waiting on the gate

                string[] ips = Array.Empty<string>();
                try
                {
                    var entry = await Dns.GetHostEntryAsync(host);
                    ips = entry.AddressList
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                 || a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        .Select(a => a.ToString())
                        .Distinct()
                        .Take(6)
                        .ToArray();
                }
                catch { /* NXDOMAIN / no resolver answer — negative cache below */ }

                _forwardCache[host] = new ForwardCacheEntry(ips, DateTime.UtcNow,
                    ips.Length == 0 ? ForwardNegativeTtl : ForwardCacheTtl);
                if (_forwardCache.Count % 10 == 0) SaveForwardCacheToDisk();
                return ips;
            }
            finally { _forwardGate.Release(); }
        }

        private void LoadForwardCacheFromDisk()
        {
            try
            {
                if (!File.Exists(ForwardCacheFilePath)) return;
                var dict = JsonSerializer.Deserialize<Dictionary<string, PersistedForwardEntry>>(
                    File.ReadAllText(ForwardCacheFilePath));
                if (dict == null) return;
                foreach (var kv in dict)
                {
                    var entry = new ForwardCacheEntry(kv.Value.I ?? Array.Empty<string>(), kv.Value.T,
                        kv.Value.I?.Length == 0 ? ForwardNegativeTtl : ForwardCacheTtl);
                    if (!entry.IsStale) _forwardCache[kv.Key] = entry;
                }
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveForwardCacheToDisk()
        {
            try
            {
                var dict = new Dictionary<string, PersistedForwardEntry>(_forwardCache.Count);
                foreach (var kv in _forwardCache)
                    dict[kv.Key] = new PersistedForwardEntry { I = kv.Value.Ips, T = kv.Value.ResolvedAt };
                AtomicWriteJson(ForwardCacheFilePath,
                    JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* disk full, permissions — silently skip */ }
        }

        // -----------------------------------------------------------------------
        // Background worker — processes one item at a time
        // -----------------------------------------------------------------------

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested) break;
                if (!IsUnresolvedHostname(item.Hostname)) continue; // resolved by another path

                try
                {
                    string hostname = await ResolveAsync(item.RemoteIp, ct);
                    item.Hostname = hostname; // triggers INotifyPropertyChanged → DataGrid refresh
                }
                catch (OperationCanceledException) { break; }
                catch { /* silently skip failed items */ }
            }

            SaveCacheToDisk();
        }

        // -----------------------------------------------------------------------
        // Resolution pipeline: reverse DNS → geo-IP fallback
        // -----------------------------------------------------------------------

        private async Task<string> ResolveAsync(string ip, CancellationToken ct)
        {
            // 1. Return fresh cache hit
            if (_cache.TryGetValue(ip, out var cached) && !cached.IsStale(DnsCacheTtl))
                return cached.Hostname;

            // 2. Reverse DNS
            string resolved = await ReversesDnsAsync(ip);

            // 3. If DNS only gave us the raw IP, try geo-IP (local ranges and
            //    prefix cache first; the API only for genuinely new blocks)
            if (resolved == ip || IsUnresolvedHostname(resolved))
            {
                var geo = await GeoIpLookupAsync(ip, ct);
                if (geo.Kind == GeoResultKind.Label && !string.IsNullOrEmpty(geo.Label))
                {
                    resolved = geo.Label;
                }
                else if (geo.Kind == GeoResultKind.Transient)
                {
                    // Rate limit / transport failure: no API answer existed, so
                    // caching the raw IP would poison this address for 24h.
                    // Leave it unresolved — the monitor's re-enqueue tick (or a
                    // later session) retries once the backoff window clears.
                    return ip;
                }
                // Negative (definitive no-data) falls through and caches the
                // raw IP as before: the prefix cache already stops API trips.
            }

            // 4. Store in cache (even failed lookups, so we don't retry immediately)
            _cache[ip] = new CacheEntry(resolved, DateTime.UtcNow);

            // 5. Periodically persist to disk (every 20 resolutions; a 60s
            //    timer is the other checkpoint)
            if (_cache.Count % 20 == 0)
                SaveCacheToDisk();

            return resolved;
        }

        private static async Task<string> ReversesDnsAsync(string ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                return string.IsNullOrEmpty(entry.HostName) ? ip : entry.HostName;
            }
            catch
            {
                return ip; // DNS NXDOMAIN or timeout
            }
        }

        private enum GeoResultKind { Label, Negative, Transient }

        private readonly struct GeoResult
        {
            public readonly GeoResultKind Kind;
            public readonly string Label;
            private GeoResult(GeoResultKind kind, string label) { Kind = kind; Label = label; }
            public static GeoResult Ok(string label) => new(GeoResultKind.Label, label);
            public static readonly GeoResult NoData = new(GeoResultKind.Negative, null);
            public static readonly GeoResult RetryLater = new(GeoResultKind.Transient, null);
        }

        /// <summary>
        /// Geo/owner label for an IP, cheapest source first:
        ///   1. locally classified ranges (IpClassifier) — no network at all;
        ///   2. non-routable addresses (RFC1918, loopback, link-local…) — no
        ///      answer exists, so no trip either;
        ///   3. the /24-prefix cache — one API answer serves a whole rotating
        ///      fleet block for two weeks;
        ///   4. ip-api.com, throttled, with backoff on transient failures.
        /// Transient failures (rate limit, timeout) return RetryLater and cache
        /// NOTHING — a wrong "unresolved" answer must not outlive the outage.
        /// </summary>
        private async Task<GeoResult> GeoIpLookupAsync(string ip, CancellationToken ct)
        {
            // 1–2. Local answers cost nothing and are always in stock.
            if (!IsGloballyRoutable(ip)) return GeoResult.NoData;
            string local = IpClassifier.Classify(ip);
            if (local != "Unclassified" && local != "IPv6/other")
                return GeoResult.Ok(local);

            // 3. Prefix cache (positive and negative both stop API trips)
            string prefix = GeoPrefixKey(ip);
            if (_geoCache.TryGetValue(prefix, out var cached))
            {
                TimeSpan ttl = cached.Negative ? GeoNegativeTtl : GeoCacheTtl;
                if (!cached.IsStale(ttl))
                    return cached.Negative ? GeoResult.NoData : GeoResult.Ok(cached.Label);
            }

            // 4. The API. Spacing uses the current backoff so a rate-limiting
            //    episode widens the gap instead of compounding the 429s.
            var elapsed = DateTime.UtcNow - _lastGeoCall;
            if (elapsed < _geoBackoff)
                await Task.Delay(_geoBackoff - elapsed, ct);
            _lastGeoCall = DateTime.UtcNow;

            try
            {
                string url = $"http://ip-api.com/json/{Uri.EscapeDataString(ip)}?fields=status,message,org,countryCode";
                string json = await _http.GetStringAsync(url, ct);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status", out var status) || status.GetString() != "success")
                {
                    // A well-formed "fail" (invalid query, reserved range the
                    // local check didn't cover) is a definitive answer.
                    _geoBackoff = GeoApiThrottle;
                    _geoCache[prefix] = new GeoCacheEntry("", DateTime.UtcNow, negative: true);
                    return GeoResult.NoData;
                }

                string org  = root.TryGetProperty("org",         out var o) ? o.GetString() ?? "" : "";
                string code = root.TryGetProperty("countryCode", out var c) ? c.GetString() ?? "" : "";

                // Strip "AS12345 " prefix
                if (org.Length > 3 && org[0] == 'A' && org[1] == 'S')
                {
                    int space = org.IndexOf(' ');
                    if (space > 0) org = org[(space + 1)..];
                }

                string label = string.IsNullOrEmpty(code) ? org : $"{org} · {code}";
                _geoBackoff = GeoApiThrottle;
                _geoCache[prefix] = new GeoCacheEntry(
                    string.IsNullOrEmpty(label) ? "" : label, DateTime.UtcNow,
                    negative: string.IsNullOrEmpty(label));
                return string.IsNullOrEmpty(label) ? GeoResult.NoData : GeoResult.Ok(label);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // HTTP 429 / timeout / unparseable body — transient: back off
                // (doubling, capped) and let the caller retry later.
                _geoBackoff = _geoBackoff >= GeoBackoffMax ? GeoBackoffMax : TimeSpan.FromTicks(Math.Min(_geoBackoff.Ticks * 2, GeoBackoffMax.Ticks));
                return GeoResult.RetryLater;
            }
        }

        /// <summary>/24 network for IPv4 ("a.b.c.0/24"); IPv6 keeps its full address as key.</summary>
        internal static string GeoPrefixKey(string ip)
        {
            if (IPAddress.TryParse(ip, out var a) && a.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = a.GetAddressBytes();
                return $"{b[0]}.{b[1]}.{b[2]}.0/24";
            }
            return ip;
        }

        /// <summary>False for addresses that can never have a geo answer — never spend an API call on them.</summary>
        internal static bool IsGloballyRoutable(string ip)
        {
            if (!IPAddress.TryParse(ip, out var a) || a.AddressFamily != AddressFamily.InterNetwork)
                return true;   // IPv6 etc. — don't gate here
            var b = a.GetAddressBytes();
            if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224) return false;   // this-net, private, loopback, multicast+
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;                 // RFC1918
            if (b[0] == 192 && b[1] == 168) return false;                              // RFC1918
            if (b[0] == 169 && b[1] == 254) return false;                              // link-local
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;                // CGNAT
            return true;
        }

        // -----------------------------------------------------------------------
        // Disk persistence  (simple JSON: { "ip": { "h": "host", "t": "utc" } })
        // -----------------------------------------------------------------------

        private void LoadCacheFromDisk()
        {
            try
            {
                if (!File.Exists(CacheFilePath)) return;

                string json = File.ReadAllText(CacheFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, PersistedEntry>>(json);
                if (dict == null) return;

                foreach (var kv in dict)
                {
                    var entry = new CacheEntry(kv.Value.H, kv.Value.T);
                    // Only load entries that are still within TTL
                    if (!entry.IsStale(DnsCacheTtl))
                        _cache[kv.Key] = entry;
                }
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveCacheToDisk()
        {
            try
            {
                var dict = new Dictionary<string, PersistedEntry>(_cache.Count);
                foreach (var kv in _cache)
                    dict[kv.Key] = new PersistedEntry { H = kv.Value.Hostname, T = kv.Value.ResolvedAt };

                AtomicWriteJson(CacheFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* disk full, permissions, etc. — silently skip */ }
        }

        // ── geo prefix cache persistence (same pattern, its own file) ─────

        private void LoadGeoCacheFromDisk()
        {
            try
            {
                if (!File.Exists(GeoCacheFilePath)) return;
                var dict = JsonSerializer.Deserialize<Dictionary<string, PersistedGeoEntry>>(
                    File.ReadAllText(GeoCacheFilePath));
                if (dict == null) return;
                foreach (var kv in dict)
                {
                    var entry = new GeoCacheEntry(kv.Value.L ?? "", kv.Value.T, kv.Value.N);
                    if (!entry.IsStale(entry.Negative ? GeoNegativeTtl : GeoCacheTtl))
                        _geoCache[kv.Key] = entry;
                }
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveGeoCacheToDisk()
        {
            try
            {
                var dict = new Dictionary<string, PersistedGeoEntry>(_geoCache.Count);
                foreach (var kv in _geoCache)
                    dict[kv.Key] = new PersistedGeoEntry { L = kv.Value.Label, T = kv.Value.ResolvedAt, N = kv.Value.Negative };
                AtomicWriteJson(GeoCacheFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* best effort */ }
        }

        /// <summary>Write-via-temp-then-replace: a crash mid-save can never truncate the cache.</summary>
        private static void AtomicWriteJson(string path, string json)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>True when the hostname value is effectively unresolved (raw IP or empty).</summary>
        public static bool IsUnresolvedHostname(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return true;
            if (hostname == "Unknown" || hostname == "-") return true;
            // Looks like a raw IPv4 address
            return IPAddress.TryParse(hostname, out _);
        }

        public int CacheCount => _cache.Count;

        // -----------------------------------------------------------------------
        // Inner types
        // -----------------------------------------------------------------------

        private sealed class CacheEntry
        {
            public string   Hostname   { get; }
            public DateTime ResolvedAt { get; }

            public CacheEntry(string hostname, DateTime resolvedAt)
            {
                Hostname   = hostname;
                ResolvedAt = resolvedAt;
            }

            public bool IsStale(TimeSpan ttl) => (DateTime.UtcNow - ResolvedAt) > ttl;
        }

        // Used only for JSON serialization (short keys to keep file small)
        private sealed class PersistedEntry
        {
            public string   H { get; set; } // hostname
            public DateTime T { get; set; } // resolved-at (UTC)
        }

        /// <summary>One org·country answer for a whole /24 block (or one IPv6 address).</summary>
        private sealed class GeoCacheEntry
        {
            public string   Label      { get; }   // "Microsoft Corporation · US"
            public DateTime ResolvedAt { get; }
            public bool     Negative   { get; }   // definitive no-data — shorter TTL

            public GeoCacheEntry(string label, DateTime resolvedAt, bool negative)
            {
                Label = label; ResolvedAt = resolvedAt; Negative = negative;
            }

            public bool IsStale(TimeSpan ttl) => (DateTime.UtcNow - ResolvedAt) > ttl;
        }

        private sealed class PersistedGeoEntry
        {
            public string   L { get; set; } // label ("" for negative entries)
            public DateTime T { get; set; } // resolved-at (UTC)
            public bool     N { get; set; } // negative?
        }

        private sealed class ForwardCacheEntry
        {
            public string[] Ips { get; }
            public DateTime ResolvedAt { get; }
            private readonly DateTime _expiresAt;

            public ForwardCacheEntry(string[] ips, DateTime resolvedAt, TimeSpan ttl)
            {
                Ips = ips;
                ResolvedAt = resolvedAt;
                _expiresAt = resolvedAt + ttl;
            }

            public bool IsStale => DateTime.UtcNow >= _expiresAt;
        }

        private sealed class PersistedForwardEntry
        {
            public string[]  I { get; set; } // resolved IPs (empty = negative entry)
            public DateTime  T { get; set; } // resolved-at (UTC)
        }
    }
}
