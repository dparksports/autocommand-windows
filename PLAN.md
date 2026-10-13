# PLAN — Python Spawns tab

> **Status: implemented (v2026.10.21).** All six phases are in. Verified by the
> offline self-test (`docs/selftest/run-selftest.ps1`): TCP reassembly, the
> audit-hook payload end-to-end (stack + SSLKEYLOGFILE), and TLS 1.2 + 1.3
> decryption of real relayed sessions. The Sysmon watcher and the raw-socket
> capture path need an elevated GUI session to exercise live — their event
> parsing mirrors the proven `SysmonWatcherService` pattern (parse EventData
> by name, never by index).

Source of truth: the brief in `C:\Users\honey\py-spawn-monitor\autocommand-glm-prompts.md`
(Prompt 0), distilled into phases below. The prototype (`spawn_monitor.py`) proved
the observational side; the tab keeps its proven primitives (Sysmon events, raw
sockets) and adds the two things the prototype could not do out-of-process: the
Python call stack at spawn (audit hook, opt-in per environment) and TLS plaintext
(SSLKEYLOGFILE + in-tab decryption).

## Goal

One new tab, three capabilities:

1. **Who/what/when** — real-time system-wide tracing of `python.exe`/`pythonw.exe`
   spawns with full parentage (parent image + parent command line), image,
   command line, event time, source, and an Exited lifecycle flag.
2. **Why (in-process)** — for instrumented environments, the Python call stack at
   the moment a spawn API fired, captured by a `sys.addaudithook` payload and
   shipped to the app as JSONL.
3. **What they send** — decrypted TLS plaintext for instrumented processes,
   derived in-tab from SSLKEYLOGFILE secrets + captured TCP streams.

## Design rules (hard constraints)

- Observational + opt-in `sitecustomize`-style injection only. Never read files
  inside venvs, never inject native DLLs, never patch the interpreter binary.
- Do not touch `RawSocketSnifferService` or its counters — the Process Monitor
  tab must be unaffected. The new capture service owns its private socket.
- Sysmon EventData is parsed by XML data NAME, never by property index.
- All secrets (TLS keys, plaintext) live in memory or in the user-ACL'd
  `%LOCALAPPDATA%\AutoCommand\pytrace` directory; never in exports.

## Phases

### P1 — Model + spawn watcher service
Files: `Models/PythonSpawnItem.cs`, `Services/PythonSpawnWatcherService.cs`.
- `PythonSpawnItem`: INotifyPropertyChanged, mirroring `SvchostMonitorItem`.
  TimeUtc, Pid, Image, CommandLine, ParentPid, ParentImage,
  ParentCommandLine, User, IntegrityLevel, Hashes, Source, Exited (notified);
  venv-pairing fields (paired launcher + badge).
- `PythonSpawnWatcherService`: EventLogWatcher on
  `Microsoft-Windows-Sysmon/Operational` for EID 1 (create) and EID 5
  (terminate), client-side filter on Image containing "python"
  (case-insensitive); EID 3 (network) for python images feeds endpoint
  correlation. Events to subscribers; no UI work; Start/Stop/IDisposable.
Accept: build passes; service fires on `python -c "import time; time.sleep(2)"`
(elevated, live).

### P2 — View + tab registration
Files: `Views/PythonSpawnView.xaml(.cs)`, edit `MainWindow.xaml`.
- Mirror `SvchostMonitorView` skeleton (code-behind UserControl,
  CollectionViewSource, dark-theme grid styles, Dispatcher marshaling).
- Tab after Process Monitor, lazy start on first selection (UserControl Loaded),
  Stop on Unloaded, restart-safe.
- Columns: time, pid, exited, image, command line, parent image, parent command
  line, user, integrity. Regex filter over cmdline+parent cmdline+image.
- Venv pairing: python child of a tracked python launch with a different image
  and a `\Scripts\` launcher parent → one row, "venv" badge, launcher shown in
  the detail pane. Correlation purely by ParentProcessId.
- Row cap 5000; Pause/Resume, Clear, Export JSONL; detail pane with a
  "Stack at spawn" placeholder.
Accept: builds; live tab lists python spawns with parentage; venv pair grouped;
export is valid JSONL.

### P3 — Audit-hook payload + installer + tail reader + stack join
Files: `Assets/PyTraceHook.py` (embedded resource), csproj entry,
`Services/PyTraceInstallerService.cs`, `Services/PyTraceReaderService.cs`,
`Models/PyTraceRecord.cs`, view detail-pane wiring,
`Views/PythonInstrumentDialog.xaml(.cs)`.
- Payload: audit hook on subprocess.Popen/_winapi.CreateProcess/os.system/
  os.exec/os.spawn/os.posix_spawn/os.startfile/sys.addaudithook; JSONL records
  (ts, pid, event, repr-safe args, stack) appended to
  `%LOCALAPPDATA%\AutoCommand\pytrace\trace-<pid>.jsonl`; sets SSLKEYLOGFILE
  (to `pytrace\sslkeys.log`) before user code unless already set or disabled via
  flag file; PID-keyed env marker against double registration.
- Installer: enumerate PEP 514 registry environments + folder picker for venvs;
  write payload + uniquely-named `.pth` (never overwrite an existing
  `sitecustomize.py`); manifest of written files; Uninstall removes only
  manifest-listed files.
- Reader: FileSystemWatcher + periodic tail on the pytrace dir; records keyed
  by pid; dead-pid file cleanup (process-gone + age).
- Join: detail pane shows trace records for the selected spawn's pid;
  un-instrumented spawns show "not instrumented — see Settings".
Accept: instrumented venv running `subprocess.run([...])` shows the stack naming
the call line; sslkeys.log receives CLIENT_RANDOM/TLSEnd lines on HTTPS.

### P4 — TCP stream capture service
Files: `Services/TcpStreamCaptureService.cs`, `Models/TcpFlowRecord.cs`.
- Private raw IPv4 socket + SIO_RCVALL (own instance; svchost counters
  untouched); TCP reassembly per 4-tuple both directions with seq dedupe and
  out-of-order handling; per-flow byte cap (16 MB default, head+tail retention,
  truncated flag); idle timeout finalize (60 s); concurrent-flow cap; optional
  finalized-flow JSONL dump under `pytrace\flows\` (size-capped, rotated).
- PID correlation from EID 3 (SourceIp:Port + DestinationIp:Port at connect
  time); caveat surfaced in UI tooltip.
Accept: `python -c "import urllib.request; urllib.request.urlopen('https://…')"`
produces a finalized flow tagged with the pid, both directions' bytes present.

### P5 — TLS plaintext in-tab
Files: `Services/TlsKeylogService.cs`, `Services/TlsDecryptor.cs`,
`Models/TlsDecryptResult.cs`, view plaintext pane; csproj: BouncyCastle.
- NSS keylog parsing (CLIENT_RANDOM TLS 1.2; TLS 1.3 handshake/app traffic
  secrets), tailed lazily.
- TLS 1.2: key block from PRF(master_secret, "key expansion"), AES-GCM /
  AES-CBC / ChaCha20-Poly1305 record decryption. TLS 1.3: per-direction keys
  via HKDF-Expand-Label, explicit nonce = static IV XOR record seq. AEAD tag
  failures classify records (handshake vs app vs undecryptable) — never a
  partial guess. Key updates / 0-RTT best-effort with explicit status flags.
- UI: flows for the pid; selecting a decrypted flow shows endpoints, TLS
  version, SNI (from ClientHello), ALPN, records as text/hex, HTTP/1.x summary
  when parseable.
- Keylog hygiene: user-only ACL on sslkeys.log; rotation at 5 MB (→ `.old`,
  `.old` deleted at app exit); "Disable TLS keylogging" toggle wipes the file
  and stops the hook from setting the env var (flag file).
Accept: instrumented venv + elevated app → HTTPS fetch shows the flow with SNI
and readable HTTP/1.1 records; non-instrumented python shows endpoints but no
plaintext.

### P6 — Docs + polish
- README "Python Spawns" section: architecture, consent model, limitations
  (loopback invisible; UDP/QUIC out of scope; pid→flow attribution by endpoint
  at connect time; custom crypto stacks ignore SSLKEYLOGFILE; -S/frozen/embedded
  interpreters can't be instrumented; elevation requirements).
- Settings persistence (`pytrace\settings.json`): instrumented environments,
  keylog toggle, flow caps/timeouts. Error surfaces: Sysmon missing → offer
  installer; sniffer AccessDenied; malformed JSONL — never swallowed.
- Version bump + release note; demo repro script under `docs/`.

## Known gaps (tracked here, not as TODOs in code)

- TLS 1.3 KeyUpdate re-keying is best-effort: records after a KeyUpdate that we
  cannot re-derive are flagged `undecrypted-after-keyupdate`, not guessed.
- 0-RTT early data may fail decryption (early-secret not in keylog) — flagged.
- Loopback traffic never crosses the NIC: invisible to SIO_RCVALL.
- UDP/QUIC out of scope.
- PID attribution of flows is by endpoint at connect time (EID 3); a reused
  4-tuple could mis-attribute — flagged in UI text.
