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
using System.Windows.Threading;
using AutoCommand.Models;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// Taskhostw Spawns tab: the same Sysmon spawn tracing the Python tab
    /// uses (ImageSpawnWatcherService with the taskhostw filter), answering
    /// who/what/when — with the "why" being the scheduled task. There is no
    /// instrumentation story here (no audit hook, no TLS): taskhostw is a
    /// Windows binary, so the attribution pane joins TaskAttributionService
    /// data to each row instead.
    /// </summary>
    public partial class TaskHostSpawnView : UserControl, IAiAuditable
    {
        private const int RowCap = 3000;

        private readonly ObservableCollection<SpawnItem> _rows = new();
        private readonly Dictionary<int, SpawnItem> _rowByPid = new();

        private ImageSpawnWatcherService _watcher;
        private TaskAttributionService _taskAttribution;

        private bool _initialized;
        private bool _watching;
        private bool _paused;
        private readonly Queue<SpawnItem> _pauseBuffer = new();
        private Regex _filter;
        private SpawnItem _selected;
        private DispatcherTimer _attributionTimer;

        public TaskHostSpawnView()
        {
            InitializeComponent();
            SpawnGrid.ItemsSource = _rows;

            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(_rows);
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                nameof(SpawnItem.TimeUtc), System.ComponentModel.ListSortDirection.Descending));
            view.Filter = FilterRow;
        }

        // -----------------------------------------------------------------------
        // Lifecycle: lazy start on first tab selection, stop on tab leave
        // -----------------------------------------------------------------------

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                _initialized = true;
                _watcher = new ImageSpawnWatcherService(ImageSpawnWatcherService.IsTaskhostwImage, "taskhostw");
                _watcher.OnError += ShowError;
                _watcher.OnSpawn += OnSpawn;

                _taskAttribution = new TaskAttributionService();
                _taskAttribution.OnError += _ => { }; // attribution gaps are non-fatal

                // Task attribution lags the spawn event by seconds (separate
                // event log); re-poll rows still missing their task name.
                _attributionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                _attributionTimer.Tick += (s, _) => EnrichUnattributed();
            }
            if (!_watching) StartWatching();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e) => StopWatching();

        private void StartWatching()
        {
            _watcher.Start();
            _taskAttribution.Start();
            _attributionTimer.Start();
            _watching = true;
            ToggleWatchBtn.Content = "Stop Watch";
            UpdateStatus();
        }

        private void StopWatching()
        {
            _watcher.Stop();
            _taskAttribution.Stop();
            _attributionTimer.Stop();
            _watching = false;
            ToggleWatchBtn.Content = "Start Watch";
            UpdateStatus();
        }

        private void ToggleWatchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_watching) StopWatching();
            else StartWatching();
        }

        private void ShowError(string message) => StatusText.Text = $"⚠ {message}";

        // -----------------------------------------------------------------------
        // Rows + task attribution
        // -----------------------------------------------------------------------

        private void OnSpawn(SpawnItem item)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_paused)
                {
                    _pauseBuffer.Enqueue(item);
                    UpdateStatus();
                    return;
                }
                AddRow(item);
            });
        }

        private void AddRow(SpawnItem item)
        {
            _rowByPid[item.Pid] = item;
            _rows.Insert(0, item);
            while (_rows.Count > RowCap)
            {
                var oldest = _rows[^1];
                _rows.RemoveAt(_rows.Count - 1);
                if (_rowByPid.TryGetValue(oldest.Pid, out var row) && row == oldest) _rowByPid.Remove(oldest.Pid);
            }
            TrySetTaskBadge(item);
            UpdateStatus();
        }

        /// <summary>Resolves the task behind the spawn; badges the row with the task's short name.</summary>
        private void TrySetTaskBadge(SpawnItem item)
        {
            if (!string.IsNullOrEmpty(item.Badge)) return;
            var info = _taskAttribution.GetTaskForPid(item.Pid);
            if (info == null || string.IsNullOrEmpty(info.TaskPath)) return;

            item.SetDetailBadge(ShortTaskName(info.TaskPath));
            if (_selected != null && _selected.Pid == item.Pid) RenderTaskPane(item);
        }

        private void EnrichUnattributed()
        {
            foreach (var item in _rows.Take(300))
                TrySetTaskBadge(item);
        }

        private static string ShortTaskName(string taskPath)
        {
            // "\Microsoft\Windows\Maintenance\WinSAT" → "WinSAT"
            string trimmed = taskPath.TrimStart('\\');
            int slash = trimmed.LastIndexOf('\\');
            return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
        }

        // -----------------------------------------------------------------------
        // Toolbar
        // -----------------------------------------------------------------------

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            _paused = !_paused;
            PauseBtn.Content = _paused ? "▶ Resume" : "⏸ Pause";
            if (!_paused)
                while (_pauseBuffer.Count > 0)
                    AddRow(_pauseBuffer.Dequeue());
            UpdateStatus();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _rows.Clear();
            _rowByPid.Clear();
            _pauseBuffer.Clear();
            _selected = null;
            DetailRecordPanel.Children.Clear();
            ResetTaskPane();
            UpdateStatus();
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0)
            {
                MessageBox.Show("Nothing to export yet.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSONL files (*.jsonl)|*.jsonl|All files (*.*)|*.*",
                FileName = $"taskhostw-spawns-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = false };
                using var writer = new StreamWriter(dialog.FileName, append: false);
                foreach (var row in _rows)
                    writer.WriteLine(JsonSerializer.Serialize(row, options));
                MessageBox.Show($"Exported {_rows.Count} spawn record(s) to:\n{dialog.FileName}",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SpawnGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(SpawnGrid, source) is DataGridRow row)
                row.IsSelected = true;
        }

        private void CopyRowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SpawnGrid.SelectedItem is SpawnItem item)
                Clipboard.SetText(JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void TracePidMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SpawnGrid.SelectedItem is not SpawnItem item || item.Pid <= 0) return;
            var win = new ProcessTraceWindow(item.Pid, "taskhostw", item.Image,
                _taskAttribution.GetTaskForPid(item.Pid))
            {
                Owner = Window.GetWindow(this)
            };
            win.Show();
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
            catch (ArgumentException)
            {
                // Incomplete pattern mid-typing: keep the last valid filter
            }
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }

        private bool FilterRow(object obj)
        {
            if (obj is not SpawnItem item) return false;
            if (_filter == null) return true;
            return _filter.IsMatch($"{item.Badge} {item.CommandLine} {item.ParentCommandLine} {item.Image}");
        }

        // -----------------------------------------------------------------------
        // Detail pane
        // -----------------------------------------------------------------------

        private void SpawnGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = SpawnGrid.SelectedItem as SpawnItem;
            RenderDetail(_selected);
        }

        private void RenderDetail(SpawnItem item)
        {
            DetailRecordPanel.Children.Clear();
            if (item == null)
            {
                ResetTaskPane();
                return;
            }

            AddDetail("Time (local)", item.TimeUtc == default ? "—" : item.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
            AddDetail("PID", item.Pid.ToString());
            AddDetail("Image", item.Image);
            AddDetail("Command line", item.CommandLine);
            AddDetail("Parent", item.ParentDisplay);
            AddDetail("Parent command line", item.ParentCommandLine);
            AddDetail("User", item.User);
            AddDetail("Integrity", item.IntegrityLevel);
            AddDetail("Hashes", item.Hashes);
            AddDetail("Lifecycle", item.LifeDisplay);

            RenderTaskPane(item);
            _ = LoadTaskSummaryAsync(item);
        }

        private void AddDetail(string label, string value)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush,
                FontSize = 10
            };
            var valueBlock = new TextBlock
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                ToolTip = value
            };
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(labelBlock);
            panel.Children.Add(valueBlock);
            DetailRecordPanel.Children.Add(panel);
        }

        private void RenderTaskPane(SpawnItem item)
        {
            var info = _taskAttribution.GetTaskForPid(item.Pid);
            TaskPathText.Text = info?.TaskPath is string path && path.Length > 0 ? path : "—";
            TaskUserText.Text = !string.IsNullOrEmpty(info?.UserContext) ? info.UserContext : "—";

            if (info?.HandlerClassId != null)
            {
                TaskHandlerText.Text = info.HandlerDllExists == true
                    ? $"{info.HandlerDll}  (CLSID {info.HandlerClassId})"
                    : $"CLSID {info.HandlerClassId} — handler DLL not registered (leftover of uninstalled software)";
            }
            else
            {
                TaskHandlerText.Text = "—";
            }

            if (string.IsNullOrEmpty(info?.TaskPath))
                TaskSummaryText.Text = "No task attribution yet — the event logs are still catching up.";
        }

        private async System.Threading.Tasks.Task LoadTaskSummaryAsync(SpawnItem item)
        {
            var info = _taskAttribution.GetTaskForPid(item.Pid);
            if (info == null || string.IsNullOrEmpty(info.TaskPath)) return;

            var (summary, error) = await TaskSchedulerService.Instance.GetTaskSummaryAsync(info.TaskPath);
            if (_selected != item) return; // selection moved on

            TaskSummaryText.Text = string.IsNullOrEmpty(error)
                ? summary ?? "(no task details)"
                : $"Could not read task '{info.TaskPath}': {error}";
        }

        private void ResetTaskPane()
        {
            TaskPathText.Text = "—";
            TaskUserText.Text = "—";
            TaskHandlerText.Text = "—";
            TaskSummaryText.Text = "Select a spawn row — task details appear here as the event logs catch up.";
        }

        // -----------------------------------------------------------------------
        // Status + audit
        // -----------------------------------------------------------------------

        private void UpdateStatus()
        {
            string paused = _paused ? $" · paused ({_pauseBuffer.Count} buffered)" : "";
            int attributed = 0;
            foreach (var row in _rows)
                if (!string.IsNullOrEmpty(row.Badge)) attributed++;
            StatusText.Text = _watching
                ? $"Watching: {_rows.Count} taskhostw spawn(s) · {attributed} attributed to a task{paused}"
                : "Watcher is stopped.";
        }

        public string GetAuditContext()
        {
            var rows = _rows.Take(20).ToList();
            if (rows.Count == 0) return "Taskhostw Spawns tab is currently empty. No taskhostw spawns recorded.";
            var sb = new StringBuilder();
            sb.AppendLine($"Recent taskhostw spawns (of {_rows.Count} recorded):");
            foreach (var row in rows)
            {
                sb.AppendLine($"- {row.TimeDisplay} PID {row.Pid} {(row.Exited ? "[exited]" : "[running]")}");
                sb.AppendLine($"  cmd: {row.CommandLine}");
                sb.AppendLine($"  task: {(string.IsNullOrEmpty(row.Badge) ? "(resolving)" : row.Badge)}");
            }
            return sb.ToString();
        }
    }
}
