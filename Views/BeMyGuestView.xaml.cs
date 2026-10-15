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
            PopulateClientButtons();
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
        /// One Register button per known MCP client, enabled when the client
        /// is detected on this machine. Anything undetected stays visible but
        /// disabled — and 'Copy MCP config' remains the universal fallback.
        /// </summary>
        private void PopulateClientButtons()
        {
            AgentClientsPanel.Children.Clear();
            foreach (var client in McpClientCatalog.Catalog)
            {
                bool detected = McpClientCatalog.IsDetected(client);
                var btn = new Button
                {
                    Content = "Register in " + client.DisplayName,
                    Tag = client,
                    Padding = new Thickness(10, 2, 10, 2),
                    Margin = new Thickness(0, 0, 8, 0),
                    IsEnabled = detected,
                    ToolTip = detected
                        ? client.ConfigPath
                        : client.DisplayName + " not detected on this machine — install it first, or use Copy MCP config.",
                };
                btn.Click += ClientRegisterBtn_Click;
                AgentClientsPanel.Children.Add(btn);
            }
        }

        private void ClientRegisterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: McpClientRegistration client }) return;

            string proxy = McpClientCatalog.ResolveAndInstallProxy();
            if (proxy == null)
            {
                MessageBox.Show(
                    "AutoCommand.AgentProxy.exe was not found next to the app.\n" +
                    "Reinstall AutoCommand (or rebuild from source) and try again.",
                    "Be My Guest", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (ok, message) = McpClientCatalog.Register(client, proxy);
            MessageBox.Show(message, ok ? "Registered" : "AI access",
                MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void CopyAgentConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            string proxy = McpClientCatalog.ResolveAndInstallProxy()
                ?? "%LOCALAPPDATA%\\AutoCommand\\tools\\AutoCommand.AgentProxy.exe";
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
