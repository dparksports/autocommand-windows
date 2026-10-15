# Smoke test for AutoCommand.AgentProxy (no elevation needed).
# 1. offline: no session file -> clean handshake, static tool list, guidance errors
# 2. online:  crafted session.json + mock JSON-RPC listener -> forwarding + auth header
$ErrorActionPreference = 'Stop'
$proxy = "$PSScriptRoot\AgentProxy\bin\Debug\net10.0-windows10.0.19041.0\AutoCommand.AgentProxy.exe"
if (-not (Test-Path $proxy)) { $proxy = "$PSScriptRoot\bin\Debug\net10.0-windows10.0.19041.0\AutoCommand.AgentProxy.exe" }
"proxy: $proxy"

$agentDir = Join-Path $env:LOCALAPPDATA 'AutoCommand\agent'
$sessionPath = Join-Path $agentDir 'session.json'
$hadSessionFile = Test-Path $sessionPath

# ── 1. offline stdio ─────────────────────────────────────────────────────────
if ($hadSessionFile) { Rename-Item $sessionPath "$sessionPath.smoketest-backup" }
$input1 = @'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","clientInfo":{"name":"smoke"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_monitor_rows","arguments":{}}}
'@
"--- OFFLINE ---"
$out1 = $input1 | & $proxy
$out1 | ForEach-Object {
    $o = $_ | ConvertFrom-Json
    if ($o.result.tools) { "handshake+tools OK: " + (($o.result.tools | ForEach-Object { $_.name }) -join ', ') }
    elseif ($o.result.content) { "offline call: " + $o.result.content[0].text.Substring(0, 80) + " (isError=$($o.result.isError))" }
    elseif ($o.result.serverInfo) { "initialize OK: " + $o.result.serverInfo.name }
    else { "unexpected: $_" }
}

# ── 2. online forwarding against a mock listener ─────────────────────────────
"--- ONLINE (mock listener) ---"
$mockCode = @'
Add-Type -AssemblyName System.Net.Http
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 47955)
$listener.Start()
$received = @()
for ($i = 0; $i -lt 2; $i++) {
    $client = $listener.AcceptTcpClient()
    $stream = $client.GetStream()
    $buf = New-Object byte[] 65536
    $sb = New-Object System.Text.StringBuilder
    do { $n = $stream.Read($buf, 0, $buf.Length); if ($n -gt 0) { [void]$sb.Append([System.Text.Encoding]::UTF8.GetString($buf, 0, $n)) } } while ($stream.DataAvailable)
    $received += $sb.ToString()
    $body = '{"jsonrpc":"2.0","id":99,"result":{"echo":"mock-ok"}}'
    $bytes = [System.Text.Encoding]::UTF8.GetBytes("HTTP/1.1 200 OK`r`nContent-Type: application/json`r`nContent-Length: $($body.Length)`r`nConnection: close`r`n`r`n$body")
    $stream.Write($bytes, 0, $bytes.Length)
    $client.Close()
}
$listener.Stop()
$received | Set-Content "$env:TEMP\agent-mock-received.txt"
'@
$mockScript = Join-Path $env:TEMP 'agent-mock.ps1'
Set-Content $mockScript $mockCode
# run mock in a separate process (start, then wait briefly for it to bind)
$mockProc = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $mockScript -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 1200

# craft the session file the app would have written
New-Item -ItemType Directory -Force -Path $agentDir | Out-Null
@{ port = 47955; token = 'SMOKETOKEN0123456789'; pid = 12345 } | ConvertTo-Json | Set-Content $sessionPath

$input2 = @'
{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"get_svchost_identity","arguments":{"pid":4}}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
'@
$out2 = $input2 | & $proxy
"forwarded responses: $($out2.Count)"
$out2 | ForEach-Object {
    $o = $_ | ConvertFrom-Json
    "  id=$($o.id) echo=$($o.result.echo)"
}

Wait-Process -Id $mockProc.Id -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$received = Get-Content "$env:TEMP\agent-mock-received.txt" -ErrorAction SilentlyContinue
$allReceived = $received -join "`n"
"captured lines: $(@($received).Count)"
"auth header present: $($allReceived -match 'Bearer SMOKETOKEN0123456789')"
"host header ok: $($allReceived -match 'Host: 127\.0\.0\.1:47955')"
"jsonrpc body present: $($allReceived -match 'tools/call')"

# cleanup: restore prior session state
Remove-Item $sessionPath -ErrorAction SilentlyContinue
if ($hadSessionFile) { Rename-Item "$sessionPath.smoketest-backup" $sessionPath }
Remove-Item $mockScript, "$env:TEMP\agent-mock-received.txt" -ErrorAction SilentlyContinue
"PROXY SMOKE TEST DONE"
