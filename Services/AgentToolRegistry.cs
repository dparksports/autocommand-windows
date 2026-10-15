using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using SvchostAnalyzer.Core;

namespace AutoCommand.Services
{
    /// <summary>
    /// The MCP tool surface the AI-access bridge exposes. Read-only tools
    /// wrap services the running (elevated) app already trusts — the AI gets
    /// answers, never credentials and never raw UAC prompts. Mutating tools
    /// exist only when the user enabled them for the session, and every call
    /// passes through the bridge's confirmation dialog before executing.
    ///
    /// Handlers run on a background thread; they must not touch UI state.
    /// Anything that needs elevation fails with a plain-language note when
    /// the session happens to run non-elevated — degrade, never guess.
    /// </summary>
    public static class AgentToolRegistry
    {
        public sealed record Tool(
            string Name,
            string Description,
            JsonObject Schema,
            bool Mutating,
            Func<JsonObject, string> Handler,
            Func<JsonObject, string> ConfirmDetail = null);

        private static readonly Regex NetstatRe = new(
            @"^\s*TCP\s+(?<local>\S+)\s+(?<remote>\S+)\s+ESTABLISHED\s+(?<pid>\d+)\s*$",
            RegexOptions.Compiled);

        // ── registry access ───────────────────────────────────────────────

        private static readonly List<Tool> Tools = new()
        {
            new Tool(
                "get_monitor_rows",
                "Live snapshot of every established outbound TCP connection on the machine, " +
                "attributed to its process — svchost instances decoded to their hosted service " +
                "(SCM ground truth + -k/-s command-line tokens) and each destination classified " +
                "(Microsoft/Azure, Akamai, Gcore, unclassified). Same view as AutoCommand's Process Monitor.",
                Schema(),
                false,
                a => ToJson(BuildMonitorSnapshot())),
            new Tool(
                "get_svchost_identity",
                "Identify one svchost.exe instance: the services the SCM says it hosts (ground truth), " +
                "its command-line -k group and -s service claim, and whether the claim corroborates. " +
                "A mismatch is a masquerade signal.",
                Schema(("pid", "Process ID of the svchost.exe instance", true)),
                false,
                a => GetSvchostIdentity(a)),

            new Tool(
                "run_pid_inquiry",
                "Full provenance trace of a PID: verdict, identity, verified spawn chain, hosted services, " +
                "Sysmon corroboration — the same report AutoCommand's PID Inquiry window renders. " +
                "Needs the elevated app for SYSTEM process command lines; degrades gracefully otherwise.",
                Schema(("pid", "Process ID to trace", true)),
                false,
                a => RunPidInquiry(a)),

            new Tool(
                "tail_sysmon",
                "Recent events from the Sysmon/Operational log (process creates, network connections, " +
                "process terminates). Needs the elevated app; returns a clear note when unreadable.",
                Schema(("minutes", "Look this many minutes back (default 30)", false),
                       ("eid", "Filter to one Sysmon event ID (e.g. 1=process create, 3=network)", false),
                       ("contains", "Only events whose description contains this text (case-insensitive)", false),
                       ("max", "Maximum events to return (default 50, cap 500)", false)),
                false,
                a => TailSysmon(a)),

            new Tool(
                "trace_analyze",
                "Run AutoCommand's svchost trace analyzer. With seconds>0: capture live traffic for that " +
                "long, then analyze (parentage, identity corroboration, destinations, beacon regularity, " +
                "upload dominance). With seconds=0: analyze the most recent saved capture file instead.",
                Schema(("seconds", "Live capture length in seconds (default 0 = analyze latest capture file, cap 120)", false),
                       ("path", "Explicit .jsonl capture path to analyze instead", false)),
                false,
                a => TraceAnalyze(a)),

            new Tool(
                "classify_ip",
                "Classify a destination IP against session-verified ranges (Microsoft/Azure, Akamai, Gcore) " +
                "and return the same block-warning text the app shows before blocking.",
                Schema(("ip", "IPv4 address to classify", true)),
                false,
                a => ClassifyIp(a)),

            // ── mutating: only present when the session allows, each call confirmed ──

            new Tool(
                "block_ip",
                "Create outbound TCP+UDP firewall block rules for a remote IP (the same 2-rule scheme " +
                "and provenance notes as the app's Process Monitor). Asks the user in AutoCommand for every call.",
                Schema(("ip", "Remote IP to block", true),
                       ("reason", "Why — stored in the rule's provenance description", false)),
                true,
                a => BlockIp(a).GetAwaiter().GetResult(),
                a => $"Create outbound firewall BLOCK rules for {a["ip"]}?\n\n{IpClassifier.WarningText(a["ip"]?.GetValue<string>())}" +
                     ReasonSuffix(a)),

            new Tool(
                "unblock_ip",
                "Remove firewall block rules previously created for a remote IP. Asks the user for every call.",
                Schema(("ip", "Remote IP to unblock", true)),
                true,
                a =>
                {
                    int n = FirewallService.Instance.RemoveBlockRulesForIpAsync(a["ip"]?.GetValue<string>())
                                   .GetAwaiter().GetResult();
                    return $"Removed {n} rule(s).";
                },
                a => $"Remove firewall block rules for {a["ip"]}?"),

            new Tool(
                "kill_process",
                "Forcefully terminate a process by PID. The confirmation dialog shows the LIVE process " +
                "name at the moment of the ask (PID-reuse guard). Asks the user for every call.",
                Schema(("pid", "Process ID to terminate", true),
                       ("reason", "Why", false)),
                true,
                a => KillProcess(a),
                a => DescribeLiveProcess(a["pid"]?.GetValue<int>() ?? 0) + ReasonSuffix(a)),

            new Tool(
                "disable_task",
                "Disable a scheduled task (reversible: schtasks /Change /TN <path> /ENABLE). " +
                "Asks the user for every call.",
                Schema(("task_path", "Scheduled task path", true),
                       ("reason", "Why", false)),
                true,
                a => DisableTask(a),
                a => $"Disable scheduled task '{a["task_path"]}'?" + ReasonSuffix(a)),
        };

        public static string[] Names(bool includeMutating) =>
            Tools.Where(t => includeMutating || !t.Mutating).Select(t => t.Name).ToArray();

        public static Tool Find(string name, bool includeMutating) =>
            Tools.FirstOrDefault(t => t.Name == name && (includeMutating || !t.Mutating));

        public static JsonArray Descriptors(bool includeMutating)
        {
            var arr = new JsonArray();
            foreach (var t in Tools.Where(t => includeMutating || !t.Mutating))
                arr.Add(new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["inputSchema"] = t.Schema.DeepClone(),
                });
            return arr;
        }

        // ── read tool handlers ────────────────────────────────────────────

        public static JsonObject BuildMonitorSnapshot()
        {
            var procs = new Dictionary<int, string>();
            try
            {
                using var s = new ManagementObjectSearcher("SELECT ProcessId, Name FROM Win32_Process");
                foreach (ManagementObject o in s.Get())
                    procs[Convert.ToInt32(o["ProcessId"])] = o["Name"]?.ToString() ?? "?";
            }
            catch { /* WMI failed — rows will carry PID only */ }

            var rows = new JsonArray();
            foreach (var line in NetstatLines())
            {
                var m = NetstatRe.Match(line);
                if (!m.Success) continue;
                int pid = int.Parse(m.Groups["pid"].Value);
                if (!procs.TryGetValue(pid, out string name)) name = "?";
                var row = new JsonObject
                {
                    ["local"] = m.Groups["local"].Value,
                    ["remote"] = m.Groups["remote"].Value,
                    ["pid"] = pid,
                    ["process"] = name,
                    ["classification"] = Views.SvchostTraceView.Classify(RemoteIpOf(m.Groups["remote"].Value)),
                };
                if (name.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var id = SvchostIdentityService.Resolve(pid);
                    row["svchost"] = new JsonObject
                    {
                        ["services"] = id.ServicesCsv,
                        ["k"] = id.Group,
                        ["s"] = id.DeclaredService,
                        ["cmdline_visible"] = id.CommandLineVisible,
                        ["mismatch"] = id.Mismatch,
                    };
                }
                rows.Add(row);
            }
            return new JsonObject { ["type"] = "monitor_snapshot", ["rows"] = rows, ["row_count"] = rows.Count };
        }


        private static string GetSvchostIdentity(JsonObject a)
        {
            int pid = a["pid"]?.GetValue<int>() ?? 0;
            string name;
            try
            {
                using var proc = Process.GetProcessById(pid);
                name = proc.ProcessName;
            }
            catch { return $"PID {pid} is not running."; }
            if (!name.Equals("svchost", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase))
                return $"PID {pid} is '{name}', not svchost.exe.";

            var id = SvchostIdentityService.Resolve(pid);
            return ToJson(new JsonObject
            {
                ["pid"] = pid,
                ["scm_services"] = id.ServicesCsv.Length > 0 ? id.ServicesCsv : "(none — strongest masquerade red flag)",
                ["services_detail"] = id.ServicesDetail,
                ["k_group"] = id.Group,
                ["s_declared"] = id.DeclaredService,
                ["command_line_visible"] = id.CommandLineVisible,
                ["mismatch"] = id.Mismatch,
                ["readable_line_without_k"] = id.NoK,
                ["verdict"] = id.Mismatch || id.NoK
                    ? "SUSPICIOUS — self-declared identity contradicts the SCM"
                    : id.CommandLineVisible ? "corroborated" : "no command line readable — rerun the app elevated to corroborate",
            });
        }

        private static string RunPidInquiry(JsonObject a)
        {
            int pid = a["pid"]?.GetValue<int>() ?? 0;
            var report = ProcessProvenanceService.Collect(pid);
            return Views.ProcessTraceWindow.BuildReportText(report);
        }

        private static string TailSysmon(JsonObject a)
        {
            int minutes = a["minutes"]?.GetValue<int>() ?? 30;
            minutes = Math.Clamp(minutes, 1, 24 * 60);
            int? eid = a["eid"] is JsonNode e ? e.GetValue<int>() : null;
            string contains = a["contains"]?.GetValue<string>();
            int max = Math.Clamp(a["max"]?.GetValue<int>() ?? 50, 1, 500);

            try
            {
                var since = DateTime.UtcNow.AddMinutes(-minutes);
                var query = new EventLogQuery("Microsoft-Windows-Sysmon/Operational",
                    PathType.LogName, "*[System[TimeCreated[@SystemTime >= '" + since.ToString("yyyy-MM-ddTHH:mm:ss.000Z") + "']]]");
                using var reader = new EventLogReader(query);
                var sb = new StringBuilder();
                int shown = 0, scanned = 0;
                for (var ev = reader.ReadEvent(); ev != null && shown < max; ev = reader.ReadEvent())
                using (ev)
                {
                    scanned++;
                    if (eid.HasValue && ev.Id != eid.Value) continue;
                    string desc;
                    try { desc = ev.FormatDescription() ?? ""; } catch { desc = ""; }
                    if (!string.IsNullOrEmpty(contains) &&
                        !desc.Contains(contains, StringComparison.OrdinalIgnoreCase)) continue;
                    int pid = -1;
                    try { pid = Convert.ToInt32(ev.Properties[2]?.Value); } catch { }
                    string stamp = ev.TimeCreated?.ToLocalTime().ToString("HH:mm:ss") ?? "??";
                    string line = stamp + $"  EID {ev.Id}  PID {pid}  " +
                                  desc.Replace("\r\n", " | ").Replace("\n", " | ").TrimEnd();
                    if (line.Length > 400) line = line[..400] + "…";
                    sb.AppendLine(line);
                    shown++;
                }
                if (shown == 0)
                    return $"No matching Sysmon events in the last {minutes} min (scanned {scanned}). " +
                           "Zero scanned usually means the log is empty or Sysmon config dropped this event class.";
                return sb.ToString();
            }
            catch (UnauthorizedAccessException)
            {
                return "The Sysmon log is not readable from this session — the running AutoCommand instance " +
                       "is not elevated. Restart AutoCommand as Administrator and start a new AI session.";
            }
            catch (Exception ex)
            {
                return "Sysmon log read failed: " + ex.Message;
            }
        }

        private static string TraceAnalyze(JsonObject a)
        {
            int seconds = a["seconds"]?.GetValue<int>() ?? 0;
            string path = a["path"]?.GetValue<string>();

            try
            {
                if (seconds <= 0)
                {
                    string file = path;
                    if (string.IsNullOrEmpty(file))
                    {
                        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                               "AutoCommand", "captures");
                        file = Directory.Exists(dir)
                            ? Directory.GetFiles(dir, "*.jsonl").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
                            : null;
                    }
                    if (file == null || !File.Exists(file))
                        return "No capture file to analyze (none passed, none in the captures folder). " +
                               "Call again with seconds>0 to capture live.";
                    var result = TraceAnalyzer.Analyze(new[] { file });
                    return $"analyzed: {Path.GetFileName(file)}\n\n{result.Report}";
                }

                seconds = Math.Min(seconds, 120);
                string temp = Path.Combine(Path.GetTempPath(), $"agent-trace-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
                using (var engine = new CaptureEngine(new CaptureOptions
                {
                    OutFile = temp,
                    PollMs = 1000,
                    Duration = TimeSpan.FromSeconds(seconds),
                }))
                {
                    var done = new ManualResetEventSlim(false);
                    engine.Completed += _ => done.Set();
                    engine.Start();
                    done.Wait(TimeSpan.FromSeconds(seconds + 10));
                }
                var analysis = TraceAnalyzer.Analyze(new[] { temp });
                try { File.Delete(temp); } catch { }
                return $"live capture {seconds}s (exit {analysis.Flags} flag(s)):\n\n{analysis.Report}";
            }
            catch (InvalidOperationException ex)
            {
                return "Capture could not start (another capture is likely running in the app): " + ex.Message;
            }
            catch (Exception ex)
            {
                return "trace_analyze failed: " + ex.Message;
            }
        }

        private static string ClassifyIp(JsonObject a)
        {
            string ip = a["ip"]?.GetValue<string>() ?? "";
            return ToJson(new JsonObject
            {
                ["ip"] = ip,
                ["classification"] = IpClassifier.Classify(ip),
                ["microsoft_infra"] = IpClassifier.IsMicrosoftInfra(ip),
                ["block_warning"] = IpClassifier.WarningText(ip),
            });
        }

        // ── mutating handlers (already user-confirmed when reached) ───────

        private static async Task<string> BlockIp(JsonObject a)
        {
            string ip = a["ip"]?.GetValue<string>();
            if (!System.Net.IPAddress.TryParse(ip, out _)) return $"'{ip}' is not a valid IP address.";
            string reason = a["reason"]?.GetValue<string>();
            string note = $"[agent request, user-confirmed {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC]" +
                          (string.IsNullOrEmpty(reason) ? "" : $" {reason}") + " · AutoCommand AI access";
            var (created, updated) = await FirewallService.Instance.AddIpBlockAsync(ip, note);
            return created > 0
                ? $"Blocked {ip} — {created} outbound TCP/UDP rule(s) created, provenance written to the rule description."
                : $"Block for {ip} already existed — refreshed its provenance ({updated} rule(s)).";
        }

        private static string KillProcess(JsonObject a)
        {
            int pid = a["pid"]?.GetValue<int>() ?? 0;
            if (pid <= 4) return "Refusing to kill PID 0–4 (system).";
            try
            {
                var proc = Process.GetProcessById(pid);
                string name = proc.ProcessName;
                proc.Kill();
                return $"Terminated PID {pid} ('{name}').";
            }
            catch (ArgumentException) { return $"PID {pid} already exited."; }
            catch (Exception ex) { return $"Kill failed: {ex.Message}"; }
        }

        private static string DisableTask(JsonObject a)
        {
            string path = a["task_path"]?.GetValue<string>();
            try
            {
                TaskSchedulerService.Instance.SetTaskEnabledAsync(path, false).GetAwaiter().GetResult();
                return $"Disabled scheduled task '{path}'. Re-enable with: schtasks /Change /TN \"{path}\" /ENABLE";
            }
            catch (Exception ex) { return $"Disable failed: {ex.Message}"; }
        }

        // ── schema / description helpers ──────────────────────────────────

        private static JsonObject Schema(params (string Name, string Desc, bool Required)[] props)
        {
            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (var (name, desc, req) in props)
            {
                bool isInt = name is "pid" or "minutes" or "eid" or "max" or "seconds";
                properties[name] = new JsonObject
                {
                    ["type"] = isInt ? "integer" : "string",
                    ["description"] = desc,
                };
                if (req) required.Add(name);
            }
            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
            };
        }

        private static string ReasonSuffix(JsonObject a)
        {
            string reason = a["reason"]?.GetValue<string>();
            return string.IsNullOrEmpty(reason) ? "" : $"\n\nStated reason: {reason}";
        }

        private static string DescribeLiveProcess(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                return $"Terminate PID {pid} — LIVE process '{proc.ProcessName}'" +
                       $" (started {proc.StartTime:yyyy-MM-dd HH:mm:ss})?";
            }
            catch { return $"Terminate PID {pid}? (process not found — it may have exited; the call will be a no-op)"; }
        }

        private static IEnumerable<string> NetstatLines()
        {
            var psi = new ProcessStartInfo("netstat", "-ano -p tcp")
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
            using var p = Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return output.Split('\n');
        }

        private static string RemoteIpOf(string remote) =>
            remote.LastIndexOf(':') is int c && c > 0 ? remote[..c].Trim('[', ']') : remote;

        private static string ToJson(JsonObject o) => o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
