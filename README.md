# AutoCommand — see what your Windows PC is really doing, then take it back with one click

![AutoCommand](Assets/corp_banner.png?v=fluent1)

A free, open-source security and control dashboard for Windows 10 and 11. One administrator window answers every question that normally costs you an afternoon — *what is this process, who launched it, where is it connecting, how do I make it stop?* — as a row in a table, a colored card, or a single button, in a clean Fluent design with light and dark themes.

> **⬇ Download** the latest self-contained build from the [**Releases page**](https://github.com/dparksports/autocommand-windows/releases) — no .NET required. Unzip, run `AutoCommand.exe` as Administrator.
>
> **🔧 How it works inside:** every technology below is documented in the [Technical Notes](docs/TECHNICAL_NOTES.md). Apache 2.0.

---

## Why it exists

Windows spreads the answers across a dozen tools that don't talk to each other. AutoCommand puts the whole picture in one place:

![Why AutoCommand](Assets/corp_why.png?v=fluent1)

- **A mystery process is phoning home.** Find the row — process, destination, owner — then block the IP, block the executable, kill the process, or disable the scheduled task that would resurrect it at 3 a.m.
- **You want a record, not a guess.** One click installs Sysmon, and from then on every process launch, connection, and DNS query is recorded locally.
- **An app keeps replacing itself.** Freeze it: the app runs, its updater fails politely, and unfreezing restores the original permissions exactly.
- **Software silently beacons usage stats.** Apply the vendor's own documented opt-out, snapshot the previous value, and verify the beacons actually stopped.

---

## The tabs

| Tab | What it gives you |
|---|---|
| 🚀 **Quick Scan** | The whole first-hour ritual — debloat, firewall, privacy, hardening — as one guided pass that re-verifies itself. |
| 📡 **Attack Surface** | The network doors Windows ships open: SSTP tunneling, KDNET debug adapters, hosted-network hotspots. Inspect, disable, remove. |
| 🛡 **Firewall** | Curated profiles (lockdown, home-LAN, per-app) in one click over the native `INetFwPolicy2` engine, with drift detection. |
| 📦 **Bloatware** | 23 preinstalled apps removed in one sweep. Use one of them? Toggle it off the list — permanently. |
| 🧭 **Sysmon Audit** | One status card + a live feed of every process launch with full command line and parent. Installed and configured from the app. |
| 🔍 **Process Monitor** | Live grid of outbound connections with packet counters (no WinPcap needed), background reverse-DNS, and attribution that survives process exit. Block, kill, inspect — with warnings before you take out something Windows needs. |
| 🐍 **Python Spawns** | Every `python.exe` launch as it happens — who spawned it (parent image + command line), what it ran, when it exited. For environments you explicitly instrument: the **Python call stack at the moment a spawn API fired**, and **decrypted TLS plaintext** of what the process sent (SNI, HTTP request/response), from its own SSLKEYLOGFILE secrets + a NIC-level TCP capture. |
| 🔬 **Svchost Trace** | A timeline over the 70+ `svchost.exe` service hosts — spawns with parents, connection lifetimes, byte flows — plus a pattern detector: bad parents, respawn loops, beacon-regular connections, upload-dominant flows. Exports JSONL; drives a headless capture service with boot auto-start. The capture engine is built into the app (no separate binary to install or unblock); an optional standalone SvchostAnalyzer.exe remains for headless 24/7 recording with boot auto-start — offered as a one-click SHA-256-verified install when you enable it. |
| 📊 **Telemetry** | Known telemetry senders (Edge, Chrome, .NET CLI, VS Code, …), the vendor's own documented opt-out, previous value snapshotted, quiet verified via the DNS cache. |
| 👁 **Privacy** | Microsoft's diagnostic task disabled, AutoCommand's own analytics opt-in, tamper-protection status visible. |
| ❄ **Update Control** | Freeze self-updating apps at the version that works — `icacls` snapshots make unfreezing exact. Windows binaries are refused. |
| 📋 **Tasks · 🚀 Startup · 🌙 Hibernation · 🌐 Connections** | Full Task Scheduler control, startup entries, hibernation posture, and a second live connection view. |
| 🔒 **OS Hardening + ✅ DBX Safety** | Self-healing guards on SSTP/kernel-debug, the Secure Boot chain hashed and watched, DBX revocations applied only with your yes. |
| ⚙ **Settings** | Appearance — **Dark/Light theme, applied instantly and remembered**; startup behavior, analytics opt-in, live version — and the status bar's **🚑 Restore Internet** button. The About card signs off: Made with ❤️ in California · Apache License 2.0 · © 2026 Dan Park, magicpoint.ai. |

![Quick Scan — one guided pass](Assets/corp_debloat.png?v=fluent1)

![Process Monitor and Sysmon Audit](Assets/corp_visibility.png?v=fluent1)

## Python Spawns, in depth

Three questions about every python process that starts on the machine, answered live in one tab:

- **Who / what / when** — Sysmon Event ID 1 traces every `python.exe`/`pythonw.exe` creation with the full parentage (parent image + parent command line), command line, user, integrity level, hashes, and an Exited flag from Event ID 5. A venv command spawns two processes (launcher + base interpreter); they collapse into one row with a **venv** badge — correlation is purely parent-PID-based, nothing inside the venv is ever read.
- **Why (the in-process answer)** — a ~90-line audit hook (`sys.addaudithook`, shipped as an embedded payload) is installed **opt-in per environment** into its site-packages. Instrumented pythons record the Python call stack whenever a spawn API fires (`subprocess.Popen`, `_winapi.CreateProcess`, `os.system`, `os.exec/spawn/posix_spawn`, `os.startfile`, even another `sys.addaudithook`), as JSONL the app tails and joins to spawn rows by pid.
- **What they send** — the hook also sets `SSLKEYLOGFILE` (CPython's own mechanism) before user code runs. The tab captures TCP streams on its own raw socket (SIO_RCVALL), attributes flows to pids at connect time (Sysmon EID 3), and decrypts TLS **1.2 and 1.3** in-tab — endpoints, SNI, ALPN, and readable HTTP request/response records. All crypto is in-box .NET (`AesGcm`, `ChaCha20Poly1305`, HKDF/PRF) — no extra dependency.

**The consent model is asymmetric by design:** everything observational (spawns, parentage, endpoints, byte counts) needs no cooperation and no opt-in. Stacks and plaintext exist *only* for environments you instrumented yourself. Discovery is automatic and exhaustive — PEP 514 registry, every PATH entry, Microsoft Store packages, conda, uv, pyenv-win, scoop/choco, a bounded deep scan for `pyvenv.cfg` venvs under your profile and dev folders, plus every python the tab has actually seen run — so the list populates itself — but installing the hook always goes through a button that shows exactly what will be written, and an uninstall that removes only the manifest-listed files. The keylog lives in a current-user-only-ACL file, rotates at 5 MB, is deleted at app exit when rotated, and one toggle wipes it and stops the hook from creating one.

**Limitations, honestly:** loopback traffic never crosses the NIC (invisible to the capture); IPv6 and UDP/QUIC are out of scope; pid→flow attribution is by endpoint at connect time; code that builds a raw `ssl.SSLContext` directly, or uses custom crypto, ignores `SSLKEYLOGFILE`; `-S`, frozen and embedded interpreters never load site at all and cannot be instrumented (a `.pth` in real site-packages survives `-I`/`-E`); TLS 1.3 KeyUpdate re-derivation is best-effort and failures are flagged, never guessed. The engine pieces that can run headless have an offline self-test (`docs/selftest/run-selftest.ps1` — reassembly, the hook payload, and decryption of a real TLS 1.2/1.3 session over a loopback relay, no admin needed); the Sysmon watcher and raw-socket capture need the app elevated.

## Blocked things, managed

Blocking is easy; *un*-blocking is where other tools strand you. AutoCommand:

- **warns before you block** — Microsoft/CDN destinations and DNS-carrying service hosts are flagged with what will break;
- **keeps rules self-describing** — 2 idempotent rules per target with full provenance in the description, readable from `wf.msc`; legacy `AutoCommand IP Block …` names **auto-migrate silently** to the clean scheme the first time you open the manager;
- **groups every block in one manager** — Microsoft/Windows rows in red, bulk unblock, legacy 4-rules-per-IP consolidation, and hit counts from the firewall's own drop log (one click to enable);
- **verifies unknown IPs for you** — one button asks the internet registries (RDAP) who owns each target, cached machine-wide;
- **restores you from mistakes** — the status-bar 🚑 button disables every AutoCommand block and flushes DNS in one click; *Restore except Microsoft/Windows* brings your real blocks back while leaving only the breakage-causing ones off.

![Update Control and Telemetry](Assets/corp_control.png?v=fluent1)

## What AutoCommand will not do

- **Nothing runs without you.** Removals, applies, freezes, and DBX updates all confirm first.
- **Non-removable means non-offered.** System-signed components never appear in the bloatware list; Windows binaries are never frozen.
- **No hidden state.** Everything the app remembers is readable JSON/text on disk (listed in the [Technical Notes](docs/TECHNICAL_NOTES.md)); its own telemetry is opt-in and off by default.

![OS Hardening](Assets/corp_hardening.png?v=fluent1)

## Build from source

```powershell
# .NET 10 SDK required
git clone https://github.com/dparksports/autocommand-windows.git
cd autocommand-windows
dotnet build AutoCommand.csproj -c Release

# self-contained build (what the releases ship)
dotnet publish AutoCommand.csproj -c Release -r win-x64 --self-contained
```

Run the published `AutoCommand.exe` as Administrator — WPF, .NET 10, Windows 10 (19041+) / Windows 11.

## Credits & license

Made with ❤️ in California

Apache License 2.0

© 2026 Dan Park, magicpoint.ai

Issues and PRs welcome — pick a tab, keep the design rules in the [Technical Notes](docs/TECHNICAL_NOTES.md), and match the existing code style.
