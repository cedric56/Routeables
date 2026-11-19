using Routeables.Abstraction.Bases;

namespace Routeables.Tests.Pages;

/// <summary>
/// A trivial <see cref="IRouteableFor"/> used to stand in for a resolved view in tests.
/// </summary>
public class FakeViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class OtherFakeViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

/// <summary>
/// A fake modal view used to test modal resolution.
/// </summary>
public class FakeModalFor<TViewModel, TResult> : IRouteableModalFor<TViewModel, TResult>
    where TViewModel : class, ICloseableViewModel<TResult>
{
    public object? Context { get; set; }

    public Task<TResult?> ShowModalAsync()
    {
        throw new NotImplementedException();
    }
}


/// <summary>The views of the tests: a view that holds nothing but the context the router gives it. A test names the one it needs; none declares its own.</summary>
public sealed class BlockedView : IRouteableFor { public object? Context { get; set; } }
public sealed class BoomView : IRouteableFor { public object? Context { get; set; } }
public sealed class CtBoomView : IRouteableFor { public object? Context { get; set; } }
public sealed class CtHomeView : IRouteableFor { public object? Context { get; set; } }
public sealed class CustomerView : IRouteableFor { public object? Context { get; set; } }
public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
public sealed class DirtyView : IRouteableFor { public object? Context { get; set; } }
public sealed class DocsView : IRouteableFor { public object? Context { get; set; } }
public sealed class FormView : IRouteableFor { public object? Context { get; set; } }
public sealed class GuardAdminView : IRouteableFor { public object? Context { get; set; } }
public sealed class GuardHomeView : IRouteableFor { public object? Context { get; set; } }
public sealed class GuardLoginView : IRouteableFor { public object? Context { get; set; } }
public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
public sealed class IntroView : IRouteableFor { public object? Context { get; set; } }
public sealed class IsoView : IRouteableFor { public object? Context { get; set; } }
public sealed class ListView : IRouteableFor { public object? Context { get; set; } }
public sealed class LoginView : IRouteableFor { public object? Context { get; set; } }
public sealed class MbAView : IRouteableFor { public object? Context { get; set; } }
public sealed class MbBView : IRouteableFor { public object? Context { get; set; } }
public sealed class MbCView : IRouteableFor { public object? Context { get; set; } }
public sealed class MeterItemView : IRouteableFor { public object? Context { get; set; } }
public sealed class MissingView : IRouteableFor { public object? Context { get; set; } }
public sealed class MsLoginView : IRouteableFor { public object? Context { get; set; } }
public sealed class MsOtherView : IRouteableFor { public object? Context { get; set; } }
public sealed class MsProductView : IRouteableFor { public object? Context { get; set; } }
public sealed class MsSecretView : IRouteableFor { public object? Context { get; set; } }
public sealed class NamedView : IRouteableFor { public object? Context { get; set; } }
public sealed class NotFoundView : IRouteableFor { public object? Context { get; set; } }
public sealed class OrdersView : IRouteableFor { public object? Context { get; set; } }
public sealed class OrphanView : IRouteableFor { public object? Context { get; set; } }
public sealed class OtherView : IRouteableFor { public object? Context { get; set; } }
public sealed class PageView : IRouteableFor { public object? Context { get; set; } }
public sealed class ProbeView : IRouteableFor { public object? Context { get; set; } }
public sealed class ProductView : IRouteableFor { public object? Context { get; set; } }
public sealed class RootView : IRouteableFor { public object? Context { get; set; } }
public sealed class SaProductView : IRouteableFor { public object? Context { get; set; } }
public sealed class SegBlockedView : IRouteableFor { public object? Context { get; set; } }
public sealed class SettingsView : IRouteableFor { public object? Context { get; set; } }
public sealed class SideView : IRouteableFor { public object? Context { get; set; } }
public sealed class SlugView : IRouteableFor { public object? Context { get; set; } }
public sealed class SoHomeView : IRouteableFor { public object? Context { get; set; } }
public sealed class SoLinkView : IRouteableFor { public object? Context { get; set; } }
public sealed class SoOldView : IRouteableFor { public object? Context { get; set; } }
public sealed class SoloView : IRouteableFor { public object? Context { get; set; } }
public sealed class StayView : IRouteableFor { public object? Context { get; set; } }
public sealed class StubView : IRouteableFor { public object? Context { get; set; } }
public sealed class TabView : IRouteableFor { public object? Context { get; set; } }
public sealed class TgHomeView : IRouteableFor { public object? Context { get; set; } }
public sealed class TgSecretView : IRouteableFor { public object? Context { get; set; } }
public sealed class ThingView : IRouteableFor { public object? Context { get; set; } }
public sealed class ThrowingView : IRouteableFor { public object? Context { get; set; } }
public sealed class UnbuildableView : IRouteableFor { public object? Context { get; set; } }
public sealed class UnroutedView : IRouteableFor { public object? Context { get; set; } }
