using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>Routing that hands typed data to the destination (<c>GoToRouteAsync&lt;TViewModel, TData&gt;</c>).</summary>
public class RouterTypedExtensionsTests
{
    public sealed record Animal(string Name);

    public sealed class AnimalView : IRouteableFor, IInitializeAsync<Animal>
    {
        public object? Context { get; set; }
        public Animal? Received { get; private set; }
        public Task InitializeAsync(Animal data, CancellationToken ct = default) { Received = data; return Task.CompletedTask; }
    }

    public sealed class AnimalVm : IInitializeAsync, IInitializeAsync<Animal>
    {
        public List<string> Calls { get; } = [];
        public Animal? Received { get; private set; }
        public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default) { Calls.Add("args"); return Task.CompletedTask; }
        public Task InitializeAsync(Animal data, CancellationToken ct = default) { Calls.Add("data"); Received = data; return Task.CompletedTask; }
    }

    public sealed class AnythingVm : IInitializeAsync<object>
    {
        public object? Received { get; private set; }
        public Task InitializeAsync(object data, CancellationToken ct = default) { Received = data; return Task.CompletedTask; }
    }

    /// <summary>Takes nothing in its constructor: a data carrier handed to it would be refused.</summary>
    public sealed class PlainVm : IInitializeAsync<string>
    {
        public string? Received { get; private set; }
        public Task InitializeAsync(string data, CancellationToken ct = default) { Received = data; return Task.CompletedTask; }
    }

    public sealed class PlainView : IRouteableFor { public object? Context { get; set; } }

    private static IRouter Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<IRouter>();
    }

    [Fact]
    public async Task The_view_model_and_the_view_receive_the_data_after_the_routing_arguments()
    {
        var router = Build(s => s.RegisterRoute<AnimalView, AnimalVm>(route: "/animal"));
        var cat = new Animal("Tom");

        var result = await router.GoToRouteAsync<AnimalVm, Animal>("/animal", cat, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        var viewModel = (AnimalVm)router.CurrentViewModel!;
        Assert.Equal(["args", "data"], viewModel.Calls);
        Assert.Same(cat, viewModel.Received);
        Assert.Same(cat, ((AnimalView)router.CurrentView!).Received);
    }

    [Fact]
    public async Task A_view_model_that_takes_object_accepts_any_data()
    {
        var router = Build(s => s.RegisterRoute<PlainView, AnythingVm>(route: "/any"));

        await router.GoToRouteAsync<AnythingVm, object>("/any", "text", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("text", ((AnythingVm)router.CurrentViewModel!).Received);
    }

    [Fact]
    public async Task The_data_is_not_a_constructor_argument_of_the_view_model()
    {
        var router = Build(s => s.RegisterRoute<PlainView, PlainVm>(route: "/plain"));

        var result = await router.GoToRouteAsync<PlainVm, string>("/plain", "hello", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal("hello", ((PlainVm)router.CurrentViewModel!).Received);
    }

    [Fact]
    public async Task A_routing_without_data_leaves_the_typed_initializer_alone()
    {
        var router = Build(s => s.RegisterRoute<PlainView, PlainVm>(route: "/plain"));

        await router.GoToAsync("/plain", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(((PlainVm)router.CurrentViewModel!).Received);
    }

    [Fact]
    public async Task A_null_router_is_refused()
        => await Assert.ThrowsAsync<ArgumentNullException>(() =>
            RouterTypedExtensions.GoToRouteAsync<PlainVm, string>(null!, "/plain", "x", cancellationToken: TestContext.Current.CancellationToken));
}
