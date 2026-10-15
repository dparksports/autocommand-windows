using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AutoCommand.Services;
using AutoCommand.Helpers;

namespace AutoCommand.Views
{
    public partial class SettingsView : UserControl
    {
        private const string TaskName = FreshSetupService.StartupTaskName;

        public SettingsView()
        {
            InitializeComponent();

            // Live version from the assembly instead of a hardcoded string
            // (same source as the status bar)
            VersionValueText.Text =
                FileVersionInfo.GetVersionInfo(Environment.ProcessPath).ProductVersion ?? "unknown";
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckStartupTask();

            // Load Auto-Mitigate Adapters preference
            AutoMitigateAdaptersCheck.Checked -= AutoMitigateAdaptersCheck_Checked;
            AutoMitigateAdaptersCheck.Unchecked -= AutoMitigateAdaptersCheck_Unchecked;
            AutoMitigateAdaptersCheck.IsChecked = SecurityEnforcer.AutoMitigateAdapters;
            AutoMitigateAdaptersCheck.Checked += AutoMitigateAdaptersCheck_Checked;
            AutoMitigateAdaptersCheck.Unchecked += AutoMitigateAdaptersCheck_Unchecked;

            // Load the security check rate (fast-check tick interval)
            CheckRateCombo.SelectionChanged -= CheckRateCombo_SelectionChanged;
            foreach (var item in CheckRateCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string tag && tag == SecurityEnforcer.CheckIntervalSeconds.ToString())
                {
                    item.IsSelected = true;
                    break;
                }
            }
            CheckRateCombo.SelectionChanged += CheckRateCombo_SelectionChanged;

            // Load theme preference (Dark | Light)
            ThemeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
            foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string tag && tag == ThemeService.Saved)
                {
                    item.IsSelected = true;
                    break;
                }
            }
            ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;

            // AI access card: mutating preference + live session status
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

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string theme)
            {
                ThemeService.ApplyTheme(theme);
            }
        }

        private void CheckRateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CheckRateCombo.SelectedItem is ComboBoxItem item
                && item.Tag is string tag && int.TryParse(tag, out int seconds))
            {
                SecurityEnforcer.CheckIntervalSeconds = seconds;
            }
        }

        private void AutoMitigateAdaptersCheck_Checked(object sender, RoutedEventArgs e)
        {
            SecurityEnforcer.AutoMitigateAdapters = true;
        }

        private void AutoMitigateAdaptersCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            SecurityEnforcer.AutoMitigateAdapters = false;
        }

        private async Task CheckStartupTask()
        {
            bool exists = false;
            await Task.Run(() =>
            {
                try
                {
                    Type tsType = Type.GetTypeFromProgID("Schedule.Service");
                    dynamic ts = Activator.CreateInstance(tsType);
                    ts.Connect();
                    dynamic rootFolder = ts.GetFolder("\\");
                    dynamic task = rootFolder.GetTask(TaskName);
                    exists = task != null;
                }
                catch
                {
                    exists = false;
                }
            });

            // Detach events to avoid infinite loop
            LaunchOnStartupCheck.Checked -= LaunchOnStartupCheck_Checked;
            LaunchOnStartupCheck.Unchecked -= LaunchOnStartupCheck_Unchecked;
            
            LaunchOnStartupCheck.IsChecked = exists;

            LaunchOnStartupCheck.Checked += LaunchOnStartupCheck_Checked;
            LaunchOnStartupCheck.Unchecked += LaunchOnStartupCheck_Unchecked;
        }

        private async void LaunchOnStartupCheck_Checked(object sender, RoutedEventArgs e)
        {
            bool ok = await FreshSetupService.EnableLaunchAtLogon();
            if (!ok)
                MessageBox.Show("Failed to create startup task.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            await CheckStartupTask();
        }

        private async void LaunchOnStartupCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            await FreshSetupService.DisableLaunchAtLogon();
            await CheckStartupTask();
        }

        // -----------------------------------------------------------------------
        // Unblock (Mark-of-the-Web removal) — see Helpers/MotwUnblocker.cs
        // -----------------------------------------------------------------------

        private async void UnblockAppBtn_Click(object sender, RoutedEventArgs e)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            UnblockAppBtn.IsEnabled = false;
            UnblockStatusText.Text = $"Scanning {dir} …";
            try
            {
                var result = await System.Threading.Tasks.Task.Run(() => MotwUnblocker.UnblockFolder(dir));
                UnblockStatusText.Text = result.Summary;
                if (result.Unblocked > 0)
                    MessageBox.Show(
                        $"Removed the download mark from {result.Unblocked} file(s) in this app's folder.\n\n" +
                        "SmartScreen / Smart App Control will no longer treat them as downloaded\n" +
                        "files — taskbar pins and direct launches work normally now.",
                        "Unblocked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                UnblockAppBtn.IsEnabled = true;
            }
        }

        private async void UnblockFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Pick the folder to unblock (e.g. a freshly extracted AutoCommand zip)"
            };
            if (dialog.ShowDialog() != true) return;

            UnblockFolderBtn.IsEnabled = false;
            UnblockStatusText.Text = $"Scanning {dialog.FolderName} …";
            try
            {
                var result = await System.Threading.Tasks.Task.Run(() => MotwUnblocker.UnblockFolder(dialog.FolderName));
                UnblockStatusText.Text = result.Summary;
                if (result.Failures.Count > 0)
                    MessageBox.Show(
                        $"{result.Summary}\n\nFiles in use or in admin-only locations could not be updated —\n" +
                        "close the apps using them, or re-run AutoCommand as Administrator and try again.",
                        "Unblock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                UnblockFolderBtn.IsEnabled = true;
            }
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
                "AI access — consent", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            var (ok, message) = AgentBridgeService.Instance.Start(AgentMutatingCheck.IsChecked == true);
            if (!ok) MessageBox.Show(message, "AI access", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    "AI access", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        "AI access", MessageBoxButton.OK, MessageBoxImage.Warning);
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