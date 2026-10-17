using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace AutoCommand.Services
{
    /// <summary>
    /// App-managed archive of the Sysmon Operational channel. The channel
    /// itself is a rolling window (192 MB ≈ half a busy day); this archive
    /// is what makes "every spawn retroactively traceable" true for weeks:
    ///
    ///   - at every app start, the entire retained channel is backfilled
    ///     (same pattern as the BITS tracer), skipping events already
    ///     archived via a (recordId, utc) watermark
    ///   - while the app runs, every new Sysmon event is appended live
    ///   - storage is one readable, greppable JSONL file per day under
    ///     %LOCALAPPDATA%\AutoCommand\sysmon-archive, pruned after
    ///     DefaultRetainDays (disclosed here and on the Sysmon Audit card)
    ///
    /// Every EventData field is preserved verbatim, so an archived EID 1 is
    /// a complete substitute for the live one (command line, parent image,
    /// hashes…). Unelevated sessions degrade silently: no channel access,
    /// no archive writes — nothing crashes, nothing is guessed.
    /// </summary>
    public static class SysmonArchiveService
    {
        public const int DefaultRetainDays = 30;

        /// <summary>Archive root. Settable so the offline self-test can
        /// redirect persistence to a temp dir (house pattern).</summary>
        public static string ArchiveDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCommand", "sysmon-archive");

        public sealed class ArchiveEvent
        {
            public DateTime TsUtc { get; init; }
            public int Eid { get; init; }
            public long RecordId { get; init; }
            public Dictionary<string, string> Data { get; init; }
        }

        private sealed class ArchiveState
        {
            public long LastRecordId { get; set; }
            public DateTime? LastUtc { get; set; }
            public long TotalEvents { get; set; }
        }

        private static readonly object _gate = new();
        private static EventLogWatcher _watcher;

        private static string StatePath => Path.Combine(ArchiveDirectory, "state.json");

        public static string DayFilePath(DateTime utc) =>
            Path.Combine(ArchiveDirectory, "sysmon-archive-" + utc.ToString("yyyy-MM-dd") + ".jsonl");

        // ── watermark ─────────────────────────────────────────────────────

        private static ArchiveState LoadState()
        {
            try
            {
                if (File.Exists(StatePath))
                    return JsonSerializer.Deserialize<ArchiveState>(File.ReadAllText(StatePath))
                           ?? new ArchiveState();
            }
            catch { /* corrupt state — an empty watermark just means the next
                      backfill re-reads the live channel; dedupe degrades to
                      "archive may hold a few boundary duplicates", never loss */ }
            return new ArchiveState();
        }

        private static void SaveState(ArchiveState st)
        {
            try
            {
                Directory.CreateDirectory(ArchiveDirectory);
                File.WriteAllText(StatePath, JsonSerializer.Serialize(st,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* disk full / permissions — watermark stalls at last
                      good save; worst case is duplicate rows, never loss */ }
        }

        /// <summary>An event is "past the watermark" when it is newer than
        /// everything archived so far. RecordId alone is not enough: it
        /// resets when the log is cleared or Sysmon reinstalled, so time is
        /// the primary key and the id breaks exact ties.</summary>
        private static bool IsPast(ArchiveEvent e, ArchiveState st) =>
            st.LastUtc == null ||
            e.TsUtc > st.LastUtc.Value ||
            (e.TsUtc == st.LastUtc.Value && e.RecordId > st.LastRecordId);

        // ── append ────────────────────────────────────────────────────────

        /// <summary>Appends one event unless the watermark already covers
        /// it. Returns true when a line was written.</summary>
        public static bool Append(ArchiveEvent e)
        {
            if (e == null) return false;
            lock (_gate)
            {
                var st = LoadState();
                if (!IsPast(e, st)) return false;

                try
                {
                    Directory.CreateDirectory(ArchiveDirectory);
                    var line = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["ts"] = e.TsUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        ["eid"] = e.Eid,
                        ["rid"] = e.RecordId,
                        ["data"] = e.Data ?? new Dictionary<string, string>(),
                    });
                    File.AppendAllText(DayFilePath(e.TsUtc), line + Environment.NewLine);

                    st.LastRecordId = e.RecordId;
                    st.LastUtc = e.TsUtc;
                    st.TotalEvents++;
                    SaveState(st);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SysmonArchive append failed: " + ex.Message);
                    return false;
                }
            }
        }

        // ── live channel → archive events ────────────────────────────────

        private static ArchiveEvent FromRecord(EventRecord rec)
        {
            var data = new Dictionary<string, string>();
            try
            {
                // Named fields come from the event XML (the same parse the
                // Sysmon watcher uses) — Properties alone is position-only.
                var doc = XDocument.Parse(rec.ToXml());
                XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
                foreach (var d in doc.Root?.Element(ns + "EventData")?.Elements(ns + "Data") ?? Enumerable.Empty<XElement>())
                {
                    string name = d.Attribute("Name")?.Value;
                    if (name != null) data[name] = d.Value;
                }
            }
            catch { /* unparseable payload — archive what we have */ }

            return new ArchiveEvent
            {
                TsUtc = rec.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                Eid = rec.Id,
                RecordId = rec.RecordId ?? 0,
                Data = data,
            };
        }

        /// <summary>
        /// Backfills everything the live channel still holds, oldest→newest,
        /// skipping events the watermark already covers. Cheap precheck: if
        /// the channel's newest record is not past the watermark, returns 0
        /// without scanning. Returns the number of events appended. An
        /// unelevated session gets an unauthorized read and returns 0.
        /// </summary>
        public static int Backfill()
        {
            try
            {
                // Precheck: newest live record vs watermark
                var newestQuery = new EventLogQuery("Microsoft-Windows-Sysmon/Operational",
                    PathType.LogName) { ReverseDirection = true };
                using (var newestReader = new EventLogReader(newestQuery))
                {
                    var newest = newestReader.ReadEvent();
                    if (newest == null) return 0;
                    using (newest)
                    {
                        lock (_gate)
                        {
                            var st0 = LoadState();
                            var probe = FromRecord(newest);
                            if (!IsPast(probe, st0)) return 0;
                        }
                    }
                }

                int appended = 0;
                var query = new EventLogQuery("Microsoft-Windows-Sysmon/Operational", PathType.LogName);
                using var reader = new EventLogReader(query);
                for (var rec = reader.ReadEvent(); rec != null; rec = reader.ReadEvent())
                    using (rec)
                    {
                        if (Append(FromRecord(rec))) appended++;
                    }
                return appended;
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("SysmonArchive backfill: channel not readable (session not elevated).");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SysmonArchive backfill failed: " + ex.Message);
                return 0;
            }
        }

        /// <summary>Appends every Sysmon event as it arrives. Idempotent;
        /// no-op when the channel is missing or unreadable.</summary>
        public static void StartLiveWatch()
        {
            lock (_gate)
            {
                if (_watcher != null) return;
                try
                {
                    _watcher = new EventLogWatcher(new EventLogQuery(
                        "Microsoft-Windows-Sysmon/Operational", PathType.LogName));
                    _watcher.EventRecordWritten += (_, e) =>
                    {
                        if (e.EventRecord == null) return;
                        try { Append(FromRecord(e.EventRecord)); }
                        catch { /* one bad event must not unwind the watcher */ }
                    };
                    _watcher.Enabled = true;
                }
                catch (Exception ex)
                {
                    _watcher = null;
                    Debug.WriteLine("SysmonArchive live watch unavailable: " + ex.Message);
                }
            }
        }

        public static void StopLiveWatch()
        {
            lock (_gate)
            {
                _watcher?.Dispose();
                _watcher = null;
            }
        }

        // ── retention ─────────────────────────────────────────────────────

        /// <summary>Deletes day files older than retainDays and drops their
        /// counts from the state. Returns files removed.</summary>
        public static int Prune(int retainDays = DefaultRetainDays)
        {
            try
            {
                if (!Directory.Exists(ArchiveDirectory)) return 0;
                var cutoff = DateTime.UtcNow.Date.AddDays(-retainDays);
                var st = LoadState();
                int removed = 0;
                foreach (var file in Directory.GetFiles(ArchiveDirectory, "sysmon-archive-*.jsonl"))
                {
                    string stem = Path.GetFileName(file)
                        .Substring("sysmon-archive-".Length)
                        .Replace(".jsonl", "");
                    if (!DateTime.TryParseExact(stem, "yyyy-MM-dd", null,
                            System.Globalization.DateTimeStyles.None, out var day) ||
                        day >= cutoff)
                        continue;

                    try
                    {
                        int lines = File.ReadLines(file).Count();
                        File.Delete(file);
                        removed++;
                        st.TotalEvents = Math.Max(0, st.TotalEvents - lines);
                    }
                    catch { /* in use / locked — try again next start */ }
                }
                if (removed > 0) SaveState(st);
                return removed;
            }
            catch
            {
                return 0;
            }
        }

        // ── queries (live channel first is the CALLER's job; these read the
        //    archive only — the overlap case is handled by preferring the
        //    live log and falling back here) ──────────────────────────────

        /// <summary>Newest-first search over archived events. `contains`
        /// matches any data value (case-insensitive).</summary>
        public static List<ArchiveEvent> Search(int? eid, string contains, DateTime? sinceUtc, int max) =>
            SearchCore(eid, contains, sinceUtc, max, null);

        /// <summary>Newest-first EID 1 create-events for a PID — the
        /// archive-backed twin of the PID Inquiry's live lookup. Streamed:
        /// the pid predicate runs per line, not over a materialized list.</summary>
        public static List<ArchiveEvent> SearchCreateEvents(int pid, int max) =>
            SearchCore(1, null, null, max,
                d => d.TryGetValue("ProcessId", out var v) && v == pid.ToString());

        private static List<ArchiveEvent> SearchCore(int? eid, string contains, DateTime? sinceUtc, int max,
            Func<Dictionary<string, string>, bool> dataFilter)
        {
            var hits = new List<ArchiveEvent>();
            try
            {
                if (!Directory.Exists(ArchiveDirectory)) return hits;
                var files = Directory.GetFiles(ArchiveDirectory, "sysmon-archive-*.jsonl")
                    .OrderByDescending(f => f, StringComparer.Ordinal)
                    .ToList();
                foreach (var file in files)
                {
                    if (hits.Count >= max) break;
                    // whole-day skip when the file predates the window
                    string stem = Path.GetFileName(file)
                        .Substring("sysmon-archive-".Length)
                        .Replace(".jsonl", "");
                    if (sinceUtc.HasValue &&
                        DateTime.TryParseExact(stem, "yyyy-MM-dd", null,
                            System.Globalization.DateTimeStyles.None, out var day) &&
                        day < sinceUtc.Value.Date)
                        continue;

                    // lines are appended oldest→newest; newest-first result
                    // means walking each day file bottom-up
                    var lines = File.ReadAllLines(file);
                    for (int li = lines.Length - 1; li >= 0 && hits.Count < max; li--)
                    {
                        string line = lines[li];
                        if (line.Length == 0) continue;
                        ArchiveEvent e;
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            var data = new Dictionary<string, string>();
                            foreach (var p in root.GetProperty("data").EnumerateObject())
                                data[p.Name] = p.Value.GetString() ?? "";
                            e = new ArchiveEvent
                            {
                                TsUtc = DateTime.Parse(root.GetProperty("ts").GetString(),
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime(),
                                Eid = root.GetProperty("eid").GetInt32(),
                                RecordId = root.GetProperty("rid").GetInt64(),
                                Data = data,
                            };
                        }
                        catch { /* truncated/corrupt line — skip it, keep the file */ continue; }

                        if (eid.HasValue && e.Eid != eid.Value) continue;
                        if (sinceUtc.HasValue && e.TsUtc < sinceUtc.Value) continue;
                        if (!string.IsNullOrEmpty(contains) &&
                            !e.Data.Values.Any(v => v != null &&
                                v.Contains(contains, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        if (dataFilter != null && !dataFilter(e.Data)) continue;
                        hits.Add(e);
                    }
                }
            }
            catch { /* unreadable archive — empty result, never a throw */ }
            return hits;
        }

        /// <summary>(day files, total events) for the Sysmon Audit card.</summary>
        public static (int Days, long Events) StatusSummary()
        {
            try
            {
                if (!Directory.Exists(ArchiveDirectory)) return (0, 0);
                int days = Directory.GetFiles(ArchiveDirectory, "sysmon-archive-*.jsonl").Length;
                return (days, LoadState().TotalEvents);
            }
            catch { return (0, 0); }
        }
    }
}
