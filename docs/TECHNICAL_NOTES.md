# AutoCommand — Technical Notes

How AutoCommand delivers what the [README](../README.md) promises: the technologies behind each capability, the design rules they follow, and every place on disk the app writes. No marketing — just what runs, what it touches, and why.

> **Note (v2026.10.22):** the consumer tabs (Quick Scan, Attack Surface, Bloatware, Telemetry, Privacy, Update Control, Tasks, Startup, Hibernation, OS Hardening, DBX Safety) moved to [Shields Up](https://github.com/dparksports/shields-up/releases/latest) and were removed from the UI. Sections below that describe those features cover services that remain in the codebase but currently have no tab in the app.

---

## Design rules

1. **Zero-PowerShell core.** Monitoring and mitigation talk to Windows directly — COM, WMI, and P/Invoke — never by spawning `powershell.exe`. Native command-line tools (`powercfg`, `bcdedit`, `schtasks`, `wevtutil`) are used only where Microsoft ships no COM or .NET surface, and always through one audited runner (`Helpers/ProcessRunner.cs`).
   *The one documented exception:* applying DBX updates uses the `Set-SecureBootUEFI` PowerShell cmdlet, because Microsoft provides no native C# API for UEFI Secure Boot variable writes.
2. **Nothing runs without you.** Bloatware removal, firewall profile applies, and DBX updates all confirm first.
3. **Non-removable means non-offered.** System-signed OS components are filtered out of the Default Apps list and are never offered by bloatware removal — by design, not by configuration.
4. **Verified downloads.** Auto-downloaded tools (Sysinternals `sigcheck64.exe`) must pass both a logged SHA256 check and a native `WinVerifyTrust` Authenticode verification; anything else is deleted, never executed.
5. **Plain, inspectable state.** User preferences live in readable JSON and text files (listed at the end), so you can back up, diff, or edit everything the app remembers.

---

## Subsystem by subsystem

### Process Monitor & network analytics
| Concern | Technology |
|---|---|
| Connection events | Sysmon — `EventLogWatcher` over the `Microsoft-Windows-Sysmon/Operational` channel; the installer configures the manifest via `wevtutil` and can repair inconsistent installs |
| Packet counters | Raw-socket sniffer — `IOControlCode.ReceiveAll` (SIO_RCVALL) on raw IP sockets, one IPv4 plus a best-effort IPv6 socket; no WinPcap/Npcap dependency |
| Name resolution | Background reverse-DNS service with a persistent JSON cache |
| Task attribution | taskhostw instance GUID on the command line correlated with Task Scheduler history (the channel is enabled once if off); COM-handler tasks get their ClassId resolved through `HKCR\CLSID\{…}\InprocServer32` to the handler DLL, flagged when unregistered |
| Live ordering | WPF `ICollectionView` live sorting on the row's `LastSeen` timestamp; stale rows detected off a UI timer |
| Row lifecycle | `LastSeen` seeded from the Sysmon event's UTC time (one-shot connections still get a real timestamp); the UI timer marks rows "— exited" once their PID is gone, and a later Sysmon event re-attributes the row to a new PID on the same remote IP |

### Sysmon Audit
| Concern | Technology |
|---|---|
| Status facts | Service state via `sc query`, event-channel health and 24 h event counts via `wevtutil`/`Get-WinEvent`, active config hash from `HKLM\SYSTEM\CurrentControlSet\Services\SysmonDrv\Parameters` |
| Recommended config | Generated `sysmon_config.xml` capturing process creations (ID 1), network connections (ID 3) and DNS queries (ID 22); applied with `Sysmon64 -c` on existing installs |
| Install/repair | Download from `live.sysinternals.com`, `Sysmon64 -i -accepteula`; inconsistent-state repair removes stale manifest registrations (reboot + auto-retry when Windows holds the registration) |
| Process-creation feed | `EventLogWatcher` over `*[System[(EventID=1)]]` plus a time-bounded historical backfill reader |

### Update Control
| Concern | Technology |
|---|---|
| Discovery | Self-update `.old` leftovers, `*updat*` folders under the user profile, and "update" autostart Run keys; vendor shown via Authenticode signature |
| Freeze | ACL snapshot with `icacls /save`, then deny ACE `(DE,WD,AD)` for the current user on the exe, then verification that the deny is present; Windows-signed/system-path binaries refused |
| Thaw | `icacls /restore` of the snapshot; deny-ACE absence verified before state cleanup |
| State | `update_freeze.json` next to the exe; ACL backups under `freeze_backups\` |

### OneSettings cadence (🔄 tab + `OneSettingsCadence` CLI)

The 🔄 **OneSettings tab** and the standalone `OneSettingsCadence` CLI are two
faces over one engine (`OneSettingsCadence/OneSettingsCadence.Core`, linked
in-process by the app the same way `SvchostAnalyzer.Core` is — the CLI keeps
its own `Main()` and builds separately). It slows the Windows inbox task
`\Microsoft\Windows\Flighting\OneSettings\RefreshCache` — the OneSettings
remote-config fetch that runs ~20-40×/day as SYSTEM off a ~3h20m repetition
(+1h random delay, +WNF state-change triggers) — to a user-chosen interval, and
restores stock behavior afterwards. Task Scheduler is reached over the
`Schedule.Service` COM interface (same dynamic-COM pattern as
`Services/TaskSchedulerService.cs`), never through `schtasks.exe`. The app
manifest already demands administrator, so the tab applies changes with no UAC
round-trip; the CLI self-elevates.

| Concern | Technology |
|---|---|
| `status` / `preview` / `runs` | Read-only, no elevation (the task's security descriptor grants Authenticated Users read). `preview` runs the exact transform `set` would apply and prints the resulting task XML; `runs` (also the tab's third card) reads the Task Scheduler operational channel and compares observed launches against the expected cadence — each launch bucketed by why the scheduler says it fired (timer / event / logon / manual-API). WNF state-change triggers fire through the same EID-107 channel as the timer, so the verdict is WNF-aware: excess 107s with WNF triggers installed are theirs, not a reverted cadence |
| `set` | Self-elevates via UAC (the elevated child tees its output back to the caller's console through a temp file); writes a timestamped backup of the live XML under `C:\ProgramData\AutoCommand\onesettings-cadence\backups\` (UTF-16, round-trip-verified), re-registers, then re-reads the live task and compares interval / random delay / WNF trigger count — any mismatch rolls back from the backup (a failed rollback exits 4 and says so loudly) |
| The transform | Touches `Triggers` only: repetition interval set, `RandomDelay` forced to `PT0S`, `WnfStateChangeTrigger` elements removed unless `--keep-wnf`. A repetition `Duration` shorter than the new interval is dropped (absent = indefinite) so the pattern cannot re-arm on the boundary. Principal (S-1-5-18), actions, settings and `SecurityDescriptor` are preserved from the live XML |
| Watchdog | Optional (`set --watchdog`, or the tab's checkbox): a SYSTEM task `\AutoCommand\OneSettingsCadenceWatchdog` (daily 03:30 + logon) re-applies the saved cadence only when the live task drifted — Windows servicing rewrites inbox task definitions (this machine's `StartBoundary` was rewritten by the OS the day the tool was written). From the CLI the watchdog runs the exe with `enforce --quiet`; from the tab it launches AutoCommand.exe with the `onesettings-enforce` argument, handled headlessly in `App.OnStartup` before any window loads. `restore` removes the watchdog so it cannot fight the restored stock definition |
| `restore` | Re-registers the newest backup (or `--backup <file>`), with the same snapshot → register → verify → roll-back discipline; clears the saved cadence and removes the watchdog on success |
| Interval input | Plain hours (`24`, `24h`, `0.5`), clock (`06:00:00`, `6:00`), or ISO 8601 (`PT24H`, `P1DT12H`); default 24h; warns below 6h or above 72h (cached OneSettings payloads declare ~72h freshness windows) but proceeds |
| Audit | Every change (what, when, old→new interval/delay/WNF count, backup filename, user, result) is one JSONL line in `onesettings-cadence\audit.jsonl` |
| Exit codes | `0` ok · `1` usage/task-not-found/IO · `2` drift found (`status`) or verified failure rolled back · `3` elevation declined · `4` rollback failed too |

Pure logic (parser, transform, drift detection, run-history field parsing and
compliance bucketing — including the exact-match event namespace a typo once
silently broke) has an offline self-test: `OneSettingsCadenceSelfTest`
(43 checks, no COM, no writes, no elevation).

| Live external starts | While the tab is open, an `EventLogWatcher` over the scheduler channel flags launches that fired with no trigger event (100 without 107/108/119 — something called `Run()`), confirmed on a 3 s deadline so out-of-order trigger events do not false-alarm; EID 129 supplies the taskhostw PID, Sysmon EID 3/5 join the fetch destinations and exit (pure correlator unit-covered; degrades to starts-without-evidence when Sysmon is absent) |

What the tab cannot enforce, honestly stated in the card itself: the watchdog
re-asserts the task *definition*; launches started by other actors (`Run()` on
the task) are counted and named but not preventable, and a component that
bypasses the task entirely (fetching OneSettings in-process from a service) is
invisible to scheduler history — it would surface as svchost traffic in the
Process Monitor, where it can be blocked like any other destination.

### Telemetry Opt-Out
| Concern | Technology |
|---|---|
| Knowledge base | `Assets\telemetry_rules.json` (detection, state probe, opt-out, revert per app); built-in seed fallback |
| Probes/appliers | JSON key read/write, user environment variables, HKLM policy values (DWORD or string) |
| Reversibility | Previous value snapshotted to `telemetry_state.json` before each change; Revert restores it (or deletes an added value) |
| Verification | `Get-DnsClientCache` checked for each app's known telemetry domains |

### Advanced Firewall
| Concern | Technology |
|---|---|
| Rule engine | `INetFwPolicy2` COM (`HNetCfg.FwPolicy2`) — enumerate, enable/disable, create block rules |
| Group names | Indirect strings (`@FirewallAPI.dll,-32752`) resolved via `SHLoadIndirectString` (`shlwapi.dll`) |
| Profiles & drift | Baseline of every rule's enabled state stored as JSON in `C:\ProgramData\AutoCommand`; re-scanned to report drift |
| 1-click blocks | Inbound + outbound rules per executable or remote address, created over the same COM interface |

### Security Enforcer
| Concern | Technology |
|---|---|
| Device events | WMI `__InstanceCreationEvent` / `__InstanceDeletionEvent` / `__InstanceModificationEvent` subscriptions on `Win32_NetworkAdapter` — zero polling idle cost |
| SSTP guard | Service state via the Service Controller; re-asserts stopped + disabled (`Start=4`) in a reconciliation sweep |
| Kernel-debug block | `bcdedit` state reads/writes plus KDNIC adapter removal |
| Adapter removal | `SetupAPI` P/Invoke (`setupapi.dll`) in `WanMiniportRemover.cs` — kernel-mode clean device removal, no `pnputil` |
| Logon task | `schtasks /create … /rl highest` via the shared runner |
| Alerts | Windows toast notifications (`Windows.UI.Notifications`) |

### Default Apps & Bloatware
| Concern | Technology |
|---|---|
| App inventory | `Windows.Management.Deployment.PackageManager` (`FindPackagesForUser`) — frameworks, resource packages, and System-signed components filtered to match Settings → Installed apps |
| Signature status | `Package.SignatureKind`, validated by the deployment engine at install time (Store / Enterprise / System / Developer / Unsigned) |
| Uninstall | `PackageManager.RemovePackageAsync` per package full name |
| OneDrive (Win32) | Not a Store package — detected via install directory + `UninstallString` registry entry (HKCU/HKLM), removed with `OneDriveSetup.exe /uninstall`, `winget` fallback |
| Your custom list | Delta config (opt-outs + additions) in `C:\ProgramData\AutoCommand\bloatware_config.json`, merged over the compiled-in defaults by `BloatwareConfig` |

### UEFI & Secure Boot
| Concern | Technology |
|---|---|
| EFI baseline | SHA256 hash of every `.efi` module on the EFI System Partition; drift reported against a stored baseline |
| Boot manager check | Sysinternals `sigcheck64.exe` — downloaded only after SHA256 + Authenticode (`wintrust.dll` `WinVerifyTrust`) gates; its UTF-16LE redirected output is decoded before parsing |
| DBX updates | `DBXUpdate.bin` downloaded from Microsoft's secureboot_objects repository, `EFI_SIGNATURE_LIST` parsed in C#, applied with `Set-SecureBootUEFI` after explicit confirmation |

### AI Assistant

> Removed in v2026.10.12 — the audit/insight feature is being rethought. The per-tab state serialization it used (`IAiAuditable.GetAuditContext()` in every view) is intentionally kept in the codebase as the foundation for whatever replaces it.

### Platform
* **.NET 10 (WPF)** front end.
* **Plugins** load through the bundled `AutoCommand.Sdk` (the Plugins tab is currently disabled in the UI; the loader ships with the app).
* **README infographics** are generated by `Assets/generate_infographics.py` (Python + matplotlib).

---

## What the app writes on your disk

| Path | Purpose |
|---|---|
| `C:\ProgramData\AutoCommand\bloatware_config.json` | Your bloatware-list customizations (delta over defaults) |
| `C:\ProgramData\AutoCommand\firewall_config.json` | Per-rule firewall overrides |
| `C:\ProgramData\AutoCommand\firewall_baseline.json` | Firewall drift baseline |
| `C:\ProgramData\AutoCommand\onesettings-cadence\state.json` | OneSettingsCadence CLI — saved desired cadence for the inbox RefreshCache task |
| `C:\ProgramData\AutoCommand\onesettings-cadence\backups\RefreshCache-*.xml` | OneSettingsCadence CLI — timestamped task-XML backups (`restore` re-registers these) |
| `C:\ProgramData\AutoCommand\onesettings-cadence\audit.jsonl` | OneSettingsCadence CLI — audit trail: every change with old→new values and the backup filename |
| `<app>\blocked.txt` | Firewall blocks created from the monitors |
| `<app>\ignored_processes.txt` | Process Monitor ignore list |
| `<app>\ultimate_autopilot_stats.csv` | Hourly connection-stats export |
| `<app>\check_interval.txt` | Security Enforcer cadence |
| `<app>\update_freeze.json` + `freeze_backups\` | Frozen self-updaters and their ACL snapshots |
| `<app>\telemetry_state.json` | Snapshots of the settings changed by telemetry opt-outs |
| `%LOCALAPPDATA%\AutoCommand\pytrace\` | Python Spawns tab state — see below |

### Python Spawns tab (`%LOCALAPPDATA%\AutoCommand\pytrace\`)

| File | Purpose |
|---|---|
| `trace-<pid>.jsonl` | Audit-hook records from instrumented pythons (written by the hook, tailed + cleaned up by the app) |
| `sslkeys.log` (+ `.old` while rotating) | TLS session keys written by instrumented pythons via `SSLKEYLOGFILE`; created with a current-user-only ACL, rotated at 5 MB, `.old` deleted at app exit |
| `no-keylog` | Flag file: the "Disable TLS keylogging" toggle — its presence makes the hook stop setting `SSLKEYLOGFILE` |
| `instrumented.json` | Manifest of python environments the audit hook was installed into (and the exact files written) |
| `env_scan.json` | Instrumentation discovery state: user-added scan roots, environments deliberately forgotten, last deep-scan time |
| `settings.json` | Tab settings (keylog toggle state, flow persistence, flow caps) |
| `flows\*.jsonl` | Optional persisted TCP flows (base64 payloads, off by default, rotated by budget) |

Outside that directory the tab only **reads**: the Sysmon operational channel (EID 1/5/3) and raw packets from the NIC on its own socket. The instrumentation itself is opt-in per python environment: two small files (`PyTraceHook_ac.py` + `.pth`) written into that environment's site-packages, tracked in the manifest, removable from the Instrument dialog. Nothing inside a venv is ever read for correlation; the interpreter binary is never touched.

The app collects **no usage telemetry** — nothing in it sends data anywhere on its own. (Usage analytics via Firebase/GA4 have been removed; the "Telemetry Opt-Out" section above is the Windows-telemetry hardening feature, unrelated.)
