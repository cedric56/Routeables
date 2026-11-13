<#
.SYNOPSIS
  Smoke test of the XAnimals samples on iOS (MAUI, Avalonia), in the iOS Simulator, driven by idb.

.DESCRIPTION
  Boots an iPhone simulator when none is booted (the newest iOS runtime installed), builds the iOS target of each sample for the
  simulator (dotnet build -f net10.0-ios -r iossimulator-*), installs it with simctl, starts it, then runs the same test as on
  Android: each page of the menu, the first animal of each list and Back, each dialog of the Dialogs page and its Result line.
  The screen is read with idb (idb ui describe-all: the accessibility tree of the app in front, in points) and tapped with
  idb ui tap. agent-desktop does not drive the Simulator: the window of the simulator is one picture to macOS accessibility.

  Not Uno: its iOS accessibility with the Skia renderer is not done yet (it would expose no text).

  Output: out/ios-smoke/report.json, report.md, out/ios-smoke.zip, and one folder per host with calls.jsonl (every idb and simctl
  call) and, for each failed step, the screen as idb saw it (.json) and a screenshot (.png); log-app.txt when a host failed.

  Needs macOS with Xcode and an iOS simulator runtime, the .NET 10 SDK with the ios workload (dotnet workload install ios maui),
  idb (brew install facebook/fb/idb-companion, then pip install fb-idb; -Setup does both) and PowerShell 7.2+.

.EXAMPLE
  pwsh agent-desktop/run-ios.ps1 -Setup
  idb installed when missing, then MAUI and Avalonia on an iPhone simulator, built when they have no app yet.

.EXAMPLE
  pwsh agent-desktop/run-ios.ps1 -Hosts Avalonia -Build -Device 'iPhone 16'
#>
[CmdletBinding()]
param(
  # Maui, Avalonia.
  [string[]] $Hosts = @('Maui', 'Avalonia'),
  [string[]] $Pages,
  # A simulator already known (xcrun simctl list devices). Default: the booted one, else an iPhone of the newest runtime.
  [string] $Udid = '',
  # The name of the device to boot when none is (its beginning is enough: 'iPhone 16').
  [string] $Device = 'iPhone',
  # Installs idb (idb-companion with Homebrew, fb-idb with pip) when it is missing.
  [switch] $Setup,
  # Builds (and installs) even when an app is there.
  [switch] $Build,
  # Leaves the simulator booted at the end (it is shut down when this script booted it).
  [switch] $KeepSimulator,
  [switch] $SkipDialogs,
  [switch] $SkipDetails,
  [int] $StartTimeoutSeconds = 120,
  [int] $StepTimeoutMs = 8000,
  [string] $OutDir = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$total = [Diagnostics.Stopwatch]::StartNew()
if (-not $IsMacOS) { Write-Host 'The iOS Simulator runs on macOS only.' -ForegroundColor Red; exit 2 }
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'out/ios-smoke' }
$OutDir = [IO.Path]::GetFullPath($OutDir)
if (Test-Path -LiteralPath $OutDir) { Remove-Item -LiteralPath $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Import-Module (Join-Path $PSScriptRoot 'lib/Android.Smoke.psm1') -Force
. (Join-Path $PSScriptRoot 'lib/Samples.ps1')

function Split-List([string[]] $Values) { @($Values | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Hosts = Split-List $Hosts
$Pages = Split-List $Pages
if (-not $Pages.Count) { $Pages = $allPages }

# ------------------------------------------------------------------------------------------------------------------------------
# idb
# ------------------------------------------------------------------------------------------------------------------------------

function Find-Idb {
  $onPath = Get-Command idb -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }
  # Where pip puts the scripts of a user install (pip install --user, or a pipx venv).
  $userBase = (& python3 -m site --user-base 2>$null)
  $places = @($(if ($userBase) { Join-Path $userBase 'bin/idb' }), (Join-Path $HOME '.local/bin/idb')) +
    @(Get-ChildItem -Path (Join-Path $HOME 'Library/Python') -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'bin/idb' })
  $places | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}

$idb = Find-Idb
$companion = Get-Command idb_companion -ErrorAction SilentlyContinue
if ((-not $idb -or -not $companion) -and $Setup) {
  Write-Host 'Installing idb (idb-companion with Homebrew, fb-idb with pip) ...' -ForegroundColor Cyan
  $log = Join-Path $OutDir 'install-idb.txt'
  if (-not $companion) { & brew tap facebook/fb *>> $log; & brew install facebook/fb/idb-companion *>> $log }
  if (-not $idb) {
    if (Get-Command pipx -ErrorAction SilentlyContinue) { & pipx install fb-idb *>> $log }
    else {
      # The Python of Homebrew refuses a plain pip install (PEP 668): a user install, allowed explicitly.
      & python3 -m pip install --user --break-system-packages fb-idb *>> $log
      if ($LASTEXITCODE -ne 0) { & python3 -m pip install --user fb-idb *>> $log }
    }
  }
  $idb = Find-Idb
  $companion = Get-Command idb_companion -ErrorAction SilentlyContinue
}
if (-not $idb -or -not $companion) {
  Write-Host "idb is missing ($(if (-not $companion) { 'idb_companion' } else { 'the idb client' })). Install it with -Setup, or:" -ForegroundColor Red
  Write-Host '  brew tap facebook/fb && brew install facebook/fb/idb-companion && pip3 install fb-idb' -ForegroundColor Red
  exit 2
}
Write-Host "idb: $idb (companion: $($companion.Source))" -ForegroundColor DarkGray

# ------------------------------------------------------------------------------------------------------------------------------
# Simulator
# ------------------------------------------------------------------------------------------------------------------------------

$devices = (& xcrun simctl list devices available -j | Out-String | ConvertFrom-Json).devices
$all = @(foreach ($runtime in $devices.PSObject.Properties) {
  if ($runtime.Name -notmatch 'SimRuntime\.iOS-(\d+)-(\d+)') { continue }
  $version = [version]"$($Matches[1]).$($Matches[2])"
  foreach ($d in @($runtime.Value)) { [pscustomobject]@{ Udid = $d.udid; Name = $d.name; State = $d.state; Version = $version } }
})
$booted = $null
if ($Udid) {
  $sim = $all | Where-Object { $_.Udid -eq $Udid } | Select-Object -First 1
  if (-not $sim) { Write-Host "No simulator $Udid (xcrun simctl list devices)." -ForegroundColor Red; exit 2 }
} else {
  $sim = $all | Where-Object { $_.State -eq 'Booted' -and $_.Name -like 'iPhone*' } | Select-Object -First 1
  if (-not $sim) { $sim = $all | Where-Object { $_.Name -like "$Device*" } | Sort-Object Version, Name -Descending | Select-Object -First 1 }
  if (-not $sim) { Write-Host "No '$Device' simulator (xcrun simctl list devices available). Add one with Xcode, Devices and Simulators." -ForegroundColor Red; exit 2 }
}
$Udid = $sim.Udid
if ($sim.State -ne 'Booted') {
  & xcrun simctl boot $Udid | Out-Null
  $booted = $Udid
}
& xcrun simctl bootstatus $Udid -b | Out-Null
Set-IosTarget $idb $Udid
# idb starts its companion for a local simulator; connect makes it known once (an error here is not fatal: describe-all says).
& $idb connect $Udid *> (Join-Path $OutDir 'idb-connect.txt')
Write-Host "Simulator: $($sim.Name) (iOS $($sim.Version), $Udid)" -ForegroundColor Cyan

# The screen in points: the frame of the application in front (SpringBoard right after boot).
$width = 393; $height = 852
try {
  $screen = (& $idb ui describe-all --json --udid $Udid 2>$null | Out-String)
  $first = @(($screen.Substring($screen.IndexOf('['))) | ConvertFrom-Json -Depth 50)[0]
  if ($first.frame.width -gt 0) { $width = [int]$first.frame.width; $height = [int]$first.frame.height }
} catch { }

# ------------------------------------------------------------------------------------------------------------------------------
# Build and install
# ------------------------------------------------------------------------------------------------------------------------------

$rid = if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'iossimulator-arm64' } else { 'iossimulator-x64' }
$catalog = [ordered]@{
  Maui     = @{ Project = 'samples/XAnimals.Maui/XAnimals.Maui.csproj'; BuildArgs = @('-f', 'net10.0-ios') }
  Avalonia = @{ Project = 'samples/XAnimals.Avalonia.iOS/XAnimals.Avalonia.iOS.csproj'; BuildArgs = @() }
}

function Find-App([string] $ProjectDir) {
  $bin = Join-Path $ProjectDir 'bin'
  if (-not (Test-Path -LiteralPath $bin)) { return $null }
  Get-ChildItem -LiteralPath $bin -Recurse -Directory -Filter '*.app' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "[\\/]Debug[\\/]" -and $_.FullName -match [regex]::Escape($rid) } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}

function Get-PlistValue([string] $App, [string] $Key) { (& plutil -extract $Key raw -o - (Join-Path $App 'Info.plist') 2>$null | Out-String).Trim() }

$targets = @()
foreach ($name in $Hosts) {
  $key = @($catalog.Keys | Where-Object { $_ -eq $name }) | Select-Object -First 1
  if (-not $key) { throw "Unknown host '$name'. iOS hosts: $($catalog.Keys -join ', ') (not Uno: no iOS accessibility with its Skia renderer yet)" }
  $spec = $catalog[$key]
  $target = @{ Name = "$key-iOS"; Package = $null; Process = $null; Apk = $null; Problem = $null }
  $targets += $target
  $project = Join-Path $RepoRoot $spec.Project
  $app = Find-App (Split-Path -Parent $project)
  if ($Build -or -not $app) {
    $log = Join-Path $OutDir "build-$key.txt"
    $buildWatch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host "[build] $key (iOS simulator, $rid) ..." -NoNewline
    # A test build for the simulator: no signing, the analyzers' warnings do not stop it.
    & dotnet build $project -c Debug -nologo @($spec.BuildArgs) "-p:RuntimeIdentifier=$rid" -p:TreatWarningsAsErrors=false *> $log
    $ok = $LASTEXITCODE -eq 0
    Write-Host (" {0} in {1:0}s" -f $(if ($ok) { 'ok' } else { 'FAILED' }), $buildWatch.Elapsed.TotalSeconds) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    if (-not $ok) {
      $errors = @(Select-String -LiteralPath $log -Pattern ': error ' | Select-Object -First 3 | ForEach-Object { $_.Line.Trim() })
      $target.Problem = "the build failed (see $(Split-Path -Leaf $log))$(if ($errors.Count) { ': ' + ($errors -join ' | ') })"
      continue
    }
    $app = Find-App (Split-Path -Parent $project)
  }
  if (-not $app) { $target.Problem = "no .app for $rid under the bin of $($spec.Project)"; continue }
  $target.Apk = $app
  $target.Package = Get-PlistValue $app 'CFBundleIdentifier'
  $target.Process = Get-PlistValue $app 'CFBundleExecutable'
  $install = & xcrun simctl install $Udid $app 2>&1 | Out-String
  if ($LASTEXITCODE -ne 0) { $target.Problem = "simctl install failed: $($install.Trim())" }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Run
# ------------------------------------------------------------------------------------------------------------------------------

$options = @{
  OutDir = $OutDir; Pages = $Pages; MenuOrder = $menuOrder; Titles = $titles; FirstAnimal = $firstAnimal; Dialogs = $dialogs
  StartTimeoutSeconds = $StartTimeoutSeconds; StepTimeoutMs = $StepTimeoutMs; SkipDialogs = [bool]$SkipDialogs; SkipDetails = [bool]$SkipDetails
  Width = $width; Height = $height
}
$reports = [ordered]@{}
try {
  foreach ($target in $targets) { $reports[$target.Name] = Invoke-AndroidSmoke $target $options }
} finally {
  if ($booted -and -not $KeepSimulator) { & xcrun simctl shutdown $booted 2>$null | Out-Null }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Report
# ------------------------------------------------------------------------------------------------------------------------------

$report = [ordered]@{ date = (Get-Date).ToString('s'); device = "$($sim.Name) (iOS $($sim.Version))"; udid = $Udid; screen = "${width}x$height pt"; seconds = [Math]::Round($total.Elapsed.TotalSeconds, 1); hosts = $reports }
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutDir 'report.json') -Encoding utf8

function Format-Count($Items) {
  $values = @($Items)
  if (-not $values.Count) { return '-' }
  "{0}/{1}" -f @($values | Where-Object { $_.status -eq 'ok' }).Count, $values.Count
}
$rows = @(foreach ($name in $reports.Keys) {
  $h = $reports[$name]
  [pscustomobject]@{ Host = $name; Status = $h.status.ToUpperInvariant(); Start = $(if ($null -ne $h.startSeconds) { "$($h.startSeconds)s" } else { '-' })
    Pages = Format-Count $h.pages.Values; Details = Format-Count $h.details.Values; Dialogs = Format-Count $h.dialogs.Values
    Time = "$($h.seconds)s"; Calls = $h.calls; Problems = ($h.problems -join '; ') }
})
$columns = 'Host', 'Status', 'Start', 'Pages', 'Details', 'Dialogs', 'Time', 'Calls'
$widths = @{}
foreach ($c in $columns) { $widths[$c] = [Math]::Max($c.Length, (@($rows | ForEach-Object { "$($_.$c)".Length }) + 0 | Measure-Object -Maximum).Maximum) }
Write-Host ''
Write-Host (($columns | ForEach-Object { $_.PadRight($widths[$_]) }) -join '  ')
Write-Host (($columns | ForEach-Object { '-' * $widths[$_] }) -join '  ')
foreach ($row in $rows) { Write-Host (($columns | ForEach-Object { "$($row.$_)".PadRight($widths[$_]) }) -join '  ') -ForegroundColor $(if ($row.Status -eq 'OK') { 'Green' } else { 'Red' }) }
foreach ($row in $rows | Where-Object Problems) { Write-Host "$($row.Host): $($row.Problems)" -ForegroundColor Red }

$md = [Text.StringBuilder]::new()
[void]$md.AppendLine("# iOS smoke ($($report.date), $($report.device), $($report.screen))`n")
[void]$md.AppendLine('| Host | Status | Start | Pages | Details | Dialogs | Time | Calls |')
[void]$md.AppendLine('| --- | --- | --- | --- | --- | --- | --- | --- |')
foreach ($row in $rows) { [void]$md.AppendLine("| $($row.Host) | $($row.Status) | $($row.Start) | $($row.Pages) | $($row.Details) | $($row.Dialogs) | $($row.Time) | $($row.Calls) |") }
foreach ($name in $reports.Keys) {
  $h = $reports[$name]
  $bad = @($h.pages.GetEnumerator()) + @($h.details.GetEnumerator()) + @($h.dialogs.GetEnumerator()) | Where-Object { $_.Value.status -ne 'ok' }
  if (-not @($bad).Count -and -not $h.problems.Count) { continue }
  [void]$md.AppendLine("`n## $name`n")
  foreach ($p in $h.problems) { [void]$md.AppendLine("- $p") }
  foreach ($item in $bad) { [void]$md.AppendLine("- **$($item.Key)**: $($item.Value.status), $($item.Value.detail)") }
}
Set-Content -LiteralPath (Join-Path $OutDir 'report.md') -Value $md.ToString() -Encoding utf8

$failed = @($rows | Where-Object { $_.Status -ne 'OK' }).Count
Write-Host ''
Write-Host ("{0} in {1:0.0}s. Report: {2}" -f $(if ($failed) { "$failed FAILED" } else { 'All passed' }), $total.Elapsed.TotalSeconds, (Join-Path $OutDir 'report.json')) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
try {
  Compress-Archive -Path (Join-Path $OutDir '*') -DestinationPath "$OutDir.zip" -Force -ErrorAction Stop
  Write-Host "Report zipped: $OutDir.zip" -ForegroundColor Cyan
} catch { Write-Host "The report could not be zipped: $($_.Exception.Message)" -ForegroundColor Yellow }
exit $(if ($failed) { 1 } else { 0 })
