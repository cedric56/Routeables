<#
.SYNOPSIS
  Smoke test of the XAnimals samples on Android (MAUI, Uno, Avalonia), in an emulator or on a device, driven by adb.

.DESCRIPTION
  Starts an Android emulator when no device is connected (the AVD given, else the first one), switches its animations off, builds
  the Android target of each sample (dotnet build -f net10.0-android) when it has no APK yet, installs it, starts it, then runs
  the same test as run.ps1 on the desktop: each page of the menu, the first animal of each list and Back, each dialog of the
  Dialogs page and its Result line. The screen is read with uiautomator (the accessibility tree of Android) and tapped with
  "input tap".

  Output: out/android-smoke/report.json, report.md, and one folder per host with calls.jsonl (every adb call and its duration)
  and, for each failed step, the screen as uiautomator saw it (.xml) and a screenshot (.png).

  Needs the Android SDK (Android Studio, or the command-line tools) with platform-tools and the emulator, an AVD (Device Manager
  of Android Studio, or -CreateAvd), the .NET 10 SDK with the android and maui workloads (dotnet workload install android maui),
  PowerShell 7.2+. Windows, Linux or macOS.

.EXAMPLE
  pwsh agent-desktop/run-android.ps1
  MAUI, Uno and Avalonia in the first AVD (started if needed), built if they have no APK.

.EXAMPLE
  pwsh agent-desktop/run-android.ps1 -Hosts Maui -Avd Pixel_8_API_35 -Build
  MAUI only, rebuilt, in that AVD.

.EXAMPLE
  pwsh agent-desktop/run-android.ps1 -CreateAvd
  Makes an AVD "routeables" (Android 15, x86_64 or arm64 like this machine) with sdkmanager and avdmanager, then tests.
#>
[CmdletBinding()]
param(
  # Maui, Uno, Avalonia.
  [string[]] $Hosts = @('Maui', 'Uno', 'Avalonia'),
  [string[]] $Pages,
  # The AVD to start when no device is connected (emulator -list-avds). Default: the first one.
  [string] $Avd = '',
  # A device already connected (adb devices), instead of the first one.
  [string] $Serial = '',
  # Creates the AVD "routeables" when there is none.
  [switch] $CreateAvd,
  # Installs the Android SDK (and a JDK) when there is none, with the InstallAndroidDependencies target of .NET for Android, into
  # %LOCALAPPDATA%\Android\Sdk (~/Android/Sdk elsewhere).
  [switch] $InstallSdk,
  # Builds (and installs) even when an APK is there.
  [switch] $Build,
  # The emulator window is not shown (CI).
  [switch] $NoWindow,
  # Leaves the emulator running at the end (it is stopped when this script started it).
  [switch] $KeepEmulator,
  [switch] $SkipDialogs,
  [switch] $SkipDetails,
  # The screen density during the test (adb shell wm density): a smaller one shows more of a page. 0: unchanged.
  [int] $Density = 0,
  [int] $StartTimeoutSeconds = 90,
  [int] $StepTimeoutMs = 6000,
  [string] $AndroidSdk = '',
  [string] $OutDir = ''
)

if ($PSVersionTable.PSVersion.Major -lt 7) {
  $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
  if (-not $pwsh) { Write-Host 'This script needs PowerShell 7.2 or later (winget install --id Microsoft.PowerShell).' -ForegroundColor Red; exit 2 }
  # -ExecutionPolicy Bypass: a script unzipped from a download is refused by pwsh otherwise (it is not signed).
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
# adb answers in UTF-8: read it so (the texts of the samples have "◀", "—").
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$total = [Diagnostics.Stopwatch]::StartNew()
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'out/android-smoke' }
$OutDir = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Import-Module (Join-Path $PSScriptRoot 'lib/Android.Smoke.psm1') -Force
. (Join-Path $PSScriptRoot 'lib/Samples.ps1')

function Split-List([string[]] $Values) { @($Values | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Hosts = Split-List $Hosts
$Pages = Split-List $Pages
if (-not $Pages.Count) { $Pages = $allPages }

# ------------------------------------------------------------------------------------------------------------------------------
# Android SDK, device
# ------------------------------------------------------------------------------------------------------------------------------

$exe = if ($IsWindows) { '.exe' } else { '' }
# Where Android Studio, Visual Studio (its MAUI workload) and the command-line tools put it.
$defaultSdk = if ($IsWindows) { Join-Path $env:LOCALAPPDATA 'Android/Sdk' } elseif ($IsMacOS) { Join-Path $HOME 'Library/Android/sdk' } else { Join-Path $HOME 'Android/Sdk' }
$candidates = @($AndroidSdk, $env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, $defaultSdk,
  $(if ($IsWindows) { Join-Path ${env:ProgramFiles(x86)} 'Android/android-sdk' }),
  $(if ($IsWindows) { Join-Path $env:ProgramFiles 'Android/android-sdk' })) | Where-Object { $_ }
$sdk = $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ "platform-tools/adb$exe") } | Select-Object -First 1
if (-not $sdk -and $InstallSdk) {
  # .NET for Android installs its own dependencies: the SDK (platform-tools, build-tools, a platform, cmdline-tools) and a JDK.
  $sdk = if ($AndroidSdk) { $AndroidSdk } else { $defaultSdk }
  $jdk = Join-Path (Split-Path -Parent $sdk) 'jdk'
  Write-Host "Installing the Android SDK into $sdk (and a JDK into $jdk) ..." -ForegroundColor Cyan
  $log = Join-Path $OutDir 'install-android-sdk.txt'
  & dotnet build (Join-Path $RepoRoot 'samples/XAnimals.Avalonia.Android/XAnimals.Avalonia.Android.csproj') -t:InstallAndroidDependencies `
    "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk" -p:AcceptAndroidSDKLicenses=True -nologo *> $log
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $sdk "platform-tools/adb$exe"))) {
    Write-Host "The installation of the Android SDK failed (see $log)." -ForegroundColor Red
    exit 2
  }
  $env:ANDROID_HOME = $sdk
  $env:JAVA_HOME = $jdk
}
if (-not $sdk) {
  Write-Host "No Android SDK found (looked in: $($candidates -join ', '))." -ForegroundColor Red
  Write-Host 'Install it with this script: pwsh agent-desktop/run-android.ps1 -InstallSdk -CreateAvd' -ForegroundColor Red
  Write-Host '(or install Android Studio, or give its folder with -AndroidSdk).' -ForegroundColor Red
  exit 2
}
$adb = Join-Path $sdk "platform-tools/adb$exe"
$emulator = Join-Path $sdk "emulator/emulator$exe"
Write-Host "Android SDK: $sdk" -ForegroundColor DarkGray

function Get-Devices { @(& $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '^(\S+)\s+device$' } | ForEach-Object { ($_ -split '\s+')[0] }) }

$startedEmulator = $null
$devices = @(Get-Devices)
if ($Serial) {
  if ($devices -notcontains $Serial) { Write-Host "No device '$Serial' (adb devices: $($devices -join ', '))" -ForegroundColor Red; exit 2 }
} elseif ($devices.Count) {
  $Serial = $devices[0]
} else {
  if (-not (Test-Path -LiteralPath $emulator) -and -not $CreateAvd) {
    Write-Host "No device, and no emulator in $sdk/emulator. Run again with -CreateAvd (it installs the emulator and an Android image)." -ForegroundColor Red
    exit 2
  }
  $avds = if (Test-Path -LiteralPath $emulator) { @(& $emulator -list-avds 2>$null | Where-Object { $_ -and $_ -notmatch '^INFO' }) } else { @() }
  if (-not $avds.Count -and $CreateAvd) {
    # An image of the architecture of this machine: an x86_64 image does not run on an ARM Mac, and the other way round.
    $abi = if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64-v8a' } else { 'x86_64' }
    $image = "system-images;android-35;google_apis;$abi"
    $tools = @(Get-ChildItem -LiteralPath (Join-Path $sdk 'cmdline-tools') -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1)
    if (-not $tools.Count) { Write-Host "No cmdline-tools in ${sdk}: install them from the SDK Manager of Android Studio." -ForegroundColor Red; exit 2 }
    $bat = if ($IsWindows) { '.bat' } else { '' }
    $sdkmanager = Join-Path $tools[0].FullName "bin/sdkmanager$bat"
    $avdmanager = Join-Path $tools[0].FullName "bin/avdmanager$bat"
    Write-Host "Creating the AVD 'routeables' ($image) ..." -ForegroundColor Cyan
    $env:ANDROID_HOME = $sdk; $env:ANDROID_SDK_ROOT = $sdk
    $jdk = Join-Path (Split-Path -Parent $sdk) 'jdk'
    if (-not $env:JAVA_HOME -and (Test-Path -LiteralPath $jdk)) { $env:JAVA_HOME = $jdk }
    $yes = @('y') * 20
    $yes | & $sdkmanager "--sdk_root=$sdk" --licenses *> (Join-Path $OutDir 'sdkmanager-licenses.txt')
    $yes | & $sdkmanager "--sdk_root=$sdk" --install $image 'platform-tools' 'emulator' *> (Join-Path $OutDir 'sdkmanager.txt')
    if (-not (Test-Path -LiteralPath $emulator)) { Write-Host "sdkmanager did not install the emulator (see $OutDir/sdkmanager.txt)." -ForegroundColor Red; exit 2 }
    'no' | & $avdmanager create avd -n routeables -k $image -d pixel_7 --force | Out-Null
    $avds = @('routeables')
  }
  if (-not $avds.Count) {
    Write-Host 'No device and no AVD. Make one in the Device Manager of Android Studio, or run again with -CreateAvd.' -ForegroundColor Red
    exit 2
  }
  $name = if ($Avd) { $Avd } else { $avds[0] }
  if ($avds -notcontains $name) { Write-Host "No AVD '$name' (there are: $($avds -join ', '))" -ForegroundColor Red; exit 2 }
  Write-Host "Starting the emulator '$name' ..." -ForegroundColor Cyan
  $arguments = @('-avd', $name, '-no-snapshot-save', '-no-boot-anim', '-netdelay', 'none', '-netspeed', 'full')
  if ($NoWindow) { $arguments += '-no-window' }
  $startedEmulator = Start-Process -FilePath $emulator -ArgumentList $arguments -PassThru `
    -RedirectStandardOutput (Join-Path $OutDir 'emulator.txt') -RedirectStandardError (Join-Path $OutDir 'emulator-err.txt')
  $watch = [Diagnostics.Stopwatch]::StartNew()
  while (-not ($devices = @(Get-Devices)).Count) {
    if ($startedEmulator.HasExited) { Write-Host "The emulator stopped (see $OutDir/emulator-err.txt)." -ForegroundColor Red; exit 2 }
    if ($watch.Elapsed.TotalSeconds -gt 180) { Write-Host 'The emulator did not come up in 180 s.' -ForegroundColor Red; exit 2 }
    Start-Sleep -Seconds 2
  }
  $Serial = $devices[0]
}
Set-AdbTarget $adb $Serial

# Booted: sys.boot_completed, then the package manager.
$watch = [Diagnostics.Stopwatch]::StartNew()
while ((Invoke-Adb $null @('shell', 'getprop', 'sys.boot_completed')).Text.Trim() -ne '1') {
  if ($watch.Elapsed.TotalSeconds -gt 240) { Write-Host "The device $Serial did not finish booting in 240 s." -ForegroundColor Red; exit 2 }
  Start-Sleep -Seconds 2
}
while (-not (Invoke-Adb $null @('shell', 'pm', 'path', 'android')).Ok) { Start-Sleep -Seconds 1 }
Write-Host "Device: $Serial ($((Invoke-Adb $null @('shell', 'getprop', 'ro.product.model')).Text.Trim()), Android $((Invoke-Adb $null @('shell', 'getprop', 'ro.build.version.release')).Text.Trim())), ready in $([int]$watch.Elapsed.TotalSeconds) s" -ForegroundColor Cyan

# No animations while the test runs: uiautomator waits for an idle screen, and taps land where the dump said.
$animations = 'window_animation_scale', 'transition_animation_scale', 'animator_duration_scale'
$savedScales = @{}
foreach ($key in $animations) {
  $savedScales[$key] = (Invoke-Adb $null @('shell', 'settings', 'get', 'global', $key)).Text.Trim()
  Invoke-Adb $null @('shell', 'settings', 'put', 'global', $key, '0') | Out-Null
}
if ($Density -gt 0) { Invoke-Adb $null @('shell', 'wm', 'density', "$Density") | Out-Null }
# The screen in pixels (the override, when there is one).
$size = (Invoke-Adb $null @('shell', 'wm', 'size')).Text
$sizes = @([regex]::Matches($size, '(\d+)x(\d+)'))
$last = $sizes[$sizes.Count - 1]
$width = [int]$last.Groups[1].Value; $height = [int]$last.Groups[2].Value
# Landscape: wm size gives the portrait size.
$rotation = (Invoke-Adb $null @('shell', 'dumpsys', 'input')).Text
if ($rotation -match 'SurfaceOrientation:\s*[13]') { $tmp = $width; $width = $height; $height = $tmp }

# ------------------------------------------------------------------------------------------------------------------------------
# Build and install
# ------------------------------------------------------------------------------------------------------------------------------

# Uno builds its Android target without CiBuild (CiBuild keeps the desktop one only).
$catalog = [ordered]@{
  Maui     = @{ Project = 'samples/XAnimals.Maui/XAnimals.Maui.csproj'; Package = 'com.companyname.xanimals.maui'; BuildArgs = @('-f', 'net10.0-android') }
  Uno      = @{ Project = 'samples/XAnimals.Uno/XAnimals.Uno.csproj'; Package = 'com.companyname.xanimals.uno'; BuildArgs = @('-f', 'net10.0-android') }
  Avalonia = @{ Project = 'samples/XAnimals.Avalonia.Android/XAnimals.Avalonia.Android.csproj'; Package = 'com.CompanyName.AvaloniaTest'; BuildArgs = @() }
}

function Find-Apk([string] $ProjectDir, [string] $Package) {
  $bin = Join-Path $ProjectDir 'bin'
  if (-not (Test-Path -LiteralPath $bin)) { return $null }
  $apk = Get-ChildItem -LiteralPath $bin -Recurse -File -Filter "$Package-Signed.apk" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]Debug[\\/]' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($apk) { $apk.FullName } else { $null }
}

$targets = @()
foreach ($name in $Hosts) {
  $key = @($catalog.Keys | Where-Object { $_ -eq $name }) | Select-Object -First 1
  if (-not $key) { throw "Unknown host '$name'. Android hosts: $($catalog.Keys -join ', ')" }
  $spec = $catalog[$key]
  $target = @{ Name = "$key-Android"; Package = $spec.Package; Apk = $null; Problem = $null }
  $targets += $target
  $project = Join-Path $RepoRoot $spec.Project
  $target.Apk = Find-Apk (Split-Path -Parent $project) $spec.Package
  if ($Build -or -not $target.Apk) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $target.Problem = 'no APK, and no dotnet to build it (the .NET 10 SDK with the android workload)'; continue }
    $log = Join-Path $OutDir "build-$key.txt"
    $buildWatch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host "[build] $key (Android) ..." -NoNewline
    # A test build: a warning of the analyzers (CA1416 ...) must not stop it; the CI build of the repository keeps them as errors.
    # EmbedAssembliesIntoApk: a Debug build leaves the .NET assemblies out of the APK by default (fast deployment: Visual Studio
    # pushes them apart); an APK installed with adb install would then stop at start ("No assemblies found").
    & dotnet build $project -c Debug -nologo @($spec.BuildArgs) -p:TreatWarningsAsErrors=false -p:EmbedAssembliesIntoApk=true *> $log
    $ok = $LASTEXITCODE -eq 0
    Write-Host (" {0} in {1:0}s" -f $(if ($ok) { 'ok' } else { 'FAILED' }), $buildWatch.Elapsed.TotalSeconds) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    if (-not $ok) {
      $errors = @(Select-String -LiteralPath $log -Pattern ': error ' | Select-Object -First 3 | ForEach-Object { $_.Line.Trim() })
      $target.Problem = "the build failed (see $(Split-Path -Leaf $log))$(if ($errors.Count) { ': ' + ($errors -join ' | ') })"
      continue
    }
    $target.Apk = Find-Apk (Split-Path -Parent $project) $spec.Package
  }
  if (-not $target.Apk) { $target.Problem = "no $($spec.Package)-Signed.apk under the bin of $($spec.Project)"; continue }
  $install = Invoke-Adb $null @('install', '-r', '-t', '-d', $target.Apk) 300
  if (-not $install.Ok -and $install.Text -match 'INSTALL_FAILED_UPDATE_INCOMPATIBLE|signatures do not match') {
    # Signed with another debug key (built on another machine): uninstalled first.
    Invoke-Adb $null @('uninstall', $spec.Package) | Out-Null
    $install = Invoke-Adb $null @('install', '-r', '-t', $target.Apk) 300
  }
  if (-not $install.Ok) { $target.Problem = "adb install failed: $($install.Text.Trim())" }
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
  foreach ($key in $animations) {
    $value = $savedScales[$key]
    if (-not $value -or $value -eq 'null') { $value = '1' }
    Invoke-Adb $null @('shell', 'settings', 'put', 'global', $key, $value) | Out-Null
  }
  if ($Density -gt 0) { Invoke-Adb $null @('shell', 'wm', 'density', 'reset') | Out-Null }
  if ($startedEmulator -and -not $KeepEmulator) { Invoke-Adb $null @('emu', 'kill') | Out-Null }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Report
# ------------------------------------------------------------------------------------------------------------------------------

$report = [ordered]@{ date = (Get-Date).ToString('s'); device = $Serial; screen = "${width}x$height"; seconds = [Math]::Round($total.Elapsed.TotalSeconds, 1); hosts = $reports }
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
[void]$md.AppendLine("# Android smoke ($($report.date), $Serial, $($report.screen))`n")
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
# The whole report in one file, to send as is: out/android-smoke.zip.
# A copy first: the emulator keeps its own log files open (emulator.txt, emulator-err.txt); a file that cannot be read is left out.
try {
  $zip = "$OutDir.zip"
  $stage = Join-Path ([IO.Path]::GetTempPath()) "android-smoke-$PID"
  if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
  foreach ($file in Get-ChildItem -LiteralPath $OutDir -Recurse -File) {
    $relative = $file.FullName.Substring($OutDir.Length).TrimStart('\', '/')
    $copy = Join-Path $stage $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $copy) | Out-Null
    try {
      $source = [IO.File]::Open($file.FullName, 'Open', 'Read', 'ReadWrite')
      try { $target = [IO.File]::Create($copy); try { $source.CopyTo($target) } finally { $target.Dispose() } } finally { $source.Dispose() }
    } catch { }
  }
  Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force -ErrorAction Stop
  Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
  Write-Host "Report zipped: $zip" -ForegroundColor Cyan
} catch { Write-Host "The report could not be zipped: $($_.Exception.Message)" -ForegroundColor Yellow }
exit $(if ($failed) { 1 } else { 0 })
