using System.Windows;

namespace AutoCommand
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply the saved theme before any window loads (brushes are
            // DynamicResource, so this also re-themes live when changed later)
            Services.ThemeService.ApplyTheme(Services.ThemeService.Saved);

            // Handle UI thread exceptions
            this.DispatcherUnhandledException += (s, args) =>
            {
                LogCrash(args.Exception, "Dispatcher");
                args.Handled = true; // Prevent app from closing
                MessageBox.Show($"A UI error occurred: {args.Exception.Message}\n\nDetails saved to crash.log.", 
                    "AutoCommand Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            // Handle background thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is System.Exception ex)
                {
                    LogCrash(ex, "AppDomain");
                }
            };

            // Handle async Task exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LogCrash(args.Exception, "TaskScheduler");
                args.SetObserved();
            };

            // Finish a Sysmon repair that was waiting for a Windows restart
            FinishSysmonRepairIfPending();

            RemoveLegacyAnalyticsRegistryValues();

            // Telemetry sinkhole: when hosts pins point at 127.0.0.1 (sinkhole
            // mode, toggled from the Black Hole tab), start the logging
            // listener immediately so proof accumulates even if that tab is
            // never opened this session. Touching the proof store also wires
            // its recorder to the sinkhole. No sinkhole pins → both are no-ops.
            Services.SinkholeService.Instance.EnsureRunningForCurrentPins();
            _ = Services.TelemetryProofService.Instance.TotalDenied;
        }

        // Older builds stored a Google Analytics consent flag and a client-id
        // GUID under HKCU\Software\AutoCommand. Usage telemetry was removed;
        // delete the leftovers so no orphaned tracking id survives.
        // Idempotent — no-op once clean.
        private void RemoveLegacyAnalyticsRegistryValues()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser
                    .OpenSubKey(@"Software\AutoCommand", writable: true);
                key?.DeleteValue("EnableAnalytics", throwOnMissingValue: false);
                key?.DeleteValue("ClientId", throwOnMissingValue: false);
            }
            catch { /* registry unavailable — nothing to clean */ }
        }

        private async void FinishSysmonRepairIfPending()
        {
            try
            {
                var installer = new AutoCommand.Services.SysmonInstallerService();
                var (verified, message) = await installer.CompletePendingRepairIfAnyAsync();
                if (message == null) return; // no repair was pending

                if (verified)
                {
                    MessageBox.Show($"Sysmon repair verified after the restart.\n\n{message}",
                        "Sysmon repair complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Sysmon repair could not be verified after the restart.\n\nDetails: {message}\n\n" +
                        "Use the Sysmon setup button in the Process Monitor view to retry the repair.",
                        "Sysmon repair", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                LogCrash(ex, "SysmonRepair");
            }
        }

        private void LogCrash(System.Exception ex, string source)
        {
            try
            {
                string logPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                string logEntry = $"\n[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\n{ex.ToString()}\n";
                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch { /* Failsafe */ }
        }
    }
}

