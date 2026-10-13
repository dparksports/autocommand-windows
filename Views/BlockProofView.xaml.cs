using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// Block Proof tab — the visible half of the telemetry sinkhole. Shows the
    /// current hosts pins and sinkhole state, a toggle that flips the pins
    /// between silent (0.0.0.0) and logging (127.0.0.1) mode, cumulative
    /// denied-attempt counters, and a live feed of every attempt caught with
    /// process, PID and the hostname from the TLS hello.
    /// </summary>
    public partial class BlockProofView : UserControl
    {
        private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(5) };
        private bool _toggling;

        private sealed class AttemptRow
        {
            public string TimeLocal { get; set; }
            public string Hostname { get; set; }
            public string ProcessName { get; set; }
            public int Pid { get; set; }
            public string DeniedBy { get; set; }
            public string CommandLine { get; set; }
            public string ProcessImage { get; set; }
            public string PeerEndpoint { get; set; }
        }

        private sealed class HostnameTotal
        {
            public string Hostname { get; set; }
            public int Count { get; set; }
        }

        public BlockProofView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            TelemetryProofService.Instance.Changed += OnProofChanged;
            SinkholeService.Instance.StatusChanged += OnProofChanged;
            _refresh.Tick += (_, _) => RefreshState();
            _refresh.Start();
            RefreshAll();
        }

        private void OnProofChanged()
        {
            if (!Dispatcher.CheckAccess()) Dispatcher.BeginInvoke(RefreshAll);
            else RefreshAll();
        }

        // ── State refresh ─────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshState();
            RefreshFeed();
            RefreshTotals();
        }

        private void RefreshState()
        {
            var pins = HostsTelemetryPins.ReadLive();

            if (pins.Count == 0)
            {
                SinkholeStatusText.Text = "No telemetry pins in the hosts file.";
                SinkholeDetailText.Text =
                    "Pins are created by the telemetry opt-out flow (Shields Up's Telemetry tab, or this app's " +
                    "TelemetryRulesService) when you choose \"Block network instead\". There is nothing to prove yet.";
                SinkholeToggle.IsEnabled = false;
                SinkholeToggle.IsChecked = false;
                PinListText.Text = "";
                return;
            }

            bool sinkhole = HostsTelemetryPins.AnySinkhole(pins);
            SinkholeToggle.IsEnabled = true;
            SinkholeToggle.IsChecked = sinkhole;

            string sink = SinkholeService.Instance.Status switch
            {
                SinkholeStatus.Listening => "listening on 127.0.0.1:443",
                SinkholeStatus.PortBusy => "port 443 busy — see below",
                SinkholeStatus.Error => "error",
                _ => "not running",
            };
            SinkholeStatusText.Text = sinkhole
                ? $"Sinkhole active — {sink}."
                : "Pins are silent (0.0.0.0) — attempts fail invisibly.";
            SinkholeDetailText.Text = sinkhole
                ? SinkholeService.Instance.StatusDetail ??
                  "Every attempt completes a handshake to this app, is logged with its process and intended hostname, then dropped. Nothing is ever sent back."
                : "Turn on sinkhole mode to make each attempt visible: the pin points at 127.0.0.1 and a local listener logs who knocked, then refuses to answer.";

            PinListText.Text = "Pinned: " + string.Join("   ·   ",
                pins.Select(p => $"{p.Hostname} → {p.Address}"));
        }

        private void RefreshTotals()
        {
            var proof = TelemetryProofService.Instance;
            if (proof.TotalDenied == 0)
            {
                TotalsText.Text = "No attempts recorded yet.";
                HostnameTotals.ItemsSource = null;
                return;
            }

            TotalsText.Text =
                $"{proof.TotalDenied:N0} attempt{(proof.TotalDenied == 1 ? "" : "s")} denied since {proof.FirstAttemptUtc?.ToLocalTime():yyyy-MM-dd HH:mm}" +
                $" · last at {proof.LastAttemptUtc?.ToLocalTime():HH:mm:ss}";

            HostnameTotals.ItemsSource = proof.TotalsByHostname()
                .Select(t => new HostnameTotal { Hostname = t.Hostname, Count = t.Count })
                .ToList();
        }

        private void RefreshFeed()
        {
            var rows = TelemetryProofService.Instance.Recent
                .Select(a => new AttemptRow
                {
                    TimeLocal = a.TimeUtc.ToLocalTime().ToString("MM-dd HH:mm:ss"),
                    Hostname = a.Hostname,
                    ProcessName = TelemetryProofService.ProcessKey(a),
                    Pid = a.Pid,
                    DeniedBy = a.DeniedBy,
                    CommandLine = a.CommandLine,
                    ProcessImage = a.ProcessImage,
                    PeerEndpoint = a.PeerEndpoint,
                })
                .ToList();
            AttemptsGrid.ItemsSource = rows;
            if (rows.Count > 0) AttemptsGrid.ScrollIntoView(rows[^1]);
        }

        // ── Sinkhole toggle ───────────────────────────────────────────────

        private async void SinkholeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_toggling) return;
            var toggle = (ToggleButton)sender;
            bool enable = toggle.IsChecked == true;

            if (enable && !await ConfirmSinkholeAsync())
            {
                toggle.IsChecked = false;
                return;
            }

            _toggling = true;
            try
            {
                string address = enable ? HostsTelemetryPins.SinkholeAddress : HostsTelemetryPins.SilentAddress;
                string path = HostsTelemetryPins.HostsPath();
                string text = File.ReadAllText(path);
                string updated = HostsTelemetryPins.SetPinAddress(text, address);
                File.WriteAllText(path, updated);           // UTF-8 without BOM, like every other writer here
                FlushDns();
                if (enable) SinkholeService.Instance.EnsureRunningForCurrentPins();
                else SinkholeService.Instance.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update the hosts file: {ex.Message}\n\nThe pins were left unchanged.",
                    "Sinkhole", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _toggling = false;
                RefreshAll();
            }
        }

        private Task<bool> ConfirmSinkholeAsync() => Task.Run(() =>
        {
            bool ok = false;
            Dispatcher.Invoke(() =>
            {
                ok = MessageBox.Show(
                    "Turn on sinkhole mode?\n\n" +
                    "The telemetry pins will point at 127.0.0.1 instead of 0.0.0.0, and AutoCommand listens on " +
                    "loopback port 443 so every attempt is logged — process, PID, and the hostname from the TLS " +
                    "hello — then dropped with no reply. Nothing leaves the machine either way.\n\n" +
                    "Two things to know:\n" +
                    "• Proof is collected while AutoCommand is running; with the app closed, attempts to the pin " +
                    "still fail instantly (nothing is listening), just unlogged\n" +
                    "• If another local server already owns 127.0.0.1:443 (local HTTPS dev), the sinkhole stays off " +
                    "and the toggle will say so",
                    "Sinkhole mode", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
            });
            return ok;
        });

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
            catch { /* best-effort — the pin applies on next resolution regardless */ }
        }

        // ── Feed interactions ─────────────────────────────────────────────

        private void AttemptsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AttemptsGrid.SelectedItem is not AttemptRow row)
            {
                DetailText.Text = "";
                return;
            }
            DetailText.Text =
                $"{row.ProcessImage ?? "(process image unavailable)"} (pid {row.Pid}) attempted {row.Hostname} " +
                $"from {row.PeerEndpoint} at {row.TimeLocal} — denied at sinkhole, no reply sent.\n" +
                $"Command line: {row.CommandLine ?? "(unavailable)"}";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var rows = TelemetryProofService.Instance.Recent;
            if (rows.Count == 0)
            {
                MessageBox.Show("Nothing recorded yet — there is no proof to export.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export denied-telemetry proof",
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"telemetry-block-proof-{DateTime.Now:yyyy-MM-dd-HHmm}.csv"
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Time (local),Hostname,Process,PID,DeniedBy,PeerEndpoint,ProcessImage,CommandLine");
            foreach (var a in rows)
            {
                sb.AppendLine(string.Join(",",
                    new[] {
                        a.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        a.Hostname, TelemetryProofService.ProcessKey(a), a.Pid.ToString(), a.DeniedBy,
                        a.PeerEndpoint, a.ProcessImage, a.CommandLine
                    }.Select(CsvEscape)));
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Exported {rows.Count} rows to\n{dialog.FileName}", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Delete the denied-attempt history (telemetry_proof.jsonl)?\n\nThe hosts pins and the sinkhole " +
                    "itself are untouched — only the record of past attempts is cleared.",
                    "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            TelemetryProofService.Instance.Clear();
            RefreshAll();
        }

        private static string CsvEscape(string value)
        {
            value ??= "";
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
