# The smoke test of the native samples, driven by agent-desktop. Loaded by run.ps1.
#
# What the trees of the five hosts look like (read from real runs on Windows), and what follows from it:
#  - a menu entry or an animal of a list is a list item whose accessible name is a class name ("...RouteableMenuEntry`1[...]",
#    "XAnimals.Models.Animal", "...ItemTemplateContext") or nothing at all (WinUI 3); the name a person reads is a text inside it;
#  - list items that share a name are twins, and agent-desktop refuses any semantic action on a twin on Windows; the semantic
#    click it accepts on a WPF or Uno menu entry does not select it either;
#  - so an entry is found by its text (find returns the text with its bounds and the path of its ancestors) and clicked at the
#    centre of that text, with a physical click: the window must be in front, which is why the hosts are tested one at a time;
#  - every host shows the title of the current page in a header outside the menu: that is how a page is known to be open;
#  - the menu can be hidden: Avalonia's drawer closes after a navigation, the pane of Uno, WinUI 3 and MAUI is an overlay that
#    closes too. When the entry is not in the tree, the button that opens the menu is pressed first.
#
# What makes it fast: the native binary of agent-desktop is called directly; no full snapshot per step (one only when a step
# fails); no fixed sleep, every wait polls for its signal; the apps start all at once, before the first test.

Set-StrictMode -Version 3.0

$script:AdPath = $null
$script:HeadedMutexName = 'RouteablesAgentDesktopHeaded'

# The roles of a list item, a menu item or a tab, whatever the toolkit.
# row: a list item on macOS when the toolkit gives it the AXRow role (Uno: listitem, option, treeitem all become AXRow).
$script:ItemRoles = 'option', 'listitem', 'menuitem', 'treeitem', 'tabitem', 'tab', 'row'

# ------------------------------------------------------------------------------------------------------------------------------
# agent-desktop
# ------------------------------------------------------------------------------------------------------------------------------

# The native binary: bin/agent-desktop-<os>-<arch>[.exe] of the global npm package. The command found on the PATH comes last:
# on Windows it is a .cmd, and when npm did not run the install script it is the Node launcher.
function Resolve-AgentDesktop([string] $Explicit) {
  if ($Explicit) { return (Resolve-Path -LiteralPath $Explicit).Path }
  $os = if ($IsWindows) { 'win32' } elseif ($IsMacOS) { 'darwin' } else { 'linux' }
  $arch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
  $name = "agent-desktop-$os-$arch$(if ($IsWindows) { '.exe' })"
  $npmRoot = $null
  try { $npmRoot = (& npm root -g 2>$null | Select-Object -First 1) } catch { }
  if ($npmRoot) {
    $candidate = Join-Path $npmRoot "agent-desktop/bin/$name"
    if (Test-Path -LiteralPath $candidate) { return $candidate }
  }
  $command = Get-Command agent-desktop -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($command) { return $command.Source }
  throw 'agent-desktop was not found. Install it (npm install -g agent-desktop) or give its path with -AgentDesktop.'
}

function Set-AgentDesktopPath([string] $Path) { $script:AdPath = $Path }

# One call. $A is the whole argument list as one array (an advanced function would read "-i" or "--app" as its own parameters).
# Every call is recorded in the trace of the context: command, duration, result code.
function Invoke-Ad([hashtable] $Ctx, [string[]] $A, [switch] $Headed) {
  $arguments = [Collections.Generic.List[string]]::new()
  $arguments.AddRange($A)
  if ($Ctx.Session) { $arguments.Add('--session'); $arguments.Add($Ctx.Session) }
  if ($Headed) { $arguments.Add('--headed') }
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $text = (& $script:AdPath @arguments 2>$null) -join "`n"
  $ms = $watch.ElapsedMilliseconds
  $result = $null
  if ($text) { try { $result = $text | ConvertFrom-Json -Depth 200 } catch { } }
  if (-not $result) {
    $result = [pscustomobject]@{ ok = $false; data = $null; error = [pscustomobject]@{ code = 'NO_JSON'; message = $text } }
  }
  $code = if ($result.ok) { $null } else { Get-Prop $result.error 'code' }
  $Ctx.Calls++
  $Ctx.AdMs += $ms
  $Ctx.Trace.Add([pscustomobject]@{ at = $Ctx.Clock.ElapsedMilliseconds; ms = $ms; cmd = ($arguments -join ' '); ok = [bool]$result.ok; code = $code })
  $result | Add-Member -NotePropertyName raw -NotePropertyValue $text -Force
  $result
}

# The pointer and the keyboard belong to one host at a time: a named mutex (re-entrant: a step that holds it can click inside).
# Two hosts tested side by side take it in turn; a step whose clicks must not be split by a click in the other window (open the
# menu, then click its entry; open a dialog, then answer it) holds it from its first click to its last.
function Invoke-WithUi([hashtable] $Ctx, [scriptblock] $Body) {
  $mutex = [Threading.Mutex]::new($false, $script:HeadedMutexName)
  $owned = $false
  try {
    try { $owned = $mutex.WaitOne([TimeSpan]::FromSeconds(120)) } catch [Threading.AbandonedMutexException] { $owned = $true }
    & $Body
  } finally {
    if ($owned) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
  }
}

# The same lock, held across a block that cannot be a script block (it assigns many variables of its function).
function Enter-Ui {
  $mutex = [Threading.Mutex]::new($false, $script:HeadedMutexName)
  $owned = $false
  try { $owned = $mutex.WaitOne([TimeSpan]::FromSeconds(120)) } catch [Threading.AbandonedMutexException] { $owned = $true }
  @{ Mutex = $mutex; Owned = $owned }
}
function Exit-Ui($Lock) {
  if (-not $Lock) { return }
  if ($Lock.Owned) { $Lock.Mutex.ReleaseMutex() }
  $Lock.Mutex.Dispose()
}

# The host whose window took the last physical input, process-wide (two hosts tested side by side run in two runspaces).
function Get-UiOwner { [AppDomain]::CurrentDomain.GetData('RouteablesUiOwner') }
function Set-UiOwner([string] $Name) { [AppDomain]::CurrentDomain.SetData('RouteablesUiOwner', $Name) }
# Another window took input since this host's last one: its window was deactivated (a drawer or a pane may have closed).
function Test-Deactivated([hashtable] $Ctx) { $Ctx.Parallel -and (Get-UiOwner) -ne $Ctx.Name }

# Physical input: the window is brought to the front first (once, and again after a step that failed; before keys, when another
# window took input since), under the lock of the pointer.
function Invoke-AdHeaded([hashtable] $Ctx, [string[]] $A) {
  if (-not $Ctx.Headed) {
    return [pscustomobject]@{ ok = $false; data = $null; raw = ''; error = [pscustomobject]@{ code = 'HEADED_DISABLED'; message = 'physical input is off (-NoHeaded)' } }
  }
  Invoke-WithUi $Ctx {
    $keys = $A[0] -eq 'press'
    if ($Ctx.WindowId -and ($Ctx.NeedFocus -or ($keys -and (Test-Deactivated $Ctx)))) {
      Invoke-Ad $Ctx @('focus-window', '--window-id', $Ctx.WindowId) | Out-Null
      $Ctx.NeedFocus = $false
    }
    $result = Invoke-Ad $Ctx $A -Headed
    Set-UiOwner $Ctx.Name
    $result
  }
}

# A property that may be missing (StrictMode refuses $obj.missing).
function Get-Prop($Object, [string] $Name) {
  if ($null -eq $Object) { return $null }
  if ($Object -is [System.Collections.IDictionary]) { return $Object[$Name] }
  $property = $Object.PSObject.Properties[$Name]
  if ($property) { $property.Value } else { $null }
}

function Get-ErrorCode($Result) { if ($Result.ok) { $null } else { Get-Prop $Result.error 'code' } }

# Polls a condition until it holds or the time is over.
function Wait-Until([scriptblock] $Condition, [int] $TimeoutMs, [int] $IntervalMs = 150) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  do {
    if (& $Condition) { return $true }
    Start-Sleep -Milliseconds $IntervalMs
  } while ($watch.ElapsedMilliseconds -lt $TimeoutMs)
  [bool](& $Condition)
}

# ------------------------------------------------------------------------------------------------------------------------------
# Finding things
# ------------------------------------------------------------------------------------------------------------------------------

# Every match of a find, with or without a ref: a text has no ref but has its bounds and the path of its ancestors.
# A window that went away (some hosts replace theirs) is looked up again once.
function Find-All([hashtable] $Ctx, [string] $Name, [string] $Role, [switch] $Exact, [string] $NativeId, [switch] $AnyWindow) {
  for ($attempt = 0; $attempt -lt 2; $attempt++) {
    $a = @('find', '--app', $Ctx.App, '--limit', '0', '--timeout-ms', '10000')
    if ($Name) { $a += @('--name', $Name) }
    if ($Role) { $a += @('--role', $Role) }
    if ($NativeId) { $a += @('--native-id', $NativeId) }
    if ($Exact) { $a += '--exact' }
    if ($Ctx.WindowId -and -not $AnyWindow) { $a += @('--window-id', $Ctx.WindowId) }
    $result = Invoke-Ad $Ctx $a
    if ($result.ok) { return @(@(Get-Prop $result.data 'matches') | Where-Object { $_ }) }
    if ((Get-ErrorCode $result) -eq 'WINDOW_NOT_FOUND' -and $Ctx.Pid) {
      $Ctx.WindowId = Resolve-MainWindow $Ctx $Ctx.Pid
      $Ctx.NeedFocus = $true
      continue
    }
    return @()
  }
  @()
}

# The path of a match is a list of "role" or "role:name" strings, from the window down.
function Get-PathRoles($Match) { @(@(Get-Prop $Match 'path') | ForEach-Object { ([string]$_ -split ':', 2)[0] }) }

# Inside a list item (a menu entry, an animal of a list), or a list item itself.
function Test-InItem($Match) {
  if ((Get-Prop $Match 'role') -in $script:ItemRoles) { return $true }
  [bool](@(Get-PathRoles $Match | Where-Object { $_ -in $script:ItemRoles }).Count)
}

function Get-Center($Match) {
  $b = Get-Prop $Match 'bounds'
  if (-not $b -or -not (Get-Prop $b 'width')) { return $null }
  [pscustomobject]@{ X = [int]((Get-Prop $b 'x') + (Get-Prop $b 'width') / 2); Y = [int]((Get-Prop $b 'y') + (Get-Prop $b 'height') / 2) }
}

# The bounds of an element that has a ref (a match with a ref carries no bounds).
function Get-RefCenter([hashtable] $Ctx, [string] $Ref) {
  $result = Invoke-Ad $Ctx @('get', $Ref, '--property', 'bounds')
  if (-not $result.ok) { return $null }
  $value = Get-Prop $result.data 'value'
  if (-not $value) { $value = Get-Prop $result.data 'bounds' }
  if (-not $value) { return $null }
  Get-Center ([pscustomobject]@{ bounds = $value })
}

# Shown on screen: an element of a closed drawer stays in the tree, offscreen, with no size or outside the window (Avalonia keeps
# the selected entry). An element with a ref carries no bounds in a find: it counts as shown, its bounds are read when clicked.
function Test-Visible([hashtable] $Ctx, $Match) {
  if (@(Get-Prop $Match 'states') -contains 'offscreen') { return $false }
  $center = Get-Center $Match
  if (-not $center) { return [bool](Get-Prop $Match 'ref_id') }
  $w = $Ctx.WindowRect
  if (-not $w) { return $true }
  $center.X -ge $w.X -and $center.X -le ($w.X + $w.Width) -and $center.Y -ge $w.Y -and $center.Y -le ($w.Y + $w.Height)
}

# The text of an entry in a list (the menu, a list of animals), when it is shown. Several lists can hold it (the Regions page has
# a menu of its own): the one most to the left wins, that is the main menu when it is shown.
function Find-ItemText([hashtable] $Ctx, [string] $Text) {
  $found = @(Find-All $Ctx -Name $Text -Exact | Where-Object { (Test-InItem $_) -and (Test-Visible $Ctx $_) })
  if (-not $found.Count) { return $null }
  # A text (with bounds) before an item with a ref (without): a click at the text is the one that works on every host.
  $found | Sort-Object { if (Get-Center $_) { 0 } else { 1 } }, { $c = Get-Center $_; if ($c) { $c.X } else { 0 } }, { $c = Get-Center $_; if ($c) { $c.Y } else { 0 } } | Select-Object -First 1
}

# The title of the current page, in the header: a text of that name that is not inside a list item.
function Test-Header([hashtable] $Ctx, [string[]] $Titles) {
  foreach ($title in $Titles) {
    $hit = Find-All $Ctx -Name $title -Exact | Where-Object { -not (Test-InItem $_) -and (Get-Prop $_ 'role') -notin 'button', 'switch' } | Select-Object -First 1
    if ($hit) {
      if (-not $Ctx.HeaderCenter) { $Ctx.HeaderCenter = Get-Center $hit }
      return $title
    }
  }
  $null
}

# The menu of Uno and WinUI 3 is a SplitView pane over the content that stays open after a navigation: its light-dismiss layer then
# takes the next click on the content. On such a host (known by that layer, or by a first click on the content that was lost), the
# pane is closed after each navigation with its own button, at its remembered place; the next navigation opens it again there.
function Close-Pane([hashtable] $Ctx, $Layout) {
  if (-not $Ctx.Headed) { return }
  Invoke-WithUi $Ctx { Close-PaneLocked $Ctx $Layout }
}

function Close-PaneLocked([hashtable] $Ctx, $Layout) {
  # Side by side, a click in the other window since the snapshot may have closed the pane: read again, under the lock.
  if ($Layout -and (Test-Deactivated $Ctx)) { $fresh = @(Get-Layout $Ctx); if ($fresh.Count) { $Layout = $fresh } }
  # A layer seen in the snapshot taken just after the click may be that of a drawer still closing by itself (Avalonia in a window
  # that is not maximized, as on macOS: 350 ms). Judged on it, the drawer would be taken for a pane that stays open, and its
  # button pressed "to close it" would open it again. Read once more when it has settled.
  elseif ($Layout -and (Get-DismissPoint $Layout)) {
    Wait-DrawerSettled $Ctx
    $fresh = @(Get-Layout $Ctx); if ($fresh.Count) { $Layout = $fresh }
  }
  if ($Layout -and $Ctx.Overlay -eq $true -and -not (Get-DismissPoint $Layout)) {
    # The pane is not open (closed by itself): its button would open it.
    $Ctx.OpenHow = 'mouse'
    return
  }
  if ($Layout -and $Ctx.Overlay -ne $true) {
    # The layout read to check the page tells whether a layer is over it: no find. A drawer that stays open after a navigation
    # (Avalonia) is closed by a click on its layer; a host whose pane is open on its first page is closed with its button below.
    $point = Get-DismissPoint $Layout
    if ($null -eq $Ctx.Overlay) { $Ctx.Overlay = [bool]$point; if ($point) { $point = $null } }
    if ($point) {
      Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($point.X),$($point.Y)") | Out-Null
      $Ctx.LayerClosed++
      Start-Sleep -Milliseconds 300
      return
    }
    if (-not $Ctx.Overlay) { return }
  }
  if ($null -eq $Ctx.Overlay) {
    # Uno names it LightDismissLayer, WinUI 3 LightDismiss (and calls it "Close").
    $layer = Find-All $Ctx -NativeId 'LightDismissLayer' | Where-Object { Get-Prop $_ 'ref_id' } | Select-Object -First 1
    if (-not $layer) { $layer = Find-All $Ctx -NativeId 'LightDismiss' | Where-Object { Get-Prop $_ 'ref_id' } | Select-Object -First 1 }
    if (-not $layer) { $layer = Find-All $Ctx -Name 'LightDismissLayer' -Exact | Where-Object { Get-Prop $_ 'ref_id' } | Select-Object -First 1 }
    $Ctx.Overlay = [bool]($layer -and (Get-RefCenter $Ctx (Get-Prop $layer 'ref_id')))
  }
  if (-not $Ctx.Overlay) { return }
  if (-not $Ctx.ToggleCenter) {
    $toggle = Find-MenuToggle $Ctx
    if (-not $toggle) { return }
    $Ctx.ToggleCenter = Get-RefCenter $Ctx (Get-Prop $toggle 'ref_id')
    if (-not $Ctx.ToggleCenter) { return }
  }
  # A drawer drawn over the page, its button under it (Avalonia when the window is not maximized, as on macOS): the place of the
  # button is then an entry of the open drawer, and a click there would open that page. Its layer closes it instead.
  $t = $Ctx.ToggleCenter
  $covered = $Layout -and @($Layout | Where-Object { $_.Shown -and $_.InItem -and $_.Bounds -and
      $t.X -ge (Get-Prop $_.Bounds 'x') -and $t.X -le ((Get-Prop $_.Bounds 'x') + (Get-Prop $_.Bounds 'width')) -and
      $t.Y -ge (Get-Prop $_.Bounds 'y') -and $t.Y -le ((Get-Prop $_.Bounds 'y') + (Get-Prop $_.Bounds 'height')) }).Count
  $point = if ($covered) { Get-DismissPoint $Layout } else { $null }
  if ($point) {
    Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($point.X),$($point.Y)") | Out-Null
    $Ctx.LayerClosed++
  } else {
    Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($t.X),$($t.Y)") | Out-Null
  }
  # From now on the menu is closed between two pages: it is opened at the same place.
  $Ctx.OpenHow = 'mouse'
  Start-Sleep -Milliseconds 300
}

# One snapshot of the window with bounds, flattened: name, role, centre, shown, inside a list item. Used to remember the places of
# the menu entries and of the buttons of the Dialogs page in one call instead of one find each.
function Get-Layout([hashtable] $Ctx, [string] $Surface) {
  $snapshot = Get-Snapshot $Ctx $Surface
  $nodes = [Collections.Generic.List[object]]::new()
  if (-not $snapshot.ok) { return $nodes }
  $stack = [Collections.Generic.Stack[object]]::new()
  $stack.Push([pscustomobject]@{ Node = (Get-Prop $snapshot.data 'tree'); InItem = $false; Hidden = $false })
  while ($stack.Count) {
    $top = $stack.Pop()
    $node = $top.Node
    if (-not $node) { continue }
    $role = Get-Prop $node 'role'
    $states = @(Get-Prop $node 'states')
    $hidden = $top.Hidden -or $states -contains 'offscreen'
    $center = Get-Center ([pscustomobject]@{ bounds = (Get-Prop $node 'bounds') })
    $name = Get-Prop $node 'name'
    $nativeId = Get-Prop (Get-Prop $node 'native_id') 'value'
    if (($name -or $nativeId) -and $center) {
      $shown = -not $hidden -and (Test-Visible $Ctx ([pscustomobject]@{ bounds = (Get-Prop $node 'bounds'); states = $states }))
      $nodes.Add([pscustomobject]@{ Name = [string]$name; Id = [string]$nativeId; Role = $role; Center = $center; Bounds = (Get-Prop $node 'bounds'); Shown = $shown; InItem = $top.InItem })
    }
    $inItem = $top.InItem -or $role -in $script:ItemRoles
    # A popup host of WinUI (MAUI's action sheet on Windows: a window "Popup", 0 x 0, reported offscreen) holds shown content: its
    # children are judged on their own bounds, not hidden with it.
    $bounds = Get-Prop $node 'bounds'
    $popupHost = $role -eq 'window' -and $states -contains 'offscreen' -and $bounds -and -not (Get-Prop $bounds 'width') -and -not (Get-Prop $bounds 'height')
    $childHidden = if ($popupHost) { $top.Hidden } else { $hidden }
    foreach ($child in @(Get-Prop $node 'children')) { if ($child) { $stack.Push([pscustomobject]@{ Node = $child; InItem = $inItem; Hidden = $childHidden }) } }
  }
  $nodes
}

# A node of a layout as a match that Invoke-ClickMatch clicks at its centre.
function ConvertTo-Match($Node) {
  $c = $Node.Center
  [pscustomobject]@{ name = $Node.Name; role = $Node.Role; ref_id = $null; states = @(); path = @(); bounds = [pscustomobject]@{ x = $c.X - 1; y = $c.Y - 1; width = 2; height = 2 } }
}

# Find-ItemText on a layout already read (no tree walk): the shown text of a list entry, the one most to the left.
function Find-ItemTextIn($Layout, [string] $Text) {
  $node = @($Layout | Where-Object { $_.Shown -and $_.InItem -and $_.Name -eq $Text }) | Sort-Object { $_.Center.X }, { $_.Center.Y } | Select-Object -First 1
  if ($node) { ConvertTo-Match $node } else { $null }
}

# Test-Header on a layout already read.
function Test-HeaderIn([hashtable] $Ctx, $Layout, [string[]] $Titles) {
  foreach ($title in $Titles) {
    $hit = @($Layout | Where-Object { $_.Name -eq $title -and -not $_.InItem -and $_.Role -notin 'button', 'switch' }) | Select-Object -First 1
    if ($hit) { if (-not $Ctx.HeaderCenter) { $Ctx.HeaderCenter = $hit.Center }; return $title }
  }
  $null
}

# The light-dismiss layer of a drawer or a pane left open over the page (Avalonia's DrawerPage, Uno, WinUI 3): where a click
# closes it without touching the page, at three quarters of its width (the pane is on the left). $null when none is shown.
function Get-DismissPoint($Layout) {
  $layer = @($Layout | Where-Object { ($_.Id -in 'LightDismissLayer', 'LightDismiss' -or $_.Name -eq 'LightDismissLayer') -and $_.Shown -and $_.Bounds }) | Select-Object -First 1
  if (-not $layer) { return $null }
  $b = $layer.Bounds
  [pscustomobject]@{ X = [int]((Get-Prop $b 'x') + 0.75 * (Get-Prop $b 'width')); Y = [int]((Get-Prop $b 'y') + (Get-Prop $b 'height') / 2) }
}

# The places of the menu entries, read once the menu is shown (the one most to the left when a page has a menu of its own).
function Update-MenuMap([hashtable] $Ctx) {
  foreach ($node in (Get-Layout $Ctx)) {
    # The button that shows the menu, read in the same snapshot: no find for it later.
    if (-not $Ctx.ToggleCenter -and $node.Shown -and $node.Role -in 'button', 'switch', 'togglebutton' -and
        ($node.Id -in 'PaneToggleButton', 'PART_PaneButton', 'TogglePaneButton' -or $node.Name -like '*navigation*')) {
      $Ctx.ToggleCenter = $node.Center
    }
    if (-not $node.Shown -or -not $node.InItem -or $node.Name -notin $Ctx.PageNames) { continue }
    $known = $Ctx.MenuMap[$node.Name]
    if (-not $known -or $node.Center.X -lt $known.X) { $Ctx.MenuMap[$node.Name] = $node.Center }
  }
}

# A click on a button: physical at its place when allowed (get bounds + click: 0.2 s, where a semantic click takes 1.2 s on Windows,
# because it walks the whole window to prove the button is unique), semantic otherwise or when that fails.
# A button that has a twin of the same name (WPF's Dialogs page has an "Error" dialog and an "Error" notification) refuses both: then
# the text of that name highest in the window is clicked (the dialogs come before the notifications on every host).
# $Avoid: places not to click (the buttons of the page, when the button is the answer of a dialog shown over the page: the dialog's
# "OK" and the page's "Ok" are twins).
# $Avoid: the buttons of the page, as Name and Center. A candidate is left out when it is at the place of a page button of the same
# name: that is its twin, not the answer (a dialog drawn over the page can cover page buttons of other names: those are fine).
function Test-Avoided($Center, [string] $Name, $Avoid) {
  if (-not $Center) { return $false }
  foreach ($button in @($Avoid)) {
    if (-not $button -or (Normalize-Label $button.Name) -ne (Normalize-Label $Name)) { continue }
    if ([Math]::Abs($button.Center.X - $Center.X) -le 12 -and [Math]::Abs($button.Center.Y - $Center.Y) -le 12) { return $true }
  }
  $false
}

# The buttons of the page, remembered on the Dialogs page, as Name and Center.
function Get-PageButtons([hashtable] $Ctx) {
  if (-not $Ctx.ButtonMap) { return @() }
  @($Ctx.ButtonMap.GetEnumerator() | ForEach-Object { [pscustomobject]@{ Name = $_.Key; Center = $_.Value } })
}

function Invoke-ClickButton([hashtable] $Ctx, $Match, $Avoid) {
  if ($Ctx.Headed) {
    $press = Invoke-ClickMatch $Ctx $Match 'mouse'
    if ($press.ok) { return $press }
  }
  $press = Invoke-ClickMatch $Ctx $Match 'click'
  if ($press.ok -or -not $Ctx.Headed) { return $press }
  $name = Get-Prop $Match 'name'
  if (-not $name) { return $press }
  $text = @(Find-All $Ctx -Name $name -Exact | Where-Object { -not (Get-Prop $_ 'ref_id') -and (Test-Visible $Ctx $_) -and -not (Test-Avoided (Get-Center $_) (Get-Prop $_ 'name') $Avoid) }) |
    Sort-Object { (Get-Center $_).Y }, { (Get-Center $_).X } | Select-Object -First 1
  if ($text) { return (Invoke-ClickMatch $Ctx $text 'mouse') }
  # No text of its own (the toolkit names the button only): its bounds are read from a snapshot of the window.
  $node = Find-InSnapshot $Ctx $name (Get-Prop $Match 'role') $Avoid
  if (-not $node) { return $press }
  Invoke-ClickMatch $Ctx $node 'mouse'
}

# The element of that name and role highest in the window, from one snapshot with bounds (a few hundred ms to a few seconds).
function Find-InSnapshot([hashtable] $Ctx, [string] $Name, [string] $Role, $Avoid) {
  $snapshot = Get-Snapshot $Ctx
  if (-not $snapshot.ok) { return $null }
  $found = [Collections.Generic.List[object]]::new()
  $stack = [Collections.Generic.Stack[object]]::new()
  $stack.Push((Get-Prop $snapshot.data 'tree'))
  while ($stack.Count) {
    $node = $stack.Pop()
    if (-not $node) { continue }
    if ((Normalize-Label (Get-Prop $node 'name')) -eq (Normalize-Label $Name) -and (-not $Role -or (Get-Prop $node 'role') -eq $Role) -and (Test-Visible $Ctx ([pscustomobject]@{ bounds = (Get-Prop $node 'bounds'); states = (Get-Prop $node 'states') }))) {
      $found.Add([pscustomobject]@{ name = (Get-Prop $node 'name'); role = (Get-Prop $node 'role'); bounds = (Get-Prop $node 'bounds'); states = (Get-Prop $node 'states') })
    }
    foreach ($child in @(Get-Prop $node 'children')) { if ($child) { $stack.Push($child) } }
  }
  $found | Where-Object { -not (Test-Avoided (Get-Center $_) $_.name $Avoid) } | Sort-Object { (Get-Center $_).Y }, { (Get-Center $_).X } | Select-Object -First 1
}

# The buttons of the title bar of a window, in English and in French: never an answer to a dialog.
$script:CaptionButtons = 'close', 'minimize', 'maximize', 'restore', 'minimise', 'maximise', 'fermer', 'réduire', 'agrandir', 'restaurer'

# A click on a match: semantic when it has a ref and that is allowed to work, else physical at the centre of its text.
function Invoke-ClickMatch([hashtable] $Ctx, $Match, [string] $How) {
  $ref = Get-Prop $Match 'ref_id'
  switch ($How) {
    'click' {
      if (-not $ref) { return [pscustomobject]@{ ok = $false; error = [pscustomobject]@{ code = 'NO_REF' } } }
      return (Invoke-Ad $Ctx @('click', $ref))
    }
    'mouse' {
      $center = Get-Center $Match
      if (-not $center -and $ref) { $center = Get-RefCenter $Ctx $ref }
      if (-not $center) { return [pscustomobject]@{ ok = $false; error = [pscustomobject]@{ code = 'NO_BOUNDS' } } }
      return (Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($center.X),$($center.Y)"))
    }
  }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Start and stop
# ------------------------------------------------------------------------------------------------------------------------------

# A process left from an earlier run (same executable) makes --app ambiguous: it is stopped first.
function Stop-Leftovers([string] $Exe) {
  $name = [IO.Path]::GetFileNameWithoutExtension($Exe)
  Get-Process -Name $name -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -eq $Exe } catch { $false }
  } | Stop-Process -Force -ErrorAction SilentlyContinue
}

# The name agent-desktop gives the process (XAnimals.Wpf.exe on Windows, the process name on Linux).
function Resolve-AppName([hashtable] $Ctx, [int] $ProcessId) {
  $result = Invoke-Ad $Ctx @('list-apps')
  if (-not $result.ok) {
    # Linux: agent-desktop refuses the whole list when it cannot prove the window of a frame of this process, which happens for every
    # window when it cannot read the X display at all (agent-desktop 1.2.0 does not connect to an X server listening only on the
    # abstract socket; lib/wsl-run.sh gives it a TCP display for that).
    $detailsKind = Get-Prop (Get-Prop $result.error 'details') 'kind'
    if ($detailsKind -eq 'native_window_unproven' -and "$(Get-Prop $result.error 'message')" -match "pid $ProcessId\b") { $Ctx.Unproven++ }
    return $null
  }
  $app = @(Get-Prop $result.data 'apps') | Where-Object { (Get-Prop $_ 'pid') -eq $ProcessId } | Select-Object -First 1
  if ($app) { Get-Prop $app 'name' } else { $null }
}

function Resolve-MainWindow([hashtable] $Ctx, [int] $ProcessId) {
  $result = Invoke-Ad $Ctx @('list-windows', '--app', $Ctx.App)
  if (-not $result.ok) { return $null }
  $windows = @(@($result.data) | Where-Object { $_ -and ((Get-Prop $_ 'pid') -eq $ProcessId -or -not (Get-Prop $_ 'pid')) })
  # The biggest one: a dialog or a pop-up of the app is smaller than its main window.
  $window = $windows | Sort-Object { $b = Get-Prop $_ 'bounds'; if ($b) { -((Get-Prop $b 'width') * (Get-Prop $b 'height')) } else { 0 } } | Select-Object -First 1
  if (-not $window) { return $null }
  $b = Get-Prop $window 'bounds'
  $Ctx.WindowRect = if ($b) { [pscustomobject]@{ X = Get-Prop $b 'x'; Y = Get-Prop $b 'y'; Width = Get-Prop $b 'width'; Height = Get-Prop $b 'height' } } else { $null }
  Get-Prop $window 'id'
}

function New-Context([string] $Name, [string] $App, [hashtable] $Options) {
  $dir = Join-Path $Options.OutDir $Name
  if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  @{
    Name = $Name; App = $App; Dir = $dir; Session = "routeables-smoke-$($Name.ToLowerInvariant())"
    WindowId = $null; WindowRect = $null; Pid = 0; Process = $null; MenuHow = $null; ToggleHow = $null; OpenHow = $null; NeedFocus = $true
    ViewModels = $Options.ViewModels; PageNames = @($Options.AllPages)
    # Remembered positions (screen coordinates, the window is maximized and does not move): they spare a find of one to two
    # seconds on Windows each time. A click at a remembered place that does not give the expected result falls back to a find.
    Overlay = $null; HeaderCenter = $null; MenuMap = @{}; MenuOrder = @($Options.MenuOrder); RankMenu = $null; RankMap = $null; ToggleCenter = $null; BackCenter = $null; ButtonMap = $null; LayerClosed = 0; HiddenId = $null; DialogKeys = $null; MenuMapRead = $false; Slot = $null; Parallel = $false; LastNavAt = 0; Unproven = 0
    PagesOpened = 0; MenuBroken = $null; MenuMisses = 0; MenuEvidence = $false; MenuTrace = $null; ScrolledFor = $null; DialogPlace = $null; DialogHidden = $false; OpenerEvidence = $false; KeysWorked = $false
    Headed = -not $Options.NoHeaded; StepMs = $Options.StepTimeoutMs; Screenshots = [bool]$Options.Screenshots
    Calls = 0; AdMs = 0; StartMs = 0; Clock = [Diagnostics.Stopwatch]::StartNew(); Trace = [Collections.Generic.List[object]]::new()
  }
}

# Starts the application of a target and returns its context; the window is waited for later, by the test.
function Start-Target([hashtable] $Target, [hashtable] $Options) {
  if (-not $script:AdPath) { Set-AgentDesktopPath $Options.AgentDesktop }
  $ctx = New-Context $Target.Name $Target.App $Options
  if ($Target.Problem -or -not $Target.Exe) { return $ctx }
  Stop-Leftovers $Target.Exe
  $ctx.Process = Start-Process -FilePath $Target.Exe -WorkingDirectory (Split-Path -Parent $Target.Exe) -PassThru `
    -RedirectStandardOutput (Join-Path $ctx.Dir 'stdout.txt') -RedirectStandardError (Join-Path $ctx.Dir 'stderr.txt')
  $ctx.Pid = $ctx.Process.Id
  $ctx
}

# Waits until the app is on the accessibility bus with a window and $Ready answers $true. Returns $null or why it failed.
function Wait-AppReady([hashtable] $Ctx, [int] $TimeoutSeconds, [scriptblock] $Ready) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $resolved = $false
  while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    if ($Ctx.Process.HasExited) {
      $tail = (Get-Content -LiteralPath (Join-Path $Ctx.Dir 'stderr.txt') -Tail 15 -ErrorAction SilentlyContinue) -join "`n"
      return "the process exited (code $($Ctx.Process.ExitCode)) before its window was ready. $tail".Trim()
    }
    if (-not $resolved) {
      $name = Resolve-AppName $Ctx $Ctx.Pid
      if ($name) { $Ctx.App = $name; $resolved = $true }
      # Refused the same way for 10 s: it will not change (it is the display connection, not the timing).
      if ($Ctx.Unproven -ge 20) {
        return "agent-desktop cannot drive this application here: it registers on the accessibility bus, but agent-desktop cannot tie its frame to an X window (native_window_unproven). Usually agent-desktop cannot read the X display: version 1.2.0 does not connect to an X server listening only on the abstract socket (@/tmp/.X11-unix/Xn). Use a display on TCP (DISPLAY=127.0.0.1:N, what lib/wsl-run.sh does) or a socket file. DISPLAY=$($env:DISPLAY)."
      }
    }
    if (-not $Ctx.WindowId) { $Ctx.WindowId = Resolve-MainWindow $Ctx $Ctx.Pid }
    if ($Ctx.WindowId -and (& $Ready)) { $Ctx.StartMs = [int]$Ctx.Clock.ElapsedMilliseconds; return $null }
    Start-Sleep -Milliseconds 300
  }
  if (-not $resolved) { return "no application with process id $($Ctx.Pid) on the accessibility bus after $TimeoutSeconds s (name tried: $($Ctx.App))" }
  if (-not $Ctx.WindowId) { return "no window for '$($Ctx.App)' after $TimeoutSeconds s" }
  "the window did not show what was expected after $TimeoutSeconds s"
}

# Every window is minimized as soon as it shows, and restored on its turn: a window that comes up late (or that Windows refuses to
# put behind, focus-window can fail) would otherwise lie over the one under test and take its physical clicks.
function Hide-StartedWindows([hashtable[]] $Contexts, [int] $TimeoutSeconds) {
  $pending = [Collections.Generic.List[hashtable]]::new()
  foreach ($ctx in $Contexts) { if ($ctx -and $ctx.Headed -and $ctx.Process) { $pending.Add($ctx) } }
  $watch = [Diagnostics.Stopwatch]::StartNew()
  while ($pending.Count -and $watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    foreach ($ctx in @($pending)) {
      if ($ctx.Process.HasExited) { $pending.Remove($ctx) | Out-Null; continue }
      $name = Resolve-AppName $ctx $ctx.Pid
      if (-not $name) { continue }
      $ctx.App = $name
      $id = Resolve-MainWindow $ctx $ctx.Pid
      $ctx.WindowRect = $null
      if (-not $id) { continue }
      Invoke-Ad $ctx @('minimize', '--window-id', $id) | Out-Null
      $ctx.HiddenId = $id
      $pending.Remove($ctx) | Out-Null
    }
    if ($pending.Count) { Start-Sleep -Milliseconds 300 }
  }
}

# The work area of the screen: the bounds of a maximized window, less the borders Windows draws outside the screen.
function Get-WorkArea([hashtable] $Ctx) {
  if (-not $Ctx.HiddenId) { return $null }
  Invoke-Ad $Ctx @('maximize', '--window-id', $Ctx.HiddenId) | Out-Null
  Start-Sleep -Milliseconds 300
  $list = Invoke-Ad $Ctx @('list-windows', '--app', $Ctx.App)
  Invoke-Ad $Ctx @('minimize', '--window-id', $Ctx.HiddenId) | Out-Null
  $window = @(@($list.data) | Where-Object { $_ -and (Get-Prop $_ 'id') -eq $Ctx.HiddenId }) | Select-Object -First 1
  $b = if ($window) { Get-Prop $window 'bounds' } else { $null }
  if (-not $b) { return $null }
  $x = [int](Get-Prop $b 'x'); $y = [int](Get-Prop $b 'y'); $w = [int](Get-Prop $b 'width'); $h = [int](Get-Prop $b 'height')
  if ($x -lt 0) { $w += 2 * $x; $x = 0 }
  if ($y -lt 0) { $h += 2 * $y; $y = 0 }
  if ($w -lt 400 -or $h -lt 400) { return $null }
  [pscustomobject]@{ X = $x; Y = $y; Width = $w; Height = $h }
}

function Show-HiddenWindow([hashtable] $Ctx) {
  if (-not $Ctx.HiddenId) { return }
  Invoke-Ad $Ctx @('restore', '--window-id', $Ctx.HiddenId) | Out-Null
  $Ctx.HiddenId = $null
  $Ctx.NeedFocus = $true
}

function Hide-Window([hashtable] $Ctx) {
  if ($Ctx.Headed -and $Ctx.WindowId -and $Ctx.Process -and -not $Ctx.Process.HasExited) {
    Invoke-Ad $Ctx @('minimize', '--window-id', $Ctx.WindowId) | Out-Null
  }
}

function Stop-App($Process) {
  if ($Process -and -not $Process.HasExited) {
    try { $Process.Kill($true) } catch { Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue }
  }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Evidence
# ------------------------------------------------------------------------------------------------------------------------------

function Get-Snapshot([hashtable] $Ctx, [string] $Surface) {
  $a = @('snapshot', '--app', $Ctx.App, '--compact', '--max-depth', '40', '--include-bounds')
  if ($Surface) { $a += @('--surface', $Surface) } elseif ($Ctx.WindowId) { $a += @('--window-id', $Ctx.WindowId) }
  Invoke-Ad $Ctx $a
}

function Save-Evidence([hashtable] $Ctx, [string] $Label, $Snapshot) {
  $safe = $Label -replace '[^\w\-]+', '_'
  if (-not $Snapshot) { $Snapshot = Get-Snapshot $Ctx }
  if ($Snapshot.raw) { Set-Content -LiteralPath (Join-Path $Ctx.Dir "$safe.json") -Value $Snapshot.raw -Encoding utf8 }
  if ($Ctx.Screenshots) {
    $a = @('screenshot', (Join-Path $Ctx.Dir "$safe.png"), '--app', $Ctx.App)
    if ($Ctx.WindowId) { $a += @('--window-id', $Ctx.WindowId) }
    Invoke-Ad $Ctx $a | Out-Null
  }
}

function Find-ErrorText($Snapshot, [string[]] $Words) {
  if (-not $Snapshot.raw) { return @() }
  @($Words | Where-Object { $Snapshot.raw.Contains($_) })
}

# ------------------------------------------------------------------------------------------------------------------------------
# Menu
# ------------------------------------------------------------------------------------------------------------------------------

# The button that shows the menu: Avalonia's "Toggle navigation drawer", MAUI's "Open navigation" (named in the language of
# Windows: "Ouvrir navigation"), the PaneToggleButton of Uno and WinUI 3. WPF's menu is always shown.
# The query that found it is kept, and tried first the next time (each find costs a second or more on Windows).
function Find-MenuToggle([hashtable] $Ctx) {
  # By name last: Uno on macOS gives no AutomationId to macOS accessibility (no accessibilityIdentifier in Uno 6.7), only the name,
  # here the content of the button of the Uno and WinUI 3 samples.
  $queries = @(@{ NativeId = 'PaneToggleButton' }, @{ NativeId = 'PART_PaneButton' }, @{ NativeId = 'TogglePaneButton' }, @{ Name = 'navigation' }, @{ Name = '☰' })
  $key = { param($q) ($q.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ';' }
  if ($Ctx.ToggleHow) {
    $known = & $key $Ctx.ToggleHow
    $queries = @($Ctx.ToggleHow) + @($queries | Where-Object { (& $key $_) -ne $known })
  }
  foreach ($query in $queries) {
    $hit = Find-All $Ctx @query | Where-Object { (Get-Prop $_ 'ref_id') -and (Get-Prop $_ 'role') -in 'button', 'switch', 'togglebutton' } | Select-Object -First 1
    if ($hit) { $Ctx.ToggleHow = $query; return $hit }
  }
  $null
}

# Whether a menu that had to be opened once (a drawer) is open now. A closed drawer keeps the texts of its entries in the tree with
# their last bounds (Avalonia), so the text cannot tell: the entry itself can, when it is named after its view model
# ("...RouteableMenuEntry`1[XAnimals.ViewModels.DogsViewModel]") and reported offscreen. Unknown (no such entry): $null.
function Test-MenuShown([hashtable] $Ctx, [string] $Page) {
  $vm = $Ctx.ViewModels[$Page]
  if (-not $vm) { return $null }
  $item = Find-All $Ctx -Name "$vm]" | Where-Object { (Get-Prop $_ 'role') -in $script:ItemRoles -and ([string](Get-Prop $_ 'name')).EndsWith(".$vm]") } | Select-Object -First 1
  if (-not $item) { return $null }
  -not (@(Get-Prop $item 'states') -contains 'offscreen')
}

# The text of a menu entry; when it is not on screen, the menu is opened first. A menu known to hide after each navigation is opened
# straight away, at the remembered place of its button (the entry is then looked for once, instead of once before and once after).
function Find-MenuEntry([hashtable] $Ctx, [string] $Page) {
  $Ctx.MenuTrace = $null
  if ($Ctx.OpenHow -and $Ctx.Headed -and (Open-MenuFast $Ctx)) {
    $Ctx.MenuTrace = "menu opened the way that worked before ($($Ctx.OpenHow)), entry not shown"
    $script:fastEntry = $null
    $null = Wait-Until -TimeoutMs $Ctx.StepMs -Condition { $script:fastEntry = Find-ShownEntry $Ctx $Page; [bool]$script:fastEntry }
    if ($script:fastEntry) { return $script:fastEntry }
  }
  $entry = Find-ItemText $Ctx $Page
  if ($entry -and $Ctx.OpenHow -and (Test-MenuShown $Ctx $Page) -eq $false) { $entry = $null }
  if ($entry) { return $entry }
  if ($Ctx.ToggleCenter -and $Ctx.Headed -and -not $Ctx.OpenHow) {
    # Its button seen in the first snapshot: pressed at its place, no find.
    $press = Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($Ctx.ToggleCenter.X),$($Ctx.ToggleCenter.Y)")
    if ($press.ok) {
      Start-Sleep -Milliseconds 450
      $script:menuEntry = $null
      $null = Wait-Until -TimeoutMs $Ctx.StepMs -Condition { $script:menuEntry = Find-ShownEntry $Ctx $Page; [bool]$script:menuEntry }
      if ($script:menuEntry) { $Ctx.OpenHow = 'mouse'; return $script:menuEntry }
    }
  }
  $toggle = Find-MenuToggle $Ctx
  if (-not $toggle) { $Ctx.MenuTrace = 'no menu button found (native id PaneToggleButton, PART_PaneButton, TogglePaneButton, or a name with "navigation" or "☰")'; return $null }
  $Ctx.MenuTrace = "button '$(Get-Prop $toggle 'name')' (role $(Get-Prop $toggle 'role'), found by $(($Ctx.ToggleHow.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ';'))"
  # Physical first: on Avalonia the semantic click on the drawer button answers ok and leaves the drawer closed. Then the Toggle
  # pattern (the button is a switch there), then the semantic click. The way that opened the menu is tried first the next time.
  $ways = @('mouse', 'toggle', 'click')
  if ($Ctx.OpenHow) { $ways = @($Ctx.OpenHow) + @($ways | Where-Object { $_ -ne $Ctx.OpenHow }) }
  if (-not $Ctx.Headed) { $ways = @($ways | Where-Object { $_ -ne 'mouse' }) }
  $first = $true
  foreach ($way in $ways) {
    # A ref is good until the next find: the button is found again before each way after the first.
    if (-not $first) { $toggle = Find-MenuToggle $Ctx; if (-not $toggle) { return $null } }
    $first = $false
    $center = $null
    $press = switch ($way) {
      'toggle' { Invoke-Ad $Ctx @('toggle', (Get-Prop $toggle 'ref_id')) }
      'mouse' {
        $center = Get-RefCenter $Ctx (Get-Prop $toggle 'ref_id')
        if ($center) { Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($center.X),$($center.Y)") }
        else { [pscustomobject]@{ ok = $false; error = [pscustomobject]@{ code = 'NO_BOUNDS' } } }
      }
      default { Invoke-ClickMatch $Ctx $toggle $way }
    }
    if (-not $press.ok) { $Ctx.MenuTrace += "; $way=$(Get-ErrorCode $press)"; continue }
    $script:menuEntry = $null
    $null = Wait-Until -TimeoutMs $Ctx.StepMs -Condition { $script:menuEntry = Find-ShownEntry $Ctx $Page; [bool]$script:menuEntry }
    if (-not $script:menuEntry) { $Ctx.MenuTrace += "; $way pressed$(if ($center) { " at $($center.X),$($center.Y)" }), entry still not shown" }
    if ($script:menuEntry) {
      $Ctx.OpenHow = $way
      if ($center) { $Ctx.ToggleCenter = $center }
      return $script:menuEntry
    }
  }
  $null
}

# A menu whose entries expose no text (MAUI's Shell flyout: items "ShellFlyoutItemView" with the automation id navItem, nothing
# inside): an entry is taken by its rank in the menu (the order of MainViewModel), at its place read once from a snapshot.
function Find-RankEntry([hashtable] $Ctx, [string] $Page) {
  $rank = [array]::IndexOf(@($Ctx.MenuOrder), $Page)
  if ($rank -lt 0) { return $null }
  $items = @(Find-All $Ctx -NativeId 'navItem' | Where-Object { @(Get-Prop $_ 'states') -notcontains 'offscreen' })
  if ($items.Count -le $rank) { return $null }
  if (-not $Ctx.RankMap -or $Ctx.RankMap.Count -ne $items.Count) {
    $Ctx.RankMap = @(Get-Layout $Ctx | Where-Object { $_.Shown -and $_.Role -in $script:ItemRoles -and $_.Name -like '*ShellFlyoutItemView' } |
      Sort-Object { $_.Center.Y } | ForEach-Object { $_.Center })
  }
  if ($Ctx.RankMap.Count -le $rank) { return $null }
  $c = $Ctx.RankMap[$rank]
  [pscustomobject]@{ name = $Page; role = 'option'; ref_id = $null; states = @(); path = @(); bounds = [pscustomobject]@{ x = $c.X - 1; y = $c.Y - 1; width = 2; height = 2 } }
}

# The entry of a page in a menu that is shown: by its text, else by its rank (a menu without texts).
function Find-ShownEntry([hashtable] $Ctx, [string] $Page) {
  $entry = $null
  # A menu known to have no texts (MAUI) is not searched for one.
  if ($Ctx.RankMenu -ne $true) {
    $layout = @()
    # A drawer: one snapshot tells both where the text is and whether it is shown (the text of a closed drawer is under an entry
    # reported offscreen), where a find and a check of the entry take two tree walks.
    if ($Ctx.OpenHow -and -not $Ctx.Overlay) { $layout = @(Get-Layout $Ctx) }
    if ($layout.Count) { $entry = Find-ItemTextIn $layout $Page }
    else {
      $entry = Find-ItemText $Ctx $Page
      if ($entry -and $Ctx.OpenHow -and -not $Ctx.Overlay -and (Test-MenuShown $Ctx $Page) -eq $false) { $entry = $null }
    }
    if ($entry) { $Ctx.RankMenu = $false }
  }
  if (-not $entry -and $Ctx.RankMenu -ne $false) {
    $entry = Find-RankEntry $Ctx $Page
    if ($entry) { $Ctx.RankMenu = $true }
  }
  if (-not $entry -and $Ctx.RankMenu -ne $true) { $entry = Show-ScrolledEntry $Ctx $Page }
  $entry
}

# An entry of a menu taller than the window (the last ones of a WinUI or Uno pane on a 1080p screen, where each entry has a title and
# a description): its text is in a list item, in the column of the window, but above or below it. One wheel turn over the menu
# towards it, then the text again. Once per page, as this runs inside waits; a text inside the window (a closed drawer keeps its
# last bounds there) is left alone.
function Show-ScrolledEntry([hashtable] $Ctx, [string] $Page) {
  $w = $Ctx.WindowRect
  if (-not $Ctx.Headed -or -not $w -or $Ctx.ScrolledFor -eq $Page) { return $null }
  $texts = @(Find-All $Ctx -Name $Page -Exact | Where-Object { Test-InItem $_ })
  $hidden = @($texts | ForEach-Object { $c = Get-Center $_; if ($c) { $c } } |
    Where-Object { $_.X -ge $w.X -and $_.X -le ($w.X + $w.Width) -and ($_.Y -lt $w.Y -or $_.Y -gt ($w.Y + $w.Height)) }) | Select-Object -First 1
  if ($hidden) {
    $x = $hidden.X
    $lines = if ($hidden.Y -gt ($w.Y + $w.Height)) { -10 } else { 10 }
  } elseif ($Ctx.ToggleCenter -and @($texts | Where-Object { -not (Get-Center $_) -or @(Get-Prop $_ 'states') -contains 'offscreen' }).Count) {
    # UI Automation gives an item of a WinUI ListView scrolled out of its viewport empty bounds: its text is there, not where.
    # The menu is in the column of its button; the entries out of view are the last ones (the menu opens at its top): down.
    $x = $Ctx.ToggleCenter.X
    $lines = -10
  } else {
    # A virtualized list (the ListBox of an Avalonia drawer in a small window, as on macOS): the entries below its viewport are not
    # created at all, so their text is nowhere. The entries that are shown tell where the menu is, and their order (MenuOrder)
    # tells which way the missing one is.
    $order = @($Ctx.MenuOrder)
    $want = [array]::IndexOf($order, $Page)
    if ($want -lt 0) { return $null }
    $shown = @(Get-Layout $Ctx | Where-Object { $_.Shown -and $_.InItem -and $_.Name -ne $Page -and $order -contains $_.Name } |
      ForEach-Object { [pscustomobject]@{ Index = [array]::IndexOf($order, $_.Name); Center = $_.Center } })
    if (-not $shown.Count) { return $null }
    $x = $shown[0].Center.X
    $lines = if ($want -gt ($shown | Measure-Object Index -Maximum).Maximum) { -10 } elseif ($want -lt ($shown | Measure-Object Index -Minimum).Minimum) { 10 } else { return $null }
  }
  $Ctx.ScrolledFor = $Page
  $wheel = Invoke-AdHeaded $Ctx @('mouse-wheel', '--x', "$x", '--y', "$([int]($w.Y + $w.Height / 2))", "--dy=$lines")
  if (-not $wheel.ok) { return $null }
  Start-Sleep -Milliseconds 300
  Find-ItemText $Ctx $Page
}

# Opens a menu that hides after each navigation, the way that worked before: at the remembered place of its button when it was
# opened by a physical click, else by finding the button again.
# A drawer closes in 350 ms after a navigation: only what is left of that time is waited for (nothing after a detail or dialogs).
function Wait-DrawerSettled([hashtable] $Ctx) {
  $left = 350 - ($Ctx.Clock.ElapsedMilliseconds - $Ctx.LastNavAt)
  if ($left -gt 0) { Start-Sleep -Milliseconds ([int]$left) }
}

function Open-MenuFast([hashtable] $Ctx, [switch] $Settled) {
  # A drawer that closes by itself after a navigation is animated: its button is pressed once it has closed.
  if (-not $Ctx.Overlay -and -not $Settled) { Wait-DrawerSettled $Ctx }
  if ($Ctx.OpenHow -eq 'mouse' -and $Ctx.ToggleCenter) {
    $press = Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($Ctx.ToggleCenter.X),$($Ctx.ToggleCenter.Y)")
  } else {
    $toggle = Find-MenuToggle $Ctx
    if (-not $toggle) { return $false }
    $press = if ($Ctx.OpenHow -eq 'toggle') { Invoke-Ad $Ctx @('toggle', (Get-Prop $toggle 'ref_id')) } else { Invoke-ClickMatch $Ctx $toggle $Ctx.OpenHow }
  }
  # A drawer slides in: its entries are in place once it has stopped.
  if ($press.ok) { Start-Sleep -Milliseconds 450 }
  [bool]$press.ok
}

# Every window of the app, snapshot: what a menu that did not open left on screen (MAUI opens its flyout in another window).
function Save-AllWindows([hashtable] $Ctx, [string] $Label) {
  $list = Invoke-Ad $Ctx @('list-windows', '--app', $Ctx.App)
  if (-not $list.ok) { Save-Evidence $Ctx $Label; return }
  $saved = $Ctx.WindowId
  $i = 0
  try {
    foreach ($window in @(@($list.data) | Where-Object { $_ })) {
      $Ctx.WindowId = Get-Prop $window 'id'
      Save-Evidence $Ctx "$Label-window$i"
      $i++
    }
  } finally { $Ctx.WindowId = $saved }
}

# Opens a page from the menu. Ok when the header shows its title (or the title of the page a guard sends it to). On a list page,
# $Proof (its first animal) shown in the list proves it as well, and that one find also gives where to click for the detail.
function Open-Page([hashtable] $Ctx, [string] $Page, [string[]] $Titles, [string] $Proof) {
  if (-not $Titles) { $Titles = @($Page) }
  $tried = @()
  if ($Ctx.MenuBroken) {
    return [pscustomobject]@{ Ok = $false; How = $null; Shown = $null; Detail = "the menu never opened ($($Ctx.MenuBroken))"; Tried = $tried; Item = $null }
  }
  # One snapshot per look (cheaper than a find on every host): it proves the page and tells whether a layer is left over it.
  $verify = {
    $script:shown = $null
    $script:proofItem = $null
    $script:pageLayout = $null
    $script:backTried = $false
    $null = Wait-Until -TimeoutMs $Ctx.StepMs -IntervalMs 100 -Condition {
      $layout = @(Get-Layout $Ctx)
      if ($layout.Count) {
        $script:pageLayout = $layout
        if ($Proof) {
          $script:proofItem = Find-ItemTextIn $layout $Proof
          if ($script:proofItem) { $script:shown = $Page }
          elseif (-not $script:backTried -and $Ctx.Headed) {
            # The page kept the detail it showed before (Uno keeps its pages; a click may have reached it while another window
            # was tested): Back brings its list.
            $stale = @($layout | Where-Object { $_.Name -eq 'Back' -and $_.Role -eq 'button' -and -not $_.InItem -and $_.Shown }) | Select-Object -First 1
            if ($stale) {
              $script:backTried = $true
              Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($stale.Center.X),$($stale.Center.Y)") | Out-Null
            }
          }
        } else {
          $script:shown = Test-HeaderIn $Ctx $layout $Titles
        }
      }
      [bool]$script:shown
    }
    if (-not $script:shown) {
      # A tree deeper than the snapshot: one look with a find.
      if ($Proof) { $script:proofItem = Find-ItemText $Ctx $Proof; if ($script:proofItem) { $script:shown = $Page } }
      else { $script:shown = Test-Header $Ctx $Titles }
    }
    $script:shown
  }

  # The remembered place of the entry: no find before the click, one after it to check the page. Only for a menu that never hides
  # (WPF): a click at a place where a hidden menu was would land on the page under it.
  $remembered = $Ctx.MenuMap[$Page]
  $fromRank = $false
  if (-not $remembered -and $Ctx.RankMenu -eq $true -and $Ctx.RankMap) {
    # A menu without texts (MAUI): the remembered place of the entry of that rank.
    $rank = [array]::IndexOf(@($Ctx.MenuOrder), $Page)
    if ($rank -ge 0 -and $rank -lt $Ctx.RankMap.Count) { $remembered = $Ctx.RankMap[$rank]; $fromRank = $true }
  }
  if ($remembered -and $Ctx.Headed -and -not $Ctx.OpenHow) {
    if ($true) {
      $answer = Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($remembered.X),$($remembered.Y)")
      if ($answer.ok -and (& $verify)) {
        $Ctx.PagesOpened++; $Ctx.LastNavAt = $Ctx.Clock.ElapsedMilliseconds
        Close-Pane $Ctx $script:pageLayout
        return [pscustomobject]@{ Ok = $true; How = 'remembered'; Shown = $script:shown; Detail = $null; Tried = $tried; Item = $script:proofItem }
      }
    }
    $tried += 'remembered=missed'
    $Ctx.MenuMap.Remove($Page)
    $Ctx.NeedFocus = $true
  } elseif ($remembered -and $Ctx.Headed -and $Ctx.OpenHow -eq 'mouse' -and $Ctx.ToggleCenter -and (($Ctx.RankMenu -ne $true -and ($Ctx.Overlay -or $Ctx.LayerClosed)) -or ($fromRank -and $Ctx.PagesOpened -ge 2))) {
    # A menu that hides (a drawer, a pane) and that this script closes itself after each page, so its state is known: opened at
    # the place of its button, its entry clicked at its remembered place. No look at the menu: the one snapshot after the click
    # proves the page.
    # The wait for a drawer to close is spent before taking the pointer: the other host can click meanwhile.
    if (-not $Ctx.Overlay) { Wait-DrawerSettled $Ctx }
    $answer = Invoke-WithUi $Ctx {
      if (Open-MenuFast $Ctx -Settled) { Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($remembered.X),$($remembered.Y)") } else { $null }
    }
    if ($answer) {
      if ($answer.ok -and (& $verify)) {
        $Ctx.PagesOpened++; $Ctx.LastNavAt = $Ctx.Clock.ElapsedMilliseconds
        Close-Pane $Ctx $script:pageLayout
        return [pscustomobject]@{ Ok = $true; How = 'remembered'; Shown = $script:shown; Detail = $null; Tried = $tried; Item = $script:proofItem }
      }
    }
    $tried += 'remembered=missed'
    $Ctx.MenuMap.Remove($Page)
    if ($fromRank) { $Ctx.RankMap = $null }
    $Ctx.NeedFocus = $true
    # The menu may be left open by the miss: closed by its layer if it has one, so that the next opening starts from a closed menu.
    Close-Pane $Ctx $script:pageLayout
  }

  $order = if ($Ctx.Headed) { @('mouse', 'click') } else { @('click') }
  if ($Ctx.MenuHow) { $order = @($Ctx.MenuHow) + @($order | Where-Object { $_ -ne $Ctx.MenuHow }) }
  # Each way twice: a click that lands while a drawer or a pane is still closing (they are animated) is lost.
  $order = @($order | ForEach-Object { $_; $_ })
  foreach ($how in $order) {
    $script:openStep = $null
    # Opening the menu, finding the entry and clicking it: one step for the pointer (a click in the other window would close a menu).
    Invoke-WithUi $Ctx {
      $entry = Find-MenuEntry $Ctx $Page
      $answer = $null
      if ($entry) {
        # The places of the menu entries, read once the menu shows them (at start, Avalonia's drawer reports them hidden: read again
        # at the next opening).
        if (-not $Ctx.MenuMapRead -and $Ctx.Headed) { Update-MenuMap $Ctx; $Ctx.MenuMapRead = $Ctx.MenuMap.Count -gt 0 }
        $answer = Invoke-ClickMatch $Ctx $entry $how
      }
      $script:openStep = @{ Entry = $entry; Answer = $answer }
    }
    $entry = $script:openStep.Entry
    if (-not $entry) {
      $trace = if ($Ctx.MenuTrace) { $Ctx.MenuTrace } else { 'no entry shown and no button tried' }
      # The first miss: what is on screen is kept (every window), with what a find of the entry gives unfiltered (its roles, path,
      # states, bounds: why it did not count as a shown menu entry).
      if (-not $Ctx.MenuEvidence) {
        $Ctx.MenuEvidence = $true
        Save-AllWindows $Ctx "menu-$Page"
        $probe = [ordered]@{ page = $Page; trace = $trace; window = $Ctx.WindowRect; matches = @(Find-All $Ctx -Name $Page -AnyWindow) }
        $probe | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Ctx.Dir "menu-$($Page -replace '[^\w\-]+', '_')-find.json") -Encoding utf8
      }
      # The menu did not open on the first page, or on two pages in a row: it will not on the next ones (each miss costs the wait
      # of every way to open it).
      $Ctx.MenuMisses++
      if ($Ctx.PagesOpened -eq 0 -or $Ctx.MenuMisses -ge 2) { $Ctx.MenuBroken = "menu entry not found: $trace" }
      return [pscustomobject]@{ Ok = $false; How = $null; Shown = $null; Detail = "menu entry not found: $trace"; Tried = $tried; Item = $null }
    }
    $Ctx.MenuMisses = 0
    $answer = $script:openStep.Answer
    $tried += "$how=$(if ($answer.ok) { 'ok' } else { Get-ErrorCode $answer })"
    if (-not $answer.ok) { continue }
    if (& $verify) {
      $Ctx.MenuHow = $how
      $Ctx.PagesOpened++; $Ctx.LastNavAt = $Ctx.Clock.ElapsedMilliseconds
      Close-Pane $Ctx $script:pageLayout
      return [pscustomobject]@{ Ok = $true; How = $how; Shown = $script:shown; Detail = $null; Tried = $tried; Item = $script:proofItem }
    }
    $tried[-1] += ' (header unchanged)'
    $Ctx.NeedFocus = $true
  }
  $what = if ($Proof) { "'$Proof' never appeared in the list" } else { "the header never showed '$($Titles -join "' or '")'" }
  [pscustomobject]@{ Ok = $false; How = $null; Shown = $null; Detail = $what; Tried = $tried; Item = $null }
}

# ------------------------------------------------------------------------------------------------------------------------------
# List -> detail -> Back
# ------------------------------------------------------------------------------------------------------------------------------

function Find-BackButton([hashtable] $Ctx) {
  Find-All $Ctx -Name 'Back' -Exact | Where-Object { (Get-Prop $_ 'role') -eq 'button' -and -not (Test-InItem $_) } | Select-Object -First 1
}

# The detail of an animal is shown: its name outside the list (the list hides while the detail is shown).
function Test-DetailShown([hashtable] $Ctx, [string] $Animal) {
  [bool](Find-All $Ctx -Name $Animal -Exact | Where-Object { -not (Test-InItem $_) } | Select-Object -First 1)
}

# Opens the first animal of the list of the page, checks its detail (its name), goes back with Back and checks that the list is
# shown again. Back is clicked at its remembered place after the first time.
function Test-Detail([hashtable] $Ctx, [string] $Animal, $Item) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $report = [ordered]@{ status = 'fail'; animal = $Animal; ms = 0; detail = $null }
  try {
    $item = if ($Item) { $Item } else { Find-ItemText $Ctx $Animal }
    if (-not $item) { $report.detail = "'$Animal' not found in the list"; Save-Evidence $Ctx "detail-$Animal"; return $report }
    $open = $false
    $order = if ($Ctx.Headed) { @('mouse', 'mouse', 'click') } else { @('click') }
    $clicks = 0
    foreach ($how in $order) {
      $press = Invoke-ClickMatch $Ctx $item $how
      if (-not $press.ok) { continue }
      $clicks++
      # The detail shows in half a second to a second and a half. A first click that only closed a menu left over the page
      # (Avalonia's drawer, the pane of WinUI 3) shows nothing: after a short wait the click is made again, and waited for longer.
      $budget = if ($clicks -eq 1) { 2500 } else { $Ctx.StepMs * 2 }
      # One snapshot per look: it shows the detail and where its Back button is, in one tree walk.
      $script:detailLayout = $null
      if (Wait-Until -TimeoutMs $budget -IntervalMs 100 -Condition {
          $layout = @(Get-Layout $Ctx)
          if (-not $layout.Count) { return (Test-DetailShown $Ctx $Animal) }
          $script:detailLayout = $layout
          [bool](@($layout | Where-Object { $_.Name -eq $Animal -and -not $_.InItem }).Count)
        }) {
        $open = $true
        break
      }
      $Ctx.NeedFocus = $true
      # The list may have moved (a pane closed): the animal is found again.
      $again = Find-ItemText $Ctx $Animal
      if ($again) { $item = $again }
    }
    if (-not $open) { $report.detail = "no detail after a click on '$Animal'"; Save-Evidence $Ctx "detail-$Animal"; return $report }

    $back = { Wait-Until -TimeoutMs $Ctx.StepMs -IntervalMs 100 -Condition {
        $layout = @(Get-Layout $Ctx)
        if ($layout.Count) { [bool](Find-ItemTextIn $layout $Animal) } else { [bool](Find-ItemText $Ctx $Animal) } } }
    # Back is found each time: its place moves with the length of the description of the animal. Taken from the snapshot that showed
    # the detail first (no find, no get); found again only when that click did not bring the list back.
    $returned = $false
    $shownBack = @($script:detailLayout | Where-Object { $_ -and $_.Name -eq 'Back' -and $_.Role -eq 'button' -and -not $_.InItem -and $_.Shown }) | Select-Object -First 1
    if ($shownBack -and $Ctx.Headed) {
      # A detail that slides in (MAUI, Avalonia) has its Back at its final place once the page has stopped.
      Start-Sleep -Milliseconds 200
      $press = Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($shownBack.Center.X),$($shownBack.Center.Y)")
      $returned = $press.ok -and (& $back)
    }
    # The Back button found, clicked at its place (twice: the first click after the detail appeared was lost once on WPF), then
    # semantically.
    $tries = 0
    while (-not $returned -and $tries -lt 3) {
      $tries++
      $button = $null
      $null = Wait-Until -TimeoutMs $Ctx.StepMs -Condition { $script:backButton = Find-BackButton $Ctx; [bool]$script:backButton }
      $button = $script:backButton
      if (-not $button) { break }
      if ($tries -lt 3 -and $Ctx.Headed) {
        $center = Get-RefCenter $Ctx (Get-Prop $button 'ref_id')
        if (-not $center) { continue }
        $press = Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($center.X),$($center.Y)")
        $returned = $press.ok -and (& $back)
      } else {
        $press = Invoke-ClickMatch $Ctx $button 'click'
        $returned = $press.ok -and (& $back)
      }
    }
    if (-not $returned) { $report.detail = 'Back did not bring the list back'; Save-Evidence $Ctx "back-$Animal"; return $report }
    $report.status = 'ok'
    $report
  } finally {
    $report.ms = $watch.ElapsedMilliseconds
  }
}

# ------------------------------------------------------------------------------------------------------------------------------
# Dialogs
# ------------------------------------------------------------------------------------------------------------------------------

function Normalize-Label([string] $Name) { if ($Name) { $Name.Replace('_', '').Replace('&', '').Trim().ToLowerInvariant() } else { '' } }

# The answer of a dialog, wherever the toolkit draws it: in another window of the app (WPF's message box), in the window that has
# the focus (Avalonia's dialog is an owned window kept out of the taskbar, which list-windows does not list), or over the page in
# the main window (Uno's and WinUI's ContentDialog). Each place is read from a snapshot with bounds. An answer is a button, a list
# item or the text of a list item (MAUI shows three choices as a list). In the main window an answer can be the twin of a button of
# the page ("OK" over "Ok"): a candidate at the place of a page button of the same name is left out. The first answer of $Answers
# found wins; the places are tried in that order.
function Find-DialogAnswer([hashtable] $Ctx, [string[]] $Answers) {
  $places = [Collections.Generic.List[object]]::new()
  # Dialogs of this host known to be drawn in its window or in the window in focus: no list of the windows (one call less).
  $list = if ($Ctx.DialogPlace -in 'focused', 'main') { [pscustomobject]@{ ok = $false } } else { Invoke-Ad $Ctx @('list-windows', '--app', $Ctx.App) }
  if ($list.ok) {
    foreach ($id in @(@($list.data) | Where-Object { $_ } | ForEach-Object { Get-Prop $_ 'id' } | Where-Object { $_ -and $_ -ne $Ctx.WindowId })) {
      $places.Add(@{ Kind = 'other'; Window = $id; Surface = $null; Main = $false })
    }
  }
  $places.Add(@{ Kind = 'focused'; Window = $null; Surface = 'focused'; Main = $false })
  $places.Add(@{ Kind = 'main'; Window = $Ctx.WindowId; Surface = $null; Main = $true })
  # Where the dialogs of this host appeared before is looked at first.
  if ($Ctx.DialogPlace) { $places = @($places | Sort-Object { if ($_.Kind -eq $Ctx.DialogPlace) { 0 } else { 1 } }) }
  $avoid = Get-PageButtons $Ctx
  $wanted = @($Answers | ForEach-Object { $_.ToLowerInvariant() })
  foreach ($place in $places) {
    $saved = $Ctx.WindowId
    if ($place.Window) { $Ctx.WindowId = $place.Window }
    try { $layout = @(Get-Layout $Ctx $place.Surface) } finally { $Ctx.WindowId = $saved }
    $candidates = @($layout | Where-Object {
      $_.Shown -and (Normalize-Label $_.Name) -in $wanted -and (Normalize-Label $_.Name) -notin $script:CaptionButtons -and
      ($_.Role -eq 'button' -or $_.Role -in $script:ItemRoles -or ($_.Role -eq 'statictext' -and $_.InItem)) -and
      -not ($place.Main -and (Test-Avoided $_.Center $_.Name $avoid))
    })
    # The focused window is the main one when no dialog is open: its candidates are those of the main window, checked last.
    if ($place.Surface -eq 'focused' -and $candidates.Count -and $avoid.Count) {
      $candidates = @($candidates | Where-Object { -not (Test-Avoided $_.Center $_.Name $avoid) })
    }
    foreach ($answer in $Answers) {
      $node = $candidates | Where-Object { (Normalize-Label $_.Name) -eq $answer.ToLowerInvariant() } | Select-Object -First 1
      if ($node) {
        $c = $node.Center
        $element = [pscustomobject]@{ name = $node.Name; role = $node.Role; ref_id = $null; states = @(); path = @(); bounds = [pscustomobject]@{ x = $c.X - 1; y = $c.Y - 1; width = 2; height = 2 } }
        $Ctx.DialogPlace = $place.Kind
        return [pscustomobject]@{ Answer = $answer; Element = $element; WindowId = $place.Window; Main = $place.Main }
      }
    }
  }
  $null
}

# Waits for a text in the tree of the app: ok, timeout, or several-windows (agent-desktop refuses a text wait while the app has
# two windows, so a dialog that is still closing is waited for here).
function Wait-Text([hashtable] $Ctx, [string] $Text, [int] $TimeoutMs) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  while ($true) {
    $left = [Math]::Max(200, $TimeoutMs - [int]$watch.ElapsedMilliseconds)
    $result = Invoke-Ad $Ctx @('wait', '--text', $Text, '--app', $Ctx.App, '--timeout', "$left")
    if ($result.ok) { return 'ok' }
    $code = Get-ErrorCode $result
    if ($code -ne 'AMBIGUOUS_TARGET') { return $(if ($code -eq 'TIMEOUT') { 'timeout' } else { "error $code" }) }
    if ($watch.ElapsedMilliseconds -ge $TimeoutMs) { return 'several-windows' }
    Start-Sleep -Milliseconds 150
  }
}

# What agent-desktop sees while a dialog is open whose answers were not found: its surfaces, the window in focus, every window. Kept
# once per host, to find where that toolkit puts its dialogs.
function Save-DialogProbe([hashtable] $Ctx, [string] $Label) {
  $Label = $Label -replace '[^\w\-]+', '_'
  $probes = [ordered]@{
    'list-surfaces' = @('list-surfaces', '--app', $Ctx.App)
    'list-windows'  = @('list-windows')
    'focused'       = @('snapshot', '--app', $Ctx.App, '--surface', 'focused', '--compact', '--max-depth', '40', '--include-bounds')
    'sheet'         = @('snapshot', '--app', $Ctx.App, '--surface', 'sheet', '--compact', '--max-depth', '40', '--include-bounds')
    'menu'          = @('snapshot', '--app', $Ctx.App, '--surface', 'menu', '--compact', '--max-depth', '40', '--include-bounds')
  }
  foreach ($name in $probes.Keys) {
    $result = Invoke-Ad $Ctx $probes[$name]
    Set-Content -LiteralPath (Join-Path $Ctx.Dir "$Label-$name.json") -Value $result.raw -Encoding utf8
  }
}

# A dialog left open after a failure would block the next ones (and may be in a window list-windows does not show): Escape.
# A dialog left open blocks every next one: its Cancel (or one of its answers) is clicked when it is in the tree, else Escape.
# Never Tab: in MAUI's task dialog the first stop is its "More Details" link, which only unfolds the footer.
function Close-StrayDialog([hashtable] $Ctx, [string[]] $Answers) {
  $answers = @('Cancel') + @($Answers | Where-Object { $_ -and $_ -ne 'Cancel' })
  $open = Find-DialogAnswer $Ctx $answers
  if ($open) {
    Invoke-ClickButton $Ctx $open.Element (Get-PageButtons $Ctx) | Out-Null
  } else {
    Invoke-AdHeaded $Ctx @('press', 'escape') | Out-Null
  }
  Start-Sleep -Milliseconds 300
}

# Opens one dialog, answers it and checks the Result line of the page.
function Test-Dialog([hashtable] $Ctx, [hashtable] $Spec, [string] $Previous) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $report = [ordered]@{ status = 'fail'; answered = $null; result = $null; ms = 0; detail = $null }
  # The places of the buttons of the page, read once from a snapshot: then a button is clicked there with no find before (a second
  # or more each on Windows).
  if ($null -eq $Ctx.ButtonMap -and $Ctx.Headed) {
    $Ctx.ButtonMap = @{}
    foreach ($node in (Get-Layout $Ctx)) {
      if (-not $node.Shown -or $node.InItem -or $node.Role -ne 'button' -or -not $node.Name) { continue }
      $known = $Ctx.ButtonMap[$node.Name]
      # The highest of two buttons of the same name: the dialogs come before the notifications.
      if (-not $known -or $node.Center.Y -lt $known.Y) { $Ctx.ButtonMap[$node.Name] = $node.Center }
    }
  }
  $place = if ($Ctx.ButtonMap) { $Ctx.ButtonMap[$Spec.Button] } else { $null }
  $opener = $null
  if (-not $place) { $opener = Find-All $Ctx -Name $Spec.Button -Exact -Role 'button' | Where-Object { Get-Prop $_ 'ref_id' } | Select-Object -First 1 }
  if (-not $place -and -not $opener -and $Ctx.Headed) {
    # A drawer or a pane left open over the page hides its buttons from the tree: Escape closes it.
    Invoke-AdHeaded $Ctx @('press', 'escape') | Out-Null
    Start-Sleep -Milliseconds 400
    $opener = Find-All $Ctx -Name $Spec.Button -Exact -Role 'button' | Where-Object { Get-Prop $_ 'ref_id' } | Select-Object -First 1
  }
  if (-not $place -and -not $opener) {
    $report.detail = 'button not found on the Dialogs page'
    if (-not $Ctx.OpenerEvidence) { $Ctx.OpenerEvidence = $true; Save-Evidence $Ctx "dialog-$($Spec.Button)-no-button" }
    $report.ms = $watch.ElapsedMilliseconds; return $report
  }
  # From the click on the button to the answer, the pointer and the keyboard stay with this host (side by side, a click in the
  # other window would take the focus from the dialog).
  $uiLock = Enter-Ui
  try {
  $press = if ($place) { Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($place.X),$($place.Y)") } else { Invoke-ClickButton $Ctx $opener }
  # TIMEOUT is expected from a semantic press that opens a modal dialog synchronously: the dialog is there all the same.
  if (-not $press.ok -and (Get-ErrorCode $press) -ne 'TIMEOUT') {
    $report.detail = "could not press the button: $(Get-ErrorCode $press)"; $report.ms = $watch.ElapsedMilliseconds; return $report
  }

  $script:dlg = $null
  # What answered this dialog in an earlier run, when it was not in the tree: keys, tried first (or 'none').
  $known = if ($Ctx.DialogKeys) { $Ctx.DialogKeys[$Spec.Button] } else { $null }
  # Keys that answered this dialog before: no search. A host where a dialog was not in the tree (Uno's ContentDialog, MAUI's
  # action sheets): one short look only, since other dialogs of that host may be in it (MAUI's task dialog and modal page are).
  if ($known -and $known -ne 'none') { Start-Sleep -Milliseconds 700 }
  elseif ($Ctx.DialogHidden -or $known -eq 'none') {
    Start-Sleep -Milliseconds 400
    $null = Wait-Until -TimeoutMs 900 -IntervalMs 200 -Condition { $script:dlg = Find-DialogAnswer $Ctx $Spec.Answers; [bool]$script:dlg }
  }
  else {
    $null = Wait-Until -TimeoutMs $Ctx.StepMs -IntervalMs 200 -Condition { $script:dlg = Find-DialogAnswer $Ctx $Spec.Answers; [bool]$script:dlg }
    if (-not $script:dlg -and $place) {
      # The first click on the page after a navigation may only have closed a drawer left over it: once more.
      Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($place.X),$($place.Y)") | Out-Null
      $null = Wait-Until -TimeoutMs $Ctx.StepMs -IntervalMs 200 -Condition { $script:dlg = Find-DialogAnswer $Ctx $Spec.Answers; [bool]$script:dlg }
    }
  }
  $dialog = $script:dlg
  $expected = $null
  if ($dialog) {
    # At its place first: an answer that has a twin in the window (the page's "Ok" next to the dialog's "OK") refuses a semantic click.
    $click = Invoke-ClickButton $Ctx $dialog.Element (Get-PageButtons $Ctx)
    if ($click.ok) {
      $report.answered = $dialog.Answer; $expected = $Spec.Results[$dialog.Answer]
      if ($Ctx.DialogKeys) { $Ctx.DialogKeys.Remove($Spec.Button) }
    }
    else { $report.answered = "keys (click refused: $(Get-ErrorCode $click))" }
  } else {
    $report.answered = 'keys (no answer found in the tree)'
    if (-not $Ctx.DialogHidden -and -not $known) {
      $Ctx.DialogHidden = $true
      Save-DialogProbe $Ctx "dialog-probe-$($Spec.Button)"
    }
  }

  # The page writes what was answered on its Result line: that is the check.
  $candidates = @(if ($expected) { $expected } else { $Spec.Results.Values | Select-Object -Unique })
  # One snapshot of the main window per look shows every candidate at once (agent-desktop's text wait misses texts deep in the
  # tree: Avalonia's Result line). The text the line showed before the dialog counts only if no other one comes.
  $check = {
    param([int] $Budget)
    $script:seenText = $null
    $script:seenPrevious = $false
    $script:readTree = $false
    $null = Wait-Until -TimeoutMs $Budget -IntervalMs 100 -Condition {
      $layout = @(Get-Layout $Ctx)
      if (-not $layout.Count) { return $false }
      $script:readTree = $true
      $names = [Collections.Generic.HashSet[string]]::new([string[]]@($layout | ForEach-Object { $_.Name }))
      foreach ($candidate in $candidates) {
        if (-not $names.Contains($candidate)) { continue }
        if ($candidate -eq $Previous) { $script:seenPrevious = $true; continue }
        $script:seenText = $candidate
        return $true
      }
      $false
    }
    if (-not $script:seenText -and $script:seenPrevious) { $script:seenText = $Previous }
    if (-not $script:seenText -and -not $script:readTree) {
      foreach ($candidate in $candidates) {
        $wait = Wait-Text $Ctx $candidate 500
        if ($wait -eq 'ok') { $script:seenText = $candidate; break }
        if ($wait -eq 'several-windows') { $report.detail = 'the dialog is still open after the answer'; break }
      }
    }
    $script:seenText
  }
  $deferCheck = $false
  if ($expected) {
    # Answered by a click: the Result line is read after the pointer is given back.
    $seen = $null
    $deferCheck = $true
  } else {
    # No answer to click: Enter (the default button), then Escape (a dialog without a default button: a modal), then Tab and Enter
    # (a dialog whose focus is on none of its buttons: a task dialog).
    $seen = $null
    $sequences = @(@('return'), @('escape'))
    # Tab and Enter could press a button of the page if no dialog were open: only where keys already answered a dialog of this host.
    if ($Ctx.KeysWorked -or $known -eq 'tab+return') { $sequences += , @('tab', 'return') }
    # The keys that answered this dialog in the last run first: one try instead of up to three.
    if ($known -and $known -ne 'none') {
      $sequences = @(, @($known -split '\+')) + @($sequences | Where-Object { ($_ -join '+') -ne $known })
    }
    # The Result line already shows one of the texts this dialog can write (Ok / Abort after Ok / Cancel: Enter writes
    # "Answer: OK." both times): Escape first, whose answer (Abort, Cancel) changes the line, so the check proves something.
    if ($Previous -and ($candidates -contains $Previous) -and $known -ne 'escape') {
      $sequences = @(, @('escape')) + @($sequences | Where-Object { ($_ -join '+') -ne 'escape' })
    }
    $budget = if ($known -eq 'none') { 800 } else { 1200 }
    foreach ($keys in $sequences) {
      $failed = $false
      foreach ($key in $keys) {
        # To the window in front, the dialog (with --app the key would go to the main window).
        $press = Invoke-AdHeaded $Ctx @('press', $key)
        if (-not $press.ok) { $report.detail = "no way to answer the dialog: $(Get-ErrorCode $press)"; $failed = $true; break }
      }
      if ($failed) { break }
      $report.answered = "$($keys -join '+') (no answer found in the tree)"
      $report.detail = $null
      $seen = & $check $budget
      if ($seen) { $Ctx.KeysWorked = $true; break }
    }
    if ($Ctx.DialogKeys) { $Ctx.DialogKeys[$Spec.Button] = if ($seen) { $report.answered.Split(' ')[0] } else { 'none' } }
  }
  } finally { Exit-Ui $uiLock }
  if ($deferCheck) { $seen = & $check $Ctx.StepMs }
  if ($seen) {
    $report.result = $seen
    if ($seen -eq $Previous) { $report.status = 'unverified'; $report.detail = 'the Result line already showed this text before the dialog' }
    else { $report.status = 'ok' }
  } else {
    if (-not $report.detail) {
      $report.detail = if ($expected) { "the Result line never showed '$expected'" } else { "the Result line shows none of: $($candidates -join ' | ')" }
    }
    Save-Evidence $Ctx "dialog-$($Spec.Button)"
    Close-StrayDialog $Ctx $Spec.Answers
  }
  $report.ms = $watch.ElapsedMilliseconds
  $report
}

# ------------------------------------------------------------------------------------------------------------------------------
# One host
# ------------------------------------------------------------------------------------------------------------------------------

function Write-Step([hashtable] $Ctx, [string] $Text, [string] $Color = 'Gray') {
  Write-Host ("[{0,-12} {1,6:0.0}s] {2}" -f $Ctx.Name, ($Ctx.Clock.ElapsedMilliseconds / 1000), $Text) -ForegroundColor $Color
}

function Get-StatusColor([string] $Status) { switch ($Status) { 'ok' { 'Green' } 'unverified' { 'Yellow' } 'skipped' { 'DarkGray' } default { 'Red' } } }

# The permission prompt of the system ("... would like to send you notifications"): macOS asks the first time an application of a bundle
# posts one, and the post waits for the answer. It belongs to another application (UserNotificationCenter, or NotificationCenter), so it is
# looked for there. Best effort: returns what was pressed, or $null when there is no prompt (nothing to answer, or not found).
function Approve-NotificationPrompt([hashtable] $Ctx) {
  foreach ($app in 'UserNotificationCenter', 'NotificationCenter') {
    foreach ($label in 'Allow', 'Autoriser') {
      $found = Invoke-Ad $Ctx @('find', '--app', $app, '--name', $label, '--role', 'button', '--exact', '--limit', '1', '--timeout-ms', '300')
      if (-not $found.ok) { continue }
      $match = @(@(Get-Prop $found.data 'matches') | Where-Object { $_ })[0]
      if (-not $match) { continue }
      $press = Invoke-ClickMatch $Ctx $match 'mouse'
      if ($press.ok) { return "$app/$label" }
    }
  }
  $null
}

# Presses one button of the notifications of the Dialogs page, and checks what comes of it.
#  - A notification of the application (Information, Success, Warning, Error): its title must appear, then a click on it must write
#    "Clicked ..." (or "Closed ...": some hosts close it after the click) on the Result line.
#  - The system notification ("System"): the Result line says "System notification sent." where the host has a notification center of the
#    system. Where it has none, "System notifications are not available on this host." is a pass only if the host is not required to
#    have one ($Ctx.SystemNotificationRequired: macOS from a bundle, Windows); else it is 'unverified' (a host that cannot, not one that fails).
function Test-Notification([hashtable] $Ctx, [hashtable] $Spec, [string] $Previous) {
  $watch = [Diagnostics.Stopwatch]::StartNew()
  $report = [ordered]@{ status = 'fail'; answered = $null; result = $null; ms = 0; detail = $null }
  # The place of the button: the lowest of that name ("Error" is a dialog above and a notification below).
  $place = $null
  $opener = $null
  if ($Ctx.Headed) {
    foreach ($node in (Get-Layout $Ctx)) {
      if (-not $node.Shown -or $node.InItem -or $node.Role -ne 'button' -or $node.Name -ne $Spec.Button) { continue }
      if (-not $place -or $node.Center.Y -gt $place.Y) { $place = $node.Center }
    }
  }
  if (-not $place) { $opener = @(Find-All $Ctx -Name $Spec.Button -Exact -Role 'button' | Where-Object { Get-Prop $_ 'ref_id' }) | Select-Object -Last 1 }
  if (-not $place -and -not $opener) {
    $report.detail = 'button not found on the Dialogs page'
    $report.ms = $watch.ElapsedMilliseconds; return $report
  }

  $uiLock = Enter-Ui
  try {
    $press = if ($place) { Invoke-AdHeaded $Ctx @('mouse-click', '--xy', "$($place.X),$($place.Y)") } else { Invoke-ClickButton $Ctx $opener }
    if (-not $press.ok -and (Get-ErrorCode $press) -ne 'TIMEOUT') {
      $report.detail = "could not press the button: $(Get-ErrorCode $press)"; return $report
    }

    # What the press shows: the title of the notification, or the answer of the page for the system one.
    $script:noteNode = $null
    $script:lastPrompt = $null
    $expect = @(if ($Spec.System) { $Spec.Sent, $Spec.Unavailable } else { $Spec.Title })
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $shown = Wait-Until -TimeoutMs $Spec.WaitMs -IntervalMs 250 -Condition {
      foreach ($node in @(Get-Layout $Ctx)) {
        if ($node.Shown -and $expect -contains $node.Name -and $node.Name -ne $Previous) { $script:noteNode = $node; return $true }
      }
      # The system notification waits for the permission prompt the first time (macOS): answered after a second, then every two.
      if ($Spec.System -and $clock.ElapsedMilliseconds -gt 1000 -and (-not $script:lastPrompt -or ($clock.ElapsedMilliseconds - $script:lastPrompt) -gt 2000)) {
        $script:lastPrompt = $clock.ElapsedMilliseconds
        $approved = Approve-NotificationPrompt $Ctx
        if ($approved) { $report.answered = $approved }
      }
      $false
    }
    if (-not $shown) {
      $report.detail = if ($Spec.System) { "the Result line showed neither '$($Spec.Sent)' nor '$($Spec.Unavailable)' in $($Spec.WaitMs) ms (a permission prompt that nothing answered?)" }
                       else { "the notification '$($Spec.Title)' did not appear in $($Spec.WaitMs) ms" }
      Save-Evidence $Ctx "notification-$($Spec.Button)"
      Save-DialogProbe $Ctx "notification-probe-$($Spec.Button)"
      return $report
    }

    if ($Spec.System) {
      $report.result = $script:noteNode.Name
      if ($report.result -eq $Spec.Sent) { $report.status = 'ok' }
      elseif ($Ctx.SystemNotificationRequired) { $report.detail = "this host must have a notification center: $($report.result)"; Save-Evidence $Ctx "notification-$($Spec.Button)" }
      else { $report.status = 'unverified'; $report.detail = $report.result }
      return $report
    }

    # An application notification: a click on it.
    $click = Invoke-ClickMatch $Ctx (ConvertTo-Match $script:noteNode) 'mouse'
    if (-not $click.ok) { $report.detail = "could not click the notification: $(Get-ErrorCode $click)"; return $report }
    $report.answered = 'click'
    $script:clickedText = $null
    $clicked = Wait-Until -TimeoutMs 3000 -IntervalMs 200 -Condition {
      foreach ($node in @(Get-Layout $Ctx)) {
        if ($Spec.Clicked -contains $node.Name) { $script:clickedText = $node.Name; return $true }
      }
      $false
    }
    if ($clicked) { $report.result = $script:clickedText; $report.status = 'ok' }
    else {
      $report.detail = "a click on the notification did not write $($Spec.Clicked -join ' or ') on the Result line"
      Save-Evidence $Ctx "notification-$($Spec.Button)-click"
    }
    return $report
  } finally {
    Exit-Ui $uiLock
    $report.ms = $watch.ElapsedMilliseconds
  }
}

# The keys that answered each dialog that is not in the tree, per host, kept between runs (in the temp folder of the user).
$script:DialogKeysFile = Join-Path ([IO.Path]::GetTempPath()) 'routeables-smoke/dialog-keys.json'
function Read-DialogKeys([string] $HostName) {
  $all = @{}
  try { if (Test-Path -LiteralPath $script:DialogKeysFile) { $all = Get-Content -Raw -LiteralPath $script:DialogKeysFile | ConvertFrom-Json -AsHashtable } } catch { $all = @{} }
  if ($all -and $all[$HostName]) { $all[$HostName] } else { @{} }
}
function Save-DialogKeys([string] $HostName, [hashtable] $Keys) {
  if ($null -eq $Keys) { return }
  $mutex = [Threading.Mutex]::new($false, 'RouteablesDialogKeys')
  try {
    try { $null = $mutex.WaitOne([TimeSpan]::FromSeconds(10)) } catch [Threading.AbandonedMutexException] { }
    $all = @{}
    if (Test-Path -LiteralPath $script:DialogKeysFile) { $all = Get-Content -Raw -LiteralPath $script:DialogKeysFile | ConvertFrom-Json -AsHashtable }
    if (-not $all) { $all = @{} }
    $all[$HostName] = $Keys
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $script:DialogKeysFile) | Out-Null
    $all | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $script:DialogKeysFile -Encoding utf8
  } catch { } finally { try { $mutex.ReleaseMutex() } catch { }; $mutex.Dispose() }
}

function Complete-Report([hashtable] $Ctx, [System.Collections.Specialized.OrderedDictionary] $Report) {
  if ($Ctx.DialogKeys -and $Report.kind -eq 'sample' -and $Report.dialogs.Count) { Save-DialogKeys $Ctx.Name $Ctx.DialogKeys }
  $Report.seconds = [Math]::Round($Ctx.Clock.ElapsedMilliseconds / 1000, 1)
  $Report.calls = $Ctx.Calls
  $Report.agentDesktopSeconds = [Math]::Round($Ctx.AdMs / 1000, 1)
  $Ctx.Trace | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 4 } | Set-Content -LiteralPath (Join-Path $Ctx.Dir 'calls.jsonl') -Encoding utf8
  $Report
}

# Brings the window to the front, as large as the screen: physical clicks then land on it.
function Initialize-Window([hashtable] $Ctx) {
  if (-not $Ctx.WindowId -or -not $Ctx.Headed) { return }
  if ($Ctx.Slot) {
    # Side by side: the window fills its half of the screen (no window over another: each one takes its own clicks).
    Invoke-Ad $Ctx @('restore', '--window-id', $Ctx.WindowId) | Out-Null
    Invoke-Ad $Ctx @('move-window', '--window-id', $Ctx.WindowId, "--x=$($Ctx.Slot.X)", "--y=$($Ctx.Slot.Y)") | Out-Null
    Invoke-Ad $Ctx @('resize-window', '--window-id', $Ctx.WindowId, '--width', "$($Ctx.Slot.Width)", '--height', "$($Ctx.Slot.Height)") | Out-Null
  } else {
    Invoke-Ad $Ctx @('maximize', '--window-id', $Ctx.WindowId) | Out-Null
  }
  $focus = Invoke-Ad $Ctx @('focus-window', '--window-id', $Ctx.WindowId)
  # Windows may refuse the foreground once: asked again after a moment.
  if (-not $focus.ok) { Start-Sleep -Milliseconds 300; $focus = Invoke-Ad $Ctx @('focus-window', '--window-id', $Ctx.WindowId) }
  $Ctx.NeedFocus = -not $focus.ok
  # The window has new bounds: read them again (they decide what is on screen).
  $id = Resolve-MainWindow $Ctx $Ctx.Pid
  if ($id) { $Ctx.WindowId = $id }
}

# The XAnimals sample: every page of the menu, the detail of the first animal of each list and Back, then the dialogs.
function Invoke-SampleSmoke([hashtable] $Ctx, [hashtable] $Target, [hashtable] $Options) {
  if (-not $script:AdPath) { Set-AgentDesktopPath $Options.AgentDesktop }
  $Ctx.Clock.Restart()
  $report = [ordered]@{ kind = 'sample'; exe = $Target.Exe; status = 'fail'; startSeconds = $null; menuActivation = $null; pages = [ordered]@{}; details = [ordered]@{}; dialogs = [ordered]@{}; notifications = [ordered]@{}; problems = @() }
  if ($Target.Problem) { $report.problems += $Target.Problem; return (Complete-Report $Ctx $report) }

  $Ctx.DialogKeys = Read-DialogKeys $Ctx.Name
  try {
    Show-HiddenWindow $Ctx
    $first = $Options.Pages[0]
    # Ready: the header shows the first page, or its entry is in the menu (some hosts open on no page until one is chosen).
    $failure = Wait-AppReady $Ctx $Options.StartTimeoutSeconds { [bool](Test-Header $Ctx @($Options.Titles[$first])) -or [bool](Find-ItemText $Ctx $first) }
    if ($failure) {
      $report.problems += $failure
      Write-Step $Ctx $failure 'Red'
      if ($Ctx.WindowId) { Save-Evidence $Ctx 'start' }
      return (Complete-Report $Ctx $report)
    }
    $report.startSeconds = [Math]::Round($Ctx.StartMs / 1000, 1)
    Initialize-Window $Ctx
    Write-Step $Ctx "ready (app '$($Ctx.App)', window $($Ctx.WindowId))" 'Cyan'

    foreach ($page in $Options.Pages) {
      $watch = [Diagnostics.Stopwatch]::StartNew()
      $titles = @($Options.Titles[$page])
      $animal = $Options.FirstAnimal[$page]
      $open = Open-Page $Ctx $page $titles $(if ($animal -and -not $Options.SkipDetails) { $animal } else { $null })
      $entry = [ordered]@{ status = 'fail'; how = $open.How; shown = $open.Shown; ms = 0; detail = $null }
      if (-not $open.Ok) { $entry.detail = "$($open.Detail): $($open.Tried -join ', ')" }
      else {
        $entry.status = 'ok'
        if ($open.Shown -ne $page) { $entry.detail = "a guard sent it to $($open.Shown)" }
        if ($Options.ScanErrors) {
          $snapshot = Get-Snapshot $Ctx
          $errors = @(Find-ErrorText $snapshot $Options.ErrorWords)
          if ($errors.Count) { $entry.status = 'fail'; $entry.detail = "error text on screen: $($errors -join ', ')"; Save-Evidence $Ctx "page-$page" $snapshot }
        }
      }
      $entry.ms = $watch.ElapsedMilliseconds
      $report.pages[$page] = $entry
      Write-Step $Ctx ("{0,-12} {1,-10} {2,6} ms  {3}" -f $page, $entry.status, $entry.ms, $(if ($entry.detail) { $entry.detail } else { "via $($open.How)" })) (Get-StatusColor $entry.status)
      if ($entry.status -eq 'fail') { Save-Evidence $Ctx "page-$page" }

      # A list of animals: its first animal, its detail, and Back.
      if ($animal -and -not $Options.SkipDetails) {
        if ($entry.status -ne 'ok' -or $open.Shown -ne $page) {
          $report.details[$page] = [ordered]@{ status = 'skipped'; animal = $animal; ms = 0; detail = 'the page did not open' }
        } else {
          $detail = Test-Detail $Ctx $animal $open.Item
          $report.details[$page] = $detail
          Write-Step $Ctx ("  detail {0,-21} {1,-10} {2,6} ms  {3}" -f $animal, $detail.status, $detail.ms, $(if ($detail.detail) { $detail.detail } else { 'list -> detail -> Back -> list' })) (Get-StatusColor $detail.status)
        }
      }
    }
    $report.menuActivation = $Ctx.MenuHow

    if (-not $Options.SkipDialogs) {
      $open = Open-Page $Ctx 'Dialogs' @('Dialogs')
      if (-not $open.Ok) { $report.problems += "the Dialogs page could not be opened: $($open.Detail)" }
      else {
        $previous = $null
        foreach ($spec in $Options.Dialogs) {
          $result = Test-Dialog $Ctx $spec $previous
          if ($result.result) { $previous = $result.result }
          $report.dialogs[$spec.Button] = $result
          Write-Step $Ctx ("dialog {0,-26} {1,-10} {2,6} ms  {3}" -f $spec.Button, $result.status, $result.ms, $(if ($result.detail) { $result.detail } else { "$($result.answered) -> $($result.result)" })) (Get-StatusColor $result.status)
        }

        # The notifications, on the same page: those of the application, then the one of the system.
        if ($Options.Notifications) {
          $Ctx.SystemNotificationRequired = [bool]$Options.SystemNotificationRequired
          foreach ($spec in $Options.NotificationSpecs) {
            $result = Test-Notification $Ctx $spec $previous
            if ($result.result) { $previous = $result.result }
            $report.notifications[$spec.Button] = $result
            Write-Step $Ctx ("notification {0,-20} {1,-10} {2,6} ms  {3}" -f $spec.Button, $result.status, $result.ms, $(if ($result.detail) { $result.detail } else { "$($result.answered) -> $($result.result)" })) (Get-StatusColor $result.status)
          }
        }
      }
    }

    $stderr = Join-Path $Ctx.Dir 'stderr.txt'
    if ((Test-Path -LiteralPath $stderr) -and (Select-String -LiteralPath $stderr -Pattern 'Unhandled exception' -SimpleMatch -Quiet)) {
      $report.problems += 'the process wrote an unhandled exception to stderr (see stderr.txt)'
    }
    $failed = @($report.pages.Values | Where-Object { $_.status -eq 'fail' }).Count + @($report.details.Values | Where-Object { $_.status -eq 'fail' }).Count +
              @($report.dialogs.Values | Where-Object { $_.status -eq 'fail' }).Count + @($report.notifications.Values | Where-Object { $_.status -eq 'fail' }).Count + $report.problems.Count
    $report.status = if ($failed) { 'fail' } else { 'ok' }
  } catch {
    $report.problems += "script error: $($_.Exception.Message) at line $($_.InvocationInfo.ScriptLineNumber)"
    Write-Step $Ctx $report.problems[-1] 'Red'
  } finally {
    Stop-App $Ctx.Process
  }
  Complete-Report $Ctx $report
}

# A project made by dotnet new from a template: Hello, World! -> About -> Hello again.
function Invoke-TemplateSmoke([hashtable] $Ctx, [hashtable] $Target, [hashtable] $Options) {
  if (-not $script:AdPath) { Set-AgentDesktopPath $Options.AgentDesktop }
  $Ctx.Clock.Restart()
  $report = [ordered]@{ kind = 'template'; exe = $Target.Exe; folder = $Target.Folder; status = 'fail'; startSeconds = $null; steps = [ordered]@{}; problems = @() }
  if ($Target.Problem) { $report.problems += $Target.Problem; return (Complete-Report $Ctx $report) }
  try {
    Show-HiddenWindow $Ctx
    $failure = Wait-AppReady $Ctx $Options.StartTimeoutSeconds { (Wait-Text $Ctx 'Hello, World!' 300) -eq 'ok' }
    if ($failure) { $report.problems += $failure; Write-Step $Ctx $failure 'Red'; return (Complete-Report $Ctx $report) }
    $report.startSeconds = [Math]::Round($Ctx.StartMs / 1000, 1)
    Initialize-Window $Ctx
    $report.steps['hello'] = 'ok'
    foreach ($step in @(@{ Key = 'about'; Button = 'About'; Text = 'the page is chosen by an address' }, @{ Key = 'back'; Button = 'Hello'; Text = 'Hello, again!' })) {
      $button = Find-All $Ctx -Name $step.Button -Exact | Where-Object { Get-Prop $_ 'ref_id' } | Select-Object -First 1
      if (-not $button) { $report.steps[$step.Key] = "button '$($step.Button)' not found"; break }
      $press = Invoke-ClickMatch $Ctx $button 'click'
      if (-not $press.ok) { $press = Invoke-ClickMatch $Ctx $button 'mouse' }
      $wait = Wait-Text $Ctx $step.Text $Ctx.StepMs
      $report.steps[$step.Key] = if ($wait -eq 'ok') { 'ok' } else { "'$($step.Text)' not shown after '$($step.Button)' ($wait)" }
      Write-Step $Ctx "$($step.Key): $($report.steps[$step.Key])" $(if ($wait -eq 'ok') { 'Green' } else { 'Red' })
      if ($wait -ne 'ok') { Save-Evidence $Ctx "template-$($step.Key)"; break }
    }
    $report.status = if (@($report.steps.Values | Where-Object { $_ -ne 'ok' }).Count -or $report.steps.Count -lt 3) { 'fail' } else { 'ok' }
  } catch {
    $report.problems += "script error: $($_.Exception.Message) at line $($_.InvocationInfo.ScriptLineNumber)"
  } finally {
    Stop-App $Ctx.Process
  }
  Complete-Report $Ctx $report
}

Export-ModuleMember -Function Resolve-AgentDesktop, Set-AgentDesktopPath, Start-Target, Hide-StartedWindows, Hide-Window, Get-WorkArea, Invoke-SampleSmoke, Invoke-TemplateSmoke
