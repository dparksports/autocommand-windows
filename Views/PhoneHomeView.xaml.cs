using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    public sealed class NonEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>null → Collapsed; pass "inverse" to flip (null → Visible).</summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool inverse = string.Equals(parameter as string, "inverse", StringComparison.OrdinalIgnoreCase);
            bool visible = inverse ? isNull : !isNull;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>count == 0 → Visible (the "nothing here" hint case).</summary>
    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is 0 ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// "What are Windows' own services doing on the network?" — one card per
    /// curated talker (DiagTrack, CryptSvc, Delivery Optimization…): the
    /// authored plain-English story, the measured connect frequency against
    /// the authored normal band, destinations, beacon regularity and identity
    /// corroboration, all from TraceMetricsService. Suppression is
    /// differentiated by catalog impact: DiagTrack gets a reversible hosts
    /// pin (sinkhole, so denied attempts become receipts), every card can
    /// block its measured destinations through the standard confirm flow,
    /// and the impact chip says what breaking the service would cost.
    /// </summary>
    public partial class PhoneHomeView : UserControl
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(60) };
        private bool _refreshing;

        public PhoneHomeView()
        {
            InitializeComponent();
            Loaded += (_, _) => { Refresh(); };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();
        }

        private void Refresh()
        {
            if (_refreshing) return;
            _refreshing = true;
            StatusText.Text = "Reading the archive…";
            Task.Run(() =>
            {
                var vms = new List<TraceMetricsService.TraceCardVM>();
                foreach (var key in ServiceCatalog.CuratedPhoneHomeKeys)
                {
                    try
                    {
                        var snap = TraceMetricsService.Compute(TraceMetricsService.Match.ForService(key));
                        var vm = TraceMetricsService.BuildCardVM(snap);
                        vm.CanQuiet = key == "diagtrack" && !DiagTrackPin.IsActive();
                        vm.IsQuieted = key == "diagtrack" && DiagTrackPin.IsActive();
                        if (vm.IsQuieted)
                            vm.ReceiptsLine = DiagTrackPin.ReceiptsLine();
                        vms.Add(vm);
                    }
                    catch { /* one bad card must not take the tab down */ }
                }
                vms.Sort((a, b) => b.ConnectsPerHour.CompareTo(a.ConnectsPerHour));
                Dispatcher.BeginInvoke(() =>
                {
                    // keep the user's selection stable across refreshes —
                    // same service, same row, even as sorting reorders them
                    var selectedKey = (RowList.SelectedItem as TraceMetricsService.TraceCardVM)?.ServiceKey;
                    RowList.ItemsSource = vms;
                    var restore = vms.FirstOrDefault(v => v.ServiceKey == selectedKey) ?? vms.FirstOrDefault();
                    if (restore != null) RowList.SelectedItem = restore;
                    StatusText.Text = $"Updated {DateTime.Now:HH:mm:ss} · rows sorted by measured connects/hr · refresh 60 s";
                    _refreshing = false;
                });
            });
        }

        // ── suppression ──────────────────────────────────────────────────

        private void QuietBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not TraceMetricsService.TraceCardVM vm ||
                vm.ServiceKey != "diagtrack") return;
            var confirm = MessageBox.Show(
                "Quiet DiagTrack (Connected User Experiences and Telemetry)?\n\n" +
                "Microsoft's telemetry endpoints will be pinned to 127.0.0.1 in the hosts file. " +
                "AutoCommand listens on loopback so every denied attempt is logged — process, PID " +
                "and hostname — then dropped with no reply. Nothing leaves the machine either way.\n\n" +
                "Effects: Windows diagnostic-data upload stops (crash reports, usage statistics). " +
                "Windows Update, Defender and sign-in are NOT affected — they use different endpoints.\n" +
                "Undo any time from this card or the Black Hole tab.",
                "Quiet DiagTrack", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;
            var (ok, message) = DiagTrackPin.Apply();
            MessageBox.Show(message, ok ? "Quiet DiagTrack" : "Quiet DiagTrack",
                MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Refresh();
        }

        private void UndoQuietBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not TraceMetricsService.TraceCardVM vm ||
                vm.ServiceKey != "diagtrack") return;
            if (MessageBox.Show(
                    "Remove the DiagTrack hosts pins?\n\nThe endpoints resolve normally again and " +
                    "Windows telemetry upload resumes. Firewall blocks, if any, are untouched (see the Blocks tab).",
                    "Undo quiet", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            var (ok, message) = DiagTrackPin.Remove();
            MessageBox.Show(message, "Undo quiet",
                MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Refresh();
        }

        private async void BlockBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not TraceMetricsService.TraceCardVM vm) return;
            var ips = vm.TopDestIps.Where(ip => !string.IsNullOrEmpty(ip)).ToList();
            if (ips.Count == 0)
            {
                MessageBox.Show("No destinations were measured for this service in the last 24 h — " +
                                      "nothing to block yet.", "Block destinations",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ServiceCatalog.TryGet(vm.ServiceKey, out var info);
            bool risky = info is { Impact: ServiceImpact.High or ServiceImpact.Medium };
            string headline = $"Block {vm.Title}'s destinations";
            string warning = risky
                ? $"{vm.Title} is catalogued as {info.Impact.ToString().ToUpper()} impact: {info.Description}"
                : $"{vm.Title} is catalogued as safe to block: {info?.Description ?? "no catalog entry — blocking the measured destinations only"}";
            string detail = "Outbound firewall rules for:\n" +
                            string.Join("\n", ips.Select(ip => "  " + ip)) +
                            "\n\nThe service can still reach other addresses; Windows Firewall blocks " +
                            "silently, so expect quiet failures if this service matters. Undo from the Blocks tab.";

            var win = new BlockConfirmWindow(headline, warning, detail, "phonehome-dest", "Phone Home destination blocks")
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true) return;
            if (win.Suppress) ConfirmPrefs.Suppress("phonehome-dest");

            int created = 0;
            try
            {
                foreach (var ip in ips)
                {
                    var (made, _) = await FirewallService.Instance.AddIpBlockAsync(ip,
                        $"[blocked {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] {vm.Title} ({vm.ServiceKey}) " +
                        $"top destination from Phone Home · AutoCommand Phone Home");
                    created += made;
                    BlocksStore.AddOrUpdate(new BlockRecord
                    {
                        Kind = "ip",
                        Key = ip,
                        ProcessName = "svchost.exe",
                        ServiceKey = vm.ServiceKey ?? "",
                        SvcDisplayName = vm.Title,
                        ClassAtBlock = IpClassifier.Classify(ip),
                        Attrib = "live",
                    }, "phone-home");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Block failed: " + ex.Message, "Block destinations",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show(
                created > 0 ? $"Blocked {created} destination(s) for {vm.Title}. Undo from the Blocks tab."
                            : "Rules already existed for all listed destinations — nothing new created.",
                "Block destinations", MessageBoxButton.OK, MessageBoxImage.Information);
            Refresh();
        }

        /// <summary>Blocks one destination IP of the selected service — the
        /// per-row action next to each destination in the detail pane.</summary>
        private async void BlockOneBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || string.IsNullOrEmpty(btn.Tag as string)) return;
            if (RowList.SelectedItem is not TraceMetricsService.TraceCardVM vm) return;
            string ip = (string)btn.Tag;

            ServiceCatalog.TryGet(vm.ServiceKey, out var info);
            bool risky = info is { Impact: ServiceImpact.High or ServiceImpact.Medium };
            string warning = risky
                ? $"{vm.Title} is catalogued as {info.Impact.ToString().ToUpper()} impact: {info.Description}"
                : IpClassifier.WarningText(ip);
            var win = new BlockConfirmWindow(
                $"Block {ip}",
                warning,
                $"Outbound firewall rules for {ip} — a measured destination of {vm.Title} ({vm.ServiceKey}).\n" +
                "Undo from the Blocks tab.",
                "phonehome-dest", "Phone Home destination blocks")
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true) return;
            if (win.Suppress) ConfirmPrefs.Suppress("phonehome-dest");

            try
            {
                var (made, _) = await FirewallService.Instance.AddIpBlockAsync(ip,
                    $"[blocked {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] {vm.Title} ({vm.ServiceKey}) " +
                    $"destination from Phone Home · AutoCommand Phone Home");
                if (made > 0)
                    BlocksStore.AddOrUpdate(new BlockRecord
                    {
                        Kind = "ip",
                        Key = ip,
                        ProcessName = "svchost.exe",
                        ServiceKey = vm.ServiceKey ?? "",
                        SvcDisplayName = vm.Title,
                        ClassAtBlock = IpClassifier.Classify(ip),
                        Attrib = "live",
                    }, "phone-home");
                MessageBox.Show(made > 0
                        ? $"Blocked {ip} for {vm.Title}. Undo from the Blocks tab."
                        : "A block for that destination already existed.",
                    "Block destination", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Block failed: " + ex.Message, "Block destination",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Refresh();
        }
    }

    /// <summary>
    /// The DiagTrack hosts pin. Written in the same "# AutoCommand
    /// telemetry-block &lt;id&gt;" marker format TelemetryRulesService uses, so the
    /// Black Hole tab parses, toggles and can remove it too — one block, any
    /// surface. Sinkhole address (127.0.0.1) rather than 0.0.0.0 so denied
    /// attempts hit SinkholeService and become receipts.
    /// </summary>
    internal static class DiagTrackPin
    {
        private const string Marker = "# AutoCommand telemetry-block phonehome-diagtrack";

        private static readonly string[] Domains =
        {
            "vortex.data.microsoft.com",
            "vortex-win.data.microsoft.com",
            "settings-win.data.microsoft.com",
            "watson.telemetry.microsoft.com",
            "umwatson.events.data.microsoft.com",
            "telemetry.appex.bing.net",
        };

        public static bool IsActive()
        {
            try { return File.ReadAllText(HostsPath()).Contains(Marker, StringComparison.Ordinal); }
            catch { return false; }
        }

        private static string HostsPath()
        {
            var sys = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            return Path.Combine(sys, "drivers", "etc", "hosts");
        }
        public static (bool Ok, string Message) Apply()
        {
            try
            {
                var path = HostsPath();
                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
                if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
                lines.Add(Marker);
                lines.AddRange(Domains.Select(d => $"127.0.0.1 {d}"));
                File.WriteAllLines(path, lines);
                FlushDns();
                try { SinkholeService.Instance.EnsureRunningForCurrentPins(); }
                catch { /* sinkhole is best-effort; pins still deny silently */ }
                return (true, $"{Domains.Length} Microsoft telemetry endpoint(s) pinned to 127.0.0.1 and the " +
                              "DNS cache flushed. DiagTrack's uploads now knock on AutoCommand's sinkhole — " +
                              "watch this card and the Black Hole tab for the receipts.");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "The hosts file refused the write — AutoCommand must run as administrator to pin endpoints.");
            }
            catch (Exception ex)
            {
                return (false, "Pin failed: " + ex.Message);
            }
        }

        public static (bool Ok, string Message) Remove()
        {
            try
            {
                var path = HostsPath();
                if (!File.Exists(path)) return (true, "No hosts file changes to remove.");
                var lines = File.ReadAllLines(path).ToList();
                int ix = lines.FindIndex(l => l.Trim() == Marker);
                if (ix < 0) return (true, "No DiagTrack pin present.");
                lines.RemoveAt(ix);
                while (ix < lines.Count && lines[ix].Trim().Length > 0 &&
                       Domains.Any(d => lines[ix].Contains(d, StringComparison.OrdinalIgnoreCase)))
                    lines.RemoveAt(ix);
                File.WriteAllLines(path, lines);
                FlushDns();
                return (true, "DiagTrack pins removed — telemetry endpoints resolve normally again.");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "The hosts file refused the write — AutoCommand must run as administrator.");
            }
            catch (Exception ex)
            {
                return (false, "Removal failed: " + ex.Message);
            }
        }

        /// <summary>Receipts: denied sinkhole attempts against the pinned
        /// domains, from the same proof feed the Black Hole tab shows.</summary>
        public static string ReceiptsLine()
        {
            try
            {
                var recent = TelemetryProofService.Instance.Recent
                    .Where(a => Domains.Any(d => a.Hostname != null &&
                                                 a.Hostname.Contains(d, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                return recent.Count == 0
                    ? "🔇 Quieted — DiagTrack has not knocked since the pin went in."
                    : $"🔇 Quieted — DiagTrack knocked {recent.Count} time(s) since the pin; all denied, nothing left this machine (last: {recent[^1].TimeUtc.ToLocalTime():MM-dd HH:mm}).";
            }
            catch
            {
                return "🔇 Quieted — receipts unavailable right now (sinkhole not running).";
            }
        }

        private static void FlushDns()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch { /* pin applies on next resolution regardless */ }
        }
    }
}
