# Native smoke test (agent-desktop)

`run.ps1` starts each native sample (WPF, Avalonia, MAUI on Windows, Uno desktop, WinUI 3), opens every page of the menu, presses the
buttons of the Dialogs page and answers the dialogs, through the accessibility tree (https://github.com/vercel-labs/agent-desktop).

```powershell
npm install -g agent-desktop
dotnet build samples/XAnimals.Wpf -c Debug        # and the other samples
pwsh agent-desktop/run.ps1 -Hosts Wpf,Avalonia,Maui,Uno,WinUI3
pwsh agent-desktop/run.ps1 -Build          # builds the samples that are not built yet
```

A host whose executable is not found is reported in `report.json` (`problems`) with the folder searched; the script looks for the newest `<App>.exe` under the `bin` folder of the sample, so another target framework is found too.

It writes `out/native-smoke/report.json` (the `out/` folder at the root of the repository, ignored by git, whatever the folder the script is started from; `-OutDir` changes it) (one line per page and per dialog) and the snapshots of every step. It is not part of the
CI: it needs a desktop session. macOS needs the Accessibility permission (`agent-desktop permissions --guide`), Linux needs a
graphical session with `at-spi2-core`; the paths of the executables at the top of the script are the Windows ones.

## The templates

```powershell
pwsh agent-desktop/run.ps1 -Templates avalonia,maui,uno,winui3 -PackageSource artifacts
```

For each template: `dotnet new` (installed from `templates/`) into a temporary folder **outside the repository** (under it, the application would inherit `Directory.Build.props`, the central package versions and `global.json`, which a user's application does not have), build in Debug, start, wait for `Hello, World!`, press **About** (the page must show
its text), press **Hello** (`Hello, again!`). The report has a `templates` section with the result of each step, and `log/` holds the output of
`dotnet new` and of the build: send that when a template does not build. The packages the templates reference (`Routeables.Maui` and so on, at the
version of `Directory.Build.props`) must be restorable: published, or in the folder given with `-PackageSource` (`dotnet pack`, then that folder).
Without `-Hosts` the samples are not run when `-Templates` is given. Like the rest of the script, it has not been run.
