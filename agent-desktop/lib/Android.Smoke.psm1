# The smoke test of the XAnimals samples on Android (an emulator or a device), driven by adb. Loaded by run-android.ps1.
#
# agent-desktop reads the accessibility tree of desktop windows; the window of the emulator is a picture to it. On Android the same
# tree comes from uiautomator: "uiautomator dump" gives every node of the screen with its text, its content description (the
# accessible name), its class and its bounds; "input tap x y" clicks. The test is the same as on the desktop: each page of the menu,
# the first animal of each list and Back, each dialog of the Dialogs page and its Result line.
#
# What differs on a phone: the menu is a drawer, opened with the button of the toolbar; a page longer than the screen is scrolled to
# find a button or a text; animations are switched off while the test runs (uiautomator waits for an idle screen, and an animated
# progress bar would never be idle).

Set-StrictMode -Version 3.0

$script:Adb = $null
$script:Serial = $null

# ------------------------------------------------------------------------------------------------------------------------------
# adb
# ------------------------------------------------------------------------------------------------------------------------------

function Set-AdbTarget([string] $Adb, [string] $Serial) { $script:Adb = $Adb; $script:Serial = $Serial }

# One adb call on the device under test; recorded in the trace of the context when there is one.
function Invoke-Adb([hashtable] $Ctx, [string[]] $A, [int] $TimeoutSeconds = 60) {
  $arguments = @('-s', $script:Serial) + $A
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $text = (& $script:Adb @arguments 2>&1 | ForEach-Object { "$_" }) -join "`n"
  $code = $LASTEXITCODE
  if ($Ctx) {
    $Ctx.Calls++
    $Ctx.AdbMs += $watch.ElapsedMilliseconds
    $Ctx.Trace.Add([pscustomobject]@{ at = $Ctx.Clock.ElapsedMilliseconds; ms = $watch.ElapsedMilliseconds; cmd = ($A -join ' '); ok = ($code -eq 0) })
  }
  [pscustomobject]@{ Ok = ($code -eq 0); Text = $text }
}

function Invoke-Shell([hashtable] $Ctx, [string] $Command) { Invoke-Adb $Ctx @('shell', $Command) }

# ------------------------------------------------------------------------------------------------------------------------------
# The screen: uiautomator dump, flattened
# ------------------------------------------------------------------------------------------------------------------------------

# Every node of the screen: Name (its text, else its content description), Class, Id, Clickable, its bounds and centre, InList
# (inside a list: a RecyclerView, a ListView ...), and Package. $null when the dump failed (the screen never went idle).
function Get-Ui([hashtable] $Ctx) {
  $xmlText = $null
  foreach ($try in 1..2) {
    $dump = Invoke-Adb $Ctx @('exec-out', 'uiautomator', 'dump', '/dev/tty')
    $end = $dump.Text.LastIndexOf('</hierarchy>')
    $start = $dump.Text.IndexOf('<?xml')
    if ($start -lt 0) { $start = $dump.Text.IndexOf('<hierarchy') }
    if ($start -ge 0 -and $end -gt $start) { $xmlText = $dump.Text.Substring($start, $end - $start + '</hierarchy>'.Length); break }
    # Some images refuse /dev/tty: a file, then read.
    $file = Invoke-Adb $Ctx @('shell', 'uiautomator dump /sdcard/routeables-ui.xml >/dev/null && cat /sdcard/routeables-ui.xml')
    $end = $file.Text.LastIndexOf('</hierarchy>')
    $start = $file.Text.IndexOf('<?xml')
    if ($start -ge 0 -and $end -gt $start) { $xmlText = $file.Text.Substring($start, $end - $start + '</hierarchy>'.Length); break }
    Start-Sleep -Milliseconds 300
  }
  if (-not $xmlText) { return $null }
  $Ctx.LastXml = $xmlText
  $doc = [xml]::new()
  try { $doc.LoadXml($xmlText) } catch { return $null }
  $nodes = [Collections.Generic.List[object]]::new()
  $stack = [Collections.Generic.Stack[object]]::new()
  foreach ($child in @($doc.DocumentElement.ChildNodes)) { $stack.Push(@{ Node = $child; InList = $false }) }
  while ($stack.Count) {
    $top = $stack.Pop()
    $n = $top.Node
    if ($n.NodeType -ne 'Element') { continue }
    $class = $n.GetAttribute('class')
    $inList = $top.InList -or $class -match 'RecyclerView|ListView|GridView|CollectionView'
    $bounds = $n.GetAttribute('bounds')
    if ($bounds -match '^\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]$') {
      $x1 = [int]$Matches[1]; $y1 = [int]$Matches[2]; $x2 = [int]$Matches[3]; $y2 = [int]$Matches[4]
      $text = $n.GetAttribute('text'); $desc = $n.GetAttribute('content-desc')
      $name = if ($text) { $text } elseif ($desc) { $desc } else { '' }
      if ($x2 -gt $x1 -and $y2 -gt $y1) {
        $nodes.Add([pscustomobject]@{
          Name = $name.Trim(); Desc = $desc; Class = $class; Id = $n.GetAttribute('resource-id'); Package = $n.GetAttribute('package')
          Clickable = $n.GetAttribute('clickable') -eq 'true'; X1 = $x1; Y1 = $y1; X2 = $x2; Y2 = $y2
          X = [int](($x1 + $x2) / 2); Y = [int](($y1 + $y2) / 2); InList = $top.InList
        })
      }
    }
    $kids = @($n.ChildNodes)
    for ($i = $kids.Count - 1; $i -ge 0; $i--) { $stack.Push(@{ Node = $kids[$i]; InList = $inList }) }
  }
  $nodes
}

# Nodes of that name (case and the ◀ of a "◀ Back" set apart: the comparison is on the whole name, ignoring case).
function Find-Named($Ui, [string] $Name) {
  @($Ui | Where-Object { $_.Name -and [string]::Equals($_.Name, $Name, [StringComparison]::OrdinalIgnoreCase) })
}

# A menu entry: its own text, or a node whose accessible name starts with it on a line of its own (Avalonia merges the texts of
# an entry into one name: "Cats" and, below, "GoToRouteAsync<TViewModel> ...").
function Find-Entry($Ui, [string] $Name) {
  $exact = @(Find-Named $Ui $Name)
  if ($exact.Count) { return $exact }
  @($Ui | Where-Object { $_.Name -and ($_.Name -split "`r?`n")[0].Trim() -ieq $Name })
}

function Invoke-Tap([hashtable] $Ctx, [int] $X, [int] $Y) { Invoke-Shell $Ctx "input tap $X $Y" | Out-Null }

# A swipe of the content: up (towards the end of the page) or down (towards its top), in the middle of the screen or at $X.
function Invoke-Swipe([hashtable] $Ctx, [string] $Direction, [int] $X = 0) {
  $x = if ($X) { $X } else { [int]($Ctx.Width / 2) }
  $from = [int]($Ctx.Height * 0.70); $to = [int]($Ctx.Height * 0.30)
  if ($Direction -eq 'down') { $tmp = $from; $from = $to; $to = $tmp }
  Invoke-Shell $Ctx "input swipe $x $from $x $to 250" | Out-Null
  Start-Sleep -Milliseconds 250
}

function Wait-Ui([hashtable] $Ctx, [int] $TimeoutMs, [scriptblock] $Condition) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  do {
    $ui = Get-Ui $Ctx
    if ($ui) {
      $hit = & $Condition $ui
      if ($hit) { return [pscustomobject]@{ Ui = $ui; Hit = $hit } }
    }
    Start-Sleep -Milliseconds 150
  } while ($watch.ElapsedMilliseconds -lt $TimeoutMs)
  $null
}

# A text or a button that may be below the fold: looked for on screen, then while scrolling towards the end of the page, then
# back towards its top. $Pick chooses among the nodes of the screen.
function Find-Scrolling([hashtable] $Ctx, [scriptblock] $Pick, [int] $Swipes = 4) {
  $ui = Get-Ui $Ctx
  if ($ui) { $hit = & $Pick $ui; if ($hit) { return $hit } }
  foreach ($direction in 'up', 'down') {
    $previous = $null
    for ($i = 0; $i -lt $Swipes; $i++) {
      Invoke-Swipe $Ctx $direction
      $ui = Get-Ui $Ctx
      if (-not $ui) { continue }
      $hit = & $Pick $ui
      if ($hit) { return $hit }
      # The end of the page: the screen did not change.
      $signature = ($ui | ForEach-Object { "$($_.Name)@$($_.Y)" }) -join '|'
      if ($signature -eq $previous) { break }
      $previous = $signature
    }
  }
  $null
}

# ------------------------------------------------------------------------------------------------------------------------------
# The menu: a drawer
# ------------------------------------------------------------------------------------------------------------------------------

# The drawer is open when several entries of the menu are on screen at once.
# The drawer is open when several entries of the menu are on screen at once, one of them not an animal page: the Regions page
# shows a menu of its own with Cats, Dogs ... Pandas, and the toolbar shows the title of the current page (left out).
function Test-DrawerOpen([hashtable] $Ctx, $Ui) {
  $names = @($Ui | Where-Object { $_.Name } | ForEach-Object { ($_.Name -split "`r?`n")[0].Trim() })
  $shown = @($Ctx.MenuNames | Where-Object { $names -contains $_ -and $_ -notin @($Ctx.CurrentTitles) })
  $others = @($shown | Where-Object { $_ -notin $Ctx.AnimalPages })
  $shown.Count -ge 4 -and $others.Count -ge 1
}

# Where the drawer is: the median X of the menu entries on screen (the toolbar title, of the same name as an entry, is elsewhere).
function Get-DrawerX([hashtable] $Ctx, $Ui) {
  $xs = @(foreach ($name in $Ctx.MenuNames) { Find-Entry $Ui $name | ForEach-Object { $_.X } }) | Sort-Object
  if (-not $xs.Count) { return $null }
  $xs[[int]($xs.Count / 2)]
}

# The button of the toolbar that opens the drawer: its accessible name speaks of navigation, a drawer or a menu (MAUI's Shell
# "Open navigation drawer", Avalonia's "Toggle navigation drawer", Uno's pane button); else the clickable node with no text at the
# top left of the screen (a hamburger icon).
function Find-DrawerButton([hashtable] $Ctx, $Ui) {
  $named = @($Ui | Where-Object { $_.Clickable -and $_.Y -lt $Ctx.Height * 0.2 -and ($_.Desc -match '(?i)navigation|drawer|menu|hamburger' -or $_.Id -match '(?i)toggle|pane|hamburger|drawer') }) |
    Sort-Object Y, X | Select-Object -First 1
  if ($named) { return $named }
  @($Ui | Where-Object { $_.Clickable -and -not $_.Name -and $_.X -lt $Ctx.Width * 0.2 -and $_.Y -lt $Ctx.Height * 0.15 -and ($_.X2 - $_.X1) -lt $Ctx.Width * 0.25 }) |
    Sort-Object Y, X | Select-Object -First 1
}

# Closes a drawer left open over the page: a tap on its scrim, at the right edge, out of the drawer.
function Close-Drawer([hashtable] $Ctx) {
  Invoke-Tap $Ctx ($Ctx.Width - 24) ([int]($Ctx.Height / 2))
  Start-Sleep -Milliseconds 300
}

# The entry of a page in an open drawer: the node of that name below the toolbar (the toolbar shows the title of the current page,
# which may be the same name). Scrolls the drawer when the entry is below its end.
function Find-DrawerEntry([hashtable] $Ctx, [string] $Page, $Ui) {
  $pick = {
    param($u)
    $x = Get-DrawerX $Ctx $u
    $all = @(Find-Entry $u $Page)
    if ($null -eq $x) { return ($all | Sort-Object { -$_.Y } | Select-Object -First 1) }
    # The one in the drawer: nearest to the other entries.
    $all | Sort-Object { [Math]::Abs($_.X - $x) }, { -$_.Y } | Select-Object -First 1
  }
  $entry = & $pick $Ui
  if ($entry) { return $entry }
  # Out of view in the drawer: scrolled in the drawer, towards its end, then back towards its top.
  $x = Get-DrawerX $Ctx $Ui
  if ($null -eq $x) { $x = [int]($Ctx.Width / 4) }
  foreach ($direction in 'up', 'down') {
    for ($i = 0; $i -lt 3; $i++) {
      Invoke-Swipe $Ctx $direction $x
      $ui = Get-Ui $Ctx
      if ($ui) { $entry = & $pick $ui; if ($entry) { return $entry } }
    }
  }
  $null
}

# Opens a page from the drawer. Ok when the list shows its first animal ($Proof), or the toolbar its title, the drawer closed.
function Open-AndroidPage([hashtable] $Ctx, [string] $Page, [string[]] $Titles, [string] $Proof) {
  $ui = Get-Ui $Ctx
  if (-not $ui) { return [pscustomobject]@{ Ok = $false; Detail = 'uiautomator could not read the screen'; Shown = $null } }
  if (-not (Test-DrawerOpen $Ctx $ui)) {
    $button = Find-DrawerButton $Ctx $ui
    if (-not $button) { return [pscustomobject]@{ Ok = $false; Detail = 'no button to open the menu in the toolbar'; Shown = $null } }
    Invoke-Tap $Ctx $button.X $button.Y
    $open = Wait-Ui $Ctx 3000 { param($u) Test-DrawerOpen $Ctx $u }
    if (-not $open) { return [pscustomobject]@{ Ok = $false; Detail = "the menu did not open (button '$($button.Name)' at $($button.X),$($button.Y))"; Shown = $null } }
    $ui = $open.Ui
  }
  $entry = Find-DrawerEntry $Ctx $Page $ui
  if (-not $entry) {
    $seen = @(Get-Ui $Ctx | Where-Object { $_.Name } | ForEach-Object { ($_.Name -split "`r?`n")[0].Trim() } | Select-Object -Unique -First 25)
    return [pscustomobject]@{ Ok = $false; Detail = "'$Page' is not in the menu (on screen: $($seen -join ' | '))"; Shown = $null }
  }
  Invoke-Tap $Ctx $entry.X $entry.Y
  # From here the toolbar shows the title of the page asked for: not an entry of the drawer.
  $Ctx.CurrentTitles = @($Titles)
  $state = @{ Closed = $false }
  $result = Wait-Ui $Ctx $Ctx.StepMs {
    param($u)
    if (Test-DrawerOpen $Ctx $u) {
      # A drawer that stays open after a navigation (Avalonia's DrawerPage): closed once, by its scrim.
      if (-not $state.Closed) { Start-Sleep -Milliseconds 400; Close-Drawer $Ctx; $state.Closed = $true }
      return $null
    }
    if ($Proof) {
      $item = @(Find-Named $u $Proof) | Sort-Object Y | Select-Object -First 1
      if ($item) { return @{ Shown = $Page; Item = $item } }
    } else {
      foreach ($title in $Titles) { if (@(Find-Named $u $title).Count) { return @{ Shown = $title; Item = $null } } }
    }
    $null
  }
  if (-not $result) {
    $what = if ($Proof) { "'$Proof' never appeared" } else { "the title never showed '$($Titles -join "' or '")'" }
    return [pscustomobject]@{ Ok = $false; Detail = $what; Shown = $null }
  }
  [pscustomobject]@{ Ok = $true; Detail = $null; Shown = $result.Hit.Shown; Item = $result.Hit.Item }
}

# ------------------------------------------------------------------------------------------------------------------------------
# List -> detail -> Back
# ------------------------------------------------------------------------------------------------------------------------------

function Find-BackIn($Ui) { @(Find-Named $Ui 'Back' | Where-Object { $_.Clickable -or $_.Class -match 'Button' }) | Select-Object -First 1 }

function Test-AndroidDetail([hashtable] $Ctx, [string] $Animal, $Item) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $report = [ordered]@{ status = 'fail'; animal = $Animal; ms = 0; detail = $null }
  try {
    if (-not $Item) { $report.detail = "'$Animal' not in the list"; return $report }
    Invoke-Tap $Ctx $Item.X $Item.Y
    $shown = Wait-Ui $Ctx ($Ctx.StepMs * 2) { param($u) $b = Find-BackIn $u; if ($b -and @(Find-Named $u $Animal).Count) { $b } else { $null } }
    if (-not $shown) { $report.detail = "no detail with a Back button after a tap on '$Animal'"; Save-AndroidEvidence $Ctx "detail-$Animal"; return $report }
    Invoke-Tap $Ctx $shown.Hit.X $shown.Hit.Y
    $back = Wait-Ui $Ctx ($Ctx.StepMs * 2) { param($u) if (-not (Find-BackIn $u) -and @(Find-Named $u $Animal).Count) { $true } else { $null } }
    if (-not $back) { $report.detail = 'Back did not bring the list back'; Save-AndroidEvidence $Ctx "back-$Animal"; return $report }
    $report.status = 'ok'
    $report
  } finally { $report.ms = $watch.ElapsedMilliseconds }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Dialogs
# ------------------------------------------------------------------------------------------------------------------------------

function Test-AndroidDialog([hashtable] $Ctx, [hashtable] $Spec, [string] $Previous) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $report = [ordered]@{ status = 'fail'; answered = $null; result = $null; ms = 0; detail = $null }
  try {
    # The button of the page: the highest of that name ("Error" is a dialog and a notification), scrolled to when below the fold.
    $opener = Find-Scrolling $Ctx { param($u) @(Find-Named $u $Spec.Button | Where-Object { $_.Clickable -or $_.Class -match 'Button' }) | Sort-Object Y | Select-Object -First 1 }
    if (-not $opener) { $report.detail = 'button not found on the Dialogs page'; Save-AndroidEvidence $Ctx "dialog-$($Spec.Button)"; return $report }
    $page = Get-Ui $Ctx
    $pageButtons = @($page | Where-Object { $_.Name -and ($_.Clickable -or $_.Class -match 'Button') })
    Invoke-Tap $Ctx $opener.X $opener.Y

    # The answer: a button of that name (any case: Android shows "OK", "YES") that is not a button of the page at the same place.
    $wanted = @($Spec.Answers)
    $found = Wait-Ui $Ctx $Ctx.StepMs {
      param($u)
      foreach ($answer in $wanted) {
        $hit = @(Find-Named $u $answer | Where-Object {
          $n = $_
          ($n.Clickable -or $n.Class -match 'Button') -and -not @($pageButtons | Where-Object {
            [string]::Equals($_.Name, $n.Name, [StringComparison]::OrdinalIgnoreCase) -and [Math]::Abs($_.X - $n.X) -lt 8 -and [Math]::Abs($_.Y - $n.Y) -lt 8 }).Count
        }) | Select-Object -First 1
        if ($hit) { return @{ Answer = $answer; Node = $hit } }
      }
      $null
    }
    if (-not $found) {
      $report.detail = 'no answer of the dialog on screen'
      Save-AndroidEvidence $Ctx "dialog-$($Spec.Button)"
      # A dialog that is open but not readable: the Back key closes it (only when the page is not what is on screen: Back on the
      # page itself would leave it).
      $now = Get-Ui $Ctx
      if ($now -and -not @(Find-Named $now $Spec.Button).Count) { Invoke-Shell $Ctx 'input keyevent 4' | Out-Null; Start-Sleep -Milliseconds 400 }
      return $report
    }
    Invoke-Tap $Ctx $found.Hit.Node.X $found.Hit.Node.Y
    $report.answered = $found.Hit.Answer
    $expected = $Spec.Results[$found.Hit.Answer]
    # The Result line: on screen, else scrolled to.
    $seen = Wait-Ui $Ctx 1200 { param($u) if (@(Find-Named $u $expected).Count) { $true } else { $null } }
    if (-not $seen) { $seen = Find-Scrolling $Ctx { param($u) if (@(Find-Named $u $expected).Count) { $true } else { $null } } 3 }
    if ($seen) {
      $report.result = $expected
      if ($expected -eq $Previous) { $report.status = 'unverified'; $report.detail = 'the Result line already showed this text before the dialog' }
      else { $report.status = 'ok' }
    } else {
      $report.detail = "the Result line never showed '$expected'"
      Save-AndroidEvidence $Ctx "dialog-$($Spec.Button)"
    }
    $report
  } finally { $report.ms = $watch.ElapsedMilliseconds }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Evidence, one host
# ------------------------------------------------------------------------------------------------------------------------------

# What was on screen when a step failed: the last dump and a screenshot.
function Save-AndroidEvidence([hashtable] $Ctx, [string] $Label) {
  $Label = $Label -replace '[^\w\-]+', '_'
  $ui = Get-Ui $Ctx
  if ($Ctx.LastXml) { Set-Content -LiteralPath (Join-Path $Ctx.Dir "$Label.xml") -Value $Ctx.LastXml -Encoding utf8 }
  $png = Join-Path $Ctx.Dir "$Label.png"
  # exec-out keeps the bytes as they are (shell would turn \n into \r\n on old devices).
  $info = [Diagnostics.ProcessStartInfo]::new($script:Adb)
  foreach ($a in @('-s', $script:Serial, 'exec-out', 'screencap', '-p')) { $info.ArgumentList.Add($a) }
  $info.RedirectStandardOutput = $true; $info.UseShellExecute = $false
  try {
    $process = [Diagnostics.Process]::Start($info)
    $file = [IO.File]::Create($png)
    try { $process.StandardOutput.BaseStream.CopyTo($file) } finally { $file.Dispose() }
    $process.WaitForExit()
  } catch { }
}

function Write-AndroidStep([hashtable] $Ctx, [string] $Text, [string] $Color = 'Gray') {
  Write-Host ("[{0,-16} {1,6:0.0}s] {2}" -f $Ctx.Name, ($Ctx.Clock.ElapsedMilliseconds / 1000), $Text) -ForegroundColor $Color
}

function Get-AndroidColor([string] $Status) { switch ($Status) { 'ok' { 'Green' } 'unverified' { 'Yellow' } 'skipped' { 'DarkGray' } default { 'Red' } } }

# A system dialog over the app ("System UI isn't responding", "... keeps stopping"): frequent on an emulator without a GPU (CI),
# where the system is slow right after boot. Answered "Wait" (else "Close app" / "OK"), so that the app comes back on top.
# True when one was answered.
function Clear-SystemDialog([hashtable] $Ctx, $Ui) {
  $system = @($Ui | Where-Object { $_.Package -in 'android', 'com.android.systemui' })
  if (-not @($system | Where-Object { $_.Name -match "isn.t responding|keeps stopping|has stopped|ne r.pond pas|s'arr.te" }).Count) { return $false }
  $button = @($system | Where-Object { $_.Id -eq 'android:id/aerr_wait' -or $_.Name -in 'Wait', 'Attendre' }) + @($system | Where-Object { $_.Name -in 'Close app', 'OK', "Fermer l'application" }) |
    Select-Object -First 1
  if (-not $button) { Invoke-Shell $Ctx 'input keyevent KEYCODE_BACK' | Out-Null } else { Invoke-Tap $Ctx $button.X $button.Y }
  Write-AndroidStep $Ctx "system dialog answered: $(@($system | Where-Object Name | ForEach-Object Name | Select-Object -First 2) -join ' / ')" 'DarkYellow'
  $true
}

# One sample on the device: started from scratch (force-stop, then its launcher activity), then pages, details and dialogs.
function Invoke-AndroidSmoke([hashtable] $Target, [hashtable] $Options) {
  $dir = Join-Path $Options.OutDir $Target.Name
  if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $ctx = @{
    Name = $Target.Name; Dir = $dir; Clock = [Diagnostics.Stopwatch]::StartNew(); Trace = [Collections.Generic.List[object]]::new()
    Calls = 0; AdbMs = 0; LastXml = $null; StepMs = $Options.StepTimeoutMs; Width = $Options.Width; Height = $Options.Height
    MenuNames = @($Options.MenuOrder); AnimalPages = @($Options.FirstAnimal.Keys); CurrentTitles = @()
  }
  $report = [ordered]@{ kind = 'android'; package = $Target.Package; apk = $Target.Apk; status = 'fail'; startSeconds = $null
    pages = [ordered]@{}; details = [ordered]@{}; dialogs = [ordered]@{}; problems = @() }
  try {
    if ($Target.Problem) { $report.problems += $Target.Problem; return $report }
    Invoke-Shell $ctx "am force-stop $($Target.Package)" | Out-Null
    Invoke-Shell $ctx 'logcat -c' | Out-Null
    $launch = Invoke-Shell $ctx "monkey -p $($Target.Package) -c android.intent.category.LAUNCHER 1"
    if ($launch.Text -match 'No activities found|monkey aborted') {
      $report.problems += "the app did not start: $($launch.Text.Trim())"; return $report
    }
    $first = $Options.Pages[0]
    $ready = Wait-Ui $ctx ($Options.StartTimeoutSeconds * 1000) {
      param($u)
      if (Clear-SystemDialog $ctx $u) { return $null }
      if (@($u | Where-Object { $_.Package -eq $Target.Package }).Count -and (@(Find-Named $u $first).Count -or @(Find-Named $u $Options.FirstAnimal[$first]).Count)) { $true } else { $null }
    }
    if (-not $ready) {
      # Why: the process died (its crash, from the log), or it runs but shows nothing uiautomator can read (a UI drawn on one
      # surface, such as a Skia renderer without accessibility), or it shows texts that are not the expected ones.
      Save-AndroidEvidence $ctx 'start'
      $errors = (Invoke-Adb $ctx @('logcat', '-d', '-t', '2000', '*:E')).Text
      $errors | Set-Content -LiteralPath (Join-Path $dir 'logcat-errors.txt') -Encoding utf8
      (Invoke-Adb $ctx @('logcat', '-d', '-t', '2000')).Text | Set-Content -LiteralPath (Join-Path $dir 'logcat.txt') -Encoding utf8
      $alive = [bool]((Invoke-Shell $ctx "pidof $($Target.Package)").Text.Trim())
      $why = if (-not $alive) {
        # The crash buffer holds the fatal exception of a Java crash; the .NET runtime writes its own (monodroid, DOTNET, mono-rt).
        # The system noise of the emulator (FeatureFlags, android.xr ...) is left out.
        $crashLog = (Invoke-Adb $ctx @('logcat', '-d', '-b', 'crash')).Text
        $runtime = @("$errors" -split "`r?`n" | Where-Object { $_ -match 'monodroid|DOTNET|mono-rt|MonoDroid|No assemblies|Unhandled|libc\s*:\s*Fatal|AndroidRuntime' -and $_ -notmatch 'FeatureFlags|android\.xr' })
        @("== crash buffer", $crashLog, "== .NET runtime and AndroidRuntime", $runtime) | Set-Content -LiteralPath (Join-Path $dir 'crash.txt') -Encoding utf8
        $lines = @("$crashLog" -split "`r?`n" | Where-Object { $_ -match "Exception|Error|$([regex]::Escape($Target.Package))" }) + $runtime
        $crash = @($lines | ForEach-Object { ($_ -replace '^.*?\s[EFW]\s+[^:]+:\s*', '').Trim() } | Where-Object { $_ } | Select-Object -Unique -First 3) -join ' | '
        "the process is not running" + $(if ($crash) { ": $crash" } else { ' and the log holds no crash of it (see crash.txt, logcat.txt)' })
      } else {
        $ui = Get-Ui $ctx
        $mine = @($ui | Where-Object { $_.Package -eq $Target.Package })
        $named = @($mine | Where-Object { $_.Name } | ForEach-Object { $_.Name } | Select-Object -Unique)
        if (-not $mine.Count) {
          $others = @($ui | Where-Object { $_.Package } | Group-Object Package | ForEach-Object {
            "$($_.Name) ($((@($_.Group | Where-Object Name | ForEach-Object Name | Select-Object -Unique -First 4)) -join ', '))" })
          "the process runs but its window is not on screen; on screen: $(if ($others) { $others -join '; ' } else { 'nothing readable' }) (see start.png)"
        }
        elseif (-not $named.Count) {
          $classes = @($mine | ForEach-Object { ($_.Class -split '\.')[-1] } | Select-Object -Unique) -join ', '
          "the process runs, its window has $($mine.Count) node(s) ($classes) and no text: the UI is drawn without exposing it to accessibility (see start.png, start.xml)"
        }
        else { "the process runs and shows: $(($named | Select-Object -First 8) -join ', ')" }
      }
      $report.problems += "the app did not show '$first' within $($Options.StartTimeoutSeconds) s; $why"
      return $report
    }
    $report.startSeconds = [Math]::Round($ctx.Clock.ElapsedMilliseconds / 1000, 1)
    Write-AndroidStep $ctx "ready ($($Target.Package))" 'Cyan'

    foreach ($page in $Options.Pages) {
      $watch = [Diagnostics.Stopwatch]::StartNew()
      $animal = $Options.FirstAnimal[$page]
      $proof = if ($animal -and -not $Options.SkipDetails) { $animal } else { $null }
      $open = Open-AndroidPage $ctx $page @($Options.Titles[$page]) $proof
      if ($open.Ok) { $ctx.CurrentTitles = @($Options.Titles[$page]) + @($open.Shown) }
      $entry = [ordered]@{ status = $(if ($open.Ok) { 'ok' } else { 'fail' }); shown = $open.Shown; ms = $watch.ElapsedMilliseconds; detail = $open.Detail }
      if ($open.Ok -and $open.Shown -ne $page) { $entry.detail = "a guard sent it to $($open.Shown)" }
      if (-not $open.Ok) { Save-AndroidEvidence $ctx "page-$page" }
      $report.pages[$page] = $entry
      Write-AndroidStep $ctx ("{0,-12} {1,-10} {2,6} ms  {3}" -f $page, $entry.status, $entry.ms, $entry.detail) (Get-AndroidColor $entry.status)
      if ($proof) {
        if (-not $open.Ok) { $report.details[$page] = [ordered]@{ status = 'skipped'; animal = $animal; ms = 0; detail = 'the page did not open' } }
        else {
          $detail = Test-AndroidDetail $ctx $animal $open.Item
          $report.details[$page] = $detail
          Write-AndroidStep $ctx ("  detail {0,-21} {1,-10} {2,6} ms  {3}" -f $animal, $detail.status, $detail.ms, $detail.detail) (Get-AndroidColor $detail.status)
        }
      }
      if ($page -eq 'Dialogs' -and $open.Ok -and -not $Options.SkipDialogs) {
        $previous = $null
        foreach ($spec in $Options.Dialogs) {
          $dialog = Test-AndroidDialog $ctx $spec $previous
          if ($dialog.result) { $previous = $dialog.result }
          $report.dialogs[$spec.Button] = $dialog
          Write-AndroidStep $ctx ("dialog {0,-26} {1,-10} {2,6} ms  {3}" -f $spec.Button, $dialog.status, $dialog.ms, $(if ($dialog.detail) { $dialog.detail } else { "$($dialog.answered) -> $($dialog.result)" })) (Get-AndroidColor $dialog.status)
        }
        # Back to the top of the page for the next one.
        Invoke-Swipe $ctx 'down'; Invoke-Swipe $ctx 'down'
      }
    }
    $bad = @($report.pages.Values) + @($report.details.Values) + @($report.dialogs.Values) | Where-Object { $_.status -eq 'fail' }
    if (-not @($bad).Count -and -not $report.problems.Count) { $report.status = 'ok' }
    $report
  } catch {
    $report.problems += "the test stopped: $($_.Exception.Message)"
    $report
  } finally {
    $report.seconds = [Math]::Round($ctx.Clock.ElapsedMilliseconds / 1000, 1)
    $report.calls = $ctx.Calls
    $ctx.Trace | ForEach-Object { $_ | ConvertTo-Json -Compress } | Set-Content -LiteralPath (Join-Path $dir 'calls.jsonl') -Encoding utf8
    Invoke-Shell $ctx "am force-stop $($Target.Package)" | Out-Null
  }
}

Export-ModuleMember -Function Set-AdbTarget, Invoke-Adb, Invoke-AndroidSmoke
