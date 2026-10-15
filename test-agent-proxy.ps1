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

# ── 3. transition: client connects FIRST, button pressed LATER ────────────────
"--- TRANSITION (connect offline -> session starts mid-connection) ---"
if (Test-Path $sessionPath) { Remove-Item $sessionPath }
# mock listener on a second port (accepts exactly ONE connection: the
# transition test forwards a single call)
$mockCode2 = $mockCode -replace '47955', '47956' -replace '\$i -lt 2', '$i -lt 1'
$mockScript2 = Join-Path $env:TEMP 'agent-mock2.ps1'
Set-Content $mockScript2 $mockCode2
$mock2 = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $mockScript2 -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 1200

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $proxy
$psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true
$psi.UseShellExecute = $false
$proc2 = [System.Diagnostics.Process]::Start($psi)

# before the session: call must answer with guidance
$proc2.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_monitor_rows","arguments":{}}}')
Start-Sleep -Milliseconds 800
# the "button press": session file appears, pointing at the mock
@{ port = 47956; token = 'TRANSITIONTOKEN'; pid = 999 } | ConvertTo-Json | Set-Content $sessionPath
$proc2.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_monitor_rows","arguments":{}}}')
$proc2.StandardInput.Close()
$proc2.WaitForExit(15000) | Out-Null
$out3 = $proc2.StandardOutput.ReadToEnd()

$guidance = $false; $changed = $false; $forwarded = $false
foreach ($l in ($out3 -split "`n" | Where-Object { $_.Trim() })) {
    $o = $l | ConvertFrom-Json
    if ($o.method -eq 'notifications/tools/list_changed') { $changed = $true }
    elseif ($o.result -and $o.result.isError) { $guidance = $true }
    elseif ($o.result -and $o.result.echo -eq 'mock-ok') { $forwarded = $true }
}
"offline call first:   $guidance (expect True)"
"list_changed pushed:  $changed (expect True)"
"call after start:     $forwarded (expect True)"

Wait-Process -Id $mock2.Id -ErrorAction SilentlyContinue
Remove-Item $sessionPath, $mockScript2 -ErrorAction SilentlyContinue
"PROXY SMOKE TEST DONE"
