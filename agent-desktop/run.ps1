<#
.SYNOPSIS
  Smoke test of the native XAnimals samples (WPF, Avalonia, MAUI, Uno, WinUI 3) and of the dotnet new templates, driven by
  agent-desktop through the accessibility tree.

.DESCRIPTION
  For every host: opens each page of the menu; on the six animal pages, opens the first animal, checks its detail and goes back
  with Back; then opens each dialog of the Dialogs page, answers it and checks the Result line of the page ("Answer: Yes.").

  The applications start all at once; they are then tested one after the other, each window in front and maximized, because
  menu entries and list items are clicked physically (they are twins in the accessibility tree: no semantic action reaches them).

  A page is "ok" when the header shows its title (Members: Features, where the guard sends a visitor who is signed out). A detail
  is "ok" when the animal and the Back button appear, and Back brings the list back. A dialog is "ok" when the Result line shows
  the text of the answer that was given.

  Output: out/native-smoke/report.json, report.md, and one folder per host with calls.jsonl (every agent-desktop call with its
  duration), stdout/stderr of the application, and the snapshot of every failed step. The exit code is 0 when everything passed.

  Needs PowerShell 7.2+, agent-desktop (npm install -g agent-desktop) and the .NET 10 SDK for -Build.
  Windows: all five hosts. Linux (a desktop session with at-spi2-core, WSLg included) and macOS: Avalonia and Uno.

.EXAMPLE
  pwsh agent-desktop/run.ps1 -Build
  Builds the samples that are not built yet (only the desktop target of MAUI and Uno), then tests every host of this OS.

.EXAMPLE
  pwsh agent-desktop/run.ps1 -Hosts Wpf,Uno -Pages Cats,Dogs -SkipDialogs
  Two hosts, two pages with their detail, no dialogs.

.EXAMPLE
  pwsh agent-desktop/run.ps1 -Templates avalonia,uno -PackageSource artifacts
  The templates only: dotnet new outside the repository, build, start, Hello -> About -> Hello.
#>
[CmdletBinding()]
param(
  # Wpf, Avalonia, Maui, Uno, WinUI3. Default: every host this OS can run.
  [string[]] $Hosts,
  # A subset of the menu (Cats, Dogs, ... About). Default: all of them.
  [string[]] $Pages,
  # avalonia, maui, uno, winui3. Without -Hosts, only the templates are tested.
  [string[]] $Templates = @(),
  # A folder holding the Routeables packages (dotnet pack -o artifacts) for the templates.
  [string] $PackageSource = '',
  # Builds the samples that have no executable yet.
  [switch] $Build,
  # Builds every sample asked for (incremental), even the ones already built.
  [switch] $BuildAll,
  # Host=path: tests this executable instead of the one found under bin (a Release build, a published folder).
  # -Exe Avalonia=C:/apps/XAnimals.Avalonia.Desktop.exe,Wpf=D:/wpf/XAnimals.Wpf.exe
  [string[]] $Exe = @(),
  [switch] $SkipDialogs,
  # After the dialogs, the notifications of the Dialogs page: the four of the application (shown, then clicked) and the one of the system.
  [switch] $Notifications,
  # macOS: runs each sample from a .app bundle made here (an Info.plist with an identifier, signed ad hoc). The system serves
  # notifications only to an application in a bundle: without it, the "System" notification answers "not available on this host".
  [switch] $Bundle,
  # The "System" notification must be sent: "not available on this host" is a failure (else it is only 'unverified').
  [switch] $RequireSystemNotification,
  # No list -> detail -> Back on the animal pages.
  [switch] $SkipDetails,
  # Looks for an error text (Exception, Unhandled ...) on every page: one full snapshot per page, slow on Windows.
  [switch] $ScanErrors,
  # Never use physical input (clicks that move the pointer, keys). Steps that need it fail instead.
  [switch] $NoHeaded,
  # Adds a PNG next to the snapshot of each failed step.
  [switch] $Screenshots,
  [int] $StartTimeoutSeconds = 90,
  # 1 (default): one host after the other, maximized. 2: two at the same time, each window on half of the screen.
  [int] $Parallel = 1,
  # How long a single step (a page, the answer of a dialog) may take.
  [int] $StepTimeoutMs = 4000,
  # The agent-desktop executable (default: the native binary of the global npm package).
  [string] $AgentDesktop = '',
  [string] $OutDir = ''
)

# Started from Windows PowerShell 5.1 (powershell.exe): the same command again in PowerShell 7 (pwsh), which the script needs.
if ($PSVersionTable.PSVersion.Major -lt 7) {
  $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
  if (-not $pwsh) {
    Write-Host 'This script needs PowerShell 7.2 or later (pwsh), which is not installed.' -ForegroundColor Red
    Write-Host 'Install it:  winget install --id Microsoft.PowerShell --source winget' -ForegroundColor Red
    Write-Host 'then run:    pwsh agent-desktop/run.ps1 -Build' -ForegroundColor Red
    exit 2
  }
  # -ExecutionPolicy Bypass: a script unzipped from a download is refused by pwsh otherwise (it is not signed).
  $forward = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
  foreach ($entry in $PSBoundParameters.GetEnumerator()) {
    $value = $entry.Value
    if ($value -is [switch]) { if ($value.IsPresent) { $forward += "-$($entry.Key)" }; continue }
    $text = if ($value -is [array]) { $value -join ',' } else { "$value" }
    if ($text) { $forward += @("-$($entry.Key)", $text) }
  }
  Write-Host "Windows PowerShell $($PSVersionTable.PSVersion): running again in $($pwsh.Source)" -ForegroundColor DarkGray
  & $pwsh.Source @forward
  exit $LASTEXITCODE
}
if ($PSVersionTable.PSVersion -lt [version]'7.2') {
  Write-Host "This script needs PowerShell 7.2 or later; this is $($PSVersionTable.PSVersion). Update it: winget upgrade --id Microsoft.PowerShell" -ForegroundColor Red
  exit 2
}

$ErrorActionPreference = 'Stop'
$total = [Diagnostics.Stopwatch]::StartNew()
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'out/native-smoke' }
$OutDir = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$modulePath = Join-Path $PSScriptRoot 'lib/AgentDesktop.Smoke.psm1'
Import-Module $modulePath -Force

# With -File, "-Hosts Wpf,Avalonia" arrives as one string.
function Split-List([string[]] $Values) { @($Values | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Hosts = Split-List $Hosts
$Templates = Split-List $Templates
$Pages = Split-List $Pages
$exeByHost = @{}
foreach ($pair in (Split-List $Exe)) {
  $hostName, $path = $pair -split '=', 2
  if (-not $path) { throw "-Exe takes Host=path, not '$pair'" }
  $exeByHost[$hostName.Trim()] = $path.Trim()
}

$currentOs = if ($IsWindows) { 'Windows' } elseif ($IsMacOS) { 'MacOS' } else { 'Linux' }
$everywhere = 'Windows', 'Linux', 'MacOS'

# ------------------------------------------------------------------------------------------------------------------------------
# What is tested
# ------------------------------------------------------------------------------------------------------------------------------

# Build: only the desktop target. MAUI and Uno also target Android and iOS; building those is what made -Build slow.
# Uno uses CiBuild like the CI does: it keeps net10.0-desktop only, so no mobile workload is needed to restore it.
$catalog = [ordered]@{
  Wpf      = @{ Project = 'samples/XAnimals.Wpf/XAnimals.Wpf.csproj'; App = 'XAnimals.Wpf'; Os = @('Windows'); BuildArgs = @() }
  Avalonia = @{ Project = 'samples/XAnimals.Avalonia.Desktop/XAnimals.Avalonia.Desktop.csproj'; App = 'XAnimals.Avalonia.Desktop'; Os = $everywhere; BuildArgs = @() }
  Maui     = @{ Project = 'samples/XAnimals.Maui/XAnimals.Maui.csproj'; App = 'XAnimals.Maui'; Os = @('Windows'); BuildArgs = @('-f', 'net10.0-windows10.0.19041.0') }
  Uno      = @{ Project = 'samples/XAnimals.Uno/XAnimals.Uno.csproj'; App = 'XAnimals.Uno'; Os = $everywhere; BuildArgs = @('-p:CiBuild=true') }
  WinUI3   = @{ Project = 'samples/XAnimals.WinUI3/XAnimals.WinUI3.csproj'; App = 'XAnimals.WinUI3'; Os = @('Windows'); BuildArgs = @() }
}
$templateCatalog = [ordered]@{
  avalonia = @{ Os = $everywhere; BuildArgs = @() }
  maui     = @{ Os = @('Windows'); BuildArgs = @('-f', 'net10.0-windows10.0.19041.0') }
  uno      = @{ Os = $everywhere; BuildArgs = @() }
  winui3   = @{ Os = @('Windows'); BuildArgs = @() }
}

# The pages, the first animals, the dialogs and their answers: lib/Samples.ps1 (shared with run-android.ps1).
. (Join-Path $PSScriptRoot 'lib/Samples.ps1')

if (-not $PSBoundParameters.ContainsKey('Hosts')) {
  $Hosts = if ($Templates.Count) { @() } else { @($catalog.Keys | Where-Object { $catalog[$_].Os -contains $currentOs }) }
}
if (-not $Pages.Count) { $Pages = $allPages }
$unknownPages = @($Pages | Where-Object { $_ -notin $allPages })
if ($unknownPages.Count) { throw "Unknown page(s): $($unknownPages -join ', '). The menu has: $($allPages -join ', ')" }

# ------------------------------------------------------------------------------------------------------------------------------
# agent-desktop
# ------------------------------------------------------------------------------------------------------------------------------

$adPath = Resolve-AgentDesktop $AgentDesktop
# A state folder made now, by this user: agent-desktop refuses one owned by another principal (made by an elevated shell, copied),
# and on Linux and macOS one that the group or others can read (a folder made under /tmp is 755).
if (-not $env:AGENT_DESKTOP_HOME) {
  $env:AGENT_DESKTOP_HOME = Join-Path ([IO.Path]::GetTempPath()) ('agent-desktop-routeables-' + [Environment]::UserName)
}
New-Item -ItemType Directory -Force -Path $env:AGENT_DESKTOP_HOME | Out-Null
if (-not $IsWindows) { & chmod 700 $env:AGENT_DESKTOP_HOME }
$version = (& $adPath version 2>$null) -join ''
try { $version = ($version | ConvertFrom-Json).data.version } catch { }
$status = (& $adPath status 2>$null) -join ''
Set-Content -LiteralPath (Join-Path $OutDir 'status.json') -Value $status -Encoding utf8
Write-Host "agent-desktop: $adPath"
try {
  $statusData = ($status | ConvertFrom-Json).data
  $access = $statusData.permissions.accessibility.state
  if ($access -ne 'granted') {
    $hint = switch ($currentOs) {
      'Linux' { 'Linux needs a desktop session with the AT-SPI bus (sudo apt install at-spi2-core; under WSL2, WSLg). Run: agent-desktop status' }
      'MacOS' { 'Grant the Accessibility permission: agent-desktop permissions --guide' }
      default { 'Run: agent-desktop status' }
    }
    throw "The accessibility tree cannot be read here (accessibility: $access). $hint"
  }
} catch [System.ArgumentException] { Write-Warning "agent-desktop status did not answer JSON: $status" }

# ------------------------------------------------------------------------------------------------------------------------------
# Build
# ------------------------------------------------------------------------------------------------------------------------------

# The newest executable of an app under a folder: <App>.exe on Windows, the apphost without extension elsewhere.
function Find-Executable([string] $Folder, [string] $App) {
  if (-not (Test-Path -LiteralPath $Folder)) { return $null }
  $file = if ($IsWindows) { "$App.exe" } else { $App }
  $all = @(Get-ChildItem -LiteralPath $Folder -Recurse -File -Filter $file -ErrorAction SilentlyContinue |
           Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' -and $_.Name -eq $file })
  $debug = @($all | Where-Object { $_.FullName -match '[\\/]Debug[\\/]' })
  $pick = if ($debug.Count) { $debug } else { $all }
  $found = $pick | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($found) { $found.FullName } else { $null }
}

function Invoke-Build([string] $Name, [string] $Project, [string[]] $Arguments) {
  $log = Join-Path $OutDir "build-$Name.txt"
  $watch = [Diagnostics.Stopwatch]::StartNew()
  Write-Host "[build] $Name ..." -NoNewline
  & dotnet build $Project -c Debug -nologo @Arguments *> $log
  $ok = $LASTEXITCODE -eq 0
  Write-Host (" {0} in {1:0}s" -f $(if ($ok) { 'ok' } else { 'FAILED' }), $watch.Elapsed.TotalSeconds) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
  if ($ok) { return $null }
  $errors = @(Select-String -LiteralPath $log -Pattern ': error ' | Select-Object -First 3 | ForEach-Object { $_.Line.Trim() })
  "the build failed (see $(Split-Path -Leaf $log))$(if ($errors.Count) { ': ' + ($errors -join ' | ') })"
}

$targets = [Collections.Generic.List[hashtable]]::new()
foreach ($name in $Hosts) {
  $key = @($catalog.Keys | Where-Object { $_ -eq $name }) | Select-Object -First 1
  if (-not $key) { throw "Unknown host '$name'. Hosts: $($catalog.Keys -join ', ')" }
  $spec = $catalog[$key]
  $target = @{ Kind = 'sample'; Name = $key; App = $spec.App; Exe = $null; Problem = $null }
  $targets.Add($target)
  if ($spec.Os -notcontains $currentOs) { $target.Problem = "$key does not run on $currentOs (it runs on: $($spec.Os -join ', '))"; continue }
  $explicit = @($exeByHost.Keys | Where-Object { $_ -eq $key }) | Select-Object -First 1
  if ($explicit) {
    $target.Exe = (Resolve-Path -LiteralPath $exeByHost[$explicit]).Path
    $target.App = [IO.Path]::GetFileNameWithoutExtension($target.Exe)
    continue
  }
  $project = Join-Path $RepoRoot $spec.Project
  $bin = Join-Path (Split-Path -Parent $project) 'bin'
  $target.Exe = Find-Executable $bin $spec.App
  if ($BuildAll -or ($Build -and -not $target.Exe)) {
    $problem = Invoke-Build $key $project $spec.BuildArgs
    if ($problem) { $target.Problem = $problem; continue }
    $target.Exe = Find-Executable $bin $spec.App
  }
  if (-not $target.Exe) {
    $target.Problem = "not built: no $($spec.App)$(if ($IsWindows) { '.exe' }) under $bin. Run with -Build, or: dotnet build $($spec.Project) -c Debug $($spec.BuildArgs -join ' ')".Trim()
  }
}

# macOS: the build output of a sample in a .app: <temp>/routeables-bundles/<App>.app/Contents/{Info.plist,MacOS/...}, signed ad hoc. Returns the
# executable inside it. An application of a bundle is the only one the system lets post notifications.
function New-MacBundle([string] $Exe, [string] $App) {
  # Outside the output folder: the artifact of a CI run would carry a copy of the whole application.
  $root = Join-Path ([IO.Path]::GetTempPath()) "routeables-bundles/$App.app"
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
  $macos = Join-Path $root 'Contents/MacOS'
  New-Item -ItemType Directory -Force -Path $macos | Out-Null
  Copy-Item -Path (Join-Path (Split-Path -Parent $Exe) '*') -Destination $macos -Recurse -Force
  $name = Split-Path -Leaf $Exe
  $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>dev.routeables.smoke.$($App.ToLowerInvariant().Replace('_', '-'))</string>
  <key>CFBundleName</key><string>$App</string>
  <key>CFBundleDisplayName</key><string>$App</string>
  <key>CFBundleExecutable</key><string>$name</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
"@
  Set-Content -LiteralPath (Join-Path $root 'Contents/Info.plist') -Value $plist -Encoding utf8
  & chmod +x (Join-Path $macos $name)
  & codesign --force --deep --sign - $root *> (Join-Path $OutDir "codesign-$App.txt")
  # The system looks the bundle up in LaunchServices: one that was never registered ("LSApplicationRecord failed to find <identifier>" in the log of
  # usernoted) is refused the notification permission at once. Started by its path from a temp folder, it never is: register it.
  $lsregister = '/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister'
  if (Test-Path -LiteralPath $lsregister) { & $lsregister -f $root *> (Join-Path $OutDir "lsregister-$App.txt") }
  Join-Path $macos $name
}

if ($Bundle) {
  if ($currentOs -ne 'MacOS') { throw '-Bundle is for macOS.' }
  foreach ($target in $targets | Where-Object { $_.Kind -eq 'sample' -and $_.Exe -and -not $_.Problem }) {
    $target.Exe = New-MacBundle $target.Exe $target.App
    Write-Host "[bundle] $($target.Name): $($target.Exe)"
  }
}

# The templates: dotnet new outside the repository (inside it, the project would inherit Directory.Build.props, the central
# package versions and global.json, which an application of a user does not have). Each gets its own name, so several can run at once.
if ($Templates.Count) {
  $templateRoot = Join-Path ([IO.Path]::GetTempPath()) 'routeables-templates'
  $installLog = Join-Path $OutDir 'templates-install.txt'
  & dotnet new install (Join-Path $RepoRoot 'templates') --force *> $installLog
  foreach ($template in $Templates) {
    $name = $template.ToLowerInvariant()
    $app = 'Rt' + (Get-Culture).TextInfo.ToTitleCase($name)
    $target = @{ Kind = 'template'; Name = "tpl-$name"; App = $app; Exe = $null; Folder = $null; Problem = $null }
    $targets.Add($target)
    if (-not $templateCatalog.Contains($name)) { $target.Problem = "unknown template '$template' ($($templateCatalog.Keys -join ', '))"; continue }
    $spec = $templateCatalog[$name]
    if ($spec.Os -notcontains $currentOs) { $target.Problem = "the $name template does not run on $currentOs"; continue }
    $folder = Join-Path $templateRoot $name
    if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
    $target.Folder = $folder
    $newLog = Join-Path $OutDir "template-$name-new.txt"
    & dotnet new "routeables-$name" -n $app -o $folder --force *> $newLog
    $csproj = Join-Path $folder "$app.csproj"
    if (-not (Test-Path -LiteralPath $csproj)) { $target.Problem = "dotnet new did not make the project (see $(Split-Path -Leaf $newLog))"; continue }
    $arguments = @($spec.BuildArgs)
    if ($PackageSource) { $arguments += @('--source', (Resolve-Path -LiteralPath $PackageSource).Path, '--source', 'https://api.nuget.org/v3/index.json') }
    $problem = Invoke-Build "template-$name" $csproj $arguments
    if ($problem) { $target.Problem = $problem; continue }
    $target.Exe = Find-Executable (Join-Path $folder 'bin') $app
    if (-not $target.Exe) { $target.Problem = "built, but no $app executable under $folder/bin" }
  }
}

if (-not $targets.Count) { throw 'Nothing to test: give -Hosts or -Templates.' }

# ------------------------------------------------------------------------------------------------------------------------------
# Run
# ------------------------------------------------------------------------------------------------------------------------------

$options = @{
  OutDir = $OutDir; AgentDesktop = $adPath; Pages = $Pages; AllPages = $allPages; MenuOrder = $menuOrder; ViewModels = $viewModels; Titles = $titles; FirstAnimal = $firstAnimal; Dialogs = $dialogs; ErrorWords = $errorWords
  StartTimeoutSeconds = $StartTimeoutSeconds; StepTimeoutMs = $StepTimeoutMs; NoHeaded = [bool]$NoHeaded
  ScanErrors = [bool]$ScanErrors; Screenshots = [bool]$Screenshots; SkipDialogs = [bool]$SkipDialogs; SkipDetails = [bool]$SkipDetails
  Notifications = [bool]$Notifications; NotificationSpecs = $notificationSpecs; SystemNotificationRequired = [bool]$RequireSystemNotification
}
Set-AgentDesktopPath $adPath

# All the applications start now, together: each takes 10 to 25 s to show its window, and that time passes while the hosts
# before it are tested. The tests themselves run one at a time, because they click physically and need the window in front.
Write-Host ("Starting {0}, then testing them one after the other" -f (($targets | ForEach-Object Name) -join ', ')) -ForegroundColor Cyan
$contexts = @{}
foreach ($target in $targets) { $contexts[$target.Name] = Start-Target $target $options }
# Each window is minimized as soon as it shows, then restored on its turn and minimized again after it: no window lies over the
# one under test (its physical clicks would land there).
if ($targets.Count -gt 1) { Hide-StartedWindows @($targets | ForEach-Object { $contexts[$_.Name] }) $StartTimeoutSeconds }

# Side by side: the screen is cut in $Parallel parts (top and bottom on a portrait screen, left and right otherwise), each host
# takes a free part, the longest hosts first. Only the clicks and the keys take turns; the reading of the trees (most of the time)
# runs at the same time.
$slots = @()
if ($Parallel -gt 1 -and $targets.Count -gt 1) {
  $probe = $targets | ForEach-Object { $contexts[$_.Name] } | Where-Object { $_.HiddenId } | Select-Object -First 1
  $area = if ($probe) { Get-WorkArea $probe } else { $null }
  if ($area) {
    $count = [Math]::Min($Parallel, $targets.Count)
    $gap = 16
    $portrait = $area.Height -gt $area.Width
    for ($i = 0; $i -lt $count; $i++) {
      if ($portrait) {
        $h = [int](($area.Height - $gap * ($count - 1)) / $count)
        $slots += [pscustomobject]@{ X = $area.X; Y = $area.Y + $i * ($h + $gap); Width = $area.Width; Height = $h }
      } else {
        $w = [int](($area.Width - $gap * ($count - 1)) / $count)
        $slots += [pscustomobject]@{ X = $area.X + $i * ($w + $gap); Y = $area.Y; Width = $w; Height = $area.Height }
      }
    }
    Write-Host ("{0} hosts at a time, on {1}x{2} parts of the screen" -f $count, $slots[0].Width, $slots[0].Height) -ForegroundColor Cyan
  } else {
    Write-Host 'The size of the screen could not be read: one host at a time' -ForegroundColor Yellow
  }
}

if ($slots.Count -gt 1) {
  $free = [Collections.Concurrent.ConcurrentQueue[object]]::new()
  foreach ($slot in $slots) { $free.Enqueue($slot) }
  # The usual length of a host (seconds): the longest start first, so that the two parts end together.
  $weight = @{ Maui = 110; Avalonia = 105; Uno = 75; Wpf = 65; WinUI3 = 45 }
  $ordered = @($targets | Sort-Object { if ($_.Kind -eq 'sample' -and $weight[$_.Name]) { -$weight[$_.Name] } else { -30 } })
  $results = $ordered | ForEach-Object -ThrottleLimit $slots.Count -Parallel {
    $target = $_
    Import-Module $using:modulePath -Force
    Set-AgentDesktopPath $using:adPath
    $queue = $using:free
    $ctx = ($using:contexts)[$target.Name]
    $slot = $null
    while (-not $queue.TryDequeue([ref]$slot)) { Start-Sleep -Milliseconds 50 }
    try {
      $ctx.Slot = $slot
      $ctx.Parallel = $true
      $report = if ($target.Kind -eq 'sample') { Invoke-SampleSmoke $ctx $target $using:options } else { Invoke-TemplateSmoke $ctx $target $using:options }
      Hide-Window $ctx
    } finally { $queue.Enqueue($slot) }
    [pscustomobject]@{ Name = $target.Name; Kind = $target.Kind; Report = $report }
  }
} else {
  $results = foreach ($target in $targets) {
    $ctx = $contexts[$target.Name]
    $report = if ($target.Kind -eq 'sample') { Invoke-SampleSmoke $ctx $target $options } else { Invoke-TemplateSmoke $ctx $target $options }
    if ($targets.Count -gt 1) { Hide-Window $ctx }
    [pscustomobject]@{ Name = $target.Name; Kind = $target.Kind; Report = $report }
  }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Report
# ------------------------------------------------------------------------------------------------------------------------------

$report = [ordered]@{
  date = (Get-Date).ToString('s'); os = [Environment]::OSVersion.VersionString; agentDesktop = $version
  seconds = [Math]::Round($total.Elapsed.TotalSeconds, 1); hosts = [ordered]@{}; templates = [ordered]@{}
}
# In the order asked for, whatever the order the runspaces finished in.
foreach ($target in $targets) {
  $result = $results | Where-Object Name -eq $target.Name | Select-Object -First 1
  if ($target.Kind -eq 'sample') { $report.hosts[$target.Name] = $result.Report } else { $report.templates[$target.Name] = $result.Report }
}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutDir 'report.json') -Encoding utf8

function Format-Count($Items) {
  $values = @($Items)
  if (-not $values.Count) { return '-' }
  $ok = @($values | Where-Object { $_.status -eq 'ok' }).Count
  $unverified = @($values | Where-Object { $_.status -eq 'unverified' }).Count
  "$ok/$($values.Count)$(if ($unverified) { " (+$unverified unverified)" })"
}

$rows = @(foreach ($name in $report.hosts.Keys) {
  $h = $report.hosts[$name]
  [pscustomobject]@{
    Host = $name; Status = $h.status.ToUpperInvariant(); Start = $(if ($null -ne $h.startSeconds) { "$($h.startSeconds)s" } else { '-' })
    Pages = Format-Count $h.pages.Values; Details = Format-Count $h.details.Values; Dialogs = Format-Count $h.dialogs.Values; Notifications = Format-Count $h.notifications.Values; Menu = $(if ($h.menuActivation) { $h.menuActivation } else { '-' })
    Time = "$($h.seconds)s"; Calls = $h.calls; Problems = ($h.problems -join ' | ')
  }
})
$rows += @(foreach ($name in $report.templates.Keys) {
  $t = $report.templates[$name]
  [pscustomobject]@{
    Host = $name; Status = $t.status.ToUpperInvariant(); Start = $(if ($null -ne $t.startSeconds) { "$($t.startSeconds)s" } else { '-' })
    Pages = (($t.steps.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; '); Details = '-'; Dialogs = '-'; Notifications = '-'; Menu = '-'
    Time = "$($t.seconds)s"; Calls = $t.calls; Problems = ($t.problems -join ' | ')
  }
})

Write-Host ''
# Written by hand: Format-Table prints nothing when the output is not a console (CI, a redirect).
$columns = 'Host', 'Status', 'Start', 'Pages', 'Details', 'Dialogs', 'Notifications', 'Menu', 'Time', 'Calls'
$widths = @{}
foreach ($c in $columns) { $widths[$c] = [Math]::Max($c.Length, (@($rows | ForEach-Object { "$($_.$c)".Length }) + 0 | Measure-Object -Maximum).Maximum) }
Write-Host (($columns | ForEach-Object { $_.PadRight($widths[$_]) }) -join '  ')
Write-Host (($columns | ForEach-Object { '-' * $widths[$_] }) -join '  ')
foreach ($row in $rows) {
  $line = ($columns | ForEach-Object { "$($row.$_)".PadRight($widths[$_]) }) -join '  '
  Write-Host $line -ForegroundColor $(if ($row.Status -eq 'OK') { 'Green' } else { 'Red' })
}
Write-Host ''
foreach ($row in $rows | Where-Object Problems) { Write-Host "$($row.Host): $($row.Problems)" -ForegroundColor Red }

$md = [Text.StringBuilder]::new()
[void]$md.AppendLine("# Native smoke test`n`n$($report.date) on $($report.os), agent-desktop $version, $($report.seconds)s`n")
[void]$md.AppendLine('| Host | Status | Start | Pages | Details | Dialogs | Notifications | Menu | Time | Calls |')
[void]$md.AppendLine('|---|---|---|---|---|---|---|---|---|---|')
foreach ($row in $rows) { [void]$md.AppendLine("| $($row.Host) | $($row.Status) | $($row.Start) | $($row.Pages) | $($row.Details) | $($row.Dialogs) | $($row.Notifications) | $($row.Menu) | $($row.Time) | $($row.Calls) |") }
foreach ($name in $report.hosts.Keys) {
  $h = $report.hosts[$name]
  $bad = @($h.pages.GetEnumerator() | Where-Object { $_.Value.status -ne 'ok' }) + @($h.details.GetEnumerator() | Where-Object { $_.Value.status -ne 'ok' } | ForEach-Object { [pscustomobject]@{ Key = "$($_.Key) detail"; Value = $_.Value } }) + @($h.dialogs.GetEnumerator() | Where-Object { $_.Value.status -ne 'ok' }) + @($h.notifications.GetEnumerator() | Where-Object { $_.Value.status -ne 'ok' } | ForEach-Object { [pscustomobject]@{ Key = "notification $($_.Key)"; Value = $_.Value } })
  if (-not $bad.Count -and -not $h.problems.Count) { continue }
  [void]$md.AppendLine("`n## $name`n")
  foreach ($p in $h.problems) { [void]$md.AppendLine("- $p") }
  foreach ($item in $bad) { [void]$md.AppendLine("- **$($item.Key)**: $($item.Value.status), $($item.Value.detail)") }
}
Set-Content -LiteralPath (Join-Path $OutDir 'report.md') -Value $md.ToString() -Encoding utf8

$failed = @($rows | Where-Object Status -ne 'OK').Count
Write-Host ("{0} in {1:0.0}s. Report: {2}" -f $(if ($failed) { "$failed FAILED" } else { 'All passed' }), $total.Elapsed.TotalSeconds, (Join-Path $OutDir 'report.json')) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $(if ($failed) { 1 } else { 0 })
