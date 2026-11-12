<#
.SYNOPSIS
  Runs every smoke test of the XAnimals samples that this machine can run, one after the other, and sums them up.

.DESCRIPTION
  - Windows: run.ps1 (WPF, Avalonia, MAUI, Uno, WinUI 3), then run-wsl.ps1 (Avalonia on Linux, in WSL) when WSL is there.
  - Linux: lib/wsl-run.sh straight on this machine (Avalonia; it needs a display: a desktop session, or xvfb-run in CI).
  - macOS: run.ps1 (Avalonia, Uno).
  - Everywhere: run-android.ps1 (MAUI, Uno, Avalonia on Android) when an Android SDK is found.

  The tests take the screen (physical clicks), so they run one after the other, never at the same time. Each one writes its
  own report (out/native-smoke, out/native-smoke-wsl, out/android-smoke); this script adds out/all-smoke/summary.md and
  summary.json, and exits with 1 when anything failed.

.EXAMPLE
  pwsh agent-desktop/run-all.ps1
  Everything this machine can run.

.EXAMPLE
  pwsh agent-desktop/run-all.ps1 -Only Desktop,Android -Build
  The desktop and Android tests, everything rebuilt.
#>
[CmdletBinding()]
param(
  # Desktop (run.ps1), Wsl (Windows only), Linux (Linux only), Android. Default: all that apply here.
  [string[]] $Only = @(),
  [string[]] $Skip = @(),
  # Builds again (passed on to every script).
  [switch] $Build,
  [string[]] $Pages = @(),
  [switch] $SkipDialogs,
  [switch] $SkipDetails,
  # Passed on to run-android.ps1.
  [string] $AndroidSerial = '',
  [string] $Avd = ''
)

if ($PSVersionTable.PSVersion.Major -lt 7) {
  $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
  if (-not $pwsh) { Write-Host 'This script needs PowerShell 7.2 or later (winget install --id Microsoft.PowerShell).' -ForegroundColor Red; exit 2 }
  $forward = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
  foreach ($entry in $PSBoundParameters.GetEnumerator()) {
    $value = $entry.Value
    if ($value -is [switch]) { if ($value.IsPresent) { $forward += "-$($entry.Key)" }; continue }
    $text = if ($value -is [array]) { $value -join ',' } else { "$value" }
    if ($text) { $forward += @("-$($entry.Key)", $text) }
  }
  & $pwsh.Source @forward
  exit $LASTEXITCODE
}

$ErrorActionPreference = 'Stop'
$total = [Diagnostics.Stopwatch]::StartNew()
$here = $PSScriptRoot
$repo = Split-Path -Parent $here
$pwshExe = (Get-Process -Id $PID).Path

function Split-List([string[]] $Values) { @($Values | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Only = Split-List $Only
$Skip = Split-List $Skip
$Pages = Split-List $Pages

$common = @()
if ($Pages.Count) { $common += @('-Pages', ($Pages -join ',')) }
if ($SkipDialogs) { $common += '-SkipDialogs' }
if ($SkipDetails) { $common += '-SkipDetails' }
$buildArgs = if ($Build) { @("-Build") } else { @() }

# What this machine can run, in the order it runs.
$suites = [Collections.Generic.List[hashtable]]::new()
if ($IsWindows) {
  $suites.Add(@{ Name = 'Desktop'; Platform = 'Windows'; Report = 'out/native-smoke'; Run = { & $pwshExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'run.ps1') @buildArgs @common } })
  if (Get-Command wsl.exe -ErrorAction SilentlyContinue) {
    $suites.Add(@{ Name = 'Wsl'; Platform = 'Linux (WSL)'; Report = 'out/native-smoke-wsl'; Run = { & $pwshExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'run-wsl.ps1') @buildArgs @common } })
  }
} elseif ($IsMacOS) {
  $suites.Add(@{ Name = 'Desktop'; Platform = 'macOS'; Report = 'out/native-smoke'; Run = { & $pwshExe -NoProfile -File (Join-Path $here 'run.ps1') -Hosts Avalonia, Uno @buildArgs @common } })
} elseif ($IsLinux) {
  $suites.Add(@{ Name = 'Linux'; Platform = 'Linux'; Report = 'out/native-smoke-wsl'; Run = {
    & bash (Join-Path $here 'lib/wsl-run.sh') $repo 'Avalonia' '0' $(if ($Build) { '1' } else { '0' }) @common } })
}
$suites.Add(@{ Name = 'Android'; Platform = 'Android'; Report = 'out/android-smoke'; Run = {
  $android = @()
  if ($AndroidSerial) { $android += @('-Serial', $AndroidSerial) }
  if ($Avd) { $android += @('-Avd', $Avd) }
  & $pwshExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'run-android.ps1') @buildArgs @common @android } })

$selected = @($suites | Where-Object { (-not $Only.Count -or $Only -contains $_.Name) -and $Skip -notcontains $_.Name })
if (-not $selected.Count) { Write-Host "Nothing to run here (suites: $(($suites | ForEach-Object Name) -join ', '))." -ForegroundColor Yellow; exit 2 }

# ------------------------------------------------------------------------------------------------------------------------------
# Run, one after the other
# ------------------------------------------------------------------------------------------------------------------------------

$runs = @()
foreach ($suite in $selected) {
  $reportPath = Join-Path $repo (Join-Path $suite.Report 'report.json')
  # An old report must not pass for this run's.
  if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
  Write-Host ''
  Write-Host ("===== {0} ({1}) =====" -f $suite.Name, $suite.Platform) -ForegroundColor Cyan
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $code = 0
  try { & $suite.Run; $code = $LASTEXITCODE } catch { Write-Host $_.Exception.Message -ForegroundColor Red; $code = 2 }
  $runs += [pscustomobject]@{ Suite = $suite; Code = $code; Seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 1); Report = $reportPath }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------------------------------------------------------------------

function Format-Count($Section) {
  if (-not $Section) { return '-' }
  $values = @($Section.PSObject.Properties | ForEach-Object { $_.Value })
  if (-not $values.Count) { return '-' }
  $ok = @($values | Where-Object { $_.status -eq 'ok' }).Count
  $unverified = @($values | Where-Object { $_.status -eq 'unverified' }).Count
  "{0}/{1}{2}" -f $ok, $values.Count, $(if ($unverified) { " (+$unverified ?)" } else { '' })
}

$rows = [Collections.Generic.List[object]]::new()
foreach ($run in $runs) {
  if (-not (Test-Path -LiteralPath $run.Report)) {
    # No report: the script stopped before testing (no SDK, no WSL, a build that failed ...): its own output says why.
    $rows.Add([pscustomobject]@{ Platform = $run.Suite.Platform; Host = '-'; Status = $(if ($run.Code -eq 0) { 'OK' } else { 'NOT RUN' }); Pages = '-'; Details = '-'; Dialogs = '-'; Time = "$($run.Seconds)s"; Problems = "no report (exit code $($run.Code))" })
    continue
  }
  $report = Get-Content -Raw -LiteralPath $run.Report | ConvertFrom-Json
  foreach ($h in $report.hosts.PSObject.Properties) {
    $v = $h.Value
    $rows.Add([pscustomobject]@{
      Platform = $run.Suite.Platform; Host = $h.Name; Status = "$($v.status)".ToUpperInvariant()
      Pages = Format-Count $v.pages; Details = Format-Count $v.details; Dialogs = Format-Count $v.dialogs
      Time = "$($v.seconds)s"; Problems = (@($v.problems) -join '; ')
    })
  }
}

$columns = 'Platform', 'Host', 'Status', 'Pages', 'Details', 'Dialogs', 'Time'
$widths = @{}
foreach ($c in $columns) { $widths[$c] = [Math]::Max($c.Length, (@($rows | ForEach-Object { "$($_.$c)".Length }) + 0 | Measure-Object -Maximum).Maximum) }
Write-Host ''
Write-Host '===== Summary =====' -ForegroundColor Cyan
Write-Host (($columns | ForEach-Object { $_.PadRight($widths[$_]) }) -join '  ')
Write-Host (($columns | ForEach-Object { '-' * $widths[$_] }) -join '  ')
foreach ($row in $rows) {
  $color = switch ($row.Status) { 'OK' { 'Green' } 'NOT RUN' { 'Yellow' } default { 'Red' } }
  Write-Host (($columns | ForEach-Object { "$($row.$_)".PadRight($widths[$_]) }) -join '  ') -ForegroundColor $color
}
foreach ($row in $rows | Where-Object Problems) { Write-Host "$($row.Platform) / $($row.Host): $($row.Problems)" -ForegroundColor DarkYellow }

$outDir = Join-Path $repo 'out/all-smoke'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
[ordered]@{ date = (Get-Date).ToString('s'); seconds = [Math]::Round($total.Elapsed.TotalSeconds, 1); rows = $rows } |
  ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outDir 'summary.json') -Encoding utf8
$md = [Text.StringBuilder]::new()
[void]$md.AppendLine("# Smoke tests ($((Get-Date).ToString('s')), $([Math]::Round($total.Elapsed.TotalSeconds))s)`n")
[void]$md.AppendLine('| Platform | Host | Status | Pages | Details | Dialogs | Time |')
[void]$md.AppendLine('| --- | --- | --- | --- | --- | --- | --- |')
foreach ($row in $rows) { [void]$md.AppendLine("| $($row.Platform) | $($row.Host) | $($row.Status) | $($row.Pages) | $($row.Details) | $($row.Dialogs) | $($row.Time) |") }
$problems = @($rows | Where-Object Problems)
if ($problems.Count) {
  [void]$md.AppendLine("`n## Problems`n")
  foreach ($row in $problems) { [void]$md.AppendLine("- **$($row.Platform) / $($row.Host)**: $($row.Problems)") }
}
[void]$md.AppendLine("`nDetails: out/native-smoke, out/native-smoke-wsl, out/android-smoke (report.md of each).")
Set-Content -LiteralPath (Join-Path $outDir 'summary.md') -Value $md.ToString() -Encoding utf8
# In GitHub Actions, the summary also goes to the page of the run.
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $md.ToString() }

$failed = @($rows | Where-Object { $_.Status -notin 'OK', 'NOT RUN' }).Count
Write-Host ''
Write-Host ("{0} in {1:0}s. Summary: {2}" -f $(if ($failed) { "$failed FAILED" } else { 'All passed' }), $total.Elapsed.TotalSeconds, (Join-Path $outDir 'summary.md')) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $(if ($failed) { 1 } else { 0 })
