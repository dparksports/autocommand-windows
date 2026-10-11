using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// Per-environment opt-in for the audit hook: discovery of PEP 514
    /// installs plus manually added venvs, install/uninstall of the embedded
    /// payload, with the manifest tracking exactly which files are ours.
    /// </summary>
    public partial class PythonInstrumentDialog : Window
    {
        private readonly PyTraceInstallerService _installer = new();
        private List<PyTraceInstallerService.PythonEnv> _envs = new();

        public PythonInstrumentDialog()
        {
            InitializeComponent();
            RefreshList();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => RefreshList();

        private void RefreshList()
        {
            _envs = _installer.DiscoverEnvironments().ToList();

            // Environments recorded in the manifest but no longer discovered
            // (e.g. a venv whose folder picker root isn't derivable) still
            // need an Uninstall surface.
            var known = _installer.LoadManifest();
            foreach (var entry in known)
            {
                if (_envs.Any(env => env.SitePackages.Equals(entry.SitePackages, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (System.IO.Directory.Exists(entry.SitePackages))
                    _envs.Add(new PyTraceInstallerService.PythonEnv
                    {
                        SitePackages = entry.SitePackages,
                        Root = entry.Root ?? entry.SitePackages,
                        Source = entry.Source ?? "manifest"
                    });
            }

            EnvList.ItemsSource = _envs
                .OrderBy(e => e.Root, StringComparer.OrdinalIgnoreCase)
                .Select(e => $"{(_installer.IsInstalled(e.SitePackages) ? "[instrumented] " : "[plain]       ")}{e.Display}")
                .ToList();
            StatusText.Text = $"{_envs.Count} environment(s) found. Instrumented ones report stacks + TLS keys.";
        }

        private void AddVenvBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Pick the venv root folder (the one containing Lib\\site-packages)"
            };
            if (dialog.ShowDialog(this) != true) return;

            var env = _installer.EnvFromVenvRoot(dialog.FolderName);
            if (env == null)
            {
                MessageBox.Show(this,
                    $"No Lib\\site-packages found under:\n{dialog.FolderName}\n\nPick the venv root folder itself, not site-packages.",
                    "Not a venv", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_envs.Any(x => x.SitePackages.Equals(env.SitePackages, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "That environment is already in the list.", "Already listed",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _envs.Add(env);
            RefreshList();
        }

        private void InstallBtn_Click(object sender, RoutedEventArgs e) =>
            ApplyToSelection(install: true);

        private void UninstallBtn_Click(object sender, RoutedEventArgs e) =>
            ApplyToSelection(install: false);

        private void ApplyToSelection(bool install)
        {
            var selected = EnvList.SelectedItems.Cast<string>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one environment first.",
                    "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (install)
            {
                var confirm = MessageBox.Show(this,
                    "Instrument the selected environment(s)?\n\n" +
                    "Writes two small files into each site-packages (PyTraceHook_ac.py + PyTraceHook_ac.pth):\n" +
                    "· every python from that environment records the Python stack when it spawns processes\n" +
                    "· every python from that environment logs TLS session keys to a user-only-ACL file\n" +
                    "   (AutoCommand\\pytrace\\sslkeys.log) so its own HTTPS traffic can be decrypted in-tab\n\n" +
                    "This is visible instrumentation — anyone reading those files knows they exist.\n" +
                    "Remove anytime from this dialog.",
                    "Instrument environments", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            int done = 0;
            var errors = new List<string>();
            foreach (string line in selected)
            {
                // The list line encodes the root after the status tag
                var env = _envs.FirstOrDefault(e => line.EndsWith(e.Display, StringComparison.Ordinal));
                if (env == null) continue;

                string error = install ? _installer.Install(env) : _installer.Uninstall(env.SitePackages);
                if (error == null) done++;
                else errors.Add($"{env.Root}: {error}");
            }

            RefreshList();
            StatusText.Text = errors.Count > 0
                ? $"{done} succeeded, {errors.Count} failed:\n{string.Join("\n", errors)}"
                : $"{done} environment(s) {(install ? "instrumented" : "cleaned")}.";
        }
    }
}
