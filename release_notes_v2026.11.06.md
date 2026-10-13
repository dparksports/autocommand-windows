# AutoCommand v2026.11.06

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **🕳 Black Hole now runs the whole ladder from one tab.** The Protection status card is actionable: pick a detected app and **Opt out** (the vendor's switch — typed write, verified by re-reading, restart-aware), **Block & log** (the hosts pin goes straight into sinkhole mode, the listener engages immediately, and the first knocked attempt lands in the feed seconds later), **Remove block**, or **Revert** — each with the full disclosure Shields Up shows. No app-hopping to get from "it's phoning home" to "watched, denied, and counted."
- **Cross-app pin actions.** Pin removal and status now match *both* apps' marker lines: a pin created by Shields Up is removable here, and vice versa — "act from either app" no longer dies at brand-locked markers. When Shields Up blocks with AutoCommand running, it offers the sinkhole address so this tab catches the attempts.
- **No-restart listener engagement.** A sinkhole pin that appears while the app is already running — from the new rule actions or from Shields Up — engages the listener within the 5-second state refresh. (A busy port 443 is deliberately left alone rather than retried every tick.)
- **Fix: snapshot store path.** `telemetry_state.json` now lives beside the exe instead of the process's current directory, so opt-out revert snapshots are found regardless of how the app was launched.

> **Note:** run elevated as usual. Rules ship at `Assets\telemetry_rules.json` next to the exe; attempt logging needs a pin in `127.0.0.1` (sinkhole) mode with the app running.
