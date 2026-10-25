using Routeables.Abstraction;

namespace XAnimals.ViewModels;

/// <summary>Whether the user of the sample is signed in. The Members page is for members: <see cref="MembersGuard"/> asks.</summary>
public sealed class MemberSession
{
    public bool IsSignedIn { get; set; }
}

/// <summary>
/// A route guard (<see cref="IRouteGuard"/>): a rule about the page that is entered, written once for the whole application. Entering
/// the Members page while signed out sends the user to the Features page.
/// </summary>
public sealed class MembersGuard(MemberSession session) : IRouteGuard
{
    public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken)
    {
        var isMembers = change.To.ViewModel is MembersViewModel || change.To.Args.Route?.Trim('/') == "members";
        return Task.FromResult(isMembers && !session.IsSignedIn
            ? RouteGuardDecision.Redirect("features")
            : RouteGuardDecision.Allow);
    }
}
