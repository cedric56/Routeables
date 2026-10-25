using Routeables.Abstraction;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The Members page: the page <see cref="MembersGuard"/> protects. Signed out, the route to it is turned away and the user lands on the
/// Features page, where they sign in and open this page again. Signing out leaves the page for Features.
/// </summary>
[Routeable("members")]
public class MembersViewModel : ViewModelBase
{
    private readonly IRouter _router;
    private readonly IMainRouteables _main;
    private readonly MemberSession _session;

    public MembersViewModel(IRouter router, IMainRouteables main, MemberSession session)
    {
        _router = router;
        _main = main;
        _session = session;
        SignOutCommand = new AsyncCommand(SignOutAsync);
    }

    public ICommand SignOutCommand { get; }

    /// <summary>What the page says: it can only be shown to a member.</summary>
    public string Welcome => "You are signed in. This page is only shown to members: the route guard turns everyone else away.";

    /// <summary>Signs out and goes back to the Features page, where the guard can be tried again.</summary>
    public async Task SignOutAsync()
    {
        _session.IsSignedIn = false;
        var result = await _router.GoToAsync("/features");

        // The router moved, not the menu: it shows the page that is now current and selects its entry.
        if (result.Success)
            _main.OnRouterMoved(_router.CurrentViewModel);
    }
}
