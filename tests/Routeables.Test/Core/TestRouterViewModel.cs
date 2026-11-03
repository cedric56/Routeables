using Routeables.Abstraction;
using Routeables.Core;

namespace Routeables.Tests.Core;

/// <summary>
/// Concrete <see cref="RouterViewModelBase"/> that records when its protected hooks are invoked,
/// so tests can assert on <see cref="RouterViewModelBase.GoBackAsync"/>/<see cref="RouterViewModelBase.GoForwardAsync"/>
/// behavior without depending on CommunityToolkit.Mvvm.
/// </summary>
public class TestRouterViewModel(IRouter router) : RouterViewModelBase(router)
{
    public int RaiseCanExecutesChangedCallCount { get; private set; }

    public object? LastViewModelChanged { get; private set; }

    protected override void RaiseCanExecutesChanged()
    {
        RaiseCanExecutesChangedCallCount++;
    }

    protected override void OnViewModelChanged(object? viewModel)
    {
        LastViewModelChanged = viewModel;
        base.OnViewModelChanged(viewModel);
    }
}
