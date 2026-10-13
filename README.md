# AutoCommand — see what your Windows PC is really doing, then take it back with one click

![AutoCommand](Assets/corp_banner.png?v=fluent1)

A free, open-source security and control dashboard for Windows 10 and 11. One administrator window answers every question that normally costs you an afternoon — *what is this process, who launched it, where is it connecting, how do I make it stop?* — as a row in a table, a colored card, or a single button, in a clean Fluent design with light and dark themes.

> **⬇ Download** the latest self-contained build from the [**Releases page**](https://github.com/dparksports/autocommand-windows/releases) — no .NET required. Unzip, run `AutoCommand.exe` as Administrator.
>
> **🔧 How it works inside:** every technology below is documented in the [Technical Notes](docs/TECHNICAL_NOTES.md). Apache 2.0.

---

> **🛡 Looking for the setup side?** The first-hour ritual — debloat, firewall presets, privacy opt-outs, OS hardening, update freezes — moved to its own app: [**Shields Up**](https://github.com/dparksports/shields-up/releases/latest) (same author, same undo-first design). AutoCommand stays what this README covers: the analyst toolkit.

## You've been here

![Why AutoCommand](Assets/corp_why.png?v=fluent1)

- **A python.exe you didn't start is sending packets to four Google addresses.** Task Manager shows you the number and nothing else. AutoCommand shows the script's full command line, its parent, every destination with reverse DNS — and, for environments you instrument, the decrypted request and the call stack that fired it.
- **You opted out of telemetry. It kept sending.** Some apps read `"sync": "false"` — quoted — as *on*. AutoCommand writes the opt-out in the exact type the app parses, re-reads the file to verify, and shows **Not honored** in amber when the app is lying. And for the ones that ignore switches entirely: pin the beacon domain, or sinkhole it and watch every denied attempt by PID and command line.
- **A scheduled task resurrects a dead updater at 3 a.m.** Kill the process and it's back by morning. Here, the row resolves to the task itself — trigger, parent, handler DLL — and one click disables the thing that keeps bringing it back.
- **svchost.exe is eating a CPU core and Windows says that's normal.** Seventy service hosts, no labels. The Svchost Trace timeline names them, and the pattern detector flags the ones with bad parents, respawn loops, or upload-dominant flows.
- **An app updated itself overnight and your workflow broke.** Pin it: the updater's write access is denied, read-and-execute untouched, and unfreezing restores the exact permissions that were there before.
- **You want a record, not a guess.** One click installs Sysmon — from then on every process launch, connection and DNS query on this machine is recorded locally, surviving process exit, exportable to CSV.

## The tabs

| Tab | What it gives you |
|---|---|
| 🔍 **Process Monitor** | Live grid of outbound connections with packet counters (no WinPcap needed), background reverse-DNS, and attribution that survives process exit. Block, kill, inspect — with warnings before you take out something Windows needs. Rows above a **user-set transmit threshold (1 KB default)** pin to the top tinted red, so heavy talkers separate themselves from the noise. Opens first. |
| 📊 **Traffic Patterns** | The "who talks to whom, how much, how often" view: every process → destination IP pair with cumulative sent/received bytes, flow counts, ports, PIDs, and reverse-DNS hostnames — plus a 24-hour per-5-minute activity ring per pair, rendered as a sparkline column and a conservative **beacon-like hint** (regular cadence + similar sizes). Time-window selector (5 min / 1 h / 24 h / all time), regex filter, CSV export, one-click IP block; history persists across app restarts. Built on the app-level shared TCP capture (one raw socket for the whole app, refcounted with Python Spawns) with all-process pid attribution from Sysmon EID 3, and it honors the Process Monitor's ignore list — a process you told the app to stop listing is excluded here too. Same documented gaps as that capture: IPv4 TCP only — loopback, IPv6, and UDP/QUIC are invisible; unattributed flows land under "(unattributed)"; bytes accumulate while a capturing tab is open. |
| 🛡 **Firewall** | Curated profiles (lockdown, home-LAN, per-app) in one click over the native `INetFwPolicy2` engine, with drift detection. |
| 🧭 **Sysmon Audit** | One status card + a live feed of every process launch with full command line and parent. Installed and configured from the app. |
| 🦠 **Windows Defender** | The Defender answer to "why is it eating my machine": **live MsMpEng CPU/memory** with a 5-minute sparkline, engine status (real-time + tamper protection, platform/engine/signature versions, last quick/full scan with durations), a **live activity feed** straight from Defender's own event log (scans started/finished/stopped, detections, definition updates, cloud-protection activity, configuration changes), scan durations paired by Scan ID, and one-click **quick/full scan + definition update** triggers. Performance knobs: scan **CPU cap** slider (5–100% with presets) and **idle-only scheduled scans**; refusals from Tamper Protection or policy are surfaced, not swallowed. The **exclusions view is read-only by design** — AutoCommand shows what's excluded (so you can spot gaps) but never adds exclusions, since that's a classic malware technique; the card explains how to add them yourself via Windows Security and why narrow build-output exclusions beat broad ones — with a one-click deep link that opens Windows Security at the right settings page (the app itself never writes an exclusion). |
| 🐍 **Python Spawns** | Every `python.exe` launch as it happens — who spawned it (parent image + command line), what it ran, when it exited. For environments you explicitly instrument: the **Python call stack at the moment a spawn API fired**, and **decrypted TLS plaintext** of what the process sent (SNI, HTTP request/response), from its own SSLKEYLOGFILE secrets + a NIC-level TCP capture. |
| 🕳 **Black Hole** | The other half of blocking telemetry: proof it worked. A **Protection status** card answers the real question — what is actually standing between each app and its beacons: flag state (opted out / on / not honored), pin state, and whether any python processes started before the flag was applied are still beaconing under the old setting. With **Black Hole logging** on, every denied attempt is caught live — hostname from the TLS hello, PID from the OS connection table, process image and command line — counted per domain and per process, persisted as plain JSONL, exportable to CSV. |
| 🧵 **Taskhostw Spawns** | The same spawn tracing for `taskhostw.exe` — and the "why" is built in: each row resolves to the **scheduled task** that launched it (task path, user context, COM-handler DLL), with the full task summary and the PID-inquiry trace one click away. |
| 🔬 **Svchost Trace** | A timeline over the 70+ `svchost.exe` service hosts — spawns with parents, connection lifetimes, byte flows — plus a pattern detector: bad parents, respawn loops, beacon-regular connections, upload-dominant flows. Exports JSONL; drives a headless capture service with boot auto-start. The capture engine is built into the app (no separate binary to install or unblock). Headless 24/7 recording with boot auto-start uses the standalone SvchostAnalyzer.exe host, which is no longer distributed with releases — an already-installed copy keeps working. |
| 🌐 **Connections** | A second live connection view. |
| ⚙ **Settings** | Appearance — **Dark/Light theme, applied instantly and remembered**; startup behavior, live version — an **Unblock card** that strips the Mark-of-the-Web SmartScreen/Smart App Control blocks on (this app's folder, or any folder — see the standalone `unblock-autocommand.ps1` release asset) — and the status bar's **🚑 Restore Internet** button. The About card signs off: Made with ❤️ in California · Apache License 2.0 · © 2026 Dan Park, magicpoint.ai. |

![See everything](Assets/corp_visibility.png?v=fluent1)

## Python Spawns, in depth

Three questions about every python process that starts on the machine, answered live in one tab:

- **Who / what / when** — Sysmon Event ID 1 traces every `python.exe`/`pythonw.exe` creation with the full parentage (parent image + parent command line), command line, user, integrity level, hashes, and an Exited flag from Event ID 5. A venv command spawns two processes (launcher + base interpreter); they collapse into one row with a **venv** badge — correlation is purely parent-PID-based, nothing inside the venv is ever read.
- **Why (the in-process answer)** — a ~90-line audit hook (`sys.addaudithook`, shipped as an embedded payload) is installed **opt-in per environment** into its site-packages. Instrumented pythons record the Python call stack whenever a spawn API fires (`subprocess.Popen`, `_winapi.CreateProcess`, `os.system`, `os.exec/spawn/posix_spawn`, `os.startfile`, even another `sys.addaudithook`), as JSONL the app tails and joins to spawn rows by pid.
- **What they send** — the hook also sets `SSLKEYLOGFILE` (CPython's own mechanism) before user code runs. The tab captures TCP streams on its own raw socket (SIO_RCVALL), attributes flows to pids at connect time (Sysmon EID 3), and decrypts TLS **1.2 and 1.3** in-tab — endpoints, SNI, ALPN, and readable HTTP request/response records. All crypto is in-box .NET (`AesGcm`, `ChaCha20Poly1305`, HKDF/PRF) — no extra dependency.

**The consent model is asymmetric by design:** everything observational (spawns, parentage, endpoints, byte counts) needs no cooperation and no opt-in. Stacks and plaintext exist *only* for environments you instrumented yourself. Discovery is automatic and exhaustive — PEP 514 registry, every PATH entry, Microsoft Store packages, conda, uv, pyenv-win, scoop/choco, a bounded deep scan for `pyvenv.cfg` venvs under your profile and dev folders, plus every python the tab has actually seen run — so the list populates itself — but installing the hook always goes through a button that shows exactly what will be written, and an uninstall that removes only the manifest-listed files. The keylog lives in a current-user-only-ACL file, rotates at 5 MB, is deleted at app exit when rotated, and one toggle wipes it and stops the hook from creating one.

**Limitations, honestly:** loopback traffic never crosses the NIC (invisible to the capture); IPv6 and UDP/QUIC are out of scope; pid→flow attribution is by endpoint at connect time; code that builds a raw `ssl.SSLContext` directly, or uses custom crypto, ignores `SSLKEYLOGFILE`; `-S`, frozen and embedded interpreters never load site at all and cannot be instrumented (a `.pth` in real site-packages survives `-I`/`-E`); TLS 1.3 KeyUpdate re-derivation is best-effort and failures are flagged, never guessed. The engine pieces that can run headless have an offline self-test (`docs/selftest/run-selftest.ps1` — reassembly, the hook payload, and decryption of a real TLS 1.2/1.3 session over a loopback relay, no admin needed); the Sysmon watcher and raw-socket capture need the app elevated.

## Black Hole, in depth

Blocking is easy; *knowing it worked* is where other tools strand you. A hosts-file pin fails invisibly — the beacon gets connection-refused before a single packet touches the NIC, and success looks identical to "it gave up." This tab turns that silence into evidence:

![Black Hole](Assets/corp_proof.png?v=bh1)

- **The pin** — telemetry opt-outs that ship with a network block (from Shields Up's Telemetry page or this app's rules engine) pin the beacon domain in the hosts file under a marker line carrying the rule id. `0.0.0.0` is the default: attempts fail instantly, system-wide, nothing logged, nothing leaves the machine.
- **The sinkhole** — flip the toggle and the pin points at `127.0.0.1` instead, where AutoCommand owns a listener on loopback 443. Every attempt now completes a real TCP handshake, sends its TLS ClientHello — from which the intended hostname is read — and is closed with **no reply, ever**. The peer PID is captured from the OS connection table while the socket is live; WMI adds the process image and full command line.
- **The proof** — each denied attempt lands in the live feed and the cumulative counters ("2,041 denied since Oct 13"), per hostname and per process, persisted as plain JSONL next to the app (`telemetry_proof.jsonl` — readable, greppable, exportable to CSV, clearable). Rows read: *time · hostname attempted · process · pid · command line · denied at sinkhole*.
- **The protection status card** — because "blocked" means two different things. A flag opt-out only governs processes started after the change (ultralytics decides at import; yesterday's still-running script keeps beaconing under the old setting — the card counts those processes by comparing start times against the settings file). A pin stops everything immediately, and only pinned attempts appear in this tab's feed.
- **Honest edges, stated in the tab itself** — proof collects while the app runs (with it closed, attempts still fail instantly, just unlogged); silent (`0.0.0.0`) pins deny attempts but leave nothing observable to log — that is what the logging mode is for; if another local server already owns `127.0.0.1:443` (local HTTPS dev is the usual suspect) the sinkhole stays off and says so while the pin keeps blocking; pin addresses are only ever `0.0.0.0` or `127.0.0.1`, enforced in code — a tampered rules file cannot redirect a domain anywhere else.

## Opt-outs that actually hold

![Update Control + Telemetry](Assets/corp_control.png?v=fluent1)

The Telemetry page (Shields Up) applies each vendor's own documented switch — and this release makes three guarantees the ecosystem rarely gives you:

- **Written the way the app reads it.** JSON booleans as booleans, re-read after writing. A quoted `"false"` that the target parses as *on* is reported as **Opt-out not honored**, in amber, with the evidence line showing the raw value — not as a fake green "Opted out."
- **Restart-aware messaging.** Opt-outs apply to processes started after the change; already-running ones keep the old setting until restarted. The result dialog says so instead of letting you wonder.
- **A ladder when the switch isn't enough.** Vendor flag → the app's own offline env var → hosts pin → sinkhole. Each rung is one click, reversible, and the last one shows you the attempts it denies.

## Blocked things, managed

Blocking is easy; *un*-blocking is where other tools strand you. AutoCommand:

- **warns before you block** — Microsoft/CDN destinations and DNS-carrying service hosts are flagged with what will break;
- **keeps rules self-describing** — 2 idempotent rules per target with full provenance in the description, readable from `wf.msc`; legacy `AutoCommand IP Block …` names **auto-migrate silently** to the clean scheme the first time you open the manager;
- **groups every block in one manager** — Microsoft/Windows rows in red, bulk unblock, legacy 4-rules-per-IP consolidation, and hit counts from the firewall's own drop log (one click to enable);
- **verifies unknown IPs for you** — one button asks the internet registries (RDAP) who owns each target, cached machine-wide;
- **restores you from mistakes** — the status-bar 🚑 button disables every AutoCommand block and flushes DNS in one click; *Restore except Microsoft/Windows* brings your real blocks back while leaving only the breakage-causing ones off.

![Hardening](Assets/corp_hardening.png?v=fluent1)

## What AutoCommand will not do

- **Nothing runs without you.** Blocks, kills, and instrumentation installs all confirm first.
- **Observational by default.** The spawn/connection tabs only watch; the invasive capabilities (call stacks, TLS plaintext) exist solely for python environments you explicitly instrument.
- **No hidden state.** Everything the app remembers is readable JSON/text on disk (listed in the [Technical Notes](docs/TECHNICAL_NOTES.md)); the app collects no usage telemetry — and the code that once did is gone, leftovers included.

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

## Community

Every kind of message has its own door — pick yours in [SUPPORT.md](SUPPORT.md):

- 💬 **Say hello or ask a question** — [Discussions → General](https://github.com/dparksports/autocommand-windows/discussions/categories/general)
- ❤️ **Send kudos / share what you caught** — [Discussions → Show and tell](https://github.com/dparksports/autocommand-windows/discussions/categories/show-and-tell)
