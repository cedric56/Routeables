using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Routeables.Tests.Razor;

public class ViewForTests
{
    private class TestViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class RouteablesViewModel : TestViewModel, IMainViewModel
    {
        public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
        public event EventHandler? LockChanged { add { } remove { } }
        public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
        public object? CurrentView { get; set; }
        public IRouteableEntry? Routeable { get; set; }

        public string? CurrentTitle { get; }
    }

    private sealed class ChildView : ComponentBase
    {
        [Parameter] public object? Context { get; set; }
        [Parameter] public string? Label { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"{Label}:{Context?.GetType().Name}");
        }
    }

    private sealed class TestView : RouteableFor<ChildView, TestViewModel>;

    private sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, Content);
    }

    [Fact]
    public void Context_is_exposed_as_the_typed_view_model()
    {
        var view = new TestView();
        var viewModel = new TestViewModel();

#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
        view.Context = viewModel;
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

        Assert.Same(viewModel, view.ViewModel);
        Assert.Same(viewModel, view.Context);
    }

    [Fact]
    public void IsLocked_is_false_when_the_context_is_not_a_routeables_view_model()
    {
#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
        var view = new TestView { Context = new TestViewModel() };
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

        Assert.False(view.IsLocked);
    }

    [Fact]
    public void IsLocked_requires_at_least_one_routeable_and_all_routeables_to_be_locked()
    {
        var viewModel = new RouteablesViewModel();
#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
        var view = new TestView { Context = viewModel };
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

        Assert.False(view.IsLocked);

        viewModel.Routeables.Add(new RouteableEntry<TestViewModel>("First", "First") { IsLock = true });
        Assert.True(view.IsLocked);

        viewModel.Routeables.Add(new RouteableEntry<TestViewModel>("Second", "Second") { IsLock = false });
        Assert.False(view.IsLocked);
    }

    [Fact]
    public async Task Render_passes_context_and_dictionary_parameters_to_the_child_component()
    {
        var viewModel = new TestViewModel();
#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
        var view = new TestView
        {
            Context = viewModel,
            DictionaryParameters = new RouteablesParameters { ["Label"] = "Rendered" }
        };
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<FragmentHost>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(FragmentHost.Content)] = view.Render() }));
            return rendered.ToHtmlString();
        });

        Assert.Equal("Rendered:TestViewModel", html);
    }
}
