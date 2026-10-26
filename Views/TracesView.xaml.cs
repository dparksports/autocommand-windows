using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AutoCommand.Models;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// User-defined tracing projects ("watch this exe and tell me what it
    /// does") — the Traces tab. A project is one JSON file under
    /// %LOCALAPPDATA%\AutoCommand\traces\projects (TraceProjectStore); this
    /// view lists them, renders the selected one through the same card VM
    /// as Phone Home, and watches the directory so hand-edits and dropped-in
    /// definitions appear without a restart. "Copy definition" puts the
    /// project's JSON on the clipboard — sharing a trace is pasting a file.
    /// </summary>
    public partial class TracesView : UserControl
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(60) };
        private FileSystemWatcher _watcher;
        private bool _refreshing;
        private List<TraceProject> _projects = new();

        public TracesView()
        {
            InitializeComponent();
            Loaded += (_, _) => Start();
            _timer.Tick += (_, _) => RefreshSelected();
            _timer.Start();
        }

        private void Start()
        {
            TraceProjectStore.EnsureSeeded();
            LoadProjects();

            try
            {
                Directory.CreateDirectory(TraceProjectStore.ProjectsDirectory);
                _watcher = new FileSystemWatcher(TraceProjectStore.ProjectsDirectory, "*.json")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                };
                _watcher.Changed += (_, _) => Dispatcher.BeginInvoke(LoadProjects);
                _watcher.Created += (_, _) => Dispatcher.BeginInvoke(LoadProjects);
                _watcher.Deleted += (_, _) => Dispatcher.BeginInvoke(LoadProjects);
                _watcher.Renamed += (_, _) => Dispatcher.BeginInvoke(LoadProjects);
            }
            catch { /* hot-reload is a nicety, never a requirement */ }
        }

        private void LoadProjects()
        {
            try
            {
                var selected = (ProjectList.SelectedItem as TraceProject)?.Name;
                _projects = TraceProjectStore.Load();
                ProjectList.ItemsSource = _projects;
                var restore = _projects.FirstOrDefault(p => p.Name == selected) ?? _projects.FirstOrDefault();
                if (restore != null && ProjectList.SelectedItem != restore) ProjectList.SelectedItem = restore;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Project list unreadable: " + ex.Message;
            }
        }

        // ── selected-project card ────────────────────────────────────────

        private TraceMetricsService.Match MatchFor(TraceProject p) =>
            string.IsNullOrWhiteSpace(p.ServiceKey)
                ? TraceMetricsService.Match.ForProcess(string.IsNullOrWhiteSpace(p.MatchProcess) ? "*.exe" : p.MatchProcess)
                : TraceMetricsService.Match.ForService(p.ServiceKey.Trim());

        private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshSelected();

        private void RefreshSelected()
        {
            if (ProjectList.SelectedItem is not TraceProject project || _refreshing) return;
            _refreshing = true;
            StatusText.Text = $"Tracing {project.Name}…";
            var match = MatchFor(project);
            var notes = project.Notes ?? "";
            var name = project.Name;
            Task.Run(() =>
            {
                TraceMetricsService.TraceCardVM vm = null;
                try
                {
                    var snap = TraceMetricsService.Compute(match);
                    vm = TraceMetricsService.BuildCardVM(snap, string.IsNullOrWhiteSpace(notes) ? null : notes);
                }
                catch (Exception ex)
                {
                    vm = new TraceMetricsService.TraceCardVM
                    {
                        Title = name, ChipProcess = "error", Story = "Compute failed: " + ex.Message,
                        MetricsLine = "", DestinationsLine = "", BeaconLine = "", IdentityLine = "",
                        VerdictText = "unavailable", VerdictKind = "unknown",
                    };
                }
                Dispatcher.BeginInvoke(() =>
                {
                    CardHost.ItemsSource = new[] { vm };
                    StatusText.Text = $"Updated {DateTime.Now:HH:mm:ss} · refresh 60 s · {TraceProjectStore.ProjectsDirectory}";
                    _refreshing = false;
                });
            });
        }

        // ── new project ──────────────────────────────────────────────────

        private void NewBtn_Click(object sender, RoutedEventArgs e)
        {
            NewForm.Visibility = Visibility.Visible;
            FormName.Text = "";
            FormProcess.Text = "";
            FormService.Text = "";
            FormNotes.Text = "";
            FormName.Focus();
            if (FormProcess.Items.Count == 0)
                Task.Run(() =>
                {
                    var names = TraceMetricsService.RecentProcessNames(120);
                    Dispatcher.BeginInvoke(() =>
                    {
                        FormProcess.ItemsSource = names;
                        StatusText.Text = $"{names.Count} process name(s) suggested from the last 2 hours";
                    });
                });
        }

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = FormName.Text.Trim();
            string process = FormProcess.Text.Trim();
            string service = FormService.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("Give the project a name.", "New project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (service.Length == 0 && process.Length == 0)
            {
                MessageBox.Show("Set a process name (wildcards allowed) or a service key.", "New project",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_projects.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A project with that name already exists.", "New project",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var project = new TraceProject
            {
                Name = name,
                MatchProcess = process.Length > 0 ? process : "svchost.exe",
                ServiceKey = service,
                Notes = FormNotes.Text.Trim(),
            };
            try
            {
                TraceProjectStore.Save(project);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the project: " + ex.Message, "New project",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            NewForm.Visibility = Visibility.Collapsed;
            LoadProjects();
            ProjectList.SelectedItem = _projects.FirstOrDefault(p => p.Name == name);
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) =>
            NewForm.Visibility = Visibility.Collapsed;

        // ── share / delete ───────────────────────────────────────────────

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is not TraceProject project) return;
            try
            {
                var path = Path.Combine(TraceProjectStore.ProjectsDirectory,
                    TraceProjectStore.SafeFileName(project.Name) + ".json");
                Clipboard.SetText(File.ReadAllText(path));
                StatusText.Text = $"Definition for '{project.Name}' copied — paste it into another machine's " +
                                  "traces\\projects folder (or share the file itself).";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not copy: " + ex.Message, "Copy definition",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is not TraceProject project) return;
            if (MessageBox.Show($"Delete the project '{project.Name}'?\n\nThe JSON file is removed. " +
                                "Nothing measured is deleted — only the watch definition.",
                    "Delete project", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            TraceProjectStore.Delete(project.Name);
            LoadProjects();
        }

        // ── block (process projects only) ────────────────────────────────

        private async void BlockAppBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is not TraceProject project) return;
            if (!string.IsNullOrWhiteSpace(project.ServiceKey))
            {
                MessageBox.Show("This project matches an svchost service — blocking svchost.exe would stop every " +
                                "Windows service at once. Block its destinations from the Phone Home tab instead.",
                    "Block application", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string image = project.MatchProcess ?? "";
            string stem = image.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? image[..^4] : image.Replace("*", "");
            string path = null;
            try
            {
                foreach (var p in Process.GetProcessesByName(stem))
                {
                    path = p.MainModule?.FileName;
                    p.Dispose();
                    if (path != null) break;
                }
            }
            catch { /* elevated/system processes may hide their path */ }

            if (path == null)
            {
                MessageBox.Show($"{image} is not running right now (or its path is hidden), so there is no " +
                                "executable to block. Block it from the Process Monitor the next time it appears.",
                    "Block application", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new BlockConfirmWindow(
                $"Block {image}",
                $"Outbound firewall rule for:\n  {path}\n\nThe application loses all outbound access; " +
                "undo from the Blocks tab or the panic button.",
                $"[project {project.Name}]", "traces-app", "Traces tab application blocks")
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true) return;
            if (win.Suppress) ConfirmPrefs.Suppress("traces-app");

            try
            {
                var (created, _) = await FirewallService.Instance.AddAppBlockAsync(path,
                    $"[blocked {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] {image} · AutoCommand Traces ({project.Name})");
                BlocksStore.AddOrUpdate(new BlockRecord
                {
                    Kind = "proc",
                    Key = path,
                    ProcessName = image,
                    Attrib = "live",
                }, "traces");
                MessageBox.Show(created > 0
                        ? $"Blocked {image} (1 outbound rule). Undo from the Blocks tab."
                        : "A block for this application already existed.",
                    "Block application", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Block failed: " + ex.Message, "Block application",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
