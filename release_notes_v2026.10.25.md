# AutoCommand v2026.10.25

## Blocked by name, blocked everywhere it runs from

Windows Firewall enforces an application rule by **exact image path** — and binaries move. The motivating failure: msedgewebview2.exe was blocked at `EdgeWebView\Application\155.0.4283.60\`, the WebView2 runtime updated to `.62` and deleted the old folder, and the block went inert while the process kept transmitting. Worse, re-blocking hit a name-only duplicate check that answered "already blocked" and did nothing. This release makes a process-name block mean what it says.

- **One rule per install path.** The first path keeps the primary rule name (`AC-BLOCK-APP app.exe (Out)`); every additional location — the new version folder after an update, a second install root, a per-user copy — gets a deterministic sibling rule (`… · <8-hex path hash>`), so the name is blocked wherever it runs from.
- **"Already blocked" is now path-aware.** Blocking the same path refreshes provenance; blocking a moved binary adds the sibling rule. The dead-end is gone.
- **New `AppBlockCoverageService`** keeps coverage current with no user action: it watches Sysmon process-create events and extends coverage **the moment a blocked exe name first runs from an uncovered path** — at the spawn, before its first connection. A startup sweep covers processes that were already running; without Sysmon, the sweep and retire pass still run.
- **Retire pass.** Rules whose program file no longer exists are removed once a live sibling covers the target — never the last rule of a target, so an app mid-repair is never silently unblocked. Runs at startup and every 6 hours.
- **Safety by construction:** auto-extension only ever blocks *more* — a malware binary name-squatting a blocked exe in another folder gets blocked too — so no confirmation gate is needed.
- **Audit trail:** every extension and retirement is journaled to `app-block-extensions.jsonl` with trigger, parent image and rule name, and survives restarts.
- **Unblock and pause/resume now operate on the whole name** — all sibling path rules — not just the row that happened to be clicked.
- **Visibility:** the Blocks tab shows a 🕳 glyph on rules whose path is gone (tooltip explains and says what happens next); the `list_blocks` bridge tool reports a `stale_rules` count per target; the block-hit feed attributes connects by name too, so a hit from a not-yet-covered sibling path reads as an app-block hit, not an unexplained connection.

## Phone Home

New tab: one card per curated Windows talker (DiagTrack, CryptSvc, Delivery Optimization…) — what the service is, what it actually sends, and its current block state. The "which of Windows' own services is phoning home, and did I already block it" question answered on one screen.

## Traces

New tab: project-based tracing. A project is a file under `%LOCALAPPDATA%\AutoCommand\traces\projects` — hand-edits and dropped-in files are picked up by a watcher, and sharing a project means copying its file. Per-project application blocking with full provenance and metrics.
