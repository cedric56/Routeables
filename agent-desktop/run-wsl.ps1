<#
.SYNOPSIS
  Runs the native smoke test of the Linux hosts (Avalonia, Uno) inside WSL, from Windows.

.DESCRIPTION
  Launches agent-desktop/lib/wsl-run.sh in a WSL distribution. That script installs what is missing in WSL (the .NET 10 SDK,
  PowerShell, Node and agent-desktop, the AT-SPI accessibility bus, the libraries of Avalonia and Uno: sudo asks for the password
  of the WSL user once), builds the samples for Linux into ~/.cache/routeables-wsl (the bin and obj folders of the repository stay
  those of Windows), starts the accessibility bus and runs agent-desktop/run.ps1 there. The windows show through WSLg.

  The report is written to out/native-smoke-wsl of the repository, next to out/native-smoke of the Windows run.

  Needs WSL 2 with WSLg (Windows 11, or Windows 10 with wsl --update) and a distribution (Ubuntu: wsl --install -d Ubuntu).

.EXAMPLE
  powershell -File agent-desktop/run-wsl.ps1
  Avalonia and Uno in the default distribution.

.EXAMPLE
  powershell -File agent-desktop/run-wsl.ps1 -Distro Ubuntu-24.04 -Hosts Uno -Setup -Build
  Uno only, reinstalls the tools and rebuilds.
#>
[CmdletBinding()]
param(
  # The WSL distribution (default: the default one).
  [string] $Distro = '',
  # Avalonia (Uno runs on Linux but does not expose its tree there yet: its Skia renderer has no AT-SPI support, "planned" in
  # its documentation; -Hosts Avalonia,Uno tries it anyway).
  [string[]] $Hosts = @('Avalonia'),
  # Installs or updates the tools in WSL even when they are there.
  [switch] $Setup,
  # Builds again even when an executable is there.
  [switch] $Build,
  # Passed on to run.ps1.
  [string[]] $Pages = @(),
  [switch] $SkipDialogs,
  [switch] $SkipDetails,
  # No test: each app is started alone and what the accessibility bus sees of it is written to out/native-smoke-wsl/diagnose.txt.
  [switch] $Diagnose,
  # The X server of the test: xephyr (default: a window of its own on the desktop of WSLg, with openbox inside), xvfb (nothing shown),
  # wslg (the display of WSLg itself: agent-desktop cannot tie the apps to their windows there).
  [ValidateSet('xephyr', 'xvfb', 'wslg')] [string] $Display = 'xephyr'
)

$ErrorActionPreference = 'Stop'
# wsl.exe writes UTF-16 unless told otherwise.
$env:WSL_UTF8 = '1'

$wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
if (-not $wsl) {
  Write-Host 'WSL is not installed. In an administrator console: wsl --install -d Ubuntu, then restart Windows.' -ForegroundColor Red
  exit 2
}
$distros = @(& wsl.exe --list --quiet 2>$null | ForEach-Object { ($_ -replace "`0", '').Trim() } | Where-Object { $_ })
if (-not $distros.Count) {
  Write-Host 'WSL has no distribution. Install one: wsl --install -d Ubuntu' -ForegroundColor Red
  exit 2
}
if ($Distro -and $distros -notcontains $Distro) {
  Write-Host "No WSL distribution '$Distro'. There are: $($distros -join ', ')" -ForegroundColor Red
  exit 2
}
$target = if ($Distro) { @('-d', $Distro) } else { @() }

$repo = Split-Path -Parent $PSScriptRoot
$script = Join-Path $PSScriptRoot 'lib/wsl-run.sh'
# -e: the command runs as is, without a shell in between (no quoting to get right from PowerShell 5.1).
$repoLinux = (& wsl.exe @target -e wslpath -a -u ($repo -replace '\\', '/')) -join ''
$scriptLinux = (& wsl.exe @target -e wslpath -a -u ($script -replace '\\', '/')) -join ''
if (-not $repoLinux -or -not $scriptLinux) { Write-Host "wslpath could not translate $repo" -ForegroundColor Red; exit 2 }

$hostList = (@($Hosts | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })) -join ','
$forward = @()
$pageList = (@($Pages | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })) -join ','
if ($pageList) { $forward += @('-Pages', $pageList) }
if ($SkipDialogs) { $forward += '-SkipDialogs' }
if ($SkipDetails) { $forward += '-SkipDetails' }

Write-Host ("WSL {0}: {1} (report: out/native-smoke-wsl)" -f $(if ($Distro) { $Distro } else { 'default distribution' }), $hostList) -ForegroundColor Cyan
# A copy of the script without carriage returns: a checkout with CRLF line ends would break bash.
$copy = '/tmp/routeables-wsl-run.sh'
& wsl.exe @target -e cp $scriptLinux $copy
& wsl.exe @target -e sed -i 's/\r$//' $copy
& wsl.exe @target -e env "ROUTEABLES_DIAGNOSE=$(if ($Diagnose) { '1' } else { '0' })" "ROUTEABLES_X=$Display" bash $copy $repoLinux $hostList $(if ($Setup) { '1' } else { '0' }) $(if ($Build) { '1' } else { '0' }) @forward
$code = $LASTEXITCODE
# The whole report in one file, to send as is: out/native-smoke-wsl.zip.
$reportDir = Join-Path $repo 'out/native-smoke-wsl'
if (Test-Path -LiteralPath $reportDir) {
  $zip = Join-Path $repo 'out/native-smoke-wsl.zip'
  try {
    Compress-Archive -Path (Join-Path $reportDir '*') -DestinationPath $zip -Force -ErrorAction Stop
    Write-Host "Report zipped: $zip" -ForegroundColor Cyan
  } catch { Write-Host "The report could not be zipped: $($_.Exception.Message)" -ForegroundColor Yellow }
}
exit $code
