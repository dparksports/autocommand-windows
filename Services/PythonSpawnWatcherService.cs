using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AutoCommand.Models;

namespace AutoCommand.Services
{
    /// <summary>
    /// Real-time tracing of python process creations system-wide from the
    /// Sysmon operational channel: EID 1 (create, with full parentage), EID 5
    /// (terminate, flips Exited) and EID 3 (network connect, used to attribute
    /// captured TCP flows to the spawning pid). EID 1 fires for every process
    /// creation on the machine, so records are pre-filtered by a cheap
    /// substring scan and discarded early unless the image path is python-ish.
    /// </summary>
    public class PythonSpawnWatcherService : IDisposable
    {
        private EventLogWatcher _spawnWatcher;
        private EventLogWatcher _exitWatcher;
        private EventLogWatcher _netWatcher;
        private bool _disposed;

        // Tracked spawns by pid — drives venv pairing and EID 5 lookup. A pid
        // reuse replaces the entry (old rows keep their own copy of the data).
        private readonly Dictionary<int, PythonSpawnItem> _tracked = new();
        private readonly object _trackedLock = new();

        // EID 5 can beat its own EID 1 through the independent watchers;
        // exits with no spawn yet wait here and stamp the item on arrival.
        private readonly Dictionary<int, DateTime> _earlyExits = new();

        // Memory guard for the pid maps on long sessions
        private const int TrackedCap = 20000;

        public event Action<string> OnError;
        public event Action<PythonSpawnItem> OnSpawn;
        public event Action<PythonSpawnItem> OnExited;
        public event Action<PythonNetworkEvent> OnNetworkConnect;

        public int TrackedCount { get { lock (_trackedLock) return _tracked.Count; } }

        /// <summary>
        /// Distinct python image paths seen spawning — the passive discovery
        /// feed for the instrumentation dialog (every python that actually
        /// ran on this machine, wherever it lives).
        /// </summary>
        public List<string> GetSeenImages()
        {
            lock (_trackedLock)
                return _tracked.Values
                    .Select(i => i.Image)
                    .Where(img => !string.IsNullOrEmpty(img))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        public void Start()
        {
            try
            {
                _spawnWatcher = Watch("*[System[(EventID=1)]]", Spawn_EventRecordWritten);
                _exitWatcher = Watch("*[System[(EventID=5)]]", Exit_EventRecordWritten);
                _netWatcher = Watch("*[System[(EventID=3)]]", Net_EventRecordWritten);
            }
            catch (EventLogNotFoundException)
            {
                OnError?.Invoke("Sysmon is not installed. Install it from the Sysmon Audit tab — spawn tracing needs it.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to start python spawn watcher: {ex.Message}");
            }
        }

        public void Stop()
        {
            _spawnWatcher?.Dispose();
            _spawnWatcher = null;
            _exitWatcher?.Dispose();
            _exitWatcher = null;
            _netWatcher?.Dispose();
            _netWatcher = null;
        }

        private static EventLogWatcher Watch(string xpath, EventHandler<EventRecordWrittenEventArgs> handler)
        {
            var watcher = new EventLogWatcher(
                new EventLogQuery("Microsoft-Windows-Sysmon/Operational", PathType.LogName, xpath));
            watcher.EventRecordWritten += handler;
            watcher.Enabled = true;
            return watcher;
        }

        // ── EID 1: process create ────────────────────────────────────────────

        private void Spawn_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                var fields = ParseIfPython(e);
                if (fields == null) return;

                int pid = ParsePid(GetValue(fields, "ProcessId"));
                if (pid <= 0) return;

                var item = new PythonSpawnItem
                {
                    TimeUtc = ParseUtcTime(GetValue(fields, "UtcTime"), e.EventRecord?.TimeCreated),
                    Pid = pid,
                    Image = GetValue(fields, "Image"),
                    CommandLine = GetValue(fields, "CommandLine"),
                    ParentPid = ParsePid(GetValue(fields, "ParentProcessId")),
                    ParentImage = GetValue(fields, "ParentImage"),
                    ParentCommandLine = GetValue(fields, "ParentCommandLine"),
                    User = GetValue(fields, "User"),
                    IntegrityLevel = GetValue(fields, "IntegrityLevel"),
                    Hashes = GetValue(fields, "Hashes"),
                    Source = "sysmon"
                };

                lock (_trackedLock)
                {
                    PairWithLauncher(item);

                    // Terminate event already observed for this pid — the
                    // process was shorter than the watcher round-trip.
                    if (_earlyExits.Remove(pid)) item.Exited = true;

                    PruneIfNeeded();
                    _tracked[pid] = item;
                }
                OnSpawn?.Invoke(item);
            }
            catch { /* a malformed record never takes the watcher down */ }
        }

        /// <summary>
        /// Venv pairing: a venv command spawns two processes — the launcher
        /// (venv\Scripts\python.exe) and the real base interpreter as its
        /// child. When the new spawn's parent is a tracked, still-running
        /// python spawn with a different image in a \Scripts\ folder, the
        /// child is the real interpreter: it carries the pair and the launcher
        /// row is absorbed into the detail pane. Correlation is purely
        /// ParentProcessId-based — nothing inside the venv is ever read.
        /// </summary>
        private void PairWithLauncher(PythonSpawnItem item)
        {
            if (item.ParentPid <= 0) return;
            if (!_tracked.TryGetValue(item.ParentPid, out var parent)) return;
            if (parent.Exited || parent.Pid == item.Pid) return;

            if (IsVenvLauncherImage(parent.Image) &&
                !string.Equals(parent.Image, item.Image, StringComparison.OrdinalIgnoreCase))
            {
                item.SetVenvLauncher(parent);
                parent.MarkAbsorbedLauncher();
            }
            else
            {
                // python spawning python without a venv layout (multiprocessing,
                // subprocess.run([...python...])) — keep both rows, badge the child.
                item.SetPythonParent();
            }
        }

        private static bool IsVenvLauncherImage(string image) =>
            !string.IsNullOrEmpty(image) &&
            image.IndexOf("\\Scripts\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
            IsPythonImage(image);

        /// <summary>Client-side filter: keep only events whose image path is python-ish.</summary>
        private static bool IsPythonImage(string image) =>
            !string.IsNullOrEmpty(image) &&
            image.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0;

        private void PruneIfNeeded()
        {
            if (_tracked.Count < TrackedCap) return;
            // Drop the oldest tenth by spawn time; view rows keep their own data.
            foreach (int pid in _tracked
                .OrderBy(kv => kv.Value.TimeUtc)
                .Take(Math.Max(1, _tracked.Count / 10))
                .Select(kv => kv.Key)
                .ToList())
            {
                _tracked.Remove(pid);
            }
        }

        // ── EID 5: process terminate ─────────────────────────────────────────

        private void Exit_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                var fields = ParseIfPython(e);
                if (fields == null) return;

                int pid = ParsePid(GetValue(fields, "ProcessId"));
                if (pid <= 0) return;

                PythonSpawnItem item;
                lock (_trackedLock)
                {
                    if (!_tracked.TryGetValue(pid, out item))
                    {
                        // EID 1 not processed yet — hold the exit for the spawn handler.
                        _earlyExits[pid] = DateTime.UtcNow;
                        if (_earlyExits.Count > 1024)
                            foreach (var stale in _earlyExits.Where(kv =>
                                     (DateTime.UtcNow - kv.Value).TotalMinutes > 10).ToList())
                                _earlyExits.Remove(stale.Key);
                        return;
                    }
                    item.Exited = true;
                }
                OnExited?.Invoke(item);
            }
            catch { }
        }

        // ── EID 3: network connect (python images only) ─────────────────────

        private void Net_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                var fields = ParseIfPython(e);
                if (fields == null) return;

                string proto = GetValue(fields, "Protocol");
                if (!string.Equals(proto, "tcp", StringComparison.OrdinalIgnoreCase)) return;

                int pid = ParsePid(GetValue(fields, "ProcessId"));
                if (pid <= 0) return;

                OnNetworkConnect?.Invoke(new PythonNetworkEvent
                {
                    TimeUtc = ParseUtcTime(GetValue(fields, "UtcTime"), e.EventRecord?.TimeCreated),
                    Pid = pid,
                    Image = GetValue(fields, "Image"),
                    SourceIp = GetValue(fields, "SourceIp"),
                    SourcePort = ParsePort(GetValue(fields, "SourcePort")),
                    DestIp = GetValue(fields, "DestinationIp"),
                    DestPort = ParsePort(GetValue(fields, "DestinationPort"))
                });
            }
            catch { }
        }

        // ── EventData helpers ────────────────────────────────────────────────
        //
        // EventData is parsed BY NAME, never by property index — the field
        // order varies across Sysmon versions. EID 1 fires for every process
        // creation on the machine, so the XML gets a cheap substring scan
        // before anything is parsed into a DOM (a cmdline/parent mentioning
        // python only costs one wasted parse; a real python image never
        // escapes the scan).

        private static Dictionary<string, string> ParseIfPython(EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null) return null;
            string xml = e.EventRecord.ToXml();
            if (xml.IndexOf("python", StringComparison.OrdinalIgnoreCase) < 0) return null;

            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
            var eventData = doc.Root?.Element(ns + "EventData");
            if (eventData == null) return null;

            var fields = new Dictionary<string, string>(16, StringComparer.Ordinal);
            foreach (var data in eventData.Elements(ns + "Data"))
            {
                string name = data.Attribute("Name")?.Value;
                if (name != null && !fields.ContainsKey(name)) fields[name] = data.Value;
            }

            return IsPythonImage(GetValue(fields, "Image")) ? fields : null;
        }

        private static string GetValue(Dictionary<string, string> fields, string name) =>
            fields.TryGetValue(name, out string value) ? value : string.Empty;

        /// <summary>Sysmon PIDs are decimal in EID 1/5 but hex (0x…) in some EID 3 versions.</summary>
        private static int ParsePid(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dec)) return dec;
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                return hex;
            return 0;
        }

        private static int ParsePort(string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) ? port : 0;

        /// <summary>Sysmon UtcTime is "yyyy-MM-dd HH:mm:ss.fff" UTC; fall back to the record stamp.</summary>
        private static DateTime ParseUtcTime(string value, DateTime? fallback)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                return parsed;
            return fallback?.ToUniversalTime() ?? DateTime.UtcNow;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }

    /// <summary>A python TCP connect as seen by Sysmon EID 3 — the pid↔flow attribution source.</summary>
    public class PythonNetworkEvent
    {
        public DateTime TimeUtc { get; set; }
        public int Pid { get; set; }
        public string Image { get; set; }
        public string SourceIp { get; set; }
        public int SourcePort { get; set; }
        public string DestIp { get; set; }
        public int DestPort { get; set; }

        /// <summary>Canonical endpoint pair, orientation as the initiator saw it.</summary>
        public string Endpoints => $"{SourceIp}:{SourcePort} → {DestIp}:{DestPort}";
    }
}
