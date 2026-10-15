# AutoCommand v2026.10.15.1

Same-day follow-up to v2026.10.15. One new capability and one real bug fix around AI access (the Be My Guest tab); everything else unchanged.

## One-click MCP registration for the popular AI clients

- **"Register in ZCode" grew into a catalog.** The Be My Guest tab now shows one Register button per known MCP client — ZCode, Cursor, Windsurf, VS Code, Claude Desktop and Gemini Antigravity — enabled when that client is detected on the machine, tooltip showing the exact config file it will touch.
- **Each client's quirks are built in.** ZCode's nested `mcp/servers` shape, VS Code's flat `servers` plus `"type": "stdio"`, Antigravity's `~/.gemini/config/mcp_config.json` (with a note about the CLI variant path), all handled per client. Any other client still has *Copy MCP config*.
- **Registration is safe by construction**, like the telemetry opt-outs: the existing file is parsed and backed up (`*.autocommand-backup`), only the `autocommand` entry is upserted — everything else in the file survives — and the write is verified by re-reading. A malformed or foreign-shaped config is refused, never clobbered.

## Bug fix: the registered proxy could not start at all

- The tools-dir installer copied **only `AutoCommand.AgentProxy.exe`** — but a .NET apphost cannot run without its `.dll`, `.runtimeconfig.json` and `.deps.json` beside it. Every MCP client spawning the registered proxy died with *"The application to execute does not exist: ...AutoCommand.AgentProxy.dll"* before a single JSON-RPC message; registration looked fine and nothing worked.
- The installer now copies the complete file set, and self-contained installs (where the runtime already lives beside the exe) register the proxy in place instead of stranding an apphost.
- Merge logic is covered by a four-case test run against real config shapes (nested ZCode layout with a foreign server preserved, malformed file refused untouched, VS Code schema, fresh create with missing directories) — the test run itself caught and fixed two first-version bugs.
