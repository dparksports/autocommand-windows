# AutoCommand v2026.10.21

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🐍 New tab: Python Spawns.** Every python process launch as it happens, with the full picture in one place:
  - **Who / what / when** — Sysmon Event ID 1 traces every `python.exe`/`pythonw.exe` creation system-wide: parent image and parent command line, command line, user, integrity level, hashes, and an Exited flag from Event ID 5. Venv commands spawn two processes (launcher + base interpreter) — they collapse into a single **venv**-badged row, correlated purely by parent PID.
  - **Why (opt-in)** — a small audit hook (`sys.addaudithook`, embedded in the app) can be installed per python environment from the new **Instrument…** dialog. Instrumented pythons record the Python call stack whenever a spawn API fires (`subprocess.Popen`, `os.system`, …), and the tab shows it joined to the spawn row by pid. Uninstall removes only the files the app wrote.
  - **The Instrument dialog finds everything by itself** — PEP 514 registry, every PATH entry, Microsoft Store packages, conda, uv, pyenv-win, scoop/choco, a cancellable deep scan for venvs (`pyvenv.cfg` hunt, bounded depth/directory budget, never follows junctions), and every python the tab's own watcher has seen run. One-click "Instrument all found" routes through the same explicit confirmation; "Forget" keeps an environment out of future discoveries. Embedded pythons (`python._pth`) surface as unusable-with-reason instead of silently missing.
  - **🧵 New tab: Taskhostw Spawns.** The spawn-tracing engine generalized (`ImageSpawnWatcherService` takes the image filter — Python Spawns is now just the python instance of it) and pointed at `taskhostw.exe`: every service-host launch with full parentage, each row resolving to the scheduled task behind it — task path as the row badge, and a detail pane with task summary, user context and the COM-handler DLL (with a flag when the CLSID is a leftover of uninstalled software).
  - **🔍 Process Monitor: high transmitters stand out.** Rows whose transmitted volume crosses a user-set threshold (1 KB default, B/KB/MB units, persisted) pin to the top of the grid tinted red, live, with a count chip — one list instead of a split, so every existing action (block/kill/ignore/trace) works exactly where it did.
  - **What they send (opt-in)** — the same hook sets `SSLKEYLOGFILE` before user code runs; the tab captures TCP streams on its own raw socket, attributes flows to pids at connect time, and decrypts **TLS 1.2 and 1.3 in-tab**: endpoints, SNI, ALPN, and readable HTTP request/response records. The keylog file is user-only-ACL'd, rotates at 5 MB, and one toggle disables keylogging entirely (wipes the file).
  - Regex filter over command lines, pause/clear, JSONL export, row cap 5000; an offline self-test for the engine lives in `docs/selftest/run-selftest.ps1`.
- **🔓 New: Unblock — in Settings and as a standalone script.** On Smart App Control / SmartScreen machines, files extracted from a downloaded zip carry the Mark-of-the-Web and unsigned AutoCommand gets blocked ("dangerous file from the web"), including taskbar pins. The Settings tab now has an **Unblock card**: one click strips the mark from this app's folder, another unblocks any folder you pick (e.g. a fresh zip extraction) — file contents are never touched, only the download marker. Can't launch the app at all? Download **`unblock-autocommand.ps1`** from the release assets, drop it next to `AutoCommand.exe`, right-click → *Run with PowerShell*.
- **No new dependencies.** TLS decryption uses in-box .NET crypto (`AesGcm`, `ChaCha20Poly1305`, hand-rolled HKDF/PRF) — verified against real TLS 1.2 and 1.3 sessions captured through a loopback relay with real keylog secrets.
- **Process Monitor untouched.** The stream capture runs on its own private raw socket; the svchost tab's counters and sniffer are unchanged.

> **Note:** spawn tracing reads the Sysmon channel and stream capture uses raw sockets — run elevated as usual. Stacks and plaintext exist only for environments you instrumented; everything else stays purely observational.

## Assets in this release

- **`AutoCommand-v2026.10.21-win-x64.zip`** — the app. Unzip, run `AutoCommand.exe` as Administrator.
