using NSubstitute;
using Routeables.Abstraction;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

public class RouteableViewModelBaseDisposeTests
{
    [Fact]
    public void Disposing_the_main_view_model_removes_its_lock_coordinator_from_the_broker()
    {
        var coordinator = Substitute.For<ILockCoordinator>();
        var router = Substitute.For<IRouter, ILockCoordinatorFactory>();
        ((ILockCoordinatorFactory)router).CreateLockCoordinator(Arg.Any<IMainViewModel>()).Returns(coordinator);

        var viewModel = TestNavigableViewModel.Create(router, new ObservableCollection<IRouteableEntry>(), initialize: false);
        coordinator.Received(1).Subscribe();

        viewModel.Dispose();
        viewModel.Dispose();        // twice is harmless

        coordinator.Received(1).Unsubscribe();
    }
}
