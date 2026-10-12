# AutoCommand v2026.10.22

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **AutoCommand is now the analyst toolkit.** The consumer setup side has moved to its own app, [**Shields Up**](https://github.com/dparksports/shields-up/releases/latest) — same author, same undo-first design — and the duplicate tabs are removed here. Eleven tabs are gone: **Quick Scan, Attack Surface, Bloatware, Telemetry, Privacy, Update Control, Tasks, Startup, Hibernation, OS Hardening, DBX Safety.** If you used any of those, install Shields Up; nothing about them changed, they just live there now. The **Firewall tab stays** — its drift detection feeds the Security Enforcer's status bar.
- **Eight tabs remain, Process Monitor opens first:** 🔍 Process Monitor · 🛡 Firewall · 🧭 Sysmon Audit · 🐍 Python Spawns · 🧵 Taskhostw Spawns · 🔬 Svchost Trace · 🌐 Connections · ⚙ Settings.
- **Settings stays.** Every card in it is app-level and none had moved: Appearance (Dark/Light), Startup Behavior (launch at logon), the **Unblock card** (SmartScreen / Smart App Control mark-of-the-web removal), Network Adapter Security, the Security Enforcer check rate, and the analytics opt-in.
- **Nothing behind the scenes changed.** The Security Enforcer, tray alerts, the hidden-telemetry engine, and the status bar — including the 🚑 **Restore Internet** panic button — are untouched. The privileged-task alert now points to Shields Up's Tasks page. No new dependencies.

> **Note:** still run elevated as usual — spawn tracing reads the Sysmon channel and the packet capture uses raw sockets.
