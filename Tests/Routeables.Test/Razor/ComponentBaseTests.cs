using Routeables.Abstraction;
using Routeables.Razor;
using System.ComponentModel;

namespace Routeables.Tests.Razor;

public class ComponentBaseTests
{
    private sealed class TestViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class ChildView : Microsoft.AspNetCore.Components.ComponentBase;
    private sealed class TestView : RouteableFor<ChildView, TestViewModel>;

    [Fact]
    public void Generic_view_uses_context_as_its_typed_view_model()
    {
        var view = new TestView();
        var viewModel = new TestViewModel();

#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
        view.Context = viewModel;
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

        Assert.Same(viewModel, view.ViewModel);
    }

    [Fact]
    public void Centered_view_supplies_the_center_class()
    {
        var view = new RouteableCenteredFor<ChildView, TestViewModel>();

        Assert.Equal("center", view.Class);
    }
}
