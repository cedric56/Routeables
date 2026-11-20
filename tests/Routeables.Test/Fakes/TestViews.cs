using Routeables.Abstraction.Bases;

namespace Routeables.Tests.Pages;

/// <summary>
/// A trivial <see cref="IRouteableFor"/> used to stand in for a resolved view in tests.
/// </summary>
public class FakeViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class SecondViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class ThirdViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class FourthViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class FifthViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class SixthViewFor : IRouteableFor
{
    public object? Context { get; set; }
}

public class FakeDetailViewFor : IRouteableFor
{
    public object? Context { get; set; }
}
