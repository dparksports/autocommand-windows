# AutoCommand v2026.10.14.1

Same-day follow-up to v2026.10.14. Identical feature set plus the two changes below; the first same-day release under the calendar versioning scheme, hence the fourth `.1` segment.

## Every svchost instance is distinguishable by name

- **Process Monitor rows no longer say just `svchost.exe`.** All ~74 instances share one binary, so a row now resolves the instance's hosted service (SCM ground truth, `Win32_Service`) plus its command-line `-k`/`-s` tokens: `3784 (svchost.exe [WpnService])` — the `-s` service is the identity, `-k netsvcs` only the group pool. The full decode (`svchost.exe -k netsvcs -p -s WpnService`, corroboration verdict, service display names and states) is one hover away.
- **Hidden command lines are disclosed, not guessed.** A standard user's WMI cannot read SYSTEM svchost command lines (66 of 74 instances here) — the tooltip and PID Inquiry say so and suggest the elevated rerun instead of inventing a `-s` claim.
- **Masquerade signals surface in three places at once.** A `-s` claim that contradicts the SCM-resident services for the same PID, or a readable command line with no `-k` at all (the SCM always passes it), flags in the Process Monitor row, the Svchost Trace timeline and its analysis pane, and the CLI analyzer's new detector #6. Per-user services match their `template_LUID` instantiation (`-s CDPUserSvc` ↔ `CDPUserSvc_56ec2`), so genuine instances never false-flag. Verified against the synthetic-threat trace (6/6 detectors), planted edge cases (exactly the 2 planted flags), and live captures of all 74 instances (0 false flags).
- **Identity sticks to the tenant, not the PID.** Rows re-attributed after PID reuse re-resolve their service; svchost firewall blocks and the PID-inquiry dropdown carry the service name so provenance survives in `wf.msc` and reports.

## Task attribution keyed by Sysmon ProcessGuid

- Attribution for `taskhostw.exe` rows now prefers Sysmon's **ProcessGuid** (unique per boot, immune to PID recycling) over the PID wherever a create-event carries it, falling back to a start-time-guarded PID lookup (±5 s) when it doesn't. A **Sysmon EID 5 (process-terminate) watcher evicts the PID-keyed entry the moment the taskhostw exits**, live and in backfill — a later process inheriting a taskhostw's PID no longer inherits its scheduled task.

## Tooling

- `release.ps1` accepts an optional fourth version segment (`2026.10.14.1`) so same-day follow-up releases stay calendar-anchored instead of future-dating the date — the failure mode this week's re-anchor commit removed.
