using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// "Why is this site slow?" — the user-facing end of the site-diagnosis
    /// pipeline: SiteFingerprintService (what recorded traffic says) +
    /// SiteProbeService (a capped, itemized live check) + SiteVerdictService
    /// (one plain-English answer, with the math one expander away).
    ///
    /// House rules, same as every tab: looking changes nothing; the check's
    /// every operation is listed; suggestions are reversible and never
    /// applied by the app.
    /// </summary>
    public partial class SiteDoctorView : UserControl
    {
        private CancellationTokenSource _runCts;
        private List<string> _suggestions = new();

        public SiteDoctorView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSuggestions();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _runCts?.Cancel();
        }

        /// <summary>
        /// Hosts this machine has actually been talking to (from the
        /// Transmissions archive), plus the passive "struggling" hint — the
        /// archive sometimes already shows the dice-roll pattern before any
        /// probe runs.
        /// </summary>
        private void RefreshSuggestions()
        {
            try
            {
                var rows = TransmissionsArchiveService.Read(max: 2000,
                    sinceUtc: DateTime.UtcNow - TimeSpan.FromHours(24));
                _suggestions = SiteFingerprintService.TopHosts(rows, 25);
                HostBox.ItemsSource = _suggestions;

                var all = SiteFingerprintService.AllFingerprints(rows);
                int baseline = SiteFingerprintService.BaselineEdgeCount(all);
                var struggling = all.Values
                    .Where(f => f.Conversations >= SiteFingerprintService.MinConversations)
                    .Select(f => SiteFingerprintService.Fingerprint(
                        rows.Where(r => string.Equals(r.Sni, f.Host, StringComparison.OrdinalIgnoreCase)).ToList(),
                        baseline))
                    .Where(f => f.LooksStruggling)
                    .OrderByDescending(f => f.Conversations)
                    .Take(3)
                    .Select(f => f.Host)
                    .ToList();

                if (struggling.Count > 0)
                {
                    StruggleHintText.Text = "From your recorded traffic, this site behaves unevenly — bursts and address-hopping:";
                    StruggleHintHosts.ItemsSource = struggling;
                    StruggleHint.Visibility = Visibility.Visible;
                }
                else
                {
                    StruggleHint.Visibility = Visibility.Collapsed;
                }
            }
            catch { /* archive unreadable — the box just has no suggestions */ }
        }

        private void StruggleHost_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string host)
            {
                HostBox.Text = host;
                RunBtn_Click(sender, e);
            }
        }

        private async void RunBtn_Click(object sender, RoutedEventArgs e)
        {
            string host = (HostBox.Text ?? "").Trim();
            if (host.Length == 0) return;

            _runCts?.Cancel();
            _runCts = new CancellationTokenSource();
            var ct = _runCts.Token;

            RunBtn.IsEnabled = false;
            VerdictPanel.Visibility = Visibility.Collapsed;
            MathExpander.Visibility = Visibility.Collapsed;
            OpsExpander.Visibility = Visibility.Collapsed;
            ProgressText.Text = "Reading recorded traffic…";

            try
            {
                // Passive half: everything the archive knows about this host.
                var rows = TransmissionsArchiveService.Read(max: 4000,
                    sinceUtc: DateTime.UtcNow - TimeSpan.FromHours(24));
                var siteRows = rows
                    .Where(r => string.Equals(r.Sni, host, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var all = SiteFingerprintService.AllFingerprints(rows);
                var fingerprint = SiteFingerprintService.Fingerprint(siteRows,
                    SiteFingerprintService.BaselineEdgeCount(all));

                // Controls: two other hosts this machine actually uses —
                // apples-to-apples beats a hardcoded benchmark.
                var controls = _suggestions
                    .Where(h => !string.Equals(h, host, StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToList();
                if (controls.Count == 0) controls = new List<string> { "example.com" };

                // Extra edges: the archive's addresses for this host, so the
                // probes cover servers DNS may not hand back right now.
                var extraEdges = fingerprint.Edges.Select(e => e.Ip).ToList();

                var progress = new Progress<string>(line => ProgressText.Text = line);
                var report = await Task.Run(() =>
                    SiteProbeService.Shared.RunAsync(host, extraEdges, controls, progress, ct), ct);

                var verdict = SiteVerdictService.Decide(fingerprint, report);
                ShowVerdict(verdict, report);
                ProgressText.Text =
                    $"Done in {(report.EndedUtc - report.StartedUtc).TotalSeconds:0}s · {report.Operations.Count} operations, all listed below.";
            }
            catch (OperationCanceledException)
            {
                ProgressText.Text = "Check cancelled.";
            }
            catch (ArgumentException ex)
            {
                ProgressText.Text = ex.Message;
            }
            catch (Exception ex)
            {
                ProgressText.Text = $"Check failed: {ex.Message}";
            }
            finally
            {
                RunBtn.IsEnabled = true;
            }
        }

        private void ShowVerdict(SiteVerdictService.Verdict verdict, SiteProbeService.SiteProbeReport report)
        {
            VerdictHeadline.Text = verdict.Headline;
            VerdictNextStep.Text = "→ " + verdict.NextStep;

            (Brush bg, Brush border, Brush fg) = verdict.Kind switch
            {
                SiteVerdictService.VerdictKind.Blocked =>
                    (Hex("#3A1414"), Hex("#E5534B"), Hex("#FF8A80")),
                SiteVerdictService.VerdictKind.HandshakeDropping =>
                    (Hex("#3A2E14"), Hex("#E5A631"), Hex("#E5A631")),
                SiteVerdictService.VerdictKind.Steering =>
                    (Hex("#14243A"), Hex("#4A9EFF"), Hex("#7FB8FF")),
                SiteVerdictService.VerdictKind.FarAway =>
                    (Hex("#2E1A3A"), Hex("#B07CE5"), Hex("#CDA9F0")),
                _ => (Hex("#122A1A"), Hex("#3FB950"), Hex("#7EE2A8")),
            };
            VerdictPanel.Background = bg;
            VerdictPanel.BorderBrush = border;
            VerdictHeadline.Foreground = fg;
            VerdictPanel.Visibility = Visibility.Visible;

            EvidenceList.ItemsSource = verdict.Evidence;
            MathExpander.Visibility = Visibility.Visible;

            OpsList.ItemsSource = report.Operations
                .Select(op => $"{op.AtUtc.ToLocalTime():HH:mm:ss} [{op.Kind}] {op.Detail}")
                .ToList();
            OpsExpander.Visibility = Visibility.Visible;
        }

        private static Brush Hex(string hex) =>
            (Brush)new BrushConverter().ConvertFromString(hex);
    }
}
