<#
.SYNOPSIS
  Smoke test of the native XAnimals samples (WPF, Avalonia, MAUI, Uno) driven by agent-desktop (accessibility tree).

.DESCRIPTION
  For every host: starts the application, opens each page of the menu, presses the buttons of the Dialogs page and answers
  the dialogs, then writes a report. Nothing is asserted from pixels: a page "opens" when the tree of the window changes and
  holds the text of the page.

  Needs: agent-desktop (npm install -g agent-desktop), the samples built in Debug, PowerShell 5.1 or 7.
  Run from the repository root:  pwsh agent-desktop/run.ps1 -Hosts Wpf,Avalonia

  The dotnet new templates (templates/) can be tried too, on Windows: -Templates avalonia,maui,uno,winui3 installs them, makes an
  application of each in out/native-smoke/templates, builds it, starts it and follows its two pages (Hello, then About through the
  button, then back). The packages the templates ask for must be restorable: publish them, or give a folder that holds the
  packages built here with -PackageSource (dotnet pack, then -PackageSource artifacts).

  The result is out/native-smoke/report.json and one snapshot per step: send report.json to be read.
  The script was written from the documented commands of agent-desktop and has not been run: the first thing it records is the
  help of each command (out/native-smoke/help-*.txt), which is what to send if a step fails.
#>
param(
  [string[]] $Hosts = @('Wpf', 'Avalonia', 'Maui', 'Uno', 'WinUI3'),
  [string[]] $Templates = @(),
  [string] $PackageSource = '',
  [string] $OutDir = 'out/native-smoke',
  [int] $StartTimeoutSeconds = 60
)

$ErrorActionPreference = 'Continue'
# With -File, "-Hosts Wpf,Avalonia" arrives as one string: split it.
$Hosts = @($Hosts | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Templates = @($Templates | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
# Asked for templates only: the samples are not run unless -Hosts is given too.
if ($Templates.Count -and -not $PSBoundParameters.ContainsKey('Hosts')) { $Hosts = @() }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# A statement that fails is written to crash.txt with the line of the script, and the run goes on with the next statement: send that file.
trap {
  $text = "$(Get-Date -Format s)`n$($_ | Out-String)`n$($_.ScriptStackTrace)`n$($_.InvocationInfo.PositionMessage)`n----`n"
  Add-Content -Path (Join-Path $OutDir 'crash.txt') -Value $text -Encoding UTF8
  Write-Host "ERROR: $($_.Exception.Message)  ($($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim()))" -ForegroundColor Red
  continue
}

# agent-desktop refuses a state folder owned by another principal (a folder made by an elevated shell, a copy, another user).
# Unless the caller chose one, it works in a folder made now, by this process.
if (-not $env:AGENT_DESKTOP_HOME) {
  $env:AGENT_DESKTOP_HOME = Join-Path ([IO.Path]::GetTempPath()) ('agent-desktop-routeables-' + [Environment]::UserName)
}
New-Item -ItemType Directory -Force -Path $env:AGENT_DESKTOP_HOME | Out-Null
Write-Host "agent-desktop state: $env:AGENT_DESKTOP_HOME"

# Where each sample is, the process name agent-desktop finds it by, and the executable to start.
$catalog = @{
  Wpf      = @{ App = 'XAnimals.Wpf';             Exe = 'samples/XAnimals.Wpf/bin/Debug/net10.0-windows/XAnimals.Wpf.exe' }
  Avalonia = @{ App = 'XAnimals.Avalonia.Desktop'; Exe = 'samples/XAnimals.Avalonia.Desktop/bin/Debug/net10.0/XAnimals.Avalonia.Desktop.exe' }
  Maui     = @{ App = 'XAnimals.Maui';            Exe = 'samples/XAnimals.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/XAnimals.Maui.exe' }
  Uno      = @{ App = 'XAnimals.Uno';             Exe = 'samples/XAnimals.Uno/bin/Debug/net10.0-desktop/XAnimals.Uno.exe' }
  WinUI3   = @{ App = 'XAnimals.WinUI3';          Exe = 'samples/XAnimals.WinUI3/bin/Debug/net10.0-windows10.0.19041.0/win-x64/XAnimals.WinUI3.exe' }
}

$pages = 'Cats', 'Dogs', 'Monkeys', 'Elephants', 'Bears', 'Pandas', 'History', 'Regions', 'Adopt a pet', 'Dialogs', 'Features', 'Members', 'About'
$dialogButtons = 'Ok', 'Error', 'Yes / No', 'Ok / Cancel', 'Ok / Abort', 'Yes / No / Cancel', 'Yes / No / Abort',
                 'Content dialog', 'Task dialog', 'Task dialog with progress', 'Show modal'
# What answers a dialog, in the order tried.
$answers = 'Yes', 'Ok', 'OK', 'Send', 'Save', 'Close', 'Cancel', 'No'
$errorWords = 'Exception', 'Unhandled', 'Error:', 'Erreur', 'could not be located'

# A simple function (no param block): an advanced one would read -i as a common parameter (-InformationAction).
function Invoke-Ad {
  $text = & agent-desktop @args 2>&1 | Out-String
  try { return ($text | ConvertFrom-Json) } catch { return [pscustomobject]@{ ok = $false; raw = $text } }
}

# Every object of the tree, whatever the name of the property that holds the children.
function Get-Nodes($node) {
  if ($null -eq $node) { return }
  if ($node -is [System.Collections.IEnumerable] -and $node -isnot [string]) { foreach ($item in $node) { Get-Nodes $item }; return }
  if ($node -isnot [pscustomobject]) { return }
  $node
  foreach ($property in $node.PSObject.Properties) {
    if ($property.Value -is [pscustomobject] -or ($property.Value -is [System.Collections.IEnumerable] -and $property.Value -isnot [string])) { Get-Nodes $property.Value }
  }
}

function Get-Snapshot([string] $app, [string] $label, [string] $dir) {
  $result = Invoke-Ad snapshot --app $app -i --compact
  $result | ConvertTo-Json -Depth 50 | Set-Content -Path (Join-Path $dir ("$label.json")) -Encoding UTF8
  $nodes = @()
  if ($result.ok -and $result.data) { $nodes = @(Get-Nodes $result.data) }
  [pscustomobject]@{ Ok = [bool]$result.ok; Nodes = $nodes; Raw = $result }
}

# A button is found by its name. A menu entry too, or by the view model it shows: the menu of the WPF sample has no accessible name,
# its entries are called "Routeables.Routing.MenuEntry`1[XAnimals.ViewModels.CatsViewModel]".
function Find-Node($snapshot, [string] $name, [string] $viewModel = '') {
  $named = @($snapshot.Nodes | Where-Object { $_.ref_id -and $_.name })
  # The exact name first: "Ok" is not "Ok / Cancel", "Yes" is not "Yes / No".
  $found = $named | Where-Object { $_.name.Trim() -eq $name } | Select-Object -First 1
  if (-not $found -and $viewModel) { $found = $named | Where-Object { $_.name -match ('\.' + [regex]::Escape($viewModel) + '\]$') } | Select-Object -First 1 }
  if (-not $found) { $found = $named | Where-Object { $_.name.Trim().StartsWith($name) } | Select-Object -First 1 }
  $found
}

# The view model behind each entry of the menu.
$viewModels = @{
  'Cats' = 'CatsViewModel'; 'Dogs' = 'DogsViewModel'; 'Monkeys' = 'MonkeysViewModel'; 'Elephants' = 'ElephantsViewModel'
  'Bears' = 'BearsViewModel'; 'Pandas' = 'PandasViewModel'; 'History' = 'HistoryViewModel'; 'Regions' = 'RegionsViewModel'
  'Adopt a pet' = 'AdoptViewModel'; 'Dialogs' = 'DialogsViewModel'; 'Features' = 'FeaturesViewModel'
  'Members' = 'MembersViewModel'; 'About' = 'AboutViewModel'
}

function Invoke-Click($node) {
  $ref = [string]$node.ref_id
  if (-not $ref.StartsWith('@')) { $ref = '@' + $ref }
  Invoke-Ad click $ref
}

# A menu entry of the WPF sample is a list item: the snapshots of the first run show that a plain click left the selection on Cats
# and the page unchanged. So the entry is activated with each command in turn until the entry is reported "selected".
# The help of agent-desktop: "click" is the accessibility press action (it answers ok and selects nothing here), "select" needs more
# arguments, "double-click" needs --headed. So a physical click (--headed) comes first.
$activations = 'headed-click', 'headed-double-click', 'focus-return'
function Invoke-Activate($app, [string] $page, [string] $viewModel, [string] $dir) {
  $tried = @()
  foreach ($command in $activations) {
    $snapshot = Get-Snapshot $app 'current' $dir
    $node = Find-Node $snapshot $page $viewModel
    if (-not $node) { return [pscustomobject]@{ Ok = $false; Command = $command; Tried = $tried; Error = 'entry not found' } }
    if (@($node.states) -contains 'selected') { return [pscustomobject]@{ Ok = $true; Command = 'already selected'; Tried = $tried; Error = $null } }
    $ref = [string]$node.ref_id
    if (-not $ref.StartsWith('@')) { $ref = '@' + $ref }
    if (@($node.states) -contains 'offscreen') { Invoke-Ad scroll-to $ref | Out-Null; Start-Sleep -Milliseconds 400 }
    $answer = switch ($command) {
      'headed-click'        { Invoke-Ad --headed click $ref }
      'headed-double-click' { Invoke-Ad --headed double-click $ref }
      'focus-return'        { Invoke-Ad --headed focus $ref | Out-Null; Invoke-Ad --headed press return }
    }
    $tried += "$command=$(if ($answer.ok) { 'ok' } else { $answer.error.code })"
    Start-Sleep -Milliseconds 1200
    $check = Find-Node (Get-Snapshot $app 'check' $dir) $page $viewModel
    if ($check -and (@($check.states) -contains 'selected')) { return [pscustomobject]@{ Ok = $true; Command = $command; Tried = $tried; Error = $null } }
  }
  [pscustomobject]@{ Ok = $false; Command = $null; Tried = $tried; Error = 'the entry never became selected' }
}

function Test-Errors($snapshot) {
  $names = ($snapshot.Nodes | ForEach-Object { $_.name }) -join "`n"
  $errorWords | Where-Object { $names -match [regex]::Escape($_) }
}

# The help of the commands: what to send when a step fails because a flag is not the one assumed here.
(& agent-desktop --help 2>&1 | Out-String) | Set-Content -Path (Join-Path $OutDir 'help.txt') -Encoding UTF8
foreach ($command in 'snapshot', 'click', 'select', 'double-click', 'scroll', 'scroll-to', 'launch', 'find', 'wait', 'list-windows', 'close-app', 'screenshot') {
  (& agent-desktop $command --help 2>&1 | Out-String) | Set-Content -Path (Join-Path $OutDir "help-$command.txt") -Encoding UTF8
}
(& agent-desktop status 2>&1 | Out-String) | Set-Content -Path (Join-Path $OutDir 'status.txt') -Encoding UTF8

$report = [ordered]@{ date = (Get-Date).ToString('s'); os = [Environment]::OSVersion.VersionString; hosts = [ordered]@{} }

foreach ($name in $Hosts) {
  $host_ = $catalog[$name]
  if (-not $host_) { Write-Warning "Unknown host '$name' (Wpf, Avalonia, Maui, Uno, WinUI3)"; continue }
  $dir = Join-Path $OutDir $name
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $entry = [ordered]@{ started = $false; pages = [ordered]@{}; dialogs = [ordered]@{}; problems = @() }
  $report.hosts[$name] = $entry

  if (-not (Test-Path $host_.Exe)) { $entry.problems += "executable not found: $($host_.Exe) (build the sample in Debug)"; continue }
  $process = Start-Process -FilePath $host_.Exe -PassThru

  # Wait until the tree of the window holds the menu.
  $snapshot = $null
  $deadline = (Get-Date).AddSeconds($StartTimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $snapshot = Get-Snapshot $host_.App 'start' $dir
    $named = @($snapshot.Nodes | Where-Object { $_.name })
    Write-Host ("[{0}] snapshot ok={1} nodes={2} named={3}" -f $name, $snapshot.Ok, $snapshot.Nodes.Count, $named.Count)
    if ($snapshot.Ok -and (Find-Node $snapshot 'Cats' $viewModels['Cats'])) { $entry.started = $true; break }
  }
  if (-not $entry.started) {
    # What agent-desktop answered, and what it sees: this is what tells why the menu is not found.
    Write-Host "[$name] the menu was not found. Last answer:"
    ($snapshot.Raw | ConvertTo-Json -Depth 4) | Select-Object -First 1 | Write-Host
    Write-Host "[$name] names seen:"
    ($snapshot.Nodes | Where-Object { $_.name } | Select-Object -First 40 | ForEach-Object { "  $($_.role): $($_.name) [$($_.ref_id)]" }) | Write-Host
    Write-Host "[$name] windows:"
    (& agent-desktop list-windows 2>&1 | Out-String) | Write-Host
    (& agent-desktop list-windows 2>&1 | Out-String) | Set-Content -Path (Join-Path $dir 'windows.txt') -Encoding UTF8
  }
  if (-not $entry.started) { $entry.problems += 'the window did not show the menu in time'; Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue; continue }

  foreach ($page in $pages) {
    $activation = Invoke-Activate $host_.App $page $viewModels[$page] $dir
    $after = Get-Snapshot $host_.App ('page-' + ($page -replace '\W', '_')) $dir
    $errors = @(Test-Errors $after)
    Write-Host ("[{0}] {1}: {2}" -f $name, $page, $(if ($activation.Ok) { "selected by $($activation.Command)" } else { "not selected ($($activation.Error); $($activation.Tried -join ', '))" }))
    $entry.pages[$page] = if (-not $activation.Ok) { "$($activation.Error): $($activation.Tried -join ', ')" }
                          elseif (-not $after.Ok) { 'window did not answer after the click' }
                          elseif ($errors.Count) { "error text on screen: $($errors -join ', ')" }
                          else { "ok via $($activation.Command) ($($after.Nodes.Count) nodes)" }
  }

  # Dialogs: open each, answer it, check the page still answers.
  $dialogsEntry = Find-Node (Get-Snapshot $host_.App 'menu' $dir) 'Dialogs' $viewModels['Dialogs']
  if ($dialogsEntry) {
    Invoke-Activate $host_.App 'Dialogs' $viewModels['Dialogs'] $dir | Out-Null
    foreach ($button in $dialogButtons) {
      $snapshot = Get-Snapshot $host_.App 'dialogs' $dir
      $node = Find-Node $snapshot $button
      if (-not $node) { $entry.dialogs[$button] = 'button not found'; continue }
      Invoke-Click $node | Out-Null
      Start-Sleep -Milliseconds 1200
      $open = Get-Snapshot $host_.App ('dialog-' + ($button -replace '\W', '_')) $dir
      $answered = $null
      foreach ($answer in $answers) {
        $target = $open.Nodes | Where-Object { $_.ref_id -and $_.name -and $_.name.Trim() -eq $answer } | Select-Object -First 1
        if ($target -and $target.ref_id -ne $node.ref_id) { Invoke-Click $target | Out-Null; $answered = $answer; break }
      }
      Start-Sleep -Milliseconds 1200
      $final = Get-Snapshot $host_.App 'dialogs-after' $dir
      $entry.dialogs[$button] = if (-not $answered) { 'no dialog button found after the click' }
                                elseif (-not $final.Ok) { "answered '$answered', then the window did not answer" }
                                else { "ok, answered '$answered'" }
    }
  } else { $entry.problems += 'Dialogs entry not found' }

  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------------------------------------------------------------
# The templates: dotnet new, build, start, and follow the two pages they hold.
# ---------------------------------------------------------------------------------------------------------------------------------
# Where the executable of each template is once built in Debug (the application is called RouteablesApp).
$templateExe = @{
  avalonia = 'bin/Debug/net10.0/RouteablesApp.exe'
  maui     = 'bin/Debug/net10.0-windows10.0.19041.0/win-x64/RouteablesApp.exe'
  uno      = 'bin/Debug/net10.0-desktop/RouteablesApp.exe'
  winui3   = 'bin/Debug/net10.0-windows10.0.19041.0/win-x64/RouteablesApp.exe'
}

# True when a node of the tree shows this text (a label, a button, a text block).
function Test-Text($snapshot, [string] $text) {
  [bool]($snapshot.Nodes | Where-Object { $_.name -and $_.name.Contains($text) } | Select-Object -First 1)
}

# Waits until the window shows the text, or until the time is over.
function Wait-Text([string] $app, [string] $text, [string] $label, [string] $dir, [int] $seconds = 20) {
  $deadline = (Get-Date).AddSeconds($seconds)
  do {
    $snapshot = Get-Snapshot $app $label $dir
    if ($snapshot.Ok -and (Test-Text $snapshot $text)) { return $true }
    Start-Sleep -Milliseconds 800
  } while ((Get-Date) -lt $deadline)
  $false
}

# Presses a button by its name: the accessibility press first, a physical click when the page does not change.
function Invoke-Button([string] $app, [string] $name, [string] $expected, [string] $dir) {
  $node = Find-Node (Get-Snapshot $app 'button' $dir) $name
  if (-not $node) { return "button '$name' not found" }
  Invoke-Click $node | Out-Null
  if (Wait-Text $app $expected ('after-' + ($name -replace '\W', '_')) $dir 6) { return $null }
  $ref = [string]$node.ref_id
  if (-not $ref.StartsWith('@')) { $ref = '@' + $ref }
  Invoke-Ad --headed click $ref | Out-Null
  if (Wait-Text $app $expected ('after-headed-' + ($name -replace '\W', '_')) $dir 10) { return $null }
  "the page did not show '$expected' after the click on '$name'"
}

$report['templates'] = [ordered]@{}
foreach ($template in $Templates) {
  $name = $template.ToLowerInvariant()
  $entry = [ordered]@{ created = $false; built = $false; started = $false; steps = [ordered]@{}; problems = @() }
  $report['templates'][$name] = $entry
  if (-not $templateExe.ContainsKey($name)) { $entry.problems += "unknown template '$template' (avalonia, maui, uno, winui3)"; continue }

  $root = Join-Path $OutDir (Join-Path 'templates' $name)
  if (Test-Path $root) { Remove-Item -Recurse -Force $root }
  New-Item -ItemType Directory -Force -Path $root | Out-Null
  $log = Join-Path $root 'log'
  New-Item -ItemType Directory -Force -Path $log | Out-Null

  # The templates of this repository, installed from the folder (no package needed to try them).
  & dotnet new install (Resolve-Path 'templates').Path *>&1 | Out-File (Join-Path $log 'install.txt')
  & dotnet new "routeables-$name" -n RouteablesApp -o (Join-Path $root 'app') --force *>&1 | Out-File (Join-Path $log 'new.txt')
  $app = Join-Path $root 'app'
  $entry.created = Test-Path (Join-Path $app 'RouteablesApp.csproj')
  if (-not $entry.created) { $entry.problems += 'dotnet new did not make the project (see log/new.txt)'; continue }

  $build = @('build', (Join-Path $app 'RouteablesApp.csproj'), '-c', 'Debug')
  if ($PackageSource) { $build += @('--source', (Resolve-Path $PackageSource).Path, '--source', 'https://api.nuget.org/v3/index.json') }
  & dotnet @build *>&1 | Out-File (Join-Path $log 'build.txt')
  $exe = Join-Path $app $templateExe[$name]
  $entry.built = ($LASTEXITCODE -eq 0) -and (Test-Path $exe)
  if (-not $entry.built) { $entry.problems += 'the build failed or the executable is missing (see log/build.txt)'; continue }

  $process = Start-Process -FilePath $exe -PassThru
  $entry.started = Wait-Text 'RouteablesApp' 'Hello, World!' 'start' $root $StartTimeoutSeconds
  if (-not $entry.started) { $entry.problems += "the first page did not show 'Hello, World!' in time"; Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue; continue }
  $entry['steps']['hello'] = 'ok'

  $failure = Invoke-Button 'RouteablesApp' 'About' 'the page is chosen by an address' $root
  $entry['steps']['about'] = if ($failure) { $failure } else { 'ok' }
  if (-not $failure) {
    $failure = Invoke-Button 'RouteablesApp' 'Hello' 'Hello, again!' $root
    $entry['steps']['back'] = if ($failure) { $failure } else { 'ok' }
  }
  Write-Host ("[template {0}] {1}" -f $name, (($entry['steps'].GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; '))
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

$report | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $OutDir 'report.json') -Encoding UTF8
Write-Host "Report: $(Join-Path $OutDir 'report.json')"
