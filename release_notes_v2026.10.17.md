# AutoCommand v2026.10.17

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **📊 Process Monitor columns regrouped** — the traffic columns now read in pairs: **Tx Bytes** sits directly after **Tx Pkts**, and **Rx Pkts** moved to directly after **Host** with **Rx Bytes** following it. Transmit and receive stats are no longer split apart by the Remote IP/Host columns, so conversations are easier to scan at a glance.

## Assets in this release

- **`AutoCommand-v2026.10.17-win-x64.zip`** — the app with the Svchost Trace capture engine bundled at `tools\SvchostAnalyzer.exe`. Unzip, run `AutoCommand.exe` as Administrator.
- **`SvchostAnalyzer-win-x64.zip` + `SHA256SUMS.txt`** — the capture engine standalone (SHA-256 verified against `SHA256SUMS.txt` before anything runs), plus checksums for both zips.
