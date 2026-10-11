# AutoCommand — Technical Notes

How AutoCommand delivers what the [README](../README.md) promises: the technologies behind each capability, the design rules they follow, and every place on disk the app writes. No marketing — just what runs, what it touches, and why.

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
| Packet counters | Raw-socket sniffer — `IOControlCode.ReceiveAll` (SIO_RCVALL) on raw IP sockets; no WinPcap/Npcap dependency |
| Name resolution | Background reverse-DNS service with a persistent JSON cache |
| Task attribution | taskhostw instance GUID on the command line correlated with Task Scheduler history (the channel is enabled once if off); COM-handler tasks get their ClassId resolved through `HKCR\CLSID\{…}\InprocServer32` to the handler DLL, flagged when unregistered |
| Live ordering | WPF `ICollectionView` live sorting on the row's `LastSeen` timestamp; stale rows detected off a UI timer |

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
* **.NET 10 (WPF)** front end; WebView2 hosts the opt-in Firebase Analytics page only.
* **Plugins** load through the bundled `AutoCommand.Sdk` (the Plugins tab is currently disabled in the UI; the loader ships with the app).
* **README infographics** are generated by `Assets/generate_infographics.py` (Python + matplotlib).

---

## What the app writes on your disk

| Path | Purpose |
|---|---|
| `C:\ProgramData\AutoCommand\bloatware_config.json` | Your bloatware-list customizations (delta over defaults) |
| `C:\ProgramData\AutoCommand\firewall_config.json` | Per-rule firewall overrides |
| `C:\ProgramData\AutoCommand\firewall_baseline.json` | Firewall drift baseline |
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

Telemetry is **opt-in** and limited to the Firebase JS SDK inside a sandboxed WebView2 control; nothing else in the app sends data anywhere on its own.
