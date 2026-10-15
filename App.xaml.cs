using System.Linq;
using System.Windows;

namespace AutoCommand
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── headless agent mode (no UI, no dialogs, no theme) ────────
            // --agent-headless [sec]
            //      start an AI session without the UI (scripts/tests).
            //      NOTE: the exe carries requireAdministrator, so MCP clients
            //      must NEVER spawn it — they spawn the small asInvoker
            //      proxy (AutoCommand.AgentProxy, ProjectReferenced and
            //      deployed next to this exe) which forwards here.
            var args = e.Args ?? System.Array.Empty<string>();
            if (args.Any(a => string.Equals(a, "--agent-headless", System.StringComparison.OrdinalIgnoreCase)))
            {
                RunAgentHeadless(args);
                return;
            }

            // ── headless OneSettings watchdog path (no UI) ──────────────
            // The \AutoCommand\OneSettingsCadenceWatchdog SYSTEM task (installed
            // from the OneSettings tab with the watchdog box checked) launches
            // AutoCommand.exe with this argument daily 03:30 and at logon to
            // re-apply the saved RefreshCache cadence if Windows servicing
            // reverted it. Silent; every action lands in the machine-readable
            // audit trail under %ProgramData%\AutoCommand\onesettings-cadence.
            if (args.Any(a => string.Equals(a, "onesettings-enforce", System.StringComparison.OrdinalIgnoreCase)))
            {
                RunOneSettingsEnforce();
                return;
            }

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

            // Anonymous usage ping (opt-in, off by default): sends the user's
            // answer into effect — consented installs ping once a day, everyone
            // else is never asked again after the bounded offer. Fully inert
            // while UsagePingService.Endpoint is unconfigured. Spec and server
            // code: telemetry/ in the repository.
            _ = Services.UsagePingService.RunStartupFlowAsync();
        }

        // Headless AI session: same bridge the Settings card starts, minus the
        // UI. Runs until the given number of seconds elapse (0 = until this
        // console's stdin closes or the process is killed). Mutating tools are
        // always off here — nothing can confirm an action without the app UI.
        private void RunOneSettingsEnforce()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                // Engine handles the no-saved-state case as a no-op (normal
                // after a restore), and audits both success and failure — a
                // failed watchdog run must never block app shutdown.
                new OneSettingsCadence.CadenceEngine().Enforce();
            }
            catch { /* audited inside the engine where possible; always exit */ }
            Shutdown();
        }

        private void RunAgentHeadless(string[] args)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            int seconds = 0;
            int flagIndex = System.Array.FindIndex(args,
                a => string.Equals(a, "--agent-headless", System.StringComparison.OrdinalIgnoreCase));
            if (flagIndex + 1 < args.Length && int.TryParse(args[flagIndex + 1], out int parsed) && parsed > 0)
                seconds = parsed;

            var (ok, message) = Services.AgentBridgeService.Instance.Start(allowMutating: false);
            System.Console.WriteLine(ok
                ? $"AutoCommand agent session active ({message}) — Ctrl+C or close stdin to stop."
                : "agent session failed: " + message);

            if (ok)
            {
                if (seconds > 0) System.Threading.Thread.Sleep(TimeSpan.FromSeconds(seconds));
                else while (System.Console.In.ReadLine() != null) { } // until stdin closes
                Services.AgentBridgeService.Instance.Stop();
            }
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // a live session must never outlive the app: the session file is
            // what grants access, so it dies here even on a crash-next-launch
            Services.AgentBridgeService.Instance.Stop();
            base.OnExit(e);
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

