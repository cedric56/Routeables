using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Razor.Services;

namespace Routeables.Tests.Razor;

public class ModalServiceTests
{
    private sealed class ModalVm(string title) : ICloseableViewModel<string>
    {
        public string Title { get; } = title;
        public ObservableCollection<RouteableCommand> Commands { get; } = [];
        public ICommand? CloseCommand => null;
        public event EventHandler<string?>? CloseRequested;
        public int Subscribers => CloseRequested?.GetInvocationList().Length ?? 0;
        public Task CloseAsync(string? value) { CloseRequested?.Invoke(this, value); return Task.CompletedTask; }
    }

    private sealed class ModalProbe : ComponentBase
    {
        [Parameter] public object? Context { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "probe");
            builder.AddContent(2, "probe for " + ((ModalVm)Context!).Title);
            builder.CloseElement();
        }
    }

    private static Task<RazorHostHarness<RouteableModalHost>> StartAsync() =>
        RazorHostHarness<RouteableModalHost>.StartAsync(services => services.AddScoped<RouteableModalService>());

    // Context is a [Parameter]; setting it directly is the only way to build the component without a renderer.
#pragma warning disable BL0005
    private static RouteableModalFor<ModalProbe, ModalVm, string> CreateModal(RouteableModalService service, ModalVm vm) =>
        new() { Modals = service, Context = vm };
#pragma warning restore BL0005

    [Fact]
    public async Task ShowModalAsync_renders_the_view_with_its_title_and_returns_the_result_on_close()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableModalService>();
        var vm = new ModalVm("Edit <animal>");

        var task = CreateModal(service, vm).ShowModalAsync();
        var html = await host.HtmlAsync();

        Assert.Contains("Edit &lt;animal&gt;", html);
        Assert.Contains("probe for Edit &lt;animal&gt;", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.False(task.IsCompleted);

        await vm.CloseAsync("saved");

        Assert.Equal("saved", await task);
        Assert.DoesNotContain("rt-modal", await host.HtmlAsync());
    }

    [Fact]
    public async Task ShowModalAsync_returns_the_default_value_when_the_view_model_closes_with_null()
    {
        await using var host = await StartAsync();
        var vm = new ModalVm("T");

        var task = CreateModal(host.Get<RouteableModalService>(), vm).ShowModalAsync();
        await vm.CloseAsync(null);

        Assert.Null(await task);
    }

    [Fact]
    public async Task The_close_subscription_is_removed_once_the_modal_is_closed()
    {
        await using var host = await StartAsync();
        var vm = new ModalVm("T");

        var task = CreateModal(host.Get<RouteableModalService>(), vm).ShowModalAsync();
        Assert.Equal(1, vm.Subscribers);
        await vm.CloseAsync("x");
        await task;

        Assert.Equal(0, vm.Subscribers);
    }

    [Fact]
    public async Task A_modal_opened_over_another_is_stacked_and_the_one_underneath_stays_open()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableModalService>();
        var first = new ModalVm("First");
        var second = new ModalVm("Second");

        var firstTask = CreateModal(service, first).ShowModalAsync();
        var secondTask = CreateModal(service, second).ShowModalAsync();
        var html = await host.HtmlAsync();

        Assert.Equal(2, service.Items.Count);
        Assert.Contains("probe for First", html);
        Assert.Contains("probe for Second", html);
        Assert.Contains("z-index: 2000", html);
        Assert.Contains("z-index: 2001", html);

        await second.CloseAsync("b");
        Assert.Equal("b", await secondTask);
        Assert.Single(service.Items);
        Assert.False(firstTask.IsCompleted);

        await first.CloseAsync("a");
        Assert.Equal("a", await firstTask);
        Assert.Empty(service.Items);
    }

    [Fact]
    public async Task Showing_a_modal_without_a_host_fails_and_leaves_no_subscription_behind()
    {
        var service = new RouteableModalService();
        var vm = new ModalVm("T");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateModal(service, vm).ShowModalAsync());

        Assert.Contains("RouteableModalHost", exception.Message);
        Assert.Equal(0, vm.Subscribers);
    }

    [Fact]
    public async Task Closing_the_same_modal_twice_is_harmless()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableModalService>();
        var changes = 0;
        service.Changed += () => changes++;

        var request = service.Show("T", _ => { });
        service.Close(request);
        service.Close(request);

        Assert.Empty(service.Items);
        Assert.Equal(2, changes);
    }
}
