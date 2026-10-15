# AutoCommand v2026.10.16

AI access (the Be My Guest tab) reliability release: the MCP server now starts in every client environment, upgrades cleanly with the app, and speaks the MCP protocol correctly. Honest accounting below — four real bugs fixed, two of them embarrassing.

## Bug fix: the proxy could not start inside desktop MCP clients

- **The trap.** Desktop MCP clients (Electron apps especially) routinely bundle their own .NET and export `DOTNET_ROOT` for everything they spawn — including copies older than .NET 10. A framework-dependent apphost honors that variable, never falls back to the machine-wide runtime, and exits **before any managed code runs**. The client just showed "server failed to start" with nothing actionable. (Reproduced with an empty `DOTNET_ROOT` and with a real .NET-8-only runtime: exit `-2147450749`, "You must install .NET to run this application".)
- **The fix.** The proxy now ships as a **self-contained single-file exe** (~15 MB, runtime bundled, IL-trimmed — `release.ps1` publishes it into the zip's `tools\AgentProxy\`). No framework lookup happens at all, so a hostile `DOTNET_ROOT` cannot break the spawn. Verified against an empty root and a genuine .NET-8-only vendored runtime.

## Bug fix: the deployed proxy never refreshed, and copy failures were swallowed

- The installer's "is the file set there?" check meant **any later release was silently skipped** — clients kept spawning a build from whenever they first registered, forever.
- When the copy did run and a file was locked by a running proxy, the code **silently registered a dev-tree path** as the client's spawn target — a folder that moves or gets cleaned.
- Now: installs go to `%LOCALAPPDATA%\AutoCommand\tools\v<version>\` — a fresh folder per app version (a running proxy never locks the copy, a version bump always refreshes), locked sources fall back to the newest previously-good install with a visible message instead of a dev path, stale folders are cleaned once unlocked, and the proxy's version follows the app's (`release.ps1` bumps both csprojs). Covered by a self-test (`dotnet run --project ProxyInstallSelfTest`) driving refresh, lock and fallback scenarios.

## Bug fix: MCP protocol conformance

- **Protocol version was blind-echoed** — ask for `1999-01-01` and we claimed it. Now negotiated against a supported set (2024-11-05, 2025-03-26, 2025-06-18), same rule in both halves.
- **Requests before `initialize` were answered** (a bare `tools/list` as the first message returned the tool list). Now refused with `-32002`, per the MCP lifecycle — in both halves.
- **`tools/list_changed` could beat the `initialize` response out the door** when a session flipped mid-connection. Now emitted after the in-flight response, and only for initialized clients.
- **Wrong error code**: a dead session answered `-32002` ("not initialized") — a lie, the client *had* initialized. Now `-32603` with the same human guidance, and forwarding failures **echo the request id** (they used to answer `id: null`, which strict clients drop).
- **Server identity** (`serverInfo`) is now identical offline and online, so client UIs stop relabeling the server when a session flips.
- **Registration preflights the install**: the Register button spawns the installed exe and requires an MCP handshake — once normally, once under a poisoned `DOTNET_ROOT` — before writing any client config. No more "registration succeeded, nothing works".

## Bug fix: successful tool calls answered `isError: null`

- Every successful `tools/call` returned `"isError": null` (a nullable flag serialized straight into the JSON-RPC envelope). The MCP schema requires a boolean when the field is present — strict clients (ZCode among them) rejected the whole response, so tools executed fine on this machine while the assistant saw a schema error. Now always a real `true`/`false`, with a regression check in the bridge smoke test.

## Also

- Proxy lifetimes (spawn parent, client name, stop reason) now land in `%LOCALAPPDATA%\AutoCommand\agent\activity.jsonl` next to sessions and tool calls, and a parent-PID watchdog plus stdin EOF make sure the proxy never outlives its client.
- `docs\TECHNICAL_NOTES.md` gained an "MCP server (AgentProxy)" section: two-half architecture, the `DOTNET_ROOT` trap, supported protocol versions, and the by-hand test recipes. `test-agent-proxy.ps1` was rewritten to publish the proxy exactly as shipped and cover negotiation, lifecycle, flip ordering, session restart, error codes and both poisoned-`DOTNET_ROOT` cases (30 checks).
