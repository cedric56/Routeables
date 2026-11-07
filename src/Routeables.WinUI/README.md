# Routeables.WinUI

Not a project and not a package: the **sources** that `Routeables.Uno` and `Routeables.WinUI3` both compile. Uno and WinUI 3 expose the same `Microsoft.UI.Xaml` API (the WinUI API), but through different assemblies, so each host builds these files itself instead of referencing the other.

- In Visual Studio they appear in the `Routeables.WinUI` folder of each of the two projects (linked files: editing one edits the real file here).
- Edit a view, the region, the menu, the dialogs here, once.
- What differs between the two hosts is inside `#if WINUI3` blocks (the Windows App SDK host); the public namespace is `Routeables.WinUI3` when it is defined, `Routeables.Uno` otherwise.
- What belongs to one host only stays in its project (`BackRequests.cs` in `Routeables.Uno`).
