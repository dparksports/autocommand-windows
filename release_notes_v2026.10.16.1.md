# AutoCommand v2026.10.16.1

Same-day follow-up to v2026.10.16. One focused fix: the Svchost Trace tab could sit permanently empty while svchost instances were plainly appearing elsewhere in the app.

## Bug fix: the Svchost Trace tab stayed empty

- **The trap.** The trace grid filled only after an explicit "▶ Start Trace" click — while the Process Monitor tab auto-starts itself the moment it opens. So svchost instances showing up there never produced a single trace row, and the blank grid read as broken detection. Worse: switching to any other tab silently **stopped a running trace** (the view treated tab navigation as shutdown), and coming back never resumed it — a user who did press Start lost the recording the first time they glanced at another tab.
- **The fix.** The trace now **auto-starts the first time the tab is shown** — the baseline svchost population and any open connections appear immediately, same contract as the Process Monitor tab. And tracing is decoupled from tab visibility: the poll keeps recording across tab switches for the whole app session (the same lifetime policy the capture engine already had — a recorder must not stop because another tab was viewed). Stop/Start remains fully manual after that.
- **Errors no longer modal.** A WMI or netstat failure during polling used to bubble up to the app-level handler and pop an error dialog **every second**. Poll and baseline failures now surface in the status line ("Trace error (tick N): … — retrying") and the trace keeps retrying.
- **The empty state explains itself.** While stopped with no rows, the timeline area shows "Trace is stopped — press ▶ Start Trace…" instead of bare whitespace, so a passive tab can't be mistaken for a dead one.
- **Minor:** repeated visits to the tab no longer stack duplicate status-refresh timer handlers (one init guard, mirroring the Process Monitor view).
