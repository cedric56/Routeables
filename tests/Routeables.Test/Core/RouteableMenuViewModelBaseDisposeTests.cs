using NSubstitute;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Routing;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

public class RouteableMenuViewModelBaseDisposeTests
{
    [Fact]
    public void Disposing_the_main_view_model_removes_its_lock_coordinator_from_the_broker()
    {
        var coordinator = Substitute.For<IRouteableLockCoordinator>();
        var router = Substitute.For<IRouter, IRouteableLockCoordinatorFactory>();
        ((IRouteableLockCoordinatorFactory)router).CreateLockCoordinator(Arg.Any<IRouteableMenuViewModel>()).Returns(coordinator);

        var viewModel = TestNavigableViewModel.Create(router, new ObservableCollection<IRouteableMenuEntry>(), initialize: false);
        coordinator.Received(1).Subscribe();

        viewModel.Dispose();
        viewModel.Dispose();        // twice is harmless

        coordinator.Received(1).Unsubscribe();
    }
}
