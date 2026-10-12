# AutoCommand v2026.10.23

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **New Firewall Quick Profile: "Default Lockdown" — and it now sits first in the list.** One pick does two things, in order: restores the Windows default firewall rule set (the native-COM equivalent of `netsh advfirewall reset`), then disables **every** remaining rule — inbound and outbound. A clean-slate total lockdown. The Security Enforcer re-captures the baseline afterwards, so anything that re-enables a rule later (Windows Update, other software) gets re-disabled automatically.
- **Default Lockdown is destructive, and it says so up front.** Resetting to defaults deletes every non-default rule — custom rules included, and AutoCommand's own AC-BLOCK blocks (they come back the next time the app blocks an IP or app). A dedicated warning dialog spells out both steps before anything is touched; declining backs out cleanly. The other five profiles keep their usual confirmation.
- **Quick Profile is now ordered:** Custom (Current) · **Default Lockdown** · Shield Up · Gaming · Office · Home · Public Strict. Shield Up moved to second.
- **Nothing else changed.** The rest of the Firewall tab, the Security Enforcer, tray alerts, and the 🚑 Restore Internet panic button are untouched. No new dependencies.

> **Note:** run elevated as usual — the reset and lockdown run through the same `INetFwPolicy2` engine as the rest of the Firewall tab.
