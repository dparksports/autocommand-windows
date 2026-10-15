// AutoCommand.AgentProxy — the process an MCP client spawns for the
// "autocommand" server entry. Deliberately tiny and privilege-free:
//
//   • reads %LOCALAPPDATA%\AutoCommand\agent\session.json — written only
//     while the user's AI session is active in the running (elevated) app
//   • forwards each stdin JSON-RPC line to 127.0.0.1:<port> with the
//     session bearer token, writes the response to stdout
//   • with no session active, completes the MCP handshake anyway and
//     answers every tool call with setup guidance, so the client connects
//     cleanly at any time and self-describes
//
// The session token never appears in any config file — the MCP
// registration is just "spawn this exe".

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// offline tools/list surface: minimal descriptors (real schemas arrive from
// the app once a session is active — that list is authoritative)
const string OfflineToolsJson = """
[
  { "name": "get_monitor_rows", "description": "Live established-connection snapshot with process attribution and svchost identity (needs an active AutoCommand AI session)." },
  { "name": "get_svchost_identity", "description": "SCM services + -k/-s identity of one svchost instance (needs an active session)." },
  { "name": "run_pid_inquiry", "description": "Full PID provenance report (needs an active session)." },
  { "name": "tail_sysmon", "description": "Recent Sysmon/Operational events (needs an active session)." },
  { "name": "trace_analyze", "description": "svchost trace capture + pattern analysis (needs an active session)." },
  { "name": "classify_ip", "description": "Destination-range classification with block warnings (needs an active session)." }
]
""";

var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

(int Port, string Token)? ReadSession()
{
    try
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   "AutoCommand", "agent", "session.json");
        var o = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        return (o["port"].GetValue<int>(), o["token"].GetValue<string>());
    }
    catch { return null; }
}

async Task<string> ForwardAsync(int port, string token, string body)
{
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        req.Headers.Accept.ParseAdd("application/json");
        req.Headers.Accept.ParseAdd("text/event-stream");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(110));
        using var resp = await http.SendAsync(req, cts.Token);
        if (resp.StatusCode == System.Net.HttpStatusCode.Accepted) return null; // notification accepted
        return await resp.Content.ReadAsStringAsync();
    }
    catch (Exception ex)
    {
        return Error(null, -32002,
            "The AutoCommand AI session ended (app closed or session stopped). " +
            "Start AutoCommand → Settings → AI access → Start session, then retry. (" + ex.Message + ")");
    }
}

static string Result(JsonNode id, JsonObject result) =>
    new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result }.ToJsonString();

static string Error(JsonNode id, int code, string message) =>
    new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    }.ToJsonString();

string AnswerOffline(JsonObject rpc)
{
    JsonNode id = rpc["id"];
    if (id == null) return null; // notification — nothing to answer
    string method = rpc["method"]?.GetValue<string>() ?? "";
    var @params = rpc["params"] as JsonObject;

    switch (method)
    {
        case "initialize":
            string requested = @params?["protocolVersion"]?.GetValue<string>();
            return Result(id, new JsonObject
            {
                ["protocolVersion"] = requested ?? "2025-03-26",
                ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
                ["serverInfo"] = new JsonObject { ["name"] = "autocommand", ["title"] = "AutoCommand (agent bridge)" },
                ["instructions"] = "Bridge to the locally running AutoCommand app. No AI session is currently " +
                    "active — every tool call answers with setup guidance until the user starts one " +
                    "(AutoCommand → Settings → AI access → Start session).",
            });
        case "ping":
            return Result(id, new JsonObject());
        case "tools/list":
            return Result(id, new JsonObject
            {
                ["tools"] = JsonNode.Parse(OfflineToolsJson),
                ["_note"] = "inactive session — full schemas arrive from the app once a session starts",
            });
        case "tools/call":
            string name = @params?["name"]?.GetValue<string>() ?? "";
            return Result(id, new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = "AutoCommand is not running or its AI session is off.\n" +
                            "Ask the user to open AutoCommand → Settings → 'AI access' → Start session, then retry.",
                    },
                },
                ["isError"] = true,
            });
        default:
            return Error(id, -32601, $"Unknown method: {method} (no active AutoCommand session)");
    }
}

string line;
while ((line = Console.ReadLine()) != null)
{
    line = line.Trim();
    if (line.Length == 0) continue;

    JsonObject rpc = null;
    try { rpc = JsonNode.Parse(line) as JsonObject; } catch { }

    if (rpc == null)
    {
        stdout.WriteLine(Error(null, -32700, "Parse error"));
        continue;
    }

    string response;
    var session = ReadSession();
    if (session != null)
        response = await ForwardAsync(session.Value.Port, session.Value.Token, line);
    else
        response = AnswerOffline(rpc);

    if (rpc["id"] != null && !string.IsNullOrEmpty(response))
        stdout.WriteLine(response);
}
// stdin closed — the MCP client is gone
