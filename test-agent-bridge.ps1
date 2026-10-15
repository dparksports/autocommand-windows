# End-to-end smoke test for the AI-access bridge (no admin needed).
# 1. starts AutoCommand --agent-headless
# 2. initialize / tools/list / tools/call over loopback with the session token
# 3. negative checks: bad token -> 401, spoofed Host -> 403
# 4. offline --agent-stdio mode answers with setup guidance
param(
    [string]$Exe = "$PSScriptRoot\bin\Debug\net10.0-windows10.0.19041.0\AutoCommand.exe"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

# -- launch headless session -------------------------------------------------
$proc = Start-Process -FilePath $Exe -ArgumentList '--agent-headless', '120' -PassThru -WindowStyle Hidden
"started pid $($proc.Id)"

$sessionPath = Join-Path $env:LOCALAPPDATA 'AutoCommand\agent\session.json'
$deadline = (Get-Date).AddSeconds(15)
while (-not (Test-Path $sessionPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
if (-not (Test-Path $sessionPath)) { throw "session.json never appeared" }
$session = Get-Content $sessionPath -Raw | ConvertFrom-Json
$port = $session.port; $token = $session.token
"session: port=$port token=$($token.Substring(0,8))... mutating=$($session.mutatingAllowed) tools=$(@($session.tools).Count)"

function Invoke-Rpc([string]$json, [string]$auth = "Bearer $token", [string]$hostHeader = "127.0.0.1:$port") {
    $client = New-Object System.Net.Http.HttpClient
    $req = New-Object System.Net.Http.HttpRequestMessage('POST', "http://127.0.0.1:$port/")
    $req.Content = New-Object System.Net.Http.StringContent($json, [System.Text.Encoding]::UTF8, 'application/json')
    if ($auth)     { $req.Headers.TryAddWithoutValidation('Authorization', $auth) | Out-Null }
    if ($hostHeader) { $req.Headers.TryAddWithoutValidation('Host', $hostHeader) | Out-Null }
    $req.Headers.Accept.ParseAdd('application/json')
    $resp = $client.SendAsync($req).GetAwaiter().GetResult()
    $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    [pscustomobject]@{ Status = [int]$resp.StatusCode; Body = $body }
    $client.Dispose()
}

function Get-ResultText([string]$body) {
    $o = $body | ConvertFrom-Json
    if ($o.result -and $o.result.content) { $o.result.content[0].text }
    else { $body }
}

# -- 1. handshake --------------------------------------------------------------
$r = Invoke-Rpc '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","clientInfo":{"name":"smoke-test"}}}'
$init = ($r.Body | ConvertFrom-Json).result
"handshake: status=$($r.Status) server=$($init.serverInfo.name) version=$($init.serverInfo.version)"
if ($init.serverInfo.name -ne 'autocommand') { throw 'handshake failed' }

# -- 2. tools/list (mutating hidden without the toggle) --------------------------
$r = Invoke-Rpc '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
$tools = ($r.Body | ConvertFrom-Json).result.tools | ForEach-Object { $_.name }
"tools: $($tools -join ', ')"

# -- 3. tools/call: get_svchost_identity on a real svchost PID --------------------
$svcPid = (Get-CimInstance Win32_Service -Filter "Name='WpnService'").ProcessId
$r = Invoke-Rpc ('{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_svchost_identity","arguments":{"pid":' + $svcPid + '}}}')
"identity (PID $svcPid): " + (Get-ResultText $r.Body)

# -- 4. tools/call: get_monitor_rows ----------------------------------------------
$r = Invoke-Rpc '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_monitor_rows","arguments":{}}}'
$snap = ((Get-ResultText $r.Body) | ConvertFrom-Json)
"monitor: $($snap.row_count) established row(s)"
$snap.rows | Select-Object -First 4 | ForEach-Object { "  $($_.process) -> $($_.remote) [$($_.classification)]" }

# -- 5. tools/call: classify_ip ----------------------------------------------------
$r = Invoke-Rpc '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"classify_ip","arguments":{"ip":"20.59.87.225"}}}'
"classify: " + ((Get-ResultText $r.Body) -replace '\s+', ' ')

# -- 6. negative: bad token -> 401 --------------------------------------------------
$r = Invoke-Rpc '{"jsonrpc":"2.0","id":6,"method":"ping"}', 'Bearer deadbeef'
"bad token: status=$($r.Status) (expect 401)"

# -- 7. negative: spoofed Host -> 403 -----------------------------------------------
$r = Invoke-Rpc '{"jsonrpc":"2.0","id":7,"method":"ping"}', "Bearer $token", 'evil.example.com:443'
"spoofed host: status=$($r.Status) (expect 403)"

# -- 8. stop: session file must disappear --------------------------------------------
Stop-Process -Id $proc.Id -Force
Start-Sleep -Milliseconds 800
"session file after stop: exists=$(Test-Path $sessionPath) (expect False)"

# -- 9. offline stdio bridge ----------------------------------------------------------
$offlineInput = @'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","clientInfo":{"name":"smoke-test"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_monitor_rows","arguments":{}}}
'@
$out = $offlineInput | & $Exe --agent-stdio
$out | ForEach-Object {
    $o = $_ | ConvertFrom-Json
    if ($o.result.tools) { "stdio offline tools: " + (($o.result.tools | ForEach-Object { $_.name }) -join ', ') }
    elseif ($o.result.content) { "stdio offline call: " + $o.result.content[0].text.Substring(0, [Math]::Min(90, $o.result.content[0].text.Length)) }
    elseif ($o.result.serverInfo) { "stdio offline handshake: " + $o.result.serverInfo.name }
    else { "stdio: $_" }
}
"SMOKE TEST DONE"
