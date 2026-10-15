using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    public partial class BeMyGuestView : UserControl
    {
        public BeMyGuestView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // AI access: mutating preference + live session status
            try
            {
                string prefPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutoCommand", "agent", "mutating_pref.txt");
                AgentMutatingCheck.IsChecked = File.Exists(prefPath) && File.ReadAllText(prefPath).Trim() == "on";
            }
            catch { AgentMutatingCheck.IsChecked = false; }
            StartAgentRefresh();
        }

        // -----------------------------------------------------------------------
        // AI access (agent bridge) — see Services/AgentBridgeService.cs
        // -----------------------------------------------------------------------

        private readonly System.Windows.Threading.DispatcherTimer _agentRefresh =
            new() { Interval = TimeSpan.FromSeconds(2) };

        private static string MutatingPrefPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCommand", "agent", "mutating_pref.txt");

        private void UserControl_Unloaded(object sender, RoutedEventArgs e) => _agentRefresh.Stop();

        private void StartAgentRefresh()
        {
            RefreshAgentUi();
            _agentRefresh.Tick -= (_, _) => RefreshAgentUi();
            _agentRefresh.Tick += (_, _) => RefreshAgentUi();
            _agentRefresh.Start();
        }

        private void RefreshAgentUi()
        {
            var bridge = AgentBridgeService.Instance;
            bool running = bridge.IsRunning;
            StartAgentBtn.IsEnabled = !running;
            StopAgentBtn.IsEnabled = running;
            AgentStatusText.Text = running
                ? $"Session ACTIVE · 127.0.0.1:{bridge.Port} · client: {bridge.ClientName} · {bridge.ConnectedCalls} call(s)"
                : "Session off. The assistant can connect but every tool answers 'start the session'.";

            AgentActivityList.ItemsSource = AgentBridgeService.RecentActivity(30)
                .Select(l => l.Length > 160 ? l[..160] + "…" : l)
                .ToList();
        }

        private void StartAgentBtn_Click(object sender, RoutedEventArgs e)
        {
            // Tier-1 consent: this is the moment the user decides what an AI
            // assistant may read — state it plainly, in one dialog.
            var answer = MessageBox.Show(
                "Start an AI access session?\n\n" +
                "An AI assistant (e.g. ZCode) will be able to READ this machine's security telemetry:\n" +
                "live connections and their processes, svchost identities, PID provenance reports,\n" +
                "Sysmon events and trace analysis. What it reads can be repeated by the assistant.\n\n" +
                "• Read-only tools answer immediately while the session is on.\n" +
                "• Mutating actions (block/kill/disable) ask you again on every single call\n" +
                "  (and only exist at all if you tick them on below).\n" +
                "• The session ends when you stop it or close AutoCommand.\n\n" +
                "Start the session?",
                "Be My Guest — consent", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            var (ok, message) = AgentBridgeService.Instance.Start(AgentMutatingCheck.IsChecked == true);
            if (!ok) MessageBox.Show(message, "Be My Guest", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshAgentUi();
        }

        private void StopAgentBtn_Click(object sender, RoutedEventArgs e)
        {
            AgentBridgeService.Instance.Stop();
            RefreshAgentUi();
        }

        private void AgentMutatingCheck_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MutatingPrefPath));
                File.WriteAllText(MutatingPrefPath, AgentMutatingCheck.IsChecked == true ? "on" : "off");
            }
            catch { /* persistence best-effort */ }

            // a running session keeps its launch-time setting; say so plainly
            if (AgentBridgeService.Instance.IsRunning)
                MessageBox.Show(
                    "Takes effect for NEW sessions. Stop and restart the session to apply it now.",
                    "Be My Guest", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// The MCP client spawns THIS exe — the asInvoker proxy, never the
        /// elevated app itself. Looked up next to the app, in the installed
        /// tools dir, or in a dev tree; installed to the tools dir when found
        /// elsewhere so the registered path stays stable across rebuilds.
        /// </summary>
        private static string ResolveAndInstallProxy()
        {
            string toolsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoCommand", "tools", "AutoCommand.AgentProxy.exe");
            if (File.Exists(toolsPath)) return toolsPath;

            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "AutoCommand.AgentProxy.exe"),
                Path.Combine(AppContext.BaseDirectory, "tools", "AutoCommand.AgentProxy.exe"),
            };
            // dev layout: walk up to the repo root, where the proxy's bin lives
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++, dir = Path.GetDirectoryName(dir))
                candidates.Add(Path.Combine(dir, "AgentProxy", "bin", "Debug", "net10.0-windows", "AutoCommand.AgentProxy.exe"));

            string found = candidates.FirstOrDefault(File.Exists);
            if (found == null) return null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(toolsPath));
                File.Copy(found, toolsPath, overwrite: true);
                return toolsPath;
            }
            catch { return found; } // copy failed (locked?) — register where it is
        }

        private void RegisterZcodeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string proxy = ResolveAndInstallProxy();
                if (proxy == null)
                {
                    MessageBox.Show(
                        "AutoCommand.AgentProxy.exe was not found next to the app.\n" +
                        "Reinstall AutoCommand (or rebuild from source) and try again.",
                        "Be My Guest", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".zcode", "cli", "config.json");

                JsonObject root;
                if (File.Exists(configPath))
                {
                    root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject ?? new JsonObject();
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                    root = new JsonObject();
                }

                var mcp = root["mcp"] as JsonObject;
                if (mcp == null) { mcp = new JsonObject(); root["mcp"] = mcp; }
                var servers = mcp["servers"] as JsonObject;
                if (servers == null) { servers = new JsonObject(); mcp["servers"] = servers; }
                servers["autocommand"] = new JsonObject
                {
                    ["command"] = proxy,
                    ["args"] = new JsonArray(),
                };
                File.WriteAllText(configPath, root.ToJsonString(
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                MessageBox.Show(
                    "Registered the 'autocommand' MCP server in ZCode's user config:\n" + configPath +
                    "\n\nProxy: " + proxy +
                    "\n\nNew ZCode sessions will auto-connect it. Tools answer with setup guidance " +
                    "whenever no AI session is active here.",
                    "Registered", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update the ZCode config:\n" + ex.Message +
                    "\n\nUse 'Copy MCP config' and add the entry by hand.", "AI access",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyAgentConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            string proxy = ResolveAndInstallProxy() ?? "%LOCALAPPDATA%\\AutoCommand\\tools\\AutoCommand.AgentProxy.exe";
            string config =
                "{\n  \"mcp\": {\n    \"servers\": {\n      \"autocommand\": {\n" +
                "        \"command\": \"" + proxy.Replace("\\", "\\\\") + "\",\n" +
                "        \"args\": []\n" +
                "      }\n    }\n  }\n}";
            Clipboard.SetText(config);
            MessageBox.Show("MCP config copied — paste it into the client's mcp.servers config " +
                "(ZCode: ~/.zcode/cli/config.json or <repo>/.zcode/config.json).",
                "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenAgentLogBtn_Click(object sender, RoutedEventArgs e)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoCommand", "agent");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
    }
}
