using Routeables.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>
/// A trivial <see cref="IRouteableFor"/> used to stand in for a resolved view in tests.
/// </summary>
public class FakeViewFor : IRouteableFor
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
