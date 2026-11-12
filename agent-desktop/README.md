# Native smoke test (agent-desktop)

`run.ps1` starts each native sample (WPF, Avalonia, MAUI on Windows, Uno desktop, WinUI 3) and, through the accessibility tree
(https://github.com/vercel-labs/agent-desktop):

1. opens every page of the menu;
2. on the six animal pages, opens the first animal of the list, checks its detail and goes back with **Back**;
3. opens every dialog of the Dialogs page, answers it and checks what the page wrote on its Result line.

```powershell
npm install -g agent-desktop
pwsh agent-desktop/run.ps1 -Build                      # builds what is not built yet, then every host of this OS
pwsh agent-desktop/run.ps1 -Hosts Wpf,Uno              # two hosts
pwsh agent-desktop/run.ps1 -Pages Cats,Dogs -SkipDialogs
```

Needs PowerShell 7.2+ (`pwsh`); started from Windows PowerShell 5.1 (`powershell`), the script runs itself again in `pwsh`. A
script that came in a zip must be unblocked once: `Get-ChildItem agent-desktop -Recurse | Unblock-File`. Windows runs the five
hosts. Linux (a desktop session with `at-spi2-core`; under WSL2, WSLg) and macOS (with the Accessibility permission:
`agent-desktop permissions --guide`) run Avalonia and Uno. agent-desktop must run in the same OS as the application: the Windows
one does not see inside a WSLg window.

**Do not use the mouse while it runs.** Menu entries and list items are clicked physically (see below): the window under test is
brought to the front and maximized, and a click of yours or another window on top of it makes a step fail.

## How it works, and why

What the accessibility trees of the five hosts show:

- a menu entry or an animal is a list item whose accessible name is a class name (`...RouteableMenuEntry`1[...]`,
  `XAnimals.Models.Animal`, `...ItemTemplateContext`) or nothing (WinUI 3); the name a person reads is a text inside it;
- list items that share a name are twins, and agent-desktop refuses every semantic action on a twin on Windows; the semantic click
  it does accept on a WPF or Uno menu entry does not select it either;
- every host shows the title of the current page in a header outside the menu;
- the menu can be hidden: Avalonia's drawer closes after a navigation, the pane of Uno, WinUI 3 and MAUI is an overlay.

So an entry is found by its text (`find` gives the bounds of the text and the path of its ancestors), clicked physically at the
centre of that text, and the page is open when the header shows its title. When the entry is not on screen, the button that opens
the menu is pressed first (Avalonia's *Toggle navigation drawer*, MAUI's *Open navigation*, the `PaneToggleButton` of Uno and
WinUI 3). Physical clicks need the window in front, so the applications all start at once, then are tested one after the other.

Speed: on Windows every walk of the tree costs 0.3 s (WinUI 3) to 1.8 s (Avalonia), whatever is asked, so each step reads the tree
once: one snapshot with bounds proves the page and tells whether a light-dismiss layer is left over it (Avalonia's drawer stays open
after a navigation: a click on that layer, right of the drawer, closes it before anything on the page is clicked); one snapshot
shows the detail and where its Back button is; the buttons of the Dialogs page are read once and clicked at their places; a menu
that hides is opened at its button and its entry clicked at its remembered place, with no look at the menu; the Result line of a
dialog is read in one snapshot for every possible answer (agent-desktop's `wait --text` misses it on Avalonia).

A dialog that is not in the accessibility tree (Uno's ContentDialog, some MAUI dialogs) is answered by keys. The keys that worked
for each one are kept between runs in `%TEMP%\routeables-smoke\dialog-keys.json` and tried first the next time (delete the file
to learn them again).

By default the hosts are tested one after the other, each window maximized. With `-Parallel 2`, two hosts are tested at the same time: the screen is cut in two (top and bottom on a portrait screen, left and
right otherwise), each window fills its half, so that no window lies over another and each takes its own clicks. Reading the
trees, most of the time, runs at the same time; the pointer and the keyboard take turns (a lock), and a step whose clicks belong
together (open the menu then click its entry, open a dialog then answer it) keeps them until its last click. A pane that the other
window's click may have closed is read again before it is closed. The longest hosts start first.

Every window is minimized as soon as it shows and restored on its turn: a window that comes up late, or that Windows does not let
`focus-window` put behind, would otherwise take the physical clicks meant for the one under test.

| Step | Passes when |
| --- | --- |
| Start | the process is on the accessibility bus and its window shows the first page (a process that exits is reported with the end of its stderr) |
| Page | the header shows its title. Members passes when it shows Features: the guard sends a visitor who is signed out there |
| Detail | the first animal of the list opens a page with its name and a Back button, and Back brings the list back |
| Dialog | the Result line shows the text of the answer given (`Answer: Abort.`, `Chose Send.`, `Note cancelled.`) |
| Template | `Hello, World!`, then About shows its text, then Hello shows `Hello, again!` |

## Options

| Option | |
| --- | --- |
| `-Hosts Wpf,Avalonia,Maui,Uno,WinUI3` | default: every host of this OS |
| `-Pages Cats,Dialogs` | a subset of the menu |
| `-SkipDetails`, `-SkipDialogs` | no list -> detail -> Back / no dialogs |
| `-Build` / `-BuildAll` | build the samples that are missing / all of them (incremental). Only the desktop target: `-f net10.0-windows10.0.19041.0` for MAUI, `-p:CiBuild=true` (net10.0-desktop) for Uno, as the CI does |
| `-Parallel 2` | two hosts at the same time, each window on half of the screen (default 1: one after the other, maximized) |
| `-Exe Avalonia=path,Wpf=path` | test this executable (a Release build, a published folder) |
| `-Templates avalonia,maui,uno,winui3` `-PackageSource artifacts` | `dotnet new` outside the repository, build, run. Without `-Hosts`, only the templates |
| `-ScanErrors` | looks for an error text (`Exception`, `Unhandled`, ...) on every page: one full snapshot per page, slow on Windows |
| `-Screenshots` | a PNG next to the snapshot of each failed step |
| `-NoHeaded` | no physical input; the menu and the lists then fail on every host, only the dialogs remain |
| `-StartTimeoutSeconds 90`, `-StepTimeoutMs 4000` | |
| `-AgentDesktop path` | default: the native binary of the global npm package (not the Node launcher) |

## Output

`out/native-smoke/` (at the root of the repository, ignored by git; `-OutDir` changes it):

- `report.json` and `report.md`: one entry per page, per detail and per dialog, with its status, its time and why it failed;
- `<Host>/calls.jsonl`: every agent-desktop call with its duration and result code;
- `<Host>/stdout.txt`, `stderr.txt`: what the application wrote;
- `<Host>/page-*.json`, `detail-*.json`, `back-*.json`, `dialog-*.json`: the snapshot (with bounds) of each failed step;
- `build-*.txt`: the output of `dotnet build`.

The exit code is 0 when everything passed, 1 otherwise. When a step fails, send `report.md` and the folder of that host.

## Everything at once: `run-all.ps1`

`pwsh agent-desktop/run-all.ps1` runs, one after the other (they all take the screen), every test this machine can run:
`run.ps1` on Windows or macOS, `run-wsl.ps1` when WSL is there, `lib/wsl-run.sh` on Linux, `run-android.ps1` when an Android SDK
is found. A suite that cannot run here (no SDK, no WSL) is shown as NOT RUN, not as a failure. The summary is in
`out/all-smoke/summary.md` (and on the page of the run in GitHub Actions); each suite keeps its own report. Options: `-Only
Desktop,Wsl,Linux,Android`, `-Skip ...`, `-Build`, `-Pages`, `-SkipDialogs`, `-SkipDetails`, `-AndroidSerial`, `-Avd`.

## In CI: `ci/native-smoke.yml`

A GitHub Actions workflow to copy to `.github/workflows/`: three jobs, on demand and every night (not on each push: 5 to 20
minutes each, real windows).

- **windows** (windows-latest): WPF, Avalonia, MAUI, Uno, WinUI 3. The hosted Windows runners have an interactive desktop, where
  UI Automation and physical clicks work; the screen is enlarged to 1920x1080 first.
- **linux** (ubuntu-24.04): Avalonia, under Xvfb with openbox and the AT-SPI bus, through `lib/wsl-run.sh`.
- **android** (ubuntu-24.04, KVM): MAUI, Uno, Avalonia in an emulator started by `reactivecircus/android-emulator-runner`.

Each job uploads its report and evidence as an artifact. Not in CI: Uno on Linux (no AT-SPI in Uno yet) and macOS (the
Accessibility permission of agent-desktop cannot be granted on a hosted runner without a screen).

## Linux hosts in WSL: `run-wsl.ps1`

From Windows, `powershell -File agent-desktop/run-wsl.ps1` tests Avalonia on Linux, inside WSL, its window shown through
WSLg. Uno runs on Linux too, but its Skia renderer has no AT-SPI support yet ("planned" in the Uno documentation): agent-desktop
cannot see it there (`-Hosts Avalonia,Uno` tries anyway). It runs `lib/wsl-run.sh` in the distribution, which:

- installs what is missing: the .NET 10 SDK (in `~/.dotnet`), PowerShell (a dotnet tool), Node 22 and agent-desktop (in
  `~/.npm-global`), the AT-SPI bus and the libraries of Avalonia and Uno (apt: `sudo` asks for the password once);
- builds the samples for Linux with `--artifacts-path ~/.cache/routeables-wsl`: the `bin` and `obj` of the repository stay those
  of Windows;
- starts a session bus and the accessibility bus, switches accessibility on, and runs `run.ps1` there.

The report goes to `out/native-smoke-wsl`. Options: `-Distro Ubuntu-24.04`, `-Hosts Uno`, `-Setup` (reinstall the tools),
`-Build` (rebuild), `-Pages`, `-SkipDialogs`, `-SkipDetails`. Needs WSL 2 with WSLg (`wsl --update`) and a distribution
(`wsl --install -d Ubuntu`).

## Android: `run-android.ps1`

`pwsh agent-desktop/run-android.ps1` tests MAUI, Uno and Avalonia on Android, in an emulator or on a connected device.
agent-desktop cannot read Android (the emulator window is a picture to it), so this script drives adb: `uiautomator dump`
gives the accessibility tree of the screen (text, content description, class, bounds of each node), `input tap` clicks.

- **Device**: the one connected (`-Serial`), else an emulator started from the AVD given (`-Avd`) or the first one;
  `-CreateAvd` makes an AVD `routeables` (Android 15, x86_64 or arm64 like the machine) with sdkmanager and avdmanager. An
  emulator started by the script is stopped at the end (`-KeepEmulator` to keep it).
- **Build**: `dotnet build -f net10.0-android` of each sample that has no APK yet (`-Build` to rebuild), then `adb install`.
  Needs the android and maui workloads (`dotnet workload install android maui`).
- **Test**: animations are switched off while it runs (uiautomator waits for an idle screen). The menu is the drawer of the
  toolbar; a drawer left open after a navigation (Avalonia) is closed by a tap on its scrim. A button or a text below the fold
  is scrolled to. Dialogs: the answer is a button of that name, in any case (Android shows "OK", "YES"); a dialog that cannot
  be read is closed with the Back key.
- **Output**: `out/android-smoke/report.json`, `report.md`, a folder per host with `calls.jsonl`, and for each failed step the
  screen as uiautomator saw it (`.xml`) and a screenshot (`.png`); `logcat-errors.txt` when the app did not start.
- `-Density 280` shows more of a page on a phone (`wm density`, reset at the end).

The SDK is found in `-AndroidSdk`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, or where Android Studio or Visual Studio put it.
Without one, `-InstallSdk` installs it (and a JDK) with the `InstallAndroidDependencies` target of .NET for Android, and
`-CreateAvd` then installs the emulator and an Android 15 image: `pwsh agent-desktop/run-android.ps1 -InstallSdk -CreateAvd`.
The emulator needs hardware virtualization (on Windows: the Windows Hypervisor Platform feature).

The Android builds of this script pass `-p:TreatWarningsAsErrors=false`: an analyzer warning must not stop a test.

## macOS

`run.ps1` runs on a Mac as it is: Avalonia and Uno (`pwsh agent-desktop/run.ps1 -Build`), once agent-desktop has the
Accessibility permission (`agent-desktop permissions --guide`). `run-android.ps1` runs there too. A macOS virtual machine on a
PC (OSX-KVM, Sosumi) is not supported here: Apple's licence allows macOS only on Apple hardware.

## Tried, and not yet

Written from the trees of a real run on Windows 11 (agent-desktop 1.2.0, the five hosts). Run on Linux (Ubuntu 24.04, Xvfb, AT-SPI)
against a GTK stand-in shaped like those trees: menu entries named by class or not named at all, a drawer that closes after each
navigation, twin animal rows, the detail and Back, the guard of Members, the dialogs; and the failures: a Back that does nothing, a
dialog that writes no result, an error text, a process that exits, a failed build. Not yet run against the real samples.
