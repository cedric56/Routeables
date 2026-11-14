<#
.SYNOPSIS
  First step towards a smoke test of the web hosts (Blazor Server, Blazor WebAssembly, Razor Pages, Avalonia Browser) with agent-desktop: starts each
  one, opens it in Chrome with accessibility switched on, and writes what agent-desktop reads of the page. It tests nothing: its output
  (out/web-probe) is what the real test will be written from, as for iOS.

.EXAMPLE
  pwsh agent-desktop/probe-web.ps1 -Hosts BlazorServer,BlazorWasm
#>
[CmdletBinding()]
param(
  [string[]] $Hosts = @('BlazorServer', 'BlazorWasm', 'Razor', 'AvaloniaBrowser'),
  [int] $StartTimeoutSeconds = 240,
  [string] $OutDir = ''
)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'out/web-probe' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Import-Module (Join-Path $PSScriptRoot 'lib/AgentDesktop.Smoke.psm1') -Force
$ad = Resolve-AgentDesktop ''
if (-not $env:AGENT_DESKTOP_HOME) { $env:AGENT_DESKTOP_HOME = Join-Path ([IO.Path]::GetTempPath()) ('agent-desktop-routeables-' + [Environment]::UserName) }
New-Item -ItemType Directory -Force -Path $env:AGENT_DESKTOP_HOME | Out-Null
if (-not $IsWindows) { & chmod 700 $env:AGENT_DESKTOP_HOME }

$catalog = [ordered]@{
  BlazorServer    = @{ Project = 'samples/Blazor/XAnimals.BlazorApp'; Url = 'http://localhost:5205' }
  BlazorWasm      = @{ Project = 'samples/Blazor/XAnimals.BlazorWebAssembly'; Url = 'http://localhost:5013' }
  Razor           = @{ Project = 'samples/Razor/XAnimals.WebApplication'; Url = 'http://localhost:5197' }
  AvaloniaBrowser = @{ Project = 'samples/XAnimals.Avalonia.Browser'; Url = 'http://localhost:5235' }
}
$chrome = $null
if ($IsWindows) { $chrome = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1 }
elseif ($IsMacOS) { $chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' }
else { $chrome = (Get-Command google-chrome, chromium, chromium-browser -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
if (-not $chrome -or -not (Test-Path -LiteralPath $chrome)) { throw 'Chrome was not found.' }

function Invoke-Ad([string[]] $Arguments, [string] $File) {
  $raw = (& $ad @Arguments '--session' 'routeables-web-probe' 2>&1) -join "`n"
  if ($File) { Set-Content -LiteralPath (Join-Path $OutDir $File) -Value $raw -Encoding utf8 }
  $raw
}

foreach ($name in $Hosts) {
  $spec = $catalog[$name]
  if (-not $spec) { Write-Host "Unknown host $name" -ForegroundColor Red; continue }
  $dir = Join-Path $OutDir $name
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $server = $null; $browser = $null
  try {
    $log = Join-Path $dir 'server-out.txt'; $err = Join-Path $dir 'server-err.txt'
    $arguments = @('run', '--project', (Join-Path $RepoRoot $spec.Project), '-c', 'Debug')
    if ($name -ne 'AvaloniaBrowser') { $arguments += @('--no-launch-profile', '--urls', $spec.Url) }
    Write-Host "[$name] dotnet $($arguments -join ' ')"
    $server = Start-Process dotnet -ArgumentList $arguments -PassThru -RedirectStandardOutput $log -RedirectStandardError $err -WindowStyle Hidden
    $url = $spec.Url
    $deadline = [DateTime]::UtcNow.AddSeconds($StartTimeoutSeconds)
    $up = $false
    while ([DateTime]::UtcNow -lt $deadline -and -not $server.HasExited) {
      if ($name -eq 'AvaloniaBrowser' -and (Test-Path $log)) {
        $listening = Select-String -LiteralPath $log -Pattern 'Now listening on: (http://\S+)' | Select-Object -First 1
        if ($listening) { $url = $listening.Matches[0].Groups[1].Value }
      }
      try { if ((Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200) { $up = $true; break } } catch { }
      Start-Sleep -Seconds 3
    }
    if (-not $up) { Set-Content (Join-Path $dir 'problem.txt') "the server did not answer on $url in $StartTimeoutSeconds s (exited: $($server.HasExited))"; Write-Host "[$name] no answer" -ForegroundColor Red; continue }
    Write-Host "[$name] up on $url"

    $profile = Join-Path ([IO.Path]::GetTempPath()) "routeables-chrome-$name"
    if (Test-Path $profile) { Remove-Item -Recurse -Force $profile }
    $browser = Start-Process $chrome -PassThru -ArgumentList @('--force-renderer-accessibility', '--no-first-run', '--no-default-browser-check', "--user-data-dir=$profile", '--window-size=1280,900', '--new-window', $url)
    Start-Sleep -Seconds 20   # Blazor WebAssembly downloads its runtime first

    $pageFile = "$name/"
    Invoke-Ad @('list-apps') "$pageFile/apps.json" | Out-Null
    Invoke-Ad @('list-windows') "$pageFile/windows.json" | Out-Null
    foreach ($app in 'Google Chrome', 'chrome', 'Chrome') {
      $text = Invoke-Ad @('snapshot', '--app', $app, '--compact', '--max-depth', '60', '--include-bounds') "$pageFile/snapshot-$($app -replace ' ','_').json"
      if ($text -match '"ok"\s*:\s*true') {
        Invoke-Ad @('find', '--app', $app, '--role', 'link', '--limit', '40', '--include-bounds') "$pageFile/links.json" | Out-Null
        Invoke-Ad @('find', '--app', $app, '--role', 'button', '--limit', '40', '--include-bounds') "$pageFile/buttons.json" | Out-Null
        Invoke-Ad @('screenshot', (Join-Path $dir 'page.png'), '--app', $app) "$pageFile/screenshot.json" | Out-Null
        Write-Host "[$name] read with --app '$app'" -ForegroundColor Green
        break
      }
    }
  } catch { Set-Content (Join-Path $dir 'problem.txt') $_.Exception.Message; Write-Host "[$name] $($_.Exception.Message)" -ForegroundColor Red }
  finally {
    if ($browser -and -not $browser.HasExited) { Stop-Process -Id $browser.Id -Force -ErrorAction SilentlyContinue }
    Get-Process chrome -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($server -and -not $server.HasExited) { & taskkill /PID $server.Id /T /F 2>$null | Out-Null; Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
  }
}
Write-Host "Probe written to $OutDir"
