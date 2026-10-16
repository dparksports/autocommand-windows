# AutoCommand — ask your Windows PC what it's really doing. Get an answer, not a hunch.

![AutoCommand](Assets/corp_banner.png?v=fluent2)

Windows keeps the answers to your questions — *what is this process, who launched it, where is it connecting, why is svchost uploading packets right now, and how do I make it stop?* — scattered across event logs, COM interfaces, the connection table, and seventy unlabeled service hosts. AutoCommand is a free, open-source administrator window that puts each answer where it belongs: a row in a table, a colored card, or one button. Windows 10 and 11, Fluent design, light and dark themes, zero PowerShell in the core.

> **⬇ Download** the latest self-contained build from the [**Releases page**](https://github.com/dparksports/autocommand-windows/releases/latest) — no .NET required. Unzip, run `AutoCommand.exe` as Administrator.
>
> **🔧 How it works inside:** every technology in this README is documented in the [Technical Notes](docs/TECHNICAL_NOTES.md). Apache 2.0.
>
> **🛡 Looking for the setup side?** The first-hour ritual — debloat, firewall presets, privacy opt-outs, OS hardening, update freezes — lives in its own app: [**Shields Up**](https://github.com/dparksports/shields-up/releases/latest) (same author, same undo-first design). AutoCommand stays what this README covers: the analyst toolkit.

---

## Six afternoons it gives back

![Why AutoCommand](Assets/corp_why.png?v=fluent2)

- **A python.exe you didn't start is sending packets to four Google addresses.** Task Manager shows you a number. AutoCommand shows the script's full command line, its parent, every destination with reverse DNS — and, for environments you instrument, the decrypted request and the Python call stack that fired it.
- **svchost.exe uploaded 294 packets to an IP and you want to know why before dinner.** The BITS Transfers tab reconstructs the exact jobs — creator process, destination owner, direction, what file arrived, SHA-256 — and says in plain words why it happened. (Real case: it was Chrome's component updater on Google's CDN. The tab now proves that in one row.)
- **You opted out of telemetry. It kept sending.** Some apps read `"sync": "false"` — quoted — as *on*. AutoCommand writes each opt-out in the exact type the app parses, re-reads it to verify, and shows **Not honored** in amber when the app is lying. For the ones that ignore switches entirely: pin the beacon domain, or sinkhole it and watch every denied attempt by PID and command line.
- **A scheduled task resurrects a dead updater at 3 a.m.** Kill the process and it's back by morning. Here the row resolves to the task itself — trigger, parent, handler DLL — and one click disables the thing that keeps bringing it back.
- **svchost.exe is eating a CPU core and Windows says that's normal.** Seventy service hosts, no labels. The Svchost Trace timeline names them, and the pattern detector flags bad parents, respawn loops, and upload-dominant flows.
- **An app updated itself overnight and your workflow broke.** Freeze it: the updater's write access is denied, read-and-execute untouched, and unfreezing restores the exact permissions that were there before.

And underneath all of it: one click installs Sysmon — from then on every process launch, connection, and DNS query on this machine is recorded locally, survives process exit, and exports to CSV. You wanted a record, not a guess.

## The tabs — fourteen doors into the same machine

![AutoCommand at a glance](Assets/autocommand_infographic.png?v=v17)

| Tab | What it gives you |
|---|---|
| 🔍 **Process Monitor** | Live grid of outbound connections with packet counters (no WinPcap needed; IPv4 and IPv6), background reverse-DNS, and attribution that survives process exit — rows whose process has died are labeled **— exited**, one-shot beacons carry the timestamp of the Sysmon event that created them, and when a new process inherits a tracked destination the row follows it. Block, kill, inspect — with warnings before you take out something Windows needs. Rows above a **user-set transmit threshold (1 KB default)** pin to the top tinted red, so heavy talkers separate themselves from the noise. Opens first, seeded with the machine's existing connections. |
| 📊 **Traffic Patterns** | The "who talks to whom, how much, how often" view: every process → destination IP pair with cumulative sent/received bytes, flow counts, ports, PIDs, and reverse-DNS hostnames — plus a 24-hour per-5-minute activity ring per pair, rendered as a sparkline, and a conservative **beacon-like hint** (regular cadence + similar sizes). Time-window selector (5 min / 1 h / 24 h / all time), regex filter, CSV export, one-click IP block; history persists across restarts. Same documented gaps as the shared capture: IPv4 TCP only — loopback, IPv6, and UDP/QUIC are invisible; bytes accumulate while a capturing tab is open. |
| 🛡 **Firewall** | Curated profiles (lockdown, home-LAN, per-app) in one click over the native `INetFwPolicy2` engine, with drift detection. |
| 🔬 **Svchost Trace** | A timeline over the 70+ `svchost.exe` service hosts — spawns with parents, connection lifetimes, byte flows — plus a pattern detector: bad parents, respawn loops, beacon-regular connections, upload-dominant flows. Exports JSONL; drives a headless capture service with boot auto-start (already-installed standalone copies keep working). |
| 🧵 **Taskhostw Spawns** | Every taskhostw launch traced live — and the "why" built in: each row resolves to the **scheduled task** behind it (task path, user context, COM-handler DLL), with the full task summary and the PID-inquiry trace one click away. |
| 🐍 **Python Spawns** | Every `python.exe` launch as it happens — who spawned it (parent image + command line), what it ran, when it exited. For environments you explicitly instrument: the **Python call stack at the moment a spawn API fired**, and **decrypted TLS plaintext** of what the process sent, from its own SSLKEYLOGFILE secrets + a NIC-level TCP capture. |
| 🕳 **Black Hole** | The other half of blocking telemetry: proof it worked. A **Protection status** card answers what is actually standing between each app and its beacons — flag state (opted out / on / not honored), pin state, and whether pre-flag processes are still beaconing — then lets you act right there: **Opt out**, **Block & log** (pin + sinkhole in one click, first denied attempt lands seconds later), **Remove block**, **Revert**, each with full disclosure. With logging on, every denied attempt is caught live — hostname from the TLS hello, PID, image, command line — counted per domain and process, persisted as plain JSONL, exportable. |
| 🧭 **Sysmon Audit** | One status card + a live feed of every process launch with full command line and parent. Installed and configured from the app — and repaired when the install is broken or inconsistent. |
| 🦠 **Windows Defender** | **Live MsMpEng CPU/memory** with a 5-minute sparkline, engine status (real-time + tamper protection, platform/engine/signature versions, scan history with durations), a **live activity feed** from Defender's own event log (scans paired by Scan ID into durations, detections, definition updates, cloud activity, config changes), and one-click quick/full scan + definition update triggers. Performance knobs: scan **CPU cap** slider and **idle-only scheduled scans**; Tamper Protection refusals are surfaced, not swallowed. The **exclusions view is read-only by design** — the app shows your gaps but never adds exclusions (a classic malware technique), with a deep link to the right Windows Security page. |
| 📥 **BITS Transfers** | Every Background Intelligent Transfer job — the service that moves Windows Update, Chrome components, Defender signatures, and is the socket that famously "phones out as svchost". Per job: **who created it** (process, PID, Authenticode signer), **where it connects** (URL host → resolved IPs → owner org), **what it moves** (direction, bytes, per-file names, size + SHA-256 of delivered files), **how often** (Patterns view per creator→host: counts, per-day averages, regularity), **why** (a reason catalog that turns "svchost uploaded packets to 34.104.35.123" into "Chrome Component Updater on Google's gvt1.com CDN — Normal"), and **whether it bypasses your firewall rules** — answered in three honest layers, not a vague yes/no. **UPLOAD-type jobs get a Review verdict and hit the app's threat status bar** (the documented ATT&CK T1197 exfiltration vector). Content is previewed only through an explicit per-file click; hashes are automatic, content never stored or exported. |
| 🔐 **Data & Privacy** | Live data-sharing settings, the who-receives-your-data catalog, and a US privacy-rights request builder. |
| 🤖 **Be My Guest** | AI access, by invitation: let an assistant (ZCode, Claude, …) read this machine's security telemetry through a local MCP endpoint — **only while you say so**. One dialog states exactly what is readable; the session listens on `127.0.0.1` only, guarded by a per-session token that never touches disk; read-only tools answer immediately, mutating ones ask on every action (and only exist if you tick them on); every tool call lands in the live activity log. One click registers the server in ZCode, Cursor, Windsurf, VS Code, Claude Desktop or Gemini Antigravity — clients spawn a small asInvoker proxy, never this elevated app. Scripts and CI: `AutoCommand.exe --agent-headless [seconds]` runs a read-only, no-UI session. |
| 🔄 **OneSettings** | How often Windows fetches OneSettings remote config (feature flags, experiment cohorts): the inbox `RefreshCache` task runs ~20-40×/day as SYSTEM; here you lower it to any cadence (default 24h) with a timestamped backup, live verification, automatic rollback, and one-click restore to stock — plus **proof it is holding**: observed launches from Task Scheduler history vs expected, bucketed by why each fired (timer / WNF / manual), with drift detection (Windows servicing rewrites inbox tasks; an optional SYSTEM watchdog re-applies your cadence daily) and a machine-readable audit trail of every change. |
| ⚙ **Settings** | Appearance — **Dark/Light theme, applied instantly and remembered**; startup behavior, live version — an **Unblock card** that strips the Mark-of-the-Web SmartScreen/Smart App Control blocks on (this app's folder, or any folder — see the standalone `unblock-autocommand.ps1` release asset) — and the status bar's **🚑 Restore Internet** panic button. The About card signs off: Made with ❤️ in California · Apache License 2.0 · © 2026 Dan Park, magicpoint.ai — and carries the **community graph** + **share** buttons ([live count](https://autocommand-usage-ping.dpark-sports.workers.dev/)). |

## BITS Transfers, in depth

![BITS Transfers](Assets/bits_infographic.png?v=1)

The tab this machine's own investigation asked for (2026-10-15: `svchost -k netsvcs -p -s BITS` uploading packets to 34.104.35.123 — Chrome Component Updater pulling from Google's gvt1.com CDN, three jobs in a minute). Five questions about every job, answered from two read-only sources stitched by job GUID:

- **Where / what** — the `Microsoft-Windows-Bits-Client/Operational` event log (EID 3 job created names the creating process + PID; 59/60 carry the URL and status code; 4 completes with file counts) gives the history; the BITS COM queue (all users, read-only) supplies what the log never records: **direction (download vs upload)**, live state/progress, and per-file **local paths**. Hostnames are forward-resolved to IPs (cached, short TTL), then attributed instantly from built-in CDN ranges or via RDAP registry lookups for unknowns.
- **How often** — the Patterns view rolls every transfer up per (creating process → host): counts, uploads, per-day averages, and an interval-regularity coefficient (the same beacon-CV math as Traffic Patterns — a low CV means machine-scheduled, not human).
- **Why** — a reason catalog maps job name + creator + host to a human explanation and an expected verdict, seeded from real triage (Chrome/gvt1, Windows cloud fonts, Defender signatures, Office CDN, Windows Update). No match → "Unattributed — review" with investigation pointers, never a guess.
- **What they transfer** — direction, byte progress, per-file remote and local names, and on completion an automatic size + SHA-256 of each delivered file (metadata only, exactly like Sysmon hashes binaries).
- **Their content** — size + hash are automatic; the bytes themselves are shown only through the explicit per-file **Preview content** button (read-only text/hex view). Nothing content-related is ever persisted or exported.
- **Does it bypass the firewall?** — answered in the banner and per job, in three honest layers: the active profile's **default outbound action** (with the Windows default of Allow, no rule is evaluated at all — bypass is the default state), any **explicit allow rules scoped to the BITS service or svchost** (the exemptions under a default-deny), and the always-true **structural fact**: transfers execute inside trusted svchost, so firewall rules targeting the *creating* process never match — the documented ATT&CK T1197 evasion.

**UPLOAD-type jobs are the security event**: update delivery is download-only, so a BITS upload is the documented exfiltration shape — those rows get a Review verdict (red) regardless of catalog matches and surface on the app-level threat status bar.

**Limitations, honestly.** The Bits-Client operational log is small (~1 MB by default), so history spans days-to-weeks — the tracer backfills the whole retained channel at start. Direction, live progress and local file paths exist only for jobs observed while the app is running; history-only rows say so instead of guessing. Files moved or deleted after delivery show "gone" with the hash captured at completion time when it was. Mid-transfer temp files (BIT*.TMP) are never read; the `qmgr` job database is not parsed. The tab is strictly read-only toward Windows: it never creates, cancels or modifies a BITS job — BITS delivers Windows Update.

## Python Spawns, in depth

Three questions about every python process that starts, answered live:

- **Who / what / when** — Sysmon Event ID 1 traces every `python.exe`/`pythonw.exe` creation with full parentage (parent image + command line), command line, user, integrity level, hashes, and an Exited flag from Event ID 5. A venv spawn (launcher + base interpreter) collapses into one row with a **venv** badge — correlation is purely parent-PID-based; nothing inside the venv is ever read.
- **Why (the in-process answer)** — a ~90-line audit hook (`sys.addaudithook`, shipped as an embedded payload) is installed **opt-in per environment** into its site-packages. Instrumented pythons record the Python call stack whenever a spawn API fires (`subprocess.Popen`, `_winapi.CreateProcess`, `os.system`, `os.exec/spawn/posix_spawn`, `os.startfile`, even another `sys.addaudithook`), as JSONL the app tails and joins to rows by pid.
- **What they send** — the hook also sets `SSLKEYLOGFILE` (CPython's own mechanism) before user code runs. The tab captures TCP streams on its own raw socket (SIO_RCVALL), attributes flows to pids at connect time (Sysmon EID 3), and decrypts TLS **1.2 and 1.3** in-tab — endpoints, SNI, ALPN, readable HTTP request/response records. All crypto is in-box .NET (`AesGcm`, `ChaCha20Poly1305`, HKDF/PRF) — no extra dependency.

**The consent model is asymmetric by design:** everything observational (spawns, parentage, endpoints, byte counts) needs no cooperation and no opt-in; stacks and plaintext exist *only* for environments you instrumented yourself. Discovery is automatic and exhaustive — PEP 514 registry, every PATH entry, Store packages, conda, uv, pyenv-win, scoop/choco, a bounded deep scan for `pyvenv.cfg` venvs, plus every python the tab has seen run — but installing the hook always goes through a button that shows exactly what will be written, and an uninstall that removes only the manifest-listed files. The keylog lives in a current-user-only-ACL file, rotates at 5 MB, is deleted at app exit when rotated, and one toggle wipes it.

**Limitations, honestly:** loopback traffic never crosses the NIC (invisible); IPv6 and UDP/QUIC are out of scope; pid→flow attribution is by endpoint at connect time; raw `ssl.SSLContext` builders and custom crypto ignore `SSLKEYLOGFILE`; `-S`, frozen and embedded interpreters cannot be instrumented; TLS 1.3 KeyUpdate re-derivation is best-effort and flagged on failure, never guessed. The headless engine pieces have an offline self-test (`docs/selftest/run-selftest.ps1`); the Sysmon watcher and raw-socket capture need the app elevated.

## Black Hole, in depth

Blocking is easy; *knowing it worked* is where other tools strand you. A hosts-file pin fails invisibly — the beacon gets connection-refused before a packet touches the NIC, and success looks identical to "it gave up." This tab turns that silence into evidence:

![Black Hole](Assets/corp_proof.png?v=bh1)

- **The pin** — telemetry opt-outs that ship with a network block pin the beacon domain in the hosts file under a marker line carrying the rule id. `0.0.0.0` default: attempts fail instantly, system-wide, nothing leaves the machine.
- **The sinkhole** — flip the toggle and the pin points at `127.0.0.1`, where AutoCommand owns a listener on loopback 443. Every attempt completes a real TCP handshake, sends its TLS ClientHello — from which the intended hostname is read — and is closed with **no reply, ever**. The peer PID is captured from the OS connection table while the socket is live; WMI adds image and full command line.
- **The proof** — each denied attempt lands in the live feed and cumulative counters ("2,041 denied since Oct 13"), per hostname and per process, persisted as plain JSONL (`telemetry_proof.jsonl` — readable, greppable, exportable, clearable).
- **Rule actions, no app-hopping** — the whole ladder runs from this tab: the vendor flag (typed, verified), **Block & log**, removal that also cleans pins created by Shields Up — and vice versa; pins are marker-matched across both apps.
- **The protection status card** — because "blocked" means two things: a flag opt-out only governs processes started after the change (the card counts still-running pre-flag beaconers by comparing start times), while a pin stops everything immediately.
- **Honest edges, stated in the tab itself** — proof collects while the app runs; silent pins deny but log nothing (that's what logging mode is for); if another server owns `127.0.0.1:443` the sinkhole stays off and says so while the pin keeps blocking; pin addresses are only ever `0.0.0.0` or `127.0.0.1`, enforced in code — a tampered rules file cannot redirect a domain anywhere else.

## Be My Guest, in depth

An AI assistant that can read your security telemetry is either a superpower or a liability — the difference is entirely in who flips the switch. This tab makes the app the one holding it:

![Be My Guest](Assets/corp_ai.png?v=1)

- **Consent first, stated plainly.** Starting a session is one dialog that says exactly what the assistant can read — live connections and their processes, svchost identities, PID provenance reports, Sysmon events and trace analysis — and that what it reads can be repeated by the assistant. That is the consent, spelled out before you give it.
- **Least privilege by construction.** `127.0.0.1` only, guarded by a per-session token that lives in memory alone. Read-only tools answer immediately; mutating tools exist only if you tick them on, and then still ask on **every single call**.
- **Everything is on the record.** The activity log shows every tool call live and persists under `%LOCALAPPDATA%\AutoCommand\agent`. Stop the session, close the app, or crash — the token dies, and a dead token opens nothing (`OnExit` enforces it).
- **The client never gets the keys to the app.** MCP clients spawn `AutoCommand.AgentProxy.exe`, a self-contained asInvoker forwarder under `%LOCALAPPDATA%\AutoCommand\tools\` — the elevated app is never launched by an assistant. One click registers the server in the client's own config (ZCode, Cursor, Windsurf, VS Code, Claude Desktop, Gemini Antigravity — cataloged with config paths and schema quirks, each entry backed up and verified after writing), the proxy is preflighted (must answer an MCP handshake even under a hostile `DOTNET_ROOT`) before anything points at it, and *Copy MCP config* covers any other client. For scripts/CI: `--agent-headless [seconds]` — no UI, **read-only**, mutating tools always off. Details in `docs/TECHNICAL_NOTES.md`.

## Opt-outs that actually hold

![Update Control + Telemetry](Assets/corp_control.png?v=fluent2)

- **Written the way the app reads it.** JSON booleans as booleans, re-read after writing. A quoted `"false"` the target parses as *on* is reported as **Opt-out not honored**, in amber, with the raw value as evidence — not a fake green checkmark.
- **Restart-aware messaging.** Opt-outs apply to processes started after the change; the result dialog says so instead of letting you wonder why the old process is still beaconing.
- **A ladder when the switch isn't enough.** Vendor flag → the app's own offline env var → hosts pin → sinkhole. Each rung is one click, reversible, and the last one shows you the attempts it denies.

## Blocked things, managed

Blocking is easy; *un*-blocking is where other tools strand you:

- **warns before you block** — Microsoft/CDN destinations and DNS-carrying service hosts are flagged with what will break;
- **keeps rules self-describing** — 2 idempotent rules per target with full provenance in the description, readable from `wf.msc`; legacy rule names auto-migrate silently on first open;
- **groups every block in one manager** — Microsoft/Windows rows in red, bulk unblock, legacy consolidation, and hit counts from the firewall's own drop log (one click to enable);
- **verifies unknown IPs for you** — one button asks the internet registries (RDAP) who owns each target, cached machine-wide;
- **restores you from mistakes** — the status-bar 🚑 button disables every AutoCommand block and flushes DNS in one click; *Restore except Microsoft/Windows* brings your real blocks back while leaving only the breakage-causing ones off.

![Hardening](Assets/corp_hardening.png?v=fluent2)

## The contract

- **Nothing runs without you.** Blocks, kills, and instrumentation installs all confirm first.
- **Observational by default.** The spawn/connection tabs only watch; invasive capabilities (call stacks, TLS plaintext, BITS file content) exist solely behind explicit, per-target opt-in — and the Be My Guest endpoint answers nothing at all until you start a session.
- **No hidden state.** Everything the app remembers is readable JSON/text on disk (listed in the [Technical Notes](docs/TECHNICAL_NOTES.md)); the app sends **nothing by default**. The sole optional exception is an **opt-in, anonymous usage count** — compact build date (e.g. `261015`) and Windows build, at most once a day, **no identifier of any kind (not even a random one)**, IP never stored — offered at most three times, off unless you say yes, revocable anytime ([privacy policy](PRIVACY.md); spec + server code are public in `telemetry/` — verify, don't trust).

## Build from source

```powershell
# .NET 10 SDK required
git clone https://github.com/dparksports/autocommand-windows.git
cd autocommand-windows
dotnet build AutoCommand.csproj -c Release

# self-contained build (what the releases ship)
dotnet publish AutoCommand.csproj -c Release -r win-x64 --self-contained
```

Run the published `AutoCommand.exe` as Administrator — WPF, .NET 10, Windows 10 (19041+) / Windows 11. Each engine ships an offline self-test that compiles the app's real sources (`TaskAttributionSelfTest`, `PyTraceSelfTest`, `OneSettingsCadenceSelfTest`, `ProxyInstallSelfTest`, `BitsTraceSelfTest`) — what runs in tests is byte-identical to what ships.

## Credits & license

Made with ❤️ in California

Apache License 2.0

© 2026 Dan Park, magicpoint.ai

## Community

Every kind of message has its own door — pick yours in [SUPPORT.md](SUPPORT.md):

- 💬 **Say hello or ask a question** — [Discussions → General](https://github.com/dparksports/autocommand-windows/discussions/categories/general)
- ❤️ **Send kudos / share what you caught** — [Discussions → Show and tell](https://github.com/dparksports/autocommand-windows/discussions/categories/show-and-tell)
