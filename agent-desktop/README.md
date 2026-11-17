# Native smoke test (agent-desktop)

`run.ps1` starts each native sample (WPF, Avalonia, MAUI on Windows, Uno desktop, WinUI 3) and, through the accessibility tree
(https://github.com/vercel-labs/agent-desktop):

1. opens every page of the menu;
2. on the six animal pages, opens the first animal, checks its detail and goes back with **Back**;
3. opens every dialog of the Dialogs page, answers it and checks the Result line.

With `-Notifications` it also shows each notification of the Dialogs page and clicks it (the Result line says "Clicked ..."); the
"System" one must write "System notification sent." (`-RequireSystemNotification`: the host must have a notification center). On
macOS, `-Bundle` runs the sample from a signed `.app` made in the temp folder: the system serves notifications only to an
application in a bundle, and asks for the permission the first time (the smoke presses Allow).

```powershell
npm install -g agent-desktop@1.3.1
pwsh agent-desktop/run.ps1 -Build                      # builds what is missing, then every host of this OS
pwsh agent-desktop/run.ps1 -Hosts Wpf,Uno              # two hosts
pwsh agent-desktop/run.ps1 -Pages Cats,Dogs -SkipDialogs
```

Needs PowerShell 7.2+ (`pwsh`; started from Windows PowerShell 5.1 the script runs itself again in `pwsh`). A script that came in
a zip must be unblocked once: `Get-ChildItem agent-desktop -Recurse | Unblock-File`. Windows runs the five hosts; Linux (a desktop
session with `at-spi2-core`, or WSLg) and macOS (with the Accessibility permission: `agent-desktop permissions --guide`) run
Avalonia and Uno. agent-desktop must run in the same OS as the application.

**Do not use the mouse while it runs:** menu entries and list items are clicked physically, in a window brought to the front and
maximized; a click of yours, or a window on top of it, makes a step fail.

## How it works

What the accessibility trees show: a menu entry or an animal is a list item named by a class name (`...RouteableMenuEntry`1[...]`,
`XAnimals.Models.Animal`) or by nothing (WinUI 3), and the name a person reads is a text inside it; items that share a name are
twins, on which agent-desktop refuses every semantic action on Windows (and its semantic click on a WPF or Uno entry does not
select it); the title of the current page is in a header outside the menu; the menu can be hidden (Avalonia's drawer closes after a
navigation, the pane of Uno, WinUI 3 and MAUI is an overlay).

So an entry is found by its text, clicked physically at the centre of that text, and the page is open when the header shows its
title. When the entry is not on screen, the button that opens the menu is pressed first (Avalonia's *Toggle navigation drawer*,
MAUI's *Open navigation*, the `PaneToggleButton` of Uno and WinUI 3). The applications all start at once, then are tested one after
the other.

Every walk of the tree costs 0.3 s (WinUI 3) to 1.8 s (Avalonia) on Windows, so a step reads it once: one snapshot proves the page
and shows a leftover light-dismiss layer (Avalonia's open drawer: a click right of it closes it first); one shows the detail and its
Back button; the buttons of the Dialogs page are read once and clicked at their places; the Result line is read in one snapshot for
every possible answer (`wait --text` misses it on Avalonia).

A dialog that is not in the tree (Uno's ContentDialog, some MAUI dialogs) is answered by keys. The keys that worked are kept in
`%TEMP%\routeables-smoke\dialog-keys.json` and tried first next time (delete the file to learn them again).

With `-Parallel 2` two hosts run at once, each window filling half of the screen (top and bottom on a portrait screen, left and
right otherwise). Trees are read at the same time; the pointer and keyboard take turns, and the clicks of one step (open the menu
then click its entry, open a dialog then answer it) are kept together. Every window is minimized as soon as it shows and restored
on its turn, so that a late window does not take the clicks meant for another.

| Step | Passes when |
| --- | --- |
| Start | the process is on the accessibility bus and its window shows the first page (a process that exits is reported with the end of its stderr) |
| Page | the header shows its title. Members passes when it shows Features: the guard sends a visitor who is signed out there |
| Detail | the first animal opens a page with its name and a Back button, and Back brings the list back |
| Dialog | the Result line shows the text of the answer given (`Answer: Abort.`, `Chose Send.`, `Note cancelled.`) |
| Template | `Hello, World!`, then About shows its text, then Hello shows `Hello, again!` |

## Options

| Option | |
| --- | --- |
| `-Hosts Wpf,Avalonia,Maui,Uno,WinUI3` | default: every host of this OS |
| `-Pages Cats,Dialogs` | a subset of the menu |
| `-SkipDetails`, `-SkipDialogs` | no list -> detail -> Back / no dialogs |
| `-Build` / `-BuildAll` | build the samples that are missing / all of them, desktop target only (`-f net10.0-windows10.0.19041.0` for MAUI, `-p:CiBuild=true` for Uno, as the CI does) |
| `-Parallel 2` | two hosts at the same time (default 1: one after the other, maximized) |
| `-Exe Avalonia=path,Wpf=path` | test this executable (a Release build, a published folder) |
| `-Templates avalonia,maui,uno,winui3` `-PackageSource artifacts` | `dotnet new` outside the repository, build, run. Without `-Hosts`, only the templates |
| `-ScanErrors` | looks for an error text (`Exception`, `Unhandled`, ...) on every page: one full snapshot per page, slow on Windows |
| `-Screenshots` | a PNG next to the snapshot of each failed step |
| `-NoHeaded` | no physical input: the menu and the lists fail on every host, only the dialogs remain |
| `-StartTimeoutSeconds 90`, `-StepTimeoutMs 4000` | timeouts |
| `-AgentDesktop path` | default: the native binary of the global npm package (not the Node launcher) |

## Output

`out/native-smoke/` (ignored by git; `-OutDir` changes it):

- `report.json`, `report.md`: one entry per page, detail and dialog, with its status, time and the reason of a failure;
- `<Host>/calls.jsonl`: every agent-desktop call with its duration and result code;
- `<Host>/stdout.txt`, `stderr.txt`: what the application wrote;
- `<Host>/page-*.json`, `detail-*.json`, `back-*.json`, `dialog-*.json`: the snapshot (with bounds) of each failed step;
- `build-*.txt`: the output of `dotnet build`.

The exit code is 0 when everything passed, 1 otherwise. When a step fails, send `report.md` and the folder of that host.

## Everything at once: `run-all.ps1`

`pwsh agent-desktop/run-all.ps1` runs, one after the other, every test this machine can run: `run.ps1` on Windows or macOS,
`run-wsl.ps1` when WSL is there, `lib/wsl-run.sh` on Linux, `run-android.ps1` when an Android SDK is found. A suite that cannot run
is shown as NOT RUN, not as a failure. The summary is in `out/all-smoke/summary.md`. Options: `-Only Desktop,Wsl,Linux,Android`,
`-Skip ...`, `-Build`, `-Pages`, `-SkipDialogs`, `-SkipDetails`, `-AndroidSerial`, `-Avd`.

## In CI: `ci/native-smoke.yml`

A GitHub Actions workflow to copy to `.github/workflows/`. It runs on demand and every night (not on each push: 5 to 20 minutes
per job, real windows), one job per platform:

- **windows** (windows-latest): WPF, Avalonia, MAUI, Uno, WinUI 3, on the interactive desktop of the hosted runner (enlarged to
  1920x1080 first).
- **linux** (ubuntu-24.04): Avalonia, under Xvfb with openbox and the AT-SPI bus, through `lib/wsl-run.sh`.
- **macos** (macos-15, Apple Silicon): Avalonia with `run.ps1`. Not Uno: on macOS its accessibility tree empties after the first
  page change. The runner's agent already holds the Accessibility permission; a step checks it (`agent-desktop permissions`) and
  stops with the reason if it is missing.
- **ios** (macos-latest, Xcode 26): MAUI and Avalonia in the iOS Simulator with `run-ios.ps1` (idb).
- **android** (ubuntu-24.04, KVM): MAUI and Avalonia in an emulator (`reactivecircus/android-emulator-runner`). Not Uno yet: its
  Android app exposes no text to uiautomator.

Each job uploads its report and evidence as an artifact. Not in CI: Uno on Linux (no AT-SPI in Uno yet). The linux and macos jobs
do not fail the workflow until they have passed once on a hosted runner (`continue-on-error`).

## Linux hosts in WSL: `run-wsl.ps1`

`powershell -File agent-desktop/run-wsl.ps1` tests Avalonia on Linux inside WSL, its window shown through WSLg. Uno runs there too,
but its Skia renderer has no AT-SPI support yet, so agent-desktop cannot see it (`-Hosts Avalonia,Uno` tries anyway). It runs
`lib/wsl-run.sh` in the distribution, which:

- installs what is missing: the .NET 10 SDK (`~/.dotnet`), PowerShell (a dotnet tool), Node 22 and agent-desktop (`~/.npm-global`),
  the AT-SPI bus and the libraries of Avalonia and Uno (apt: `sudo` asks for the password once);
- builds the samples for Linux with `--artifacts-path ~/.cache/routeables-wsl`, so the `bin` and `obj` of the repository stay those
  of Windows;
- starts a session bus and the accessibility bus, switches accessibility on, and runs `run.ps1` there;
- uses an X server of its own: Xephyr (one window on the WSLg desktop, openbox inside; `-Display xvfb` shows nothing). Not the
  display of WSLg: its window manager keeps no `_NET_CLIENT_LIST`, so agent-desktop cannot tie an app to its window, and its
  Wayland side has no physical input.

**agent-desktop 1.2.0 and the X display.** Under WSLg `/tmp/.X11-unix` is read-only, so the test's X server can only listen on the
abstract socket, to which agent-desktop 1.2.0 cannot connect (its probe accepts it, its connection does not): every window came
back `native_window_unproven`. The fault is not Avalonia's. `lib/wsl-run.sh` therefore also starts the X server on TCP, protected
by an xauth cookie (no `-ac`), and sets `DISPLAY=127.0.0.1:N`. A fix for agent-desktop itself is proposed separately
(abstract-socket support in `crates/linux/src/system/x11/bounded.rs`). If the test still reports `native_window_unproven`, it says
so after 10 s instead of waiting 90 s.

`-Diagnose` runs no test: it starts each app alone and writes what the accessibility bus and agent-desktop see of it to
`out/native-smoke-wsl/diagnose.txt`. The report goes to `out/native-smoke-wsl`. Other options: `-Distro Ubuntu-24.04`, `-Hosts Uno`,
`-Setup` (reinstall the tools), `-Build`, `-Pages`, `-SkipDialogs`, `-SkipDetails`. Needs WSL 2 with WSLg (`wsl --update`) and a
distribution (`wsl --install -d Ubuntu`).

## Android: `run-android.ps1`

`pwsh agent-desktop/run-android.ps1` tests MAUI, Uno and Avalonia on Android, in an emulator or on a connected device.
agent-desktop cannot read Android (the emulator window is a picture to it), so the script drives adb: `uiautomator dump` gives the
accessibility tree (text, content description, class, bounds), `input tap` clicks.

- **Device**: the one connected (`-Serial`), else an emulator started from `-Avd` or the first AVD; `-CreateAvd` makes an AVD
  `routeables` (Android 15, x86_64 or arm64 like the machine). An emulator started by the script is stopped at the end
  (`-KeepEmulator` keeps it).
- **Build**: `dotnet build -f net10.0-android` of each sample without an APK (`-Build` rebuilds), then `adb install`. Needs the
  android and maui workloads (`dotnet workload install android maui`). The builds pass `-p:TreatWarningsAsErrors=false`: an analyzer
  warning must not stop a test.
- **Test**: animations are switched off (uiautomator waits for an idle screen). The menu is the drawer of the toolbar; an Avalonia
  drawer left open is closed by a tap on its scrim. A button or text below the fold is scrolled to. A dialog answer is a button of
  that name, in any case (Android shows "OK", "YES"); a dialog that cannot be read is closed with the Back key. `-Density 280`
  shows more of a page on a phone (reset at the end).
- **Output**: `out/android-smoke/report.json`, `report.md`, a folder per host with `calls.jsonl`, and for each failed step the
  screen as uiautomator saw it (`.xml`) and a screenshot (`.png`); `logcat-errors.txt` when the app did not start.

The SDK is found in `-AndroidSdk`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, or where Android Studio or Visual Studio put it. Without
one, `-InstallSdk` installs it (and a JDK) with the `InstallAndroidDependencies` target of .NET for Android, and `-CreateAvd` then
installs the emulator and an Android 15 image: `pwsh agent-desktop/run-android.ps1 -InstallSdk -CreateAvd`. The emulator needs
hardware virtualization (on Windows: the Windows Hypervisor Platform feature).

## macOS

`run.ps1` runs on a Mac as it is (Avalonia and Uno: `pwsh agent-desktop/run.ps1 -Build`), once agent-desktop has the Accessibility
permission (`agent-desktop permissions --guide`); so does `run-android.ps1`. A macOS virtual machine on a PC (OSX-KVM, Sosumi) is
not supported: Apple's licence allows macOS only on Apple hardware.

## iOS: `run-ios.ps1`

MAUI and Avalonia in the iOS Simulator, on a Mac: `pwsh agent-desktop/run-ios.ps1 -Setup -Build`. The test is the one of Android
(the same module, `lib/Android.Smoke.psm1`, with another backend). agent-desktop cannot read the Simulator (its window is one
picture to macOS accessibility), so the screen is read with idb (`idb ui describe-all`: the accessibility tree of the app in front,
in points) and tapped with `idb ui tap`; the app is installed, started and stopped with `xcrun simctl`.

- `-Setup` installs idb when missing: `idb-companion` with Homebrew, `fb-idb` with pip.
- Simulator: the one booted, else an iPhone of the newest iOS runtime (`-Device`, `-Udid`); shut down at the end when the script
  booted it.
- Each sample is built unsigned for the simulator of this Mac (`-r iossimulator-arm64` or `-x64`) and installed with simctl.
- A system alert asking for a permission (notifications) is allowed when it shows.
- Not Uno: its iOS accessibility with the Skia renderer is not done yet (no text would be exposed).
- Output: `out/ios-smoke` (report; for a failed step the tree as idb read it, `.json`, and a screenshot), zipped in
  `out/ios-smoke.zip`; `log-app.txt` (the unified log of the app and its crash report) when a host failed.

In CI: the `ios` job (Xcode 26, its iOS runtime, an iPhone booted as in the `device-apple` job of `ci.yml`).

## Tried, and not yet

Written from the trees of a real run on Windows 11 (agent-desktop 1.2.0, the five hosts). Run on Linux (Ubuntu 24.04, Xvfb, AT-SPI)
against a GTK stand-in shaped like those trees (entries named by class or not at all, a drawer that closes after each navigation,
twin animal rows, the detail and Back, the guard of Members, the dialogs) and its failures (a Back that does nothing, a dialog that
writes no result, an error text, a process that exits, a failed build). Not yet run against the real samples.
