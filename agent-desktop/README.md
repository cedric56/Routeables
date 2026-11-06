# Native smoke test (agent-desktop)

`run.ps1` starts each native sample (WPF, Avalonia, MAUI on Windows, Uno desktop), opens every page of the menu, presses the
buttons of the Dialogs page and answers the dialogs, through the accessibility tree (https://github.com/vercel-labs/agent-desktop).

```powershell
npm install -g agent-desktop
dotnet build samples/XAnimals.Wpf -c Debug        # and the other samples
pwsh tests/native-smoke/run.ps1 -Hosts Wpf,Avalonia,Maui,Uno
```

It writes `out/native-smoke/report.json` (one line per page and per dialog) and the snapshots of every step. It is not part of the
CI: it needs a desktop session. macOS needs the Accessibility permission (`agent-desktop permissions --guide`), Linux needs a
graphical session with `at-spi2-core`; the paths of the executables at the top of the script are the Windows ones.
