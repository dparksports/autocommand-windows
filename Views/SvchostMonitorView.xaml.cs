using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AutoCommand.Helpers;
using AutoCommand.Models;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    public partial class SvchostMonitorView : UserControl, IAiAuditable
    {
        private readonly ConcurrentDictionary<string, SvchostMonitorItem> _trackedIps = new();
        private readonly ObservableCollection<SvchostMonitorItem> _uiCollection = new();
        
        private DnsResolutionService _dnsService;
        private SysmonWatcherService _sysmonService;
        private RawSocketSnifferService _snifferService;
        private readonly SysmonInstallerService _installerService = new();
        private bool _isMonitoring = false;
        private DispatcherTimer _saveTimer;
        private DispatcherTimer _lastSeenRefreshTimer;
        private readonly string _csvPath = "ultimate_autopilot_stats.csv";

        // Firewall blocks created from this monitor, persisted in blocked.txt
        private readonly List<BlockedEntry> _blockedEntries = new();
        private readonly string _blockedPath = "blocked.txt";

        // High-transmitter threshold in bytes, persisted in tx_threshold.txt
        private long _txThreshold = 1024;
        private readonly string _txThresholdPath = "tx_threshold.txt";
        private bool _loadingThreshold = true;

        // Scheduled-task attribution for taskhostw rows (who/what/why launched)
        private readonly TaskAttributionService _taskAttribution = new();

        public SvchostMonitorView()
        {
            InitializeComponent();
            SvchostGrid.ItemsSource = _uiCollection;

            // Most-recent-first order that keeps itself current: the LastSeen
            // setter raises PropertyChanged on every packet, and live sorting
            // repositions that row at the top. The Remote IP tiebreaker keeps
            // equal-timestamp rows in a stable order instead of reshuffling.
            // High transmitters sort ahead of everything (the red-pinned
            // heavy talkers), still most-recent-first within their group.
            // (ListCollectionView implements ICollectionViewLiveShaping
            // explicitly, so the shaping members need the interface cast.)
            if (CollectionViewSource.GetDefaultView(_uiCollection) is ICollectionViewLiveShaping liveShaping)
            {
                liveShaping.LiveSortingProperties.Add(nameof(SvchostMonitorItem.IsHighTransmitter));
                liveShaping.LiveSortingProperties.Add(nameof(SvchostMonitorItem.LastSeen));
                liveShaping.IsLiveSorting = true;
            }
            if (CollectionViewSource.GetDefaultView(_uiCollection) is ListCollectionView view)
            {
                view.SortDescriptions.Add(new SortDescription(
                    nameof(SvchostMonitorItem.IsHighTransmitter), ListSortDirection.Descending));
                view.SortDescriptions.Add(new SortDescription(
                    nameof(SvchostMonitorItem.LastSeen), ListSortDirection.Descending));
                view.SortDescriptions.Add(new SortDescription(
                    nameof(SvchostMonitorItem.RemoteIp), ListSortDirection.Ascending));
            }
        }

        private bool _isInitialized = false;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            CheckSysmonStatus();

            _dnsService   = new DnsResolutionService();
            _sysmonService = new SysmonWatcherService(_trackedIps, _dnsService);
            _sysmonService.MonitorAllProcesses = true;
            _sysmonService.FilterKnownCloudIps = HideCloudCheck.IsChecked == true;
            _snifferService = new RawSocketSnifferService(_trackedIps);

            _sysmonService.OnError += ShowError;
            _sysmonService.OnNewConnectionTracked += AddToUi;

            _snifferService.OnError += ShowError;

            // Attribution errors are non-fatal (rows just lack task info)
            _taskAttribution.OnError += msg => { };
            _taskAttribution.Start();

            RefreshIgnoredUi();
            LoadBlockedEntries();
            RefreshBlockedUi();
            LoadTxThreshold();

            // Setup auto-save every 1 hour
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _saveTimer.Tick += (s, args) => SaveToCsv();

            // Refresh relative "Last Packet" column + DNS cache chip every 10 seconds
            _lastSeenRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _lastSeenRefreshTimer.Tick += (s, args) =>
            {
                foreach (var item in _uiCollection)
                    item.RefreshLastSeenDisplay();

                // Re-enqueue any rows still showing raw IPs (handles items loaded from CSV etc.)
                foreach (var item in _uiCollection)
                    _dnsService.EnqueueForResolution(item);

                // Fill in scheduled-task attribution as the event logs catch up
                foreach (var item in _uiCollection)
                    EnrichWithTaskInfo(item);

                DnsCacheChip.Text = $"DNS cache: {_dnsService.CacheCount} entries";
                UpdateHighTxChip();
            };
            _lastSeenRefreshTimer.Start();

            // Start DNS service before Sysmon so first events can hit the cache
            _dnsService.Start();

            // Auto-start monitor on load
            ToggleMonitorBtn_Click(null, null);
        }

        private void CheckSysmonStatus()
        {
            if (!_installerService.IsSysmonInstalled())
            {
                SetupSysmonBtn.Visibility = Visibility.Visible;
                MonitorStatusText.Text = "Sysmon is not installed — install it from the Sysmon Audit tab. Network tracking will not work.";
            }
            else
            {
                SetupSysmonBtn.Visibility = Visibility.Collapsed;
            }
        }

        private async void SetupSysmonBtn_Click(object sender, RoutedEventArgs e)
        {
            SetupSysmonBtn.IsEnabled = false;

            // Inconsistent state = leftovers of a failed install (stale event
            // manifest registration / stray Sysmon64.exe) that make every
            // install attempt fail. Offer a guided repair instead.
            if (_installerService.DetectInstallState() == SysmonInstallState.Inconsistent)
            {
                await RepairSysmonFlowAsync();
                return;
            }

            MonitorStatusText.Text = "Downloading and configuring Sysmon... please wait.";

            var result = await _installerService.InstallAndConfigureAsync();
            if (result.Success)
            {
                MessageBox.Show("Sysmon has been successfully installed and configured for network tracking.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                CheckSysmonStatus();
                MonitorStatusText.Text = "Sysmon ready. Click 'Start Monitor' to begin tracking.";
            }
            else
            {
                MessageBox.Show($"Failed to install Sysmon.\n\nDetails: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetupSysmonBtn.IsEnabled = true;
                MonitorStatusText.Text = "Sysmon is not installed — install it from the Sysmon Audit tab. Network tracking will not work.";
            }
        }

        private async Task RepairSysmonFlowAsync()
        {
            var repair = MessageBox.Show(
                "Sysmon is in an inconsistent install state: leftovers of an earlier failed install " +
                "(a stale event-manifest registration and/or a stray Sysmon64.exe) make every install attempt fail.\n\n" +
                "Repair now? The leftovers will be removed and Sysmon reinstalled. " +
                "If Windows still holds the stale registration, a restart will be offered to finish the repair.",
                "Sysmon repair", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);

            if (repair != MessageBoxResult.Yes)
            {
                SetupSysmonBtn.IsEnabled = true;
                MonitorStatusText.Text = "Sysmon repair cancelled. Network tracking will not work.";
                return;
            }

            MonitorStatusText.Text = "Repairing Sysmon... removing leftovers and reinstalling.";
            var repaired = await _installerService.RepairAsync();

            if (repaired.Success)
            {
                MessageBox.Show("Sysmon repair completed and verified. No restart was needed.", "Repair complete", MessageBoxButton.OK, MessageBoxImage.Information);
                CheckSysmonStatus();
                MonitorStatusText.Text = "Sysmon ready. Click 'Start Monitor' to begin tracking.";
                return;
            }

            if (repaired.RebootRequired)
            {
                var restart = MessageBox.Show(
                    "Windows still holds the stale Sysmon registration; it can only be cleared during a restart.\n\n" +
                    "Restart Windows now to finish the repair? Save your work first — the PC will reboot. " +
                    "After the restart AutoCommand will reinstall and verify Sysmon automatically.",
                    "Sysmon repair — restart required", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

                if (restart == MessageBoxResult.Yes)
                {
                    MonitorStatusText.Text = "Restarting Windows to complete the Sysmon repair...";
                    ProcessRunner.RunDetached("shutdown.exe",
                        "/r /t 10 /c \"AutoCommand: restarting to complete the Sysmon repair\"");
                    return;
                }

                MessageBox.Show("The repair will finish automatically after the next Windows restart: " +
                    "AutoCommand will reinstall and verify Sysmon on startup.", "Repair pending restart",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                MonitorStatusText.Text = "Sysmon repair pending restart. Network tracking will not work until then.";
                return;
            }

            MessageBox.Show($"Sysmon repair failed.\n\nDetails: {repaired.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SetupSysmonBtn.IsEnabled = true;
            MonitorStatusText.Text = "Sysmon is not installed. Network tracking will not work.";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (_sysmonService != null)
                _sysmonService.FilterKnownCloudIps = HideCloudCheck.IsChecked == true;
        }

        // -----------------------------------------------------------------------
        // High-transmitter threshold (pinned + tinted rows at/above it)
        // -----------------------------------------------------------------------

        private void LoadTxThreshold()
        {
            try
            {
                if (File.Exists(_txThresholdPath) &&
                    long.TryParse(File.ReadAllText(_txThresholdPath).Trim(), out long bytes) &&
                    bytes >= 0)
                {
                    _txThreshold = bytes;
                }
            }
            catch { /* first run or unreadable file — keep the 1 KB default */ }

            SvchostMonitorItem.TxThresholdBytes = _txThreshold;

            // Reflect the byte value in the number/unit controls
            long unit = _txThreshold >= 1048576 ? 1048576 : _txThreshold >= 1024 ? 1024 : 1;
            TxUnitCombo.SelectionChanged -= TxThreshold_Changed;
            foreach (var item in TxUnitCombo.Items.OfType<ComboBoxItem>())
                if (item.Tag is string tag && long.Parse(tag) == unit) item.IsSelected = true;
            TxUnitCombo.SelectionChanged += TxThreshold_Changed;
            TxThresholdBox.Text = (_txThreshold / unit).ToString();
            UpdateHighTxChip();
            _loadingThreshold = false;
        }

        private void TxThreshold_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingThreshold) return;

            if (!long.TryParse(TxThresholdBox.Text.Trim(), out long value) || value < 0)
                return; // mid-typing or invalid — keep the last valid threshold

            long unit = 1;
            if (TxUnitCombo.SelectedItem is ComboBoxItem unitItem && unitItem.Tag is string tag)
                long.TryParse(tag, out unit);

            _txThreshold = value * unit;
            SvchostMonitorItem.TxThresholdBytes = _txThreshold;
            try { File.WriteAllText(_txThresholdPath, _txThreshold.ToString()); }
            catch { /* persistence is best-effort — the live threshold still applies */ }

            // Re-sort immediately: live sorting reacts to per-row changes,
            // not to the shared threshold moving under them
            CollectionViewSource.GetDefaultView(_uiCollection).Refresh();
            UpdateHighTxChip();
        }

        private void UpdateHighTxChip()
        {
            int count = 0;
            foreach (var item in _uiCollection)
                if (item.IsHighTransmitter) count++;
            HighTxCountText.Text = $"High TX: {count} ≥ {FormatThreshold(_txThreshold)}";
        }

        private static string FormatThreshold(long bytes) =>
            bytes >= 1048576 ? $"{bytes / 1048576.0:0.#} MB" :
            bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : $"{bytes} B";

        private void ShowError(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(msg, "Monitor Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (_isMonitoring) ToggleMonitorBtn_Click(null, null); // Stop on critical error
            });
        }

        private void AddToUi(SvchostMonitorItem item)
        {
            Dispatcher.Invoke(() =>
            {
                // The event may have raced with the user adding an ignore entry
                if (_sysmonService.IsProcessIgnored(item.ProcessName))
                {
                    _trackedIps.TryRemove(item.RemoteIp, out _);
                    return;
                }
                EnrichWithTaskInfo(item);
                _uiCollection.Add(item);
            });
        }

        /// <summary>
        /// Stamps scheduled-task attribution (task path + user) onto taskhostw
        /// rows once the event logs have caught up with the launch.
        /// </summary>
        private void EnrichWithTaskInfo(SvchostMonitorItem item)
        {
            if (item.TaskName != null) return;
            var info = _taskAttribution.GetTaskForPid(item.ProcessId);
            if (info == null || string.IsNullOrEmpty(info.TaskPath)) return;
            item.SetTaskInfo(info.TaskPath, info.UserContext);
        }

        private void ToggleMonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoring)
            {
                _sysmonService.Start();
                _snifferService.Start();
                _saveTimer.Start();
                
                ToggleMonitorBtn.Content = "Stop Monitor";
                MonitorStatusText.Text = "Monitor active: Sysmon event listening and Raw Socket sniffing enabled.";
                _isMonitoring = true;
            }
            else
            {
                _sysmonService.Stop();
                _snifferService.Stop();
                _saveTimer.Stop();
                
                ToggleMonitorBtn.Content = "Start Monitor";
                MonitorStatusText.Text = "Monitor is stopped.";
                _isMonitoring = false;
            }
        }

        private void AddProcessBtn_Click(object sender, RoutedEventArgs e)
        {
            string procName = NewProcessBox.Text.Trim();
            if (string.IsNullOrEmpty(procName)) return;

            // Ensure it ends with .exe for the internal filter
            if (!procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                procName += ".exe";
            }

            _sysmonService.AddTargetProcess(procName);
            NewProcessBox.Clear();
            MessageBox.Show($"Added {procName} to tracking list.", "Process Added", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // -----------------------------------------------------------------------
        // Process ignore list
        // -----------------------------------------------------------------------

        private void IgnoreProcessBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) IgnoreProcessBtn_Click(sender, e);
        }

        private void IgnoreProcessBtn_Click(object sender, RoutedEventArgs e)
        {
            string procName = IgnoreProcessBox.Text.Trim();
            if (string.IsNullOrEmpty(procName)) return;

            IgnoreProcess(procName);
            IgnoreProcessBox.Clear();
        }

        private void IgnoreRowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is SvchostMonitorItem item && !string.IsNullOrEmpty(item.ProcessName))
                IgnoreProcess(item.ProcessName);
        }

        private void RowMenu_Opened(object sender, RoutedEventArgs e)
        {
            var sel = SvchostGrid.SelectedItem as SvchostMonitorItem;

            bool traceable = sel != null && sel.ProcessId > 0;
            TracePidMenuItem.IsEnabled = traceable;
            TracePidMenuItem.Header = traceable
                ? $"🔎 Trace PID {sel.ProcessId} — how was it spawned?"
                : "🔎 Trace: how was this PID spawned?";

            IgnoreRowMenuItem.Header = sel != null && !string.IsNullOrEmpty(sel.ProcessName)
                ? $"🚫 Ignore process '{sel.ProcessName}'"
                : "🚫 Ignore process (no row selected)";

            bool validIp = sel != null && System.Net.IPAddress.TryParse(sel.RemoteIp, out _);
            BlockIpMenuItem.IsEnabled = validIp;
            BlockIpMenuItem.Header = validIp
                ? $"⛔ Block remote IP {sel.RemoteIp} in Firewall"
                : "⛔ Block Remote IP in Firewall";

            bool blockableProcess = sel != null && sel.ProcessId > 0;
            BlockProcessMenuItem.IsEnabled = blockableProcess;
            BlockProcessMenuItem.Header = blockableProcess && !string.IsNullOrEmpty(sel.ProcessName)
                ? $"⛔ Block process '{sel.ProcessName}' in Firewall"
                : "⛔ Block Process in Firewall";

            bool killable = sel != null && sel.ProcessId > 0;
            KillProcessMenuItem.IsEnabled = killable;
            KillProcessMenuItem.Header = killable && !string.IsNullOrEmpty(sel.ProcessName)
                ? $"💀 Kill process '{sel.ProcessName}' (PID {sel.ProcessId})"
                : "💀 Kill process";

            bool hasTask = sel != null && !string.IsNullOrEmpty(sel.TaskName);
            TaskDetailsMenuItem.IsEnabled = hasTask;
            TaskDetailsMenuItem.Header = hasTask
                ? $"📋 Scheduled task '{sel.TaskName}'"
                : "📋 Scheduled task details";
            DisableTaskMenuItem.IsEnabled = hasTask;
        }

        private void IgnoredListBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshIgnoredUi();
            IgnoredListPopup.IsOpen = true;
        }

        private void RemoveIgnoredBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IgnoredListBox.SelectedItem is string name)
            {
                _sysmonService.RemoveIgnoredProcess(name);
                RefreshIgnoredUi();
            }
        }

        /// <summary>
        /// Hides a process name from the list: adds it to the persisted ignore
        /// list, then removes any rows already on screen for it.
        /// </summary>
        private void IgnoreProcess(string procName)
        {
            _sysmonService.AddIgnoredProcess(procName);
            PruneIgnoredRows();
            RefreshIgnoredUi();
        }

        /// <summary>Drops rows whose process is now ignored (list + packet counters).</summary>
        private void PruneIgnoredRows()
        {
            foreach (var item in _uiCollection.Where(i => _sysmonService.IsProcessIgnored(i.ProcessName)).ToList())
            {
                _uiCollection.Remove(item);
                _trackedIps.TryRemove(item.RemoteIp, out _);
            }
        }

        private void RefreshIgnoredUi()
        {
            var ignored = _sysmonService.IgnoredProcesses;
            IgnoredListBtn.Content = $"Ignored: {ignored.Count}";
            IgnoredListBox.ItemsSource = ignored.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void SvchostGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click selects the row under the cursor so the "Ignore
            // process" menu item knows which process was meant.
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(SvchostGrid, source) is DataGridRow row)
            {
                row.IsSelected = true;
            }
        }

        // -----------------------------------------------------------------------
        // PID inquiry — provenance tracing ("how was this spawned?")
        // -----------------------------------------------------------------------

        private void TracePidMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is SvchostMonitorItem item)
                OpenPidTrace(item.ProcessId, item);
        }

        /// <summary>
        /// Refreshes the inquiry dropdown with every PID currently shown in
        /// the grid (most recently active first). Rebuilding ItemsSource
        /// clears the editable text, so whatever the user typed is restored.
        /// </summary>
        private void PidCombo_DropDownOpened(object sender, EventArgs e)
        {
            string typed = PidCombo.Text;
            PidCombo.ItemsSource = _uiCollection
                .GroupBy(i => i.ProcessId)
                .Select(g => g.OrderByDescending(i => i.LastSeen).First())
                .OrderByDescending(i => i.LastSeen)
                .Select(i => i.ProcessId == 0
                    ? "0 · System"
                    : $"{i.ProcessId} · {i.ProcessName ?? "?"}")
                .ToList();
            PidCombo.Text = typed;
        }

        private void PidCombo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TracePidBtn_Click(sender, e);
        }

        private void TracePidBtn_Click(object sender, RoutedEventArgs e)
        {
            // Accepts both a bare PID and a picked "1234 · name" entry; with
            // nothing typed or picked, falls back to the selected row so the
            // button does the obvious thing instead of demanding input.
            string text = (PidCombo.Text ?? string.Empty).Trim();
            int len = 0;
            while (len < text.Length && char.IsDigit(text[len])) len++;
            if (len > 0 && int.TryParse(text.AsSpan(0, len), out int pid) && pid >= 0)
            {
                OpenPidTrace(pid, _uiCollection.FirstOrDefault(i => i.ProcessId == pid));
                return;
            }

            if (SvchostGrid.SelectedItem is SvchostMonitorItem row && row.ProcessId > 0)
            {
                OpenPidTrace(row.ProcessId, row);
                return;
            }

            MessageBox.Show("Enter or pick a PID first — e.g. 20264 (the '1234 · name' dropdown entries work too) — or select a row and press Trace.",
                "PID Inquiry", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Opens the read-only PID inquiry window. Row hints (name, Sysmon
        /// image path, task attribution) travel along so the report stays
        /// meaningful even when the process has already exited.
        /// </summary>
        private void OpenPidTrace(int pid, SvchostMonitorItem item)
        {
            if (pid <= 0)
            {
                MessageBox.Show("The System/Idle process has no spawn chain to trace.",
                    "PID Inquiry", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var win = new ProcessTraceWindow(
                pid,
                item?.ProcessName,
                item?.ImagePath,
                item != null ? _taskAttribution.GetTaskForPid(pid) : null)
            {
                Owner = Window.GetWindow(this)
            };
            win.Show();
        }

        // -----------------------------------------------------------------------
        // Firewall blocking (remote IP / process)
        // -----------------------------------------------------------------------

        /// <summary>A firewall block created from this monitor, persisted in blocked.txt.</summary>
        private sealed class BlockedEntry
        {
            public string Kind;         // "ip" or "proc"
            public string Key;          // remote IP or executable path
            public string ProcessName;
            public DateTime Timestamp;

            public override string ToString() => Kind == "ip"
                ? $"IP  {Key}  ({ProcessName}, blocked {Timestamp:yyyy-MM-dd HH:mm})"
                : $"EXE {Key}  ({ProcessName}, blocked {Timestamp:yyyy-MM-dd HH:mm})";
        }

        private async void BlockRemoteIP_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is not SvchostMonitorItem item) return;

            string ip = item.RemoteIp;
            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                MessageBox.Show($"'{ip}' is not a valid IP address.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string warning = Services.IpClassifier.WarningText(ip);
            string confirm = $"{warning}\nBlock all outbound traffic to remote IP '{ip}' (contacted by '{item.ProcessName}')?\n\n" +
                             "2 rules are created: outbound TCP and UDP. Inbound is already covered by the\n" +
                             "firewall's stateful filtering — this avoids the old 4-rule clutter.\n" +
                             "Undo anytime: right-click this row again, or 'Blocked' → Manage all.";
            if (warning.Length > 0)
                confirm += "\n\n⚠ Think twice: this IP is Microsoft infrastructure.";
            if (MessageBox.Show(confirm, "Confirm Block",
                    MessageBoxButton.YesNo, warning.Length > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                string note = BuildProvenance(item, $"→ {ip}");
                var (created, updated) = await Services.FirewallService.Instance.AddIpBlockAsync(ip, note);
                RecordBlockedEntry("ip", ip, item.ProcessName);
                MessageBox.Show(created > 0
                        ? $"Blocked remote IP {ip} — 2 rules (outbound TCP/UDP).\nProvenance written to the rule description (visible in wf.msc)."
                        : $"Block for {ip} already existed — refreshed its provenance (no duplicate rules).",
                    "Blocked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to block remote IP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BlockProcess_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is not SvchostMonitorItem item) return;

            if (item.ProcessId == 0)
            {
                MessageBox.Show("Cannot block System/Idle process.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string path = GetProcessPath(item.ProcessId);
                if (string.IsNullOrEmpty(path))
                {
                    MessageBox.Show("Could not determine process path (access denied or process exited).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string warning = "";
                if (string.Equals(item.ProcessName, "svchost.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.ProcessName, "taskhostw.exe", StringComparison.OrdinalIgnoreCase))
                    warning = $"⚠ '{item.ProcessName}' hosts Windows SERVICES. One instance carries DNS (Dnscache),\n" +
                              "DHCP, diagnostics and more — blocking it is the #1 cause of 'I blocked something\n" +
                              "and lost my internet'. If a specific connection looks bad, block the IP, not this process.\n\n";
                else if (path.StartsWith(Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase))
                    warning = $"⚠ '{item.ProcessName}' is a Windows component. Blocking it can break shell features,\n" +
                              "widgets, or web content in Start/Settings.\n\n";

                if (MessageBox.Show($"{warning}Block all outbound network traffic for\n{path}\n(1 outbound rule; undo from 'Blocked' → Manage all)?",
                        "Confirm Block", MessageBoxButton.YesNo, warning.Length > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                string note = BuildProvenance(item, $"app: {path}");
                var (created, updated) = await Services.FirewallService.Instance.AddAppBlockAsync(path, note);
                RecordBlockedEntry("proc", path, item.ProcessName);
                MessageBox.Show(created > 0
                        ? $"Blocked application (1 outbound rule):\n{path}"
                        : $"Block for {System.IO.Path.GetFileName(path)} already existed — provenance refreshed.",
                    "Blocked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to block process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Full provenance embedded into the firewall rule Description.</summary>
        private static string BuildProvenance(SvchostMonitorItem item, string what)
        {
            string host = "";
            try { host = item.HostnameDisplay is string h && h.Contains("…") == false && h != "resolving…" ? $" host {h}" : ""; } catch { }
            return $"[blocked {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] {item.ProcessName} PID {item.ProcessId} {what}{host} · " +
                   $"rx {item.RxBytesDisplay} tx {item.TxBytesDisplay} · AutoCommand Process Monitor";
        }

        private void BlockedListBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshBlockedUi();
            BlockedListPopup.IsOpen = true;
        }

        private void ManageBlockedBtn_Click(object sender, RoutedEventArgs e)
        {
            BlockedListPopup.IsOpen = false;
            var win = new BlockedRulesWindow { Owner = Window.GetWindow(this) };
            win.Show();
        }

        private async void UnblockBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockedListBox.SelectedItem is not BlockedEntry entry) return;

            if (MessageBox.Show($"Remove the firewall block for '{entry.Key}'?",
                "Confirm Unblock", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                int removed = entry.Kind == "ip"
                    ? await FirewallService.Instance.RemoveBlockRulesForIpAsync(entry.Key)
                    : await FirewallService.Instance.RemoveBlockRulesForAppAsync(entry.Key);

                _blockedEntries.Remove(entry);
                SaveBlockedEntries();
                RefreshBlockedUi();

                MessageBox.Show(removed > 0
                    ? $"Removed {removed} firewall rule(s) for:\n{entry.Key}"
                    : $"No matching firewall rules found (already removed?) for:\n{entry.Key}",
                    "Unblocked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to unblock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is not SvchostMonitorItem item) return;

            if (item.ProcessId == 0)
            {
                MessageBox.Show("Cannot kill System/Idle process.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var proc = Process.GetProcessById(item.ProcessId);

                // PID reuse guard: the row may be stale — the original process
                // exited and Windows recycled the PID to something else.
                // Verify the live identity before pulling the trigger.
                string liveName = string.Empty;
                try { liveName = proc.ProcessName; } catch { }

                string knownName = Path.GetFileNameWithoutExtension(item.ProcessName ?? string.Empty);
                string confirmName = item.ProcessName;
                if (knownName.Length > 0 && !liveName.Equals(knownName, StringComparison.OrdinalIgnoreCase))
                {
                    confirmName = liveName;
                    var stale = MessageBox.Show(
                        $"This row is stale: PID {item.ProcessId} now runs '{liveName}', not '{item.ProcessName}'.\n\n" +
                        $"Kill the running process '{liveName}' (PID {item.ProcessId}) anyway?\n" +
                        "This forcefully terminates the application.",
                        "PID reused — confirm kill", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (stale != MessageBoxResult.Yes) return;
                }
                else if (MessageBox.Show(
                    $"Kill process '{confirmName}' (PID {item.ProcessId})?\nThis forcefully terminates the application.",
                    "Confirm Kill", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                proc.Kill();
                await Task.Delay(500); // Give it a moment to release ports
            }
            catch (ArgumentException)
            {
                MessageBox.Show($"Process {item.ProcessId} is no longer running (it already exited).",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to kill process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TaskDetails_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is not SvchostMonitorItem item || string.IsNullOrEmpty(item.TaskName)) return;

            var (summary, error) = await TaskSchedulerService.Instance.GetTaskSummaryAsync(item.TaskName);

            // What code the task actually executes (COM-handler DLL), when
            // attribution has it — DLL tasks have no command line of their own
            var launch = _taskAttribution.GetTaskForPid(item.ProcessId);
            if (launch?.HandlerClassId != null)
            {
                string handler = launch.HandlerDllExists == true
                    ? $"Handler DLL: {launch.HandlerDll} (CLSID {launch.HandlerClassId})"
                    : $"Handler CLSID {launch.HandlerClassId} is not registered — the task's DLL is gone (leftover of uninstalled software)";
                summary = string.IsNullOrEmpty(summary) ? handler : $"{handler}\n\n{summary}";
                error = null;
            }

            MessageBox.Show(
                string.IsNullOrEmpty(error)
                    ? summary
                    : $"Could not read task '{item.TaskName}': {error}",
                "Scheduled task", MessageBoxButton.OK,
                string.IsNullOrEmpty(error) ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private async void DisableTask_Click(object sender, RoutedEventArgs e)
        {
            if (SvchostGrid.SelectedItem is not SvchostMonitorItem item || string.IsNullOrEmpty(item.TaskName)) return;

            if (MessageBox.Show(
                $"Disable scheduled task '{item.TaskName}'?\n\n" +
                "The task will not run again until it is re-enabled (Tasks tab, or:\n" +
                $"schtasks /Change /TN \"{item.TaskName}\" /ENABLE).\n\n" +
                "The running instance keeps going — use 'Kill process' to end it now.",
                "Confirm Disable Task", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                await TaskSchedulerService.Instance.SetTaskEnabledAsync(item.TaskName, false);
                MessageBox.Show($"Scheduled task disabled:\n{item.TaskName}", "Task Disabled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to disable task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Resolves an executable path for a PID; falls back to WMI when the
        /// process module cannot be read (bitness mismatch) or the process exited.
        /// </summary>
        private static string GetProcessPath(int processId)
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                string path = proc.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path)) return path;
            }
            catch { }

            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}");
                using var results = searcher.Get();
                foreach (System.Management.ManagementObject obj in results)
                {
                    return obj["ExecutablePath"]?.ToString();
                }
            }
            catch { }
            return null;
        }

        // -----------------------------------------------------------------------
        // blocked.txt persistence (format: kind|key|processName|timestamp)
        // -----------------------------------------------------------------------

        private void RecordBlockedEntry(string kind, string key, string processName)
        {
            // One record per key — re-blocking refreshes it instead of duplicating
            _blockedEntries.RemoveAll(b => b.Kind == kind && b.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            _blockedEntries.Add(new BlockedEntry { Kind = kind, Key = key, ProcessName = processName ?? "", Timestamp = DateTime.Now });
            SaveBlockedEntries();
            RefreshBlockedUi();
        }

        private void LoadBlockedEntries()
        {
            _blockedEntries.Clear();
            try
            {
                foreach (string line in File.ReadAllLines(_blockedPath))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 4) continue;
                    if (parts[0] != "ip" && parts[0] != "proc") continue;
                    _blockedEntries.Add(new BlockedEntry
                    {
                        Kind = parts[0],
                        Key = parts[1],
                        ProcessName = parts[2],
                        Timestamp = DateTime.TryParse(parts[3], out DateTime ts) ? ts : DateTime.Now
                    });
                }
            }
            catch { /* first run or unreadable file — start with an empty block list */ }
        }

        private void SaveBlockedEntries()
        {
            try
            {
                File.WriteAllLines(_blockedPath, _blockedEntries.Select(b => $"{b.Kind}|{b.Key}|{b.ProcessName}|{b.Timestamp:O}"));
            }
            catch { /* persistence is best-effort — the firewall rules themselves are the source of truth */ }
        }

        private void RefreshBlockedUi()
        {
            BlockedListBtn.Content = $"Blocked: {_blockedEntries.Count}";
            BlockedListBox.ItemsSource = _blockedEntries
                .OrderBy(b => b.Kind).ThenBy(b => b.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void SaveCsvBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveToCsv();
            MessageBox.Show($"Data saved to {_csvPath}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveToCsv()
        {
            try
            {
                bool writeHeader = !File.Exists(_csvPath);
                using var writer = new StreamWriter(_csvPath, true);
                
                if (writeHeader)
                {
                    writer.WriteLine("Timestamp,PID,ProcessName,RemoteIP,Host,RxPackets,TxPackets,RxBytes,TxBytes,LastSeen");
                }

                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var snap = _trackedIps.Values.ToList();
                
                foreach (var item in snap)
                {
                    writer.WriteLine($"{ts},{item.ProcessId},{item.ProcessName},{item.RemoteIp},{item.Hostname},{item.RxPackets},{item.TxPackets},{item.RxBytes},{item.TxBytes},{item.LastSeen:u}");
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Failed to save CSV: {ex.Message}"));
            }
        }

        public string GetAuditContext()
        {
            var items = _uiCollection.ToList();
            if (items.Count == 0) return "Process Monitor is currently empty. No active connections tracked.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Currently Tracked Processes and Connections:");
            foreach (var item in items)
            {
                sb.AppendLine($"- Process: {item.ProcessName} (PID: {item.ProcessId}) | Remote IP: {item.RemoteIp} ({item.Hostname}) | Packets: {item.TxPackets} sent, {item.RxPackets} received | Last Seen: {item.LastSeen:u}");
            }
            return sb.ToString();
        }
    }
}
