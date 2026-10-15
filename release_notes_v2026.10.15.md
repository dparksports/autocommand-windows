# AutoCommand v2026.10.15

The count goes live, AI access becomes a first-class tab, and the README is rebuilt around seven refreshed infographics.

## The anonymous count is live

- **Opt-in, off by default, offered at most three times.** A one-time dialog states the entire payload before you decide: app build date and Windows build, at most once per UTC day, revocable anytime by deleting `HKCU\Software\AutoCommand\UsagePing`. **No identifier of any kind — not even a random install id.** The IP is never read, stored, or forwarded; the country arrives pre-derived at Cloudflare's edge, and the server keeps only daily aggregate counters (one table, no per-install rows anywhere). Spec, worker code, and schema are public in this repo's [`telemetry/`](telemetry/) folder — verify, don't trust. The full policy is in [PRIVACY.md](PRIVACY.md).
- **The Settings About card gained two buttons**: the live [community graph](https://autocommand-usage-ping.dpark-sports.workers.dev/) (daily actives, country mix) and *Share* (copies the download link).
- **The count tracks compact build dates.** The ping folds the version's date to `YYMMDD` — this release reports `261015` — so the adoption series is day-level and same-day revisions fold together by design. The app's own versioning stays `2026.10.NN`.

## 🤖 Be My Guest — AI access in its own tab

- **The AI access card grew into a full tab.** One place for the whole lifecycle: the consent dialog that states exactly what an assistant can read, session start/stop with live status (`127.0.0.1:port · client · call count`), the mutating-tools switch, and the **live activity log** of every tool call, now taller and always at hand.
- **Connect-an-assistant card** documents the trust boundary in-tab: clients spawn the small asInvoker `AutoCommand.AgentProxy` (never this elevated app), the per-session token never touches disk, and `AutoCommand.exe --agent-headless [seconds]` runs a no-UI, read-only session for scripts and CI — mutating tools are always off there, since nothing can confirm an action without the app's UI.

## The weekly ritual is automated

- **`telemetry/server/snapshot-usage.ps1`** snapshots the count (`/v1/stats`), sums real GitHub download counts across both apps' releases, runs the **plausibility anchor** (yesterday's DAU must never exceed cumulative downloads — violations are flagged `SPOOFED-SUSPECTED`, annotated, never deleted), and writes the dated JSON + flat CSV time series into `fundraising/metrics/history/`. `-Install` registers a Monday 09:00 Scheduled Task that catches up if the machine was off; `-Uninstall` removes it. Documented in [`telemetry/docs/WEEKLY_RITUAL.md`](telemetry/docs/WEEKLY_RITUAL.md) along with the expanded deferred-hardening items (rate-limiting `/v1/ping` on a custom domain, token rotation, the emergency off-switch).

## The README, rebuilt

- Rewritten from scratch around **seven refreshed Fluent infographics** — same palette as the app's light theme — including a new *Be My Guest* panel and a banner that now says **"AI only with consent."** The tab table covers all thirteen tabs, the three deep-dives (Python Spawns, Black Hole, Be My Guest) keep every honest limitation, and the usage-count paragraph describes the shipped no-identifier design.

## Also in this build (since v2026.10.14.1)

- **OneSettings**: cadence control tab + CLI for the inbox `RefreshCache` task, live external-start alerts with Sysmon fetch evidence, and a headless `onesettings-enforce` entry point for the watchdog.
- **AgentProxy**: session-start mid-connection lights up without a client restart.
- **Process Monitor**: svchost attribution hardened further; the svchost-watch engine is vendored in-repo (the sibling repository is deprecated).

## Under the hood

- `release.ps1` examples back on the calendar scheme; the compact `YYMMDD` lives where it belongs — in the count, not the app version.
