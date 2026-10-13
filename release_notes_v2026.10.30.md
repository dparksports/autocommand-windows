# AutoCommand v2026.10.30

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **Defender tab: performance changes now apply instantly on Tamper-Protected builds.** Live testing exposed the real behavior behind the v2026.10.29 refusal dialogs: `Set-MpPreference` under Tamper Protection doesn't fail fast — it blocks for 30–60 seconds (past the read timeout) and may only apply after the app has already reported failure. The tab now checks the tamper state it already knows and **skips `Set-MpPreference` entirely, writing the Defender Scan policy values directly** (`AvgCpuLoadFactor` for the CPU cap, `ScanOnlyIfIdle` for idle-only scanning under `HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Scan`) — instant, reliable, tamper-safe. Non-tamper machines keep the direct-write-first flow with automatic policy fallback.
- **Idle-only checkbox fixed.** It wasn't broken — its write was failing and the card kept snapping back to Defender's actual state. With the policy route it applies (and reverts) properly, with a "via policy" confirmation.
- **Activity feed descriptions readable.** The feed was showing (and tooltiping) only the first 200 characters of each Defender event. The grid cell now shows the event's first line, and hovering shows the **full multi-line description** — tooltip appears quickly and stays 30 seconds so it can actually be read.
- **Read timeout raised to 60 s** as insurance for slow first-call module loads; policy-owned values (CPU cap and idle-only) are displayed as the effective setting whenever they exist.
- Known state note: on a machine where a preset appeared to fail in v2026.10.29, the value may nevertheless have applied after the timeout — the tab now shows the truth either way.

> **Note:** run elevated as usual — the policy writes need the same admin rights the app already requests.
