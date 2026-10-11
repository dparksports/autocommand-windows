# release.ps1 - one-command release pipeline for AutoCommand.
#
# <Version> in AutoCommand.csproj is the single source of truth; everything else
# (assembly attributes, git tag, zip names, SHA256SUMS.txt, release title) is
# derived from it. Never type a version anywhere else.
#
# NOTE: keep this file pure ASCII. Windows PowerShell 5.1 reads BOM-less scripts
# as ANSI, and non-ASCII characters corrupt execution in ways that are no fun to
# debug (an em-dash once swallowed an exit statement).
#
# Usage:
#   powershell -File release.ps1 -Version 2026.10.16   # full release (pushes!)
#   powershell -File release.ps1 -Version 2026.10.16 -Yes   # no confirm prompt
#   powershell -File release.ps1 -DryRun               # package + verify only
#
# Full release requires: clean working tree on master, release_notes_v<Version>.md
# already written, and `gh` authenticated. Steps: bump csproj -> publish
# self-contained win-x64 -> verify the exe reports the csproj version -> zip app +
# analyzer -> SHA256SUMS.txt (both zips) -> confirm -> commit -> tag -> push ->
# gh release create. If it dies mid-run after the bump, the csproj is left
# modified - fix and re-run.

[CmdletBinding()]
param(
    [string]$Version,
    [switch]$DryRun,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Die($msg)  { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }
function Assert-Exit([string]$what) {
    if ($LASTEXITCODE -ne 0) { Die "$what failed (exit $LASTEXITCODE)" }
}

Step "Parameters: Version=[$Version] DryRun=[$DryRun] Yes=[$Yes]"

# -- The source of truth ------------------------------------------------------
$csproj = Join-Path $PSScriptRoot 'AutoCommand.csproj'
$csprojText = [System.IO.File]::ReadAllText($csproj)
if ($csprojText -notmatch '(?m)^\s*<Version>([\d.]+)</Version>\s*$') {
    Die 'cannot read <Version> from AutoCommand.csproj'
}
$currentVersion = $Matches[1]

if (-not $DryRun) {
    if (-not $Version) { Die 'usage: release.ps1 -Version <N.N.N> (e.g. 2026.10.16) [-DryRun] [-Yes]' }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { Die "version '$Version' must be three-part (e.g. 2026.10.16)" }
    if ([version]$Version -le [version]$currentVersion) {
        Die "v$Version must be greater than the current v$currentVersion"
    }
    git rev-parse -q --verify "refs/tags/v$Version" | Out-Null
    if ($LASTEXITCODE -eq 0) { Die "tag v$Version already exists" }
    if ((git branch --show-current) -ne 'master') { Die 'releases are cut from master only' }
    $dirty = git status --porcelain --untracked-files=no
    if ($dirty) { Die "working tree not clean - commit or stash first:`n$dirty" }
    if (-not (Test-Path "release_notes_v$Version.md")) { Die "release_notes_v$Version.md not found - write the release notes first" }

    Step "Bumping AutoCommand.csproj to $Version (currently v$currentVersion)"
    $newText = $csprojText -replace "(?m)^(\s*<Version>)[\d.]+(</Version>\s*)$", "`${1}$Version`${2}"
    if ($newText -eq $csprojText) { Die 'failed to rewrite <Version> in AutoCommand.csproj' }
    [System.IO.File]::WriteAllText($csproj, $newText)
}

$relVersion = if ($DryRun) { $currentVersion } else { $Version }
$notesFile = "release_notes_v$relVersion.md"
$appZip    = "publish/AutoCommand-v$relVersion-win-x64.zip"
$anaZip    = "publish/SvchostAnalyzer-win-x64.zip"
$sumsFile  = "publish/SHA256SUMS.txt"

# -- Build --------------------------------------------------------------------
Step "Publishing self-contained win-x64 build (v$relVersion)"
dotnet publish AutoCommand.csproj -c Release -r win-x64 --self-contained true -o "publish/v$relVersion"
Assert-Exit 'dotnet publish'

# Standalone capture engine — separate release asset. The app itself links
# SvchostAnalyzer.Core and runs captures in-process (no bundled child exe
# for SmartScreen to block); this exe is the headless/boot-autostart host.
Step "Publishing SvchostAnalyzer CLI (standalone asset)"
if (-not (Test-Path '..\svchost-watch\SvchostAnalyzer\SvchostAnalyzer.csproj')) {
    Die 'svchost-watch checkout not found next to autocommand1004 - required for the SvchostAnalyzer asset'
}
$analyzerDir = "publish/analyzer-v$relVersion"
dotnet publish '..\svchost-watch\SvchostAnalyzer\SvchostAnalyzer.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $analyzerDir
Assert-Exit 'dotnet publish analyzer'

# -- Verify the binary carries the csproj version - the whole point -----------
$exe = "publish/v$relVersion/AutoCommand.exe"
$binaryVersion = (Get-Item $exe).VersionInfo.ProductVersion
if ($binaryVersion -ne $relVersion) {
    Die "published exe reports ProductVersion '$binaryVersion' but expected '$relVersion' - the version pipeline is broken"
}
Step "Verified exe ProductVersion: $binaryVersion"

# -- Package ------------------------------------------------------------------
Step "Zipping release assets"
foreach ($z in @($appZip, $anaZip)) { if (Test-Path $z) { Remove-Item $z } }
Compress-Archive -Path "publish/v$relVersion/*" -DestinationPath $appZip
Compress-Archive -Path "publish/analyzer-v$relVersion/SvchostAnalyzer.exe" -DestinationPath $anaZip

$sums = @(
    "{0}  {1}" -f (Get-FileHash $appZip -Algorithm SHA256).Hash.ToLower(), (Split-Path $appZip -Leaf)
    "{0}  {1}" -f (Get-FileHash $anaZip -Algorithm SHA256).Hash.ToLower(), (Split-Path $anaZip -Leaf)
)
[System.IO.File]::WriteAllLines((Join-Path $PSScriptRoot $sumsFile), $sums)
Step "Wrote $sumsFile (app + analyzer)"

if ($DryRun) {
    Step "Dry run complete - build, zips and checksums verified; nothing committed, tagged or uploaded"
    exit 0
}

# -- Fail-closed gate: nothing below may run without a validated version ------
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Die "refusing to release: Version=[$Version] is not a valid three-part version"
}
if (-not $Yes) {
    $answer = Read-Host "About to commit, tag v$Version, PUSH to origin and create the GitHub release. Type 'release' to proceed"
    if ($answer -ne 'release') { Die 'aborted - nothing was committed, tagged or pushed' }
}

# -- Commit, tag, push, release -----------------------------------------------
Step "Committing and tagging v$Version"
git add AutoCommand.csproj "release_notes_v$Version.md"
git commit -m "release: v$Version"
Assert-Exit 'git commit'
git tag "v$Version"
Assert-Exit 'git tag'

Step "Pushing master and tag"
git push origin master
Assert-Exit 'git push master'
git push origin "v$Version"
Assert-Exit 'git push tag'

Step "Creating GitHub release"
gh release create "v$Version" --title "v$Version" --notes-file "release_notes_v$Version.md" $appZip $anaZip $sumsFile
Assert-Exit 'gh release create'

Step "Released v${Version}: https://github.com/dparksports/autocommand-windows/releases/tag/v${Version}"
