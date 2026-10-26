# AutoCommand v2026.10.25.3

## The AI bridge that heals itself — and starts itself

Two failure modes both looked like "MCP not working," and both are gone:

**App restarts no longer break the AI session.** The agent proxy and the in-app bridge each enforce MCP's initialize-before-request rule — but the client's handshake is consumed once, possibly hours before an app update or restart gives the bridge a fresh lifecycle. Every tool call then failed with "Server not initialized" forever, invisible to the client (its pipe to the proxy never broke). The proxy now tracks which bridge instance it has handshaked with (`pid:port:token` from `session.json`) and replays the client's handshake the moment that changes — plus one reactive retry if a bridge resets without a session-file change. Each replay announces `tools/list_changed`, so a restarted app's new tools appear immediately. A `session.json` whose process is gone (crash without cleanup) now reads as offline instead of timing out on every call.

**"Investigate my PC" works even when nothing is running.** Two opt-in checkboxes in Settings → AI access, each with its own one-time consent:

- **Start the session automatically whenever AutoCommand runs** — the consent you gave at the checkbox stands on every launch; read-only unless the mutating preference is separately on.
- **If AutoCommand isn't running, let my AI assistant launch it in the background** — when a tool call arrives with no live session and no app running, the proxy starts AutoCommand headless: no window, **read-only**, exits by itself after 30 minutes. Windows shows one UAC approval (the app manages the firewall, so it runs elevated). A 5-minute cooldown prevents prompt storms; an already-running app is never spawned over — its session being off stays a Settings action.

Safety unchanged: loopback-only, per-session random token, token file deleted the instant the app exits, mutating actions still gated by their own preference and a per-action confirmation dialog — which a headless session can never show, so background sessions are read-only by construction.

Also in this release: the bridge feed now records `session_stopped` (making tonight's class of diagnosis a one-liner), headless mode exits silently when the interactive app is already running instead of starting a second bridge, and the offline tool guidance points at the new checkboxes so the first run teaches the fix.
