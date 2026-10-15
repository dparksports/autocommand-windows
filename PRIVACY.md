# AutoCommand privacy policy

**The short version: by default, AutoCommand sends nothing. Not at startup,
not on a timer, not "anonymized" — nothing.**

The only exception is a **voluntary, opt-in daily count**. If — and only if —
you clicked **"Yes, count me in"** in the dialog the app showed you once, the
app sends the following, at most once per day:

| Field | Value | Why |
|---|---|---|
| `a` | `"autocommand"` | Which app |
| `v` | Build date, compact (e.g. `261015`) | Day-level track of which builds people run |
| `o` | Windows build (e.g. `10.0.26300`) | Know which builds to support |

**There is no identifier in the count — not even a random one.** No
install id, no machine id, no account, nothing that could ever be connected
back to you, your PC, or your Windows account. Each consenting install adds
1 to a daily total, and the server stores daily totals and nothing else:
no per-user rows exist anywhere in the system.

**What this count is not:** no hostnames, no process names, no connection
data, no behavioral events, and nothing about anything you do inside the app.

**Your IP address is never read, stored, or forwarded by AutoCommand's
counting code.** The receiving server (a Cloudflare Worker; code public in
this repository under `telemetry/`) learns only the country code Cloudflare
itself derives at the network edge, and increments a counter. Its entire
database is daily totals per (app, version, country) — a leak of it reveals
counts, and nothing else. The spec, worker code, and schema are public in the
repository's `telemetry/` folder: verify, don't trust.

**Changing your mind:** delete the registry key
`HKEY_CURRENT_USER\Software\AutoCommand\UsagePing` (or set its `Consent`
value to `no`). That withdraws consent permanently. Uninstalling the app
removes the settings with it.

**Everything else the app stores** stays on your machine, in readable files
listed in the [Technical Notes](docs/TECHNICAL_NOTES.md) — there is no other
network sender anywhere in the codebase.

Contact: SECURITY.md lists how to reach the maintainer.

© 2026 Dan Park, magicpoint.ai
