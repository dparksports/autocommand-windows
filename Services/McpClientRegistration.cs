using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoCommand.Services
{
    /// <summary>
    /// One MCP client AutoCommand knows how to register the 'autocommand'
    /// server into, in one click from the Be My Guest tab.
    /// </summary>
    public sealed class McpClientRegistration
    {
        public string Id;            // stable id (button Tag)
        public string DisplayName;   // button label
        public string ConfigPath;    // user-scope config file (may not exist yet)
        public string RootKeyPath;   // path to the servers map inside the file,
                                     // '/'-separated: "mcp/servers" (ZCode),
                                     // "servers" (VS Code), "mcpServers" (most)
        public bool IncludeType;     // VS Code entries want "type": "stdio"
        public string DetectDir;     // existence probe for the client's install
        public string Note;          // appended to the success dialog, may be null
    }

    public static class McpClientCatalog
    {
        // ── client catalog ────────────────────────────────────────────────
        // User-scope only (per-repo/project scopes are deliberately out of
        // scope here — 'Copy MCP config' covers them). Paths verified against
        // each client's current docs; see the Notes for known drift.

        private static string Env(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);

        public static readonly IReadOnlyList<McpClientRegistration> Catalog = new[]
        {
            new McpClientRegistration
            {
                Id = "zcode", DisplayName = "ZCode",
                ConfigPath = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".zcode", "cli", "config.json"),
                RootKeyPath = "mcp/servers", DetectDir = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".zcode"),
                Note = null,
            },
            new McpClientRegistration
            {
                Id = "cursor", DisplayName = "Cursor",
                ConfigPath = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"),
                RootKeyPath = "mcpServers", DetectDir = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".cursor"),
                Note = null,
            },
            new McpClientRegistration
            {
                Id = "windsurf", DisplayName = "Windsurf",
                ConfigPath = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
                RootKeyPath = "mcpServers", DetectDir = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf"),
                Note = null,
            },
            new McpClientRegistration
            {
                Id = "vscode", DisplayName = "VS Code",
                ConfigPath = Path.Combine(Env(Environment.SpecialFolder.ApplicationData), "Code", "User", "mcp.json"),
                RootKeyPath = "servers", IncludeType = true,
                DetectDir = Path.Combine(Env(Environment.SpecialFolder.ApplicationData), "Code", "User"),
                Note = "In VS Code, run 'Developer: Reload Window' (or restart it) to pick up the new server.",
            },
            new McpClientRegistration
            {
                Id = "claude-desktop", DisplayName = "Claude Desktop",
                ConfigPath = Path.Combine(Env(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json"),
                RootKeyPath = "mcpServers", DetectDir = Path.Combine(Env(Environment.SpecialFolder.ApplicationData), "Claude"),
                Note = null,
            },
            new McpClientRegistration
            {
                Id = "gemini", DisplayName = "Gemini Antigravity",
                ConfigPath = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".gemini", "config", "mcp_config.json"),
                RootKeyPath = "mcpServers", DetectDir = Path.Combine(Env(Environment.SpecialFolder.UserProfile), ".gemini"),
                Note = "Some Antigravity CLI versions read %USERPROFILE%\\.gemini\\antigravity-cli\\mcp_config.json " +
                       "instead — if the CLI doesn't pick the server up, copy the file there too.",
            },
        };

        // ── registration ──────────────────────────────────────────────────

        public static bool IsDetected(McpClientRegistration client) =>
            Directory.Exists(client.DetectDir);

        /// <summary>
        /// Writes the 'autocommand' entry into the client's config: parse the
        /// existing file, back it up, upsert only our entry, verify by
        /// re-reading. A malformed or foreign-shaped file is never clobbered.
        /// </summary>
        public static (bool ok, string message) Register(McpClientRegistration client, string proxyPath)
        {
            try
            {
                var entry = new JsonObject();
                if (client.IncludeType) entry["type"] = "stdio";
                entry["command"] = proxyPath;
                entry["args"] = new JsonArray();

                var (ok, error) = UpsertEntry(client.ConfigPath, client.RootKeyPath, "autocommand", entry);
                if (!ok) return (false, error);

                string message =
                    "Registered the 'autocommand' MCP server for " + client.DisplayName + " (user scope):\n" +
                    client.ConfigPath +
                    "\n\nProxy: " + proxyPath +
                    "\n\nNew " + client.DisplayName + " sessions will auto-connect it. Tools answer with " +
                    "setup guidance whenever no AI session is active here.";
                if (!string.IsNullOrEmpty(client.Note)) message += "\n\n" + client.Note;
                return (true, message);
            }
            catch (Exception ex)
            {
                return (false, "Could not update the " + client.DisplayName + " config:\n" + ex.Message +
                    "\n\nUse 'Copy MCP config' and add the entry by hand.");
            }
        }

        /// <summary>
        /// Shared merge: idempotent, backup-before-write, verify-after-write.
        /// rootKeyPath is '/'-separated ("mcp/servers" for ZCode, "servers"
        /// for VS Code, "mcpServers" for most) — missing levels are created,
        /// non-object levels abort instead of being overwritten.
        /// </summary>
        internal static (bool ok, string error) UpsertEntry(string configPath, string rootKeyPath, string serverName, JsonObject entry)
        {
            JsonObject root = null;
            if (File.Exists(configPath))
            {
                string text = File.ReadAllText(configPath);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    try { root = JsonNode.Parse(text) as JsonObject; }
                    catch (JsonException) { root = null; } // malformed → refuse below, never clobber
                    if (root == null)
                        return (false, "The config is not a readable JSON object — not touching it:\n" + configPath +
                            "\n\nUse 'Copy MCP config' and add the entry by hand.");
                    File.WriteAllText(configPath + ".autocommand-backup", text); // revert is a rename away
                }
            }
            root ??= new JsonObject();

            var keys = rootKeyPath.Split('/');
            JsonObject section = root;
            for (int i = 0; i < keys.Length; i++)
            {
                var next = section[keys[i]] as JsonObject;
                if (next == null && section[keys[i]] != null)
                    return (false, "The config already has a '" + keys[i] + "' that is not an object — not touching it:\n" +
                        configPath + "\n\nUse 'Copy MCP config' and add the entry by hand.");
                next ??= new JsonObject();
                section[keys[i]] = next;
                section = next;
            }
            section[serverName] = entry.DeepClone(); // clone: callers may reuse the node across registrations

            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            // verify: the re-read file must carry our entry exactly
            var check = JsonNode.Parse(File.ReadAllText(configPath));
            foreach (string k in keys) check = check?[k];
            var verifiedEntry = check?[serverName] as JsonObject;
            bool verified = verifiedEntry != null &&
                            verifiedEntry["command"]?.GetValue<string>() == entry["command"]!.GetValue<string>();
            return verified
                ? (true, null)
                : (false, "Wrote the config but verification re-read failed:\n" + configPath +
                    "\n\nCheck the file, or use 'Copy MCP config' by hand.");
        }

        // ── proxy install ─────────────────────────────────────────────────

        /// <summary>
        /// The proxy is a real .NET app: an apphost exe without its .dll,
        /// .runtimeconfig.json and .deps.json next to it cannot start at all
        /// ("The application to execute does not exist"). Any install must
        /// carry the complete file set — or register the exe where the
        /// complete set already lives.
        /// </summary>
        private static readonly string[] ProxyFileSet =
        {
            "AutoCommand.AgentProxy.exe",
            "AutoCommand.AgentProxy.dll",
            "AutoCommand.AgentProxy.runtimeconfig.json",
            "AutoCommand.AgentProxy.deps.json",
        };

        private static string ToolsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCommand", "tools");

        private static bool FileSetComplete(string dir) =>
            ProxyFileSet.All(f => File.Exists(Path.Combine(dir, f)));

        /// <summary>
        /// Find a runnable proxy and return the path MCP configs should spawn.
        ///   • tools dir already holds a complete set → stable path, done
        ///   • self-contained app dir (hostfxr.dll present) → register in
        ///     place; the runtime lives beside the exe and copying only our
        ///     few files would strand the apphost
        ///   • framework-dependent source (dev tree) → copy the file set to
        ///     the stable tools path (the machine building it has the runtime)
        /// </summary>
        public static string ResolveAndInstallProxy()
        {
            string toolsExe = Path.Combine(ToolsDir, "AutoCommand.AgentProxy.exe");
            if (FileSetComplete(ToolsDir)) return toolsExe;

            var sourceDirs = new List<string>
            {
                AppContext.BaseDirectory,
                Path.Combine(AppContext.BaseDirectory, "tools"),
            };
            // dev layout: walk up to the repo root, where the proxy's bin lives
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++, dir = Path.GetDirectoryName(dir))
                sourceDirs.Add(Path.Combine(dir, "AgentProxy", "bin", "Debug", "net10.0-windows"));

            foreach (string src in sourceDirs)
            {
                if (!FileSetComplete(src)) continue;

                if (File.Exists(Path.Combine(src, "hostfxr.dll"))) // self-contained publish
                    return Path.Combine(src, "AutoCommand.AgentProxy.exe");

                try
                {
                    Directory.CreateDirectory(ToolsDir);
                    foreach (string f in ProxyFileSet)
                        File.Copy(Path.Combine(src, f), Path.Combine(ToolsDir, f), overwrite: true);
                    return toolsExe;
                }
                catch { return Path.Combine(src, "AutoCommand.AgentProxy.exe"); } // copy failed (locked?) — register where it is
            }
            return null;
        }
    }
}
