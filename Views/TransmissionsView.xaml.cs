using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AutoCommand.Models;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// Transmissions tab: the visible half of the pipeline. Every finalized
    /// TLS conversation becomes a row (decrypted content, or the honest
    /// "encrypted (no keys)" status), every decoded HTTP message is
    /// selectable and viewable as pretty / text / hex / headers, and the
    /// full untruncated bodies live in the local archive with a SHA-256 so
    /// "what was sent" stays provable after the fact.
    ///
    /// Watching deliberately outlives tab navigation (same recorder rule as
    /// the Spawns feeds); it stops on Stop Watch or app exit.
    /// </summary>
    public partial class TransmissionsView : UserControl, IAiAuditable
    {
        private const int RowCap = 5000;

        private readonly ObservableCollection<TransmissionRecord> _rows = new();
        private readonly Dictionary<string, TransmissionRecord> _rowById = new();

        private TransmissionsPipelineService _pipeline;
        private bool _initialized;
        private bool _watching;
        private bool _backfilled;
        private Regex _filter;
        private bool _bigOnly = true;

        private TransmissionRecord _selected;
        private List<HttpStreamDecoder.HttpMessage> _messages = new();
        private DateTime _archiveStatusAt = DateTime.MinValue;
        private (int Days, long Bytes) _archiveStatus;

        private const int TextCapBytes = 512 * 1024;
        private const int HexCapBytes = 256 * 1024;

        public TransmissionsView()
        {
            InitializeComponent();
            FlowGrid.ItemsSource = _rows;

            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(_rows);
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                nameof(TransmissionRecord.TsUtc), System.ComponentModel.ListSortDirection.Descending));
            view.Filter = FilterRow;
        }

        // -----------------------------------------------------------------------
        // Lifecycle — lazy start on first show, keeps watching afterwards
        // -----------------------------------------------------------------------

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                _initialized = true;
                InitializeServices();
            }
            if (!_watching) StartWatching();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Deliberately keeps watching (recorder rule — see class doc).
        }

        public bool IsWatching => _watching;

        private void InitializeServices()
        {
            _pipeline = TransmissionsPipelineService.Shared;
            _pipeline.OnRecord += OnRecord;
            _pipeline.OnError += ShowError;

            SystemKeylogCheck.IsChecked = TransmissionKeyService.IsSystemKeylogEnabled();
            SystemKeylogCheck.ToolTip = TransmissionKeyService.CoverageNote;
            BigOnlyCheck.IsChecked = _bigOnly;
        }

        private void StartWatching()
        {
            _pipeline.Start();
            _watching = true;
            ToggleWatchBtn.Content = "Stop Watch";
            UpdateStatus();
            BackfillArchive();
        }

        private void StopWatching()
        {
            _pipeline.Stop();
            _watching = false;
            ToggleWatchBtn.Content = "Start Watch";
            UpdateStatus();
        }

        private void ToggleWatchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_watching) StopWatching();
            else StartWatching();
        }

        /// <summary>Previous sessions' archived rows load behind the live
        /// feed so "what did this machine send yesterday" is answered in the
        /// same grid.</summary>
        private void BackfillArchive()
        {
            if (_backfilled) return;
            _backfilled = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                var archived = TransmissionsArchiveService.Read(max: 500);
                Dispatcher.BeginInvoke(() =>
                {
                    foreach (var record in archived) // already newest-first
                        AddRow(record);
                    if (archived.Count > 0)
                        StatusText.Text = $"Loaded {archived.Count} archived transmission(s) from previous sessions";
                });
            });
        }

        private void ShowError(string message)
        {
            Dispatcher.Invoke(() => StatusText.Text = $"⚠ {message}");
        }

        // -----------------------------------------------------------------------
        // Rows
        // -----------------------------------------------------------------------

        private void OnRecord(TransmissionRecord record)
        {
            Dispatcher.BeginInvoke(() => AddRow(record));
        }

        private void AddRow(TransmissionRecord record)
        {
            if (_rowById.ContainsKey(record.Id)) return;
            _rowById[record.Id] = record;
            _rows.Add(record);
            while (_rows.Count > RowCap)
            {
                var oldest = _rows[^1];
                _rows.RemoveAt(_rows.Count - 1);
                if (_rowById.TryGetValue(oldest.Id, out var row) && row == oldest) _rowById.Remove(oldest.Id);
            }
            UpdateStatus();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _rows.Clear();
            _rowById.Clear();
            _selected = null;
            _messages = new();
            DetailRecordPanel.Children.Clear();
            MessageCombo.ItemsSource = null;
            ContentText.Text = string.Empty;
            ContentFooter.Text = string.Empty;
            UpdateStatus();
        }

        // -----------------------------------------------------------------------
        // Filter
        // -----------------------------------------------------------------------

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string pattern = FilterBox.Text.Trim();
            try
            {
                _filter = pattern.Length > 0
                    ? new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
                    : null;
            }
            catch (ArgumentException) { /* mid-typing: keep the last valid filter */ }
            if (FlowGrid != null)
                CollectionViewSource.GetDefaultView(_rows).Refresh();
        }

        private void ApplyFilter(object sender, RoutedEventArgs e)
        {
            _bigOnly = BigOnlyCheck.IsChecked == true;
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }

        private bool FilterRow(object obj)
        {
            if (obj is not TransmissionRecord record) return false;
            if (_bigOnly && !record.BigUpload) return false;
            if (_filter == null) return true;
            return _filter.IsMatch($"{record.Image} {record.Sni} {record.RemoteIp} {record.Provider} {record.Status}");
        }

        // -----------------------------------------------------------------------
        // Detail pane
        // -----------------------------------------------------------------------

        private void FlowGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderDetail(FlowGrid.SelectedItem as TransmissionRecord);
        }

        private void FlowGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(FlowGrid, source) is DataGridRow row)
                row.IsSelected = true;
        }

        private void CopyRowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (FlowGrid.SelectedItem is TransmissionRecord record)
                Clipboard.SetText(JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void RenderDetail(TransmissionRecord record)
        {
            DetailRecordPanel.Children.Clear();
            _selected = record;
            _messages = new();
            MessageCombo.ItemsSource = null;

            if (record == null)
            {
                ContentText.Text = string.Empty;
                ContentFooter.Text = string.Empty;
                return;
            }

            AddDetail("Time (local)", record.TsUtc == default ? "—" : record.TsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
            AddDetail("PID", record.Pid > 0 ? record.Pid.ToString() : "unattributed");
            AddDetail("Image", record.Image ?? "—");
            AddDetail("Destination", record.ServerEndpoint);
            AddDetail("Host (SNI)", record.HostDisplay);
            AddDetail("Provider", record.ProviderAndCountry);
            AddDetail("TLS", $"{record.TlsVersion ?? "?"}{(string.IsNullOrEmpty(record.Alpn) ? "" : $" · ALPN {record.Alpn}")}");
            AddDetail("Cipher", record.CipherSuite ?? "—");
            AddDetail("Wire bytes", $"{record.UpDisplay} up / {record.DownDisplay} down");
            AddDetail("Decrypted app bytes", record.AppBytesDisplay);
            AddDetail("Status", record.Status);
            AddDetail("Messages", record.MessageCount.ToString());
            AddDetail("Content kinds", string.IsNullOrEmpty(record.ContentKinds) ? "—" : record.ContentKinds);
            AddDetail("SHA-256 (both directions)", record.Sha256 ?? "(no decrypted content)");

            if (!record.Decrypted)
            {
                ContentText.Text = record.Status.Contains("no keys")
                    ? "Encrypted conversation — this app did not write TLS session keys.\n\n" +
                      "To see its contents: launch it with 🚀 Launch under watch…, or enable 🔑 System-wide key capture\n" +
                      "(works for Chromium/Electron apps, curl, git, OpenSSL- and Python-based tools; Schannel-based\n" +
                      "system components never write keys)."
                    : $"No content to show — {record.Status}.";
                ContentFooter.Text = $"Wire evidence retained: {record.UpDisplay} up, {record.DownDisplay} down to {record.ServerEndpoint}.";
                return;
            }

            byte[] request, response;
            if (!_pipeline.TryGetBodies(record.Id, out request, out response) && record.HasContent)
            {
                var loaded = _pipeline.LoadFromArchive(record);
                request = loaded.Request;
                response = loaded.Response;
            }

            var messages = new List<HttpStreamDecoder.HttpMessage>();
            messages.AddRange(HttpStreamDecoder.Decode(request, fromClient: true, alpn: record.Alpn));
            messages.AddRange(HttpStreamDecoder.Decode(response, fromClient: false, alpn: record.Alpn));
            _messages = messages;

            MessageCombo.ItemsSource = messages.Select(m => m.Display).ToList();
            MessageCombo.SelectedIndex = messages.Count > 0 ? 0 : -1;
            if (messages.Count == 0)
            {
                ContentText.Text = "Decrypted, but the plaintext carried no parseable bytes.";
                ContentFooter.Text = $"App bytes: {record.ClientAppBytes} up / {record.ServerAppBytes} down.";
            }
        }

        private void AddDetail(string label, string value)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = FindResource("TextSecondaryBrush") as Brush,
                FontSize = 10
            };
            var valueBlock = new TextBlock
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                ToolTip = value
            };
            panel.Children.Add(labelBlock);
            panel.Children.Add(valueBlock);
            DetailRecordPanel.Children.Add(panel);
        }

        // -----------------------------------------------------------------------
        // Content viewer
        // -----------------------------------------------------------------------

        private void MessageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentText == null) return; // during InitializeComponent
            RenderContent();
        }

        private void ViewModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentText == null || MessageCombo == null) return;
            RenderContent();
        }

        private void RenderContent()
        {
            if (MessageCombo == null || ContentText == null) return;
            int index = MessageCombo.SelectedIndex;
            if (index < 0 || index >= _messages.Count)
            {
                if (_selected != null && _selected.Decrypted)
                    ContentText.Text = "Select a message to view its content.";
                return;
            }
            var message = _messages[index];
            byte[] body = message.Body ?? Array.Empty<byte>();

            string text = ViewModeCombo.SelectedIndex switch
            {
                3 => message.HeaderText(),
                2 => HexDump(body, HexCapBytes),
                1 => AsText(body, TextCapBytes),
                _ => Pretty(body, message.Kind),
            };
            ContentText.Text = text;

            var footer = new StringBuilder();
            footer.Append(message.FromClient ? "↑ sent by this machine" : "↓ received");
            footer.Append($" · {body.Length:N0} byte body");
            if (message.ContentEncoding != null) footer.Append($" · {message.ContentEncoding} decoded");
            if (message.Chunked) footer.Append(" · chunked decoded");
            if (!message.IsHttp) footer.Append(" · non-HTTP stream (raw bytes shown)");
            if (body.Length > (ViewModeCombo.SelectedIndex == 2 ? HexCapBytes : TextCapBytes))
                footer.Append($" · view truncated, full body on disk / 💾 Save body");
            if (_selected?.Sha256 != null) footer.Append($" · flow SHA-256 {_selected.Sha256[..16]}…");
            ContentFooter.Text = footer.ToString();
        }

        private static string Pretty(byte[] body, string kind)
        {
            if (body.Length == 0) return "(empty body)";
            if (kind == "json")
            {
                try
                {
                    var node = System.Text.Json.Nodes.JsonNode.Parse(body);
                    if (node != null)
                        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }
                catch { /* fall through to raw text */ }
            }
            if (kind == "form")
                return UrlDecodeForm(AsText(body, TextCapBytes));
            return AsText(body, TextCapBytes);
        }

        private static string UrlDecodeForm(string form)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var pair in form.Split('&', ';'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq < 0) { sb.AppendLine(pair); continue; }
                    sb.Append(Uri.UnescapeDataString(pair[..eq].Replace('+', ' ')))
                      .Append(" = ")
                      .AppendLine(Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' ')));
                }
                return sb.ToString();
            }
            catch { return form; }
        }

        private static string AsText(byte[] data, int cap)
        {
            int take = Math.Min(data.Length, cap);
            var sb = new StringBuilder(take);
            for (int i = 0; i < take; i++)
            {
                char c = (char)data[i];
                sb.Append(c == '\r' || c == '\n' || (c >= 32 && c < 127) || c == '\t' ? c : '·');
            }
            if (data.Length > take) sb.Append($" … (+{data.Length - take} B)");
            return sb.ToString();
        }

        private static string HexDump(byte[] data, int cap)
        {
            int take = Math.Min(data.Length, cap);
            var sb = new StringBuilder(take * 4);
            for (int i = 0; i < take; i += 16)
            {
                sb.Append(i.ToString("X6")).Append("  ");
                for (int j = 0; j < 16; j++)
                    sb.Append(i + j < take ? data[i + j].ToString("X2") : "  ").Append(' ');
                sb.Append(' ');
                for (int j = 0; j < 16 && i + j < take; j++)
                {
                    char c = (char)data[i + j];
                    sb.Append(c >= 32 && c < 127 ? c : '.');
                }
                sb.AppendLine();
            }
            if (data.Length > take) sb.AppendLine($"… (+{data.Length - take} B)");
            return sb.ToString();
        }

        private void SaveBodyBtn_Click(object sender, RoutedEventArgs e)
        {
            int index = MessageCombo?.SelectedIndex ?? -1;
            if (index < 0 || index >= _messages.Count)
            {
                MessageBox.Show("Select a message first.", "Save body", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var message = _messages[index];
            byte[] body = message.Body ?? Array.Empty<byte>();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "All files (*.*)|*.*",
                FileName = $"transmission-{(_selected?.Id[..12] ?? "body")}-{(message.FromClient ? "request" : "response")}.bin"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                File.WriteAllBytes(dialog.FileName, body);
                MessageBox.Show($"Saved {body.Length:N0} byte(s) to:\n{dialog.FileName}",
                    "Save body", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Save body", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -----------------------------------------------------------------------
        // Export
        // -----------------------------------------------------------------------

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var visible = _rows
                .Cast<TransmissionRecord>()
                .Where(FilterRow)
                .ToList();
            if (visible.Count == 0)
            {
                MessageBox.Show("Nothing to export yet.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSONL files (*.jsonl)|*.jsonl|All files (*.*)|*.*",
                FileName = $"transmissions-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = false };
                using var writer = new StreamWriter(dialog.FileName, append: false);
                foreach (var row in visible) // newest-first, as displayed
                    writer.WriteLine(JsonSerializer.Serialize(row, options));
                MessageBox.Show($"Exported {visible.Count} transmission row(s) (metadata) to:\n{dialog.FileName}",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -----------------------------------------------------------------------
        // Key acquisition
        // -----------------------------------------------------------------------

        private void SystemKeylogCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_pipeline == null) return; // event during InitializeComponent
            bool want = SystemKeylogCheck.IsChecked == true;
            bool current = TransmissionKeyService.IsSystemKeylogEnabled();
            if (want == current) return;

            try
            {
                if (want)
                {
                    var confirm = MessageBox.Show(
                        "Turn on system-wide TLS key capture?\n\n" +
                        "SSLKEYLOGFILE will point at AutoCommand's key file (current-user-only ACL,\n" +
                        "rotated at 5 MB). Apps that honor it — Chromium/Electron browsers and apps,\n" +
                        "curl, git, OpenSSL- and Python-based tools — write TLS session keys from their\n" +
                        "next launch on, and the Transmissions tab shows their traffic decrypted.\n\n" +
                        "The key file decrypts your own TLS traffic: it stays local, and turning this\n" +
                        "off wipes it. Windows system components using Schannel ignore the variable —\n" +
                        "their flows keep the 'encrypted (no keys)' status.",
                        "System-wide key capture", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes)
                    {
                        SystemKeylogCheck.IsChecked = false;
                        return;
                    }
                    TransmissionKeyService.EnableSystemKeylog();
                }
                else
                {
                    var confirm = MessageBox.Show(
                        "Turn off system-wide TLS key capture?\n\n" +
                        "The environment variable is removed and the key file is wiped — apps launched\n" +
                        "afterwards stop writing TLS secrets, so their future traffic cannot be\n" +
                        "decrypted here (existing captures stay ciphertext).",
                        "System-wide key capture", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes)
                    {
                        SystemKeylogCheck.IsChecked = true;
                        return;
                    }
                    TransmissionKeyService.DisableSystemKeylog();
                }
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "System-wide key capture", MessageBoxButton.OK, MessageBoxImage.Warning);
                SystemKeylogCheck.IsChecked = TransmissionKeyService.IsSystemKeylogEnabled();
            }
        }

        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a program to launch under watch",
                Filter = "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            string arguments = PromptForArguments(dialog.FileName, Window.GetWindow(this));
            try
            {
                var proc = TransmissionKeyService.LaunchUnderWatch(dialog.FileName, arguments, null);
                StatusText.Text = $"Launched '{Path.GetFileName(dialog.FileName)}' (PID {proc?.Id}) under watch — " +
                                  $"its TLS traffic decrypts as soon as it connects.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Launch failed: {ex.Message}", "Launch under watch",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>Minimal arguments prompt — built in code so the tab
        /// stays one file.</summary>
        private static string PromptForArguments(string exePath, Window owner)
        {
            var window = new Window
            {
                Title = "🚀 Launch under watch",
                Width = 520,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = (Application.Current?.TryFindResource("SurfaceBrush") as Brush) ?? SystemColors.ControlBrush
            };
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = $"Launching:\n{exePath}\n\nSSLKEYLOGFILE will be set for this program only.",
                TextWrapping = TextWrapping.Wrap
            });
            var box = new TextBox
            {
                Margin = new Thickness(0, 10, 0, 10),
                FontFamily = new FontFamily("Consolas")
            };
            panel.Children.Add(box);
            var launch = new Button
            {
                Content = "Launch",
                Width = 90,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };
            string arguments = null;
            launch.Click += (_, _) =>
            {
                arguments = box.Text;
                window.Close();
            };
            panel.Children.Add(launch);
            window.Content = panel;
            box.Focus();
            window.ShowDialog();
            return arguments ?? "";
        }

        // -----------------------------------------------------------------------
        // Status + audit
        // -----------------------------------------------------------------------

        private void UpdateStatus()
        {
            if (StatusText == null || _pipeline == null) return;
            if (!_watching)
            {
                StatusText.Text = "Watcher is stopped.";
                return;
            }
            // Archive size walks the disk — cache it briefly instead of on
            // every incoming row.
            if (DateTime.UtcNow - _archiveStatusAt > TimeSpan.FromSeconds(10))
            {
                _archiveStatusAt = DateTime.UtcNow;
                _archiveStatus = TransmissionsArchiveService.StatusSummary();
            }
            var (days, bytes) = _archiveStatus;
            string size = bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F0} MB" : $"{bytes / 1024.0:F0} KB";
            StatusText.Text =
                $"Watching: {_rows.Count} transmission(s) · {_pipeline.DecryptedCount} decrypted · " +
                $"{_pipeline.EncryptedNoKeysCount} encrypted-no-keys · queue {_pipeline.LiveQueueDepth}" +
                (days > 0 ? $" · archive {days} day(s), {size} on disk" : "") +
                (_pipeline.DroppedCount > 0 ? $" · {_pipeline.DroppedCount} dropped (busy)" : "");
        }

        /// <summary>Agent-facing summary — metadata only, never bodies
        /// (credentials and tokens ride inside decrypted content).</summary>
        public string GetAuditContext()
        {
            var rows = _rows.Take(20).ToList();
            if (rows.Count == 0)
                return "Transmissions tab is currently empty. No TLS conversations recorded this session.";
            var sb = new StringBuilder();
            sb.AppendLine($"Recent transmissions (of {_rows.Count} recorded, metadata only):");
            foreach (var row in rows)
            {
                sb.AppendLine($"- {row.TimeDisplay} PID {row.Pid} ({row.ProcessDisplay}) → {row.ServerEndpoint} " +
                              $"[{row.HostDisplay}] {row.ProviderAndCountry} — {row.Status}, " +
                              $"{row.UpDisplay} up / {row.DownDisplay} down" +
                              (row.BigUpload ? " [>10KB upload]" : ""));
            }
            return sb.ToString();
        }
    }
}
