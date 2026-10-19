# AutoCommand v2026.10.19.1

Same-day follow-up to the Blocks tab: on a machine carrying blocks forward
from a prior install, the tab showed "svchost — service unknown" for every
IP block. Three gaps, three fixes — identity now arrives without anyone
asking for it.

## Why it showed svchost

The blocks were made by the previous release, which recorded only
"svchost.exe" labels. The new build's import looked for the legacy file
next to the exe — but the app runs elevated (CWD becomes System32), the
install lives in a fresh versioned folder each release, and the previous
install's blocked.txt (or a copy placed into the new folder after the app
had already started) sat just outside every path it checked. The blocks
were then rebuilt from the firewall rules themselves, which carry no
service identity. Recoverable — but only via the manual "Repair
attribution" button.

## The fixes

- **Prior installs are discovered.** The import now scans the app's own
  folder, the previous versioned install folders under Downloads/Desktop,
  and the app-data directory for legacy blocked.txt files — newest install
  first, reading foreign files without touching them. Reconcile-built
  records are upgraded in place when a richer legacy row covers the same
  target (precise timestamps, process labels), instead of being skipped as
  duplicates.
- **The rules speak for themselves.** Block rules embed their provenance —
  "[blocked …] svchost.exe [netprofm] PID 1216 → …" — so records rebuilt
  from the firewall now inherit the service identity the monitor resolved
  at block time, no archive required.
- **Attribution repair runs itself.** When the Blocks tab opens and rows
  are still unattributed, the Sysmon/SCM recovery pass runs once per
  session in the background and the list re-renders with names. The manual
  button stays for re-runs.
- **A Service column on every row.** Each blocked IP now carries its own
  "which network service did I block?" answer — display name in the row,
  plain-language description on hover — independent of the group header.

On the machine that motivated this: 66 blocks, all shown as svchost this
morning, now read as Network List Service ×18, DiagTrack telemetry ×13,
Cryptographic Services ×11, Windows Push Notifications ×3, BITS, the
Defender trio by name — with 7 honestly unresolved where the archive
holds no evidence.
