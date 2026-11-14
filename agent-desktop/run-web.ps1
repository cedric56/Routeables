<#
.SYNOPSIS
  Smoke test of the web hosts (Blazor Server, Blazor WebAssembly, Razor Pages) in Chrome, driven by agent-desktop through the accessibility
  tree of the page: the same steps as run.ps1 for the native hosts: every page of the menu, the first animal of each list and Back, every dialog
  of the Dialogs page, then its notifications. Avalonia Browser draws in a canvas (no accessibility tree): only its start is checked.

.DESCRIPTION
  Written from the trees of probe-web.ps1 (a real run on Windows): the menu is a group named "Menu" whose entries are links (Razor) or buttons
  (Blazor); an animal is a link; a dialog is read as the buttons that appear. What the trees of the dialogs and of the notifications look like has
  not been seen yet: the evidence of a failed step (the snapshot, a screenshot) is what the next version is written from.

  Output: out/web-smoke/report.json and report.md, one folder per host with the server log and the evidence. Exit code 0 when nothing failed.

.EXAMPLE
  pwsh agent-desktop/run-web.ps1 -Hosts Razor,BlazorWasm -Pages Cats,Dialogs
#>
[CmdletBinding()]
param(
  [string[]] $Hosts = @('BlazorServer', 'BlazorWasm', 'Razor', 'AvaloniaBrowser'),
  [string[]] $Pages = @(),
  [switch] $SkipDetails,
  [switch] $SkipDialogs,
  [switch] $SkipNotifications,
  [int] $StartTimeoutSeconds = 240,
  [int] $StepTimeoutMs = 6000,
  [string] $OutDir = ''
)
$ErrorActionPreference = 'Stop'
$total = [Diagnostics.Stopwatch]::StartNew()
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'out/web-smoke' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Import-Module (Join-Path $PSScriptRoot 'lib/AgentDesktop.Smoke.psm1') -Force   # Resolve-AgentDesktop
. (Join-Path $PSScriptRoot 'lib/Samples.ps1')
function Split-List([string[]] $Values) { @($Values | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Hosts = Split-List $Hosts
$Pages = Split-List $Pages
if (-not $Pages.Count) { $Pages = $allPages }
$ad = Resolve-AgentDesktop ''
if (-not $env:AGENT_DESKTOP_HOME) { $env:AGENT_DESKTOP_HOME = Join-Path ([IO.Path]::GetTempPath()) ('agent-desktop-routeables-' + [Environment]::UserName) }
New-Item -ItemType Directory -Force -Path $env:AGENT_DESKTOP_HOME | Out-Null
if (-not $IsWindows) { & chmod 700 $env:AGENT_DESKTOP_HOME }

$catalog = [ordered]@{
  BlazorServer    = @{ Project = 'samples/Blazor/XAnimals.BlazorApp'; Url = 'http://localhost:5205'; Tree = $true }
  BlazorWasm      = @{ Project = 'samples/Blazor/XAnimals.BlazorWebAssembly'; Url = 'http://localhost:5013'; Tree = $true }
  Razor           = @{ Project = 'samples/Razor/XAnimals.WebApplication'; Url = 'http://localhost:5197'; Tree = $true }
  AvaloniaBrowser = @{ Project = 'samples/XAnimals.Avalonia.Browser'; Url = 'http://localhost:5235'; Tree = $false }
}
$chrome = $null
if ($IsWindows) { $chrome = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1 }
elseif ($IsMacOS) { $chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' }
else { $chrome = (Get-Command google-chrome, chromium, chromium-browser -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
if (-not $chrome -or -not (Test-Path -LiteralPath $chrome)) { throw 'Chrome was not found.' }

# ------------------------------------------------------------------------------------------------------------------------------
# agent-desktop and the tree of the page
# ------------------------------------------------------------------------------------------------------------------------------

$script:app = $null
$script:dir = $null

function Invoke-Ad([string[]] $Arguments, [switch] $Headed) {
  $all = @($Arguments) + @('--session', 'routeables-web-smoke')
  if ($Headed) { $all += '--headed' }
  $text = (& $ad @all 2>$null) -join "`n"
  $result = $null
  if ($text) { try { $result = $text | ConvertFrom-Json -Depth 200 } catch { } }
  if (-not $result) { $result = [pscustomobject]@{ ok = $false; data = $null; error = [pscustomobject]@{ code = 'NO_JSON'; message = $text } } }
  $result | Add-Member -NotePropertyName raw -NotePropertyValue $text -Force
  $result
}

# The nodes of the page (what is under "Chrome Legacy Window"), flat: role, name, ref, bounds, and whether the node is inside the menu.
function Get-PageNodes {
  $snap = Invoke-Ad @('snapshot', '--app', $script:app, '--compact', '--max-depth', '50', '--include-bounds')
  $script:lastSnapshot = $snap.raw
  $nodes = [Collections.Generic.List[object]]::new()
  if (-not $snap.ok) { return , $nodes }
  $walk = $null
  $walk = {
    param($node, $inPage, $inMenu, $inDialog)
    $name = "$($node.name)"
    if ($node.role -eq 'group' -and $name -eq 'Chrome Legacy Window') { $inPage = $true }
    if ($inPage -and $node.role -eq 'group' -and $name -eq 'Menu') { $inMenu = $true }
    # A dialog is a window (or a dialog) of the page: its buttons are the answers, not the buttons of the page behind it.
    if ($inPage -and $node.role -in 'window', 'dialog') { $inDialog = $true }
    if ($inPage) {
      $b = $node.bounds
      $nodes.Add([pscustomobject]@{
        Role = "$($node.role)"; Name = $name; Ref = $node.ref_id; InMenu = $inMenu; InDialog = $inDialog
        X = if ($b) { [double]$b.x } else { 0 }; Y = if ($b) { [double]$b.y } else { 0 }
        W = if ($b) { [double]$b.width } else { 0 }; H = if ($b) { [double]$b.height } else { 0 }
      })
    }
    foreach ($child in @($node.children)) { if ($child) { & $walk $child $inPage $inMenu $inDialog } }
  }
  & $walk $snap.data.tree $false $false $false
  , $nodes
}

function Wait-Nodes([scriptblock] $Match, [int] $TimeoutMs = $StepTimeoutMs) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  do {
    $nodes = Get-PageNodes
    $found = @($nodes | Where-Object $Match)
    if ($found.Count) { return $found }
    Start-Sleep -Milliseconds 300
  } while ($watch.ElapsedMilliseconds -lt $TimeoutMs)
  @()
}

# A semantic click first (agent-desktop refuses it on a twin: two nodes of the same name); else a click of the mouse at the centre of the node
# when it is on screen.
function Invoke-Click($Node) {
  if ($Node.Ref) {
    $semantic = Invoke-Ad @('click', $Node.Ref)
    if ($semantic.ok) { return $true }
  }
  if ($Node.W -gt 0 -and $Node.H -gt 0) {
    $x = [int]($Node.X + $Node.W / 2); $y = [int]($Node.Y + $Node.H / 2)
    return [bool](Invoke-Ad @('mouse-click', '--xy', "$x,$y") -Headed).ok
  }
  $false
}

function Save-Evidence([string] $Label) {
  $safe = $Label -replace '[^\w\-]+', '_'
  if ($script:lastSnapshot) { Set-Content -LiteralPath (Join-Path $script:dir "$safe.json") -Value $script:lastSnapshot -Encoding utf8 }
  Invoke-Ad @('screenshot', (Join-Path $script:dir "$safe.png"), '--app', $script:app) | Out-Null
}

function New-Step([string] $Status, [string] $Detail) { [ordered]@{ status = $Status; detail = $Detail } }

# ------------------------------------------------------------------------------------------------------------------------------
# The steps
# ------------------------------------------------------------------------------------------------------------------------------

function Test-Page([string] $Page) {
  $entry = @(Wait-Nodes { $_.InMenu -and $_.Role -in 'link', 'button' -and ($_.Name -eq $Page -or $_.Name.StartsWith("$Page ")) }) | Select-Object -First 1
  if (-not $entry) { Save-Evidence "page-$Page-no-entry"; return New-Step 'fail' "no entry '$Page' in the menu" }
  if (-not (Invoke-Click $entry)) { Save-Evidence "page-$Page-click"; return New-Step 'fail' 'the entry could not be clicked' }
  $titles = @($script:titles[$Page])
  $shown = Wait-Nodes { -not $_.InMenu -and ($_.Role -eq 'statictext' -or $_.Role -eq 'document') -and $_.Name -in $titles }
  if ($shown.Count) { return New-Step 'ok' $null }
  Save-Evidence "page-$Page"
  New-Step 'fail' "the title '$($titles -join "' or '")' never appeared"
}

function Test-Detail([string] $Page, [string] $Animal) {
  # A link or a button first; else the smallest node of that name (not the picture or the text inside it).
  # Else the text of the name (Blazor: the item is a div without a role, the tree has only its picture and its texts): a click of the mouse on it.
  $item = @(Wait-Nodes { -not $_.InMenu -and $_.Role -notin 'image', 'document' -and $_.Name.StartsWith($Animal) }) |
    Sort-Object @{ Expression = { if ($_.Role -in 'link', 'button') { 0 } else { 1 } } }, @{ Expression = { $_.W * $_.H } } | Select-Object -First 1
  if (-not $item) { Save-Evidence "detail-$Page-no-animal"; return New-Step 'fail' "'$Animal' never appeared" }
  if (-not (Invoke-Click $item)) { Save-Evidence "detail-$Page-click"; return New-Step 'fail' "'$Animal' could not be clicked" }
  $back = @(Wait-Nodes { -not $_.InMenu -and $_.Role -in 'link', 'button' -and $_.Name -match '^\W*Back\b' }) | Select-Object -First 1
  if (-not $back) { Save-Evidence "detail-$Page"; return New-Step 'fail' 'no Back button on the detail' }
  if (-not (Invoke-Click $back)) { Save-Evidence "detail-$Page-back"; return New-Step 'fail' 'Back could not be clicked' }
  # The list is back when the Back button is gone and the animal is on the page again.
  $watch = [Diagnostics.Stopwatch]::StartNew()
  do {
    $nodes = Get-PageNodes
    $stillBack = @($nodes | Where-Object { -not $_.InMenu -and $_.Role -in 'link', 'button' -and $_.Name -match '^\W*Back\b' }).Count
    $listed = @($nodes | Where-Object { -not $_.InMenu -and $_.Role -ne 'image' -and $_.Name.StartsWith($Animal) }).Count
    if (-not $stillBack -and $listed) { return New-Step 'ok' $null }
    Start-Sleep -Milliseconds 300
  } while ($watch.ElapsedMilliseconds -lt $StepTimeoutMs)
  Save-Evidence "detail-$Page-after-back"
  New-Step 'fail' 'Back did not bring the list back'
}

# The button of that name on the page: the highest one (the dialogs come above the notifications), or the lowest.
function Find-PageButton([string] $Name, [switch] $Lowest) {
  $all = @(Wait-Nodes { -not $_.InMenu -and $_.Role -eq 'button' -and $_.Name -eq $Name })
  if (-not $all.Count) { return $null }
  if ($Lowest) { $all | Sort-Object Y -Descending | Select-Object -First 1 } else { $all | Sort-Object Y | Select-Object -First 1 }
}

function Test-Dialog($Spec) {
  $opener = Find-PageButton $Spec.Button
  if (-not $opener) { Save-Evidence "dialog-$($Spec.Button)-no-button"; return New-Step 'fail' 'button not found on the Dialogs page' }
  if (-not (Invoke-Click $opener)) { Save-Evidence "dialog-$($Spec.Button)-click"; return New-Step 'fail' 'the button could not be clicked' }
  Start-Sleep -Milliseconds 400
  $answered = $null
  foreach ($answer in $Spec.Answers) {
    # An answer is a button that was not there before: below the opener, or in a layer over the page; the first one of that name that is not the opener.
    # The one in the dialog if the tree has one (a page that posts a form is reloaded: the opener has a new ref, and the same name as an answer);
    # else the first one that is not the opener (same place on the screen).
    $match = @(Wait-Nodes { $_.Role -in 'button', 'link' -and $_.Name -ieq $answer -and -not $_.InMenu -and ($_.InDialog -or ($_.Ref -ne $opener.Ref -and ($_.X -ne $opener.X -or $_.Y -ne $opener.Y))) } 1500) |
      Sort-Object @{ Expression = { if ($_.InDialog) { 0 } else { 1 } } } | Select-Object -First 1
    if ($match -and (Invoke-Click $match)) { $answered = $answer; break }
  }
  if (-not $answered) { Save-Evidence "dialog-$($Spec.Button)"; return New-Step 'fail' "no answer ($($Spec.Answers -join ', ')) found" }
  $expected = $Spec.Results[$answered]
  $seen = Wait-Nodes { -not $_.InMenu -and $_.Name -eq $expected }
  if ($seen.Count) { return New-Step 'ok' "answered $answered" }
  Save-Evidence "dialog-$($Spec.Button)-result"
  New-Step 'fail' "answered $answered; the Result line never showed '$expected'"
}

function Test-Notification($Spec) {
  $opener = Find-PageButton $Spec.Button -Lowest
  if (-not $opener) { Save-Evidence "notification-$($Spec.Button)-no-button"; return New-Step 'fail' 'button not found on the Dialogs page' }
  if (-not (Invoke-Click $opener)) { Save-Evidence "notification-$($Spec.Button)-click"; return New-Step 'fail' 'the button could not be clicked' }
  if ($Spec.System) {
    $seen = Wait-Nodes { -not $_.InMenu -and ($_.Name -eq $Spec.Sent -or $_.Name -eq $Spec.Unavailable) } $Spec.WaitMs
    if (-not $seen.Count) { Save-Evidence "notification-$($Spec.Button)"; return New-Step 'unverified' 'no answer on the Result line (the browser asks for a permission that nothing answers?)' }
    if ($seen[0].Name -eq $Spec.Sent) { return New-Step 'ok' $null }
    return New-Step 'unverified' $seen[0].Name
  }
  # The notification is a button whose name is its title and its message together (a button hides the texts inside it from the tree: Razor), or
  # the title alone (Blazor, a text of its own).
  # The first look after the click, kept for the report: a notification that goes away on its own may be gone when the waiting ends.
  $first = Get-PageNodes
  $firstSeen = @($first | Where-Object { -not $_.InMenu -and $_.Name -and $_.Role -in 'statictext', 'button', 'link' } | ForEach-Object { $_.Name } | Select-Object -Unique -First 40)
  $shown = @(Wait-Nodes { -not $_.InMenu -and ($_.Name -eq $Spec.Title -or ($_.Role -in 'button', 'link' -and $_.Name.StartsWith("$($Spec.Title) "))) } $Spec.WaitMs) |
    Sort-Object @{ Expression = { if ($_.Role -in 'button', 'link') { 0 } else { 1 } } } | Select-Object -First 1
  if (-not $shown) { Save-Evidence "notification-$($Spec.Button)"; return New-Step 'fail' "the notification '$($Spec.Title)' did not appear (first look after the click: $($firstSeen -join ' | '))" }
  if (-not (Invoke-Click $shown)) { Save-Evidence "notification-$($Spec.Button)-click"; return New-Step 'fail' 'the notification could not be clicked' }
  $clicked = Wait-Nodes { -not $_.InMenu -and $_.Name -in $Spec.Clicked }
  if ($clicked.Count) { return New-Step 'ok' $clicked[0].Name }
  Save-Evidence "notification-$($Spec.Button)-click-result"
  New-Step 'fail' "a click on the notification did not write $($Spec.Clicked -join ' or ')"
}

# ------------------------------------------------------------------------------------------------------------------------------
# Hosts
# ------------------------------------------------------------------------------------------------------------------------------

$report = [ordered]@{ date = (Get-Date).ToString('s'); os = [Environment]::OSVersion.VersionString; hosts = [ordered]@{} }

foreach ($name in $Hosts) {
  $spec = $catalog[$name]
  if (-not $spec) { Write-Host "Unknown host $name" -ForegroundColor Red; continue }
  $script:dir = Join-Path $OutDir $name
  New-Item -ItemType Directory -Force -Path $script:dir | Out-Null
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $h = [ordered]@{ status = 'fail'; seconds = 0; pages = [ordered]@{}; details = [ordered]@{}; dialogs = [ordered]@{}; notifications = [ordered]@{}; problems = @() }
  $report.hosts[$name] = $h
  $server = $null; $browser = $null
  try {
    # Development: the static files of the libraries (_framework, _content) are served from the build only in that environment.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $arguments = @('run', '--project', (Join-Path $RepoRoot $spec.Project), '-c', 'Debug')
    if ($name -ne 'AvaloniaBrowser') { $arguments += @('--no-launch-profile', '--urls', $spec.Url) }
    $server = Start-Process dotnet -ArgumentList $arguments -PassThru -RedirectStandardOutput (Join-Path $script:dir 'server-out.txt') -RedirectStandardError (Join-Path $script:dir 'server-err.txt') -WindowStyle Hidden
    $url = $spec.Url
    $deadline = [DateTime]::UtcNow.AddSeconds($StartTimeoutSeconds)
    $up = $false
    while ([DateTime]::UtcNow -lt $deadline -and -not $server.HasExited) {
      if ($name -eq 'AvaloniaBrowser') {
        $listening = Select-String -LiteralPath (Join-Path $script:dir 'server-out.txt') -Pattern 'Now listening on: (http://\S+)' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($listening) { $url = $listening.Matches[0].Groups[1].Value }
      }
      try { if ((Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200) { $up = $true; break } } catch { }
      Start-Sleep -Seconds 3
    }
    if (-not $up) { $h.problems += "the server did not answer on $url in $StartTimeoutSeconds s"; continue }

    $profileDir = Join-Path ([IO.Path]::GetTempPath()) "routeables-chrome-$name"
    if (Test-Path $profileDir) { Remove-Item -Recurse -Force $profileDir }
    $browser = Start-Process $chrome -PassThru -ArgumentList @('--force-renderer-accessibility', '--no-first-run', '--no-default-browser-check', "--user-data-dir=$profileDir", '--window-position=0,0', '--window-size=1800,1000', '--new-window', $url)
    # Chrome is "chrome.exe" to agent-desktop on Windows, "Google Chrome" on macOS: the name list-apps gives.
    $script:app = $null
    $appDeadline = [DateTime]::UtcNow.AddSeconds(60)
    while (-not $script:app -and [DateTime]::UtcNow -lt $appDeadline) {
      $apps = Invoke-Ad @('list-apps')
      $chromeApp = @($apps.data.apps) | Where-Object { $_.name -match 'chrome' } | Select-Object -First 1
      if ($chromeApp) { $script:app = $chromeApp.name } else { Start-Sleep -Seconds 2 }
    }
    if (-not $script:app) { $h.problems += 'agent-desktop does not list Chrome'; continue }

    if (-not $spec.Tree) {
      # A canvas: the page has a title and nothing else for the accessibility tree.
      $ready = Wait-Nodes { $_.Role -eq 'document' } 60000
      Invoke-Ad @('screenshot', (Join-Path $script:dir 'page.png'), '--app', $script:app) | Out-Null
      $h.pages['start'] = New-Step 'unverified' "the page '$($ready[0].Name)' loaded; Avalonia Browser draws in a canvas: no accessibility tree to read"
      $h.status = 'ok'
      continue
    }

    # The first render: Blazor WebAssembly downloads its runtime first.
    $menuUp = Wait-Nodes { $_.InMenu -and $_.Role -in 'link', 'button' } 120000
    if (-not $menuUp.Count) { Save-Evidence 'start'; $h.problems += 'the page never showed a menu'; continue }

    foreach ($page in $Pages) {
      $h.pages[$page] = Test-Page $page
      Write-Host ("[{0}] {1}: {2} {3}" -f $name, $page, $h.pages[$page].status, $h.pages[$page].detail)
      if (-not $SkipDetails -and $firstAnimal.ContainsKey($page) -and $h.pages[$page].status -eq 'ok') {
        $h.details[$page] = Test-Detail $page $firstAnimal[$page]
        Write-Host ("[{0}] {1} detail: {2} {3}" -f $name, $page, $h.details[$page].status, $h.details[$page].detail)
      }
    }
    if (($Pages -contains 'Dialogs') -and (-not $SkipDialogs -or -not $SkipNotifications)) {
      $dialogsPage = Test-Page 'Dialogs'
      if ($dialogsPage.status -eq 'ok') {
        if (-not $SkipDialogs) {
          foreach ($dialog in $dialogs) {
            $h.dialogs[$dialog.Button] = Test-Dialog $dialog
            Write-Host ("[{0}] dialog {1}: {2} {3}" -f $name, $dialog.Button, $h.dialogs[$dialog.Button].status, $h.dialogs[$dialog.Button].detail)
          }
        }
        if (-not $SkipNotifications) {
          foreach ($note in $notificationSpecs) {
            $h.notifications[$note.Button] = Test-Notification $note
            Write-Host ("[{0}] notification {1}: {2} {3}" -f $name, $note.Button, $h.notifications[$note.Button].status, $h.notifications[$note.Button].detail)
          }
        }
      } else { $h.problems += 'the Dialogs page did not open' }
    }
    $bad = @($h.pages.Values) + @($h.details.Values) + @($h.dialogs.Values) + @($h.notifications.Values) | Where-Object { $_.status -eq 'fail' }
    $h.status = if (@($bad).Count -or $h.problems.Count) { 'fail' } else { 'ok' }
  } catch {
    $h.problems += "script error: $($_.Exception.Message) at line $($_.InvocationInfo.ScriptLineNumber)"
  } finally {
    $h.seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 1)
    if ($browser -and -not $browser.HasExited) { Stop-Process -Id $browser.Id -Force -ErrorAction SilentlyContinue }
    Get-Process chrome -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($server -and -not $server.HasExited) {
      if ($IsWindows) { & taskkill /PID $server.Id /T /F 2>$null | Out-Null }
      Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
  }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Report
# ------------------------------------------------------------------------------------------------------------------------------

function Format-Count($Items) {
  $values = @($Items)
  if (-not $values.Count) { return '-' }
  $ok = @($values | Where-Object { $_.status -eq 'ok' }).Count
  $unverified = @($values | Where-Object { $_.status -eq 'unverified' }).Count
  "$ok/$($values.Count)$(if ($unverified) { " (+$unverified ?)" })"
}

$report.seconds = [Math]::Round($total.Elapsed.TotalSeconds, 1)
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutDir 'report.json') -Encoding utf8
$md = [Text.StringBuilder]::new()
[void]$md.AppendLine("# Web smoke test`n`n$($report.date) on $($report.os), $($report.seconds)s`n")
[void]$md.AppendLine('| Host | Status | Pages | Details | Dialogs | Notifications | Time |')
[void]$md.AppendLine('|---|---|---|---|---|---|---|')
foreach ($key in $report.hosts.Keys) {
  $h = $report.hosts[$key]
  [void]$md.AppendLine("| $key | $($h.status.ToUpperInvariant()) | $(Format-Count $h.pages.Values) | $(Format-Count $h.details.Values) | $(Format-Count $h.dialogs.Values) | $(Format-Count $h.notifications.Values) | $($h.seconds)s |")
}
foreach ($key in $report.hosts.Keys) {
  $h = $report.hosts[$key]
  $lines = @($h.problems | ForEach-Object { "- $_" })
  foreach ($section in 'pages', 'details', 'dialogs', 'notifications') {
    foreach ($entry in $h[$section].GetEnumerator() | Where-Object { $_.Value.status -ne 'ok' }) { $lines += "- **$section / $($entry.Key)**: $($entry.Value.status), $($entry.Value.detail)" }
  }
  if ($lines.Count) { [void]$md.AppendLine("`n## $key`n"); foreach ($line in $lines) { [void]$md.AppendLine($line) } }
}
Set-Content -LiteralPath (Join-Path $OutDir 'report.md') -Value $md.ToString() -Encoding utf8
Write-Host $md.ToString()
$failed = @($report.hosts.Values | Where-Object { $_.status -ne 'ok' }).Count
exit $(if ($failed) { 1 } else { 0 })
