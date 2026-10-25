using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public sealed class PickerView : IRouteableFor { public object? Context { get; set; } }
public sealed class PickerVm : IReturnsResult<string>
{
    private string? _picked;
    public void Pick(string value) => _picked = value;
    public bool TryGetResult(out string result) { result = _picked ?? string.Empty; return _picked is not null; }
}
public sealed class ChildView : IRouteableFor { public object? Context { get; set; } }
public sealed class ChildVm;

/// <summary>Routing with a result: the caller waits for the page to be left and gets what the page gave back.</summary>
public class RouterResultTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRegion("side");
        services.RegisterRoute<AboutView, AboutVm>(route: "/about");
        services.RegisterRoute<PickerView, PickerVm>(route: "/picker");
        services.RegisterRoute<ChildView, ChildVm>(route: "/child");
        return services.BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_value_the_page_gave_is_returned_when_it_is_popped()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/about", cancellationToken: Ct);

        var waiting = router.GoToForResultAsync<string>("/picker", cancellationToken: Ct);
        Assert.False(waiting.IsCompleted);
        router.GetCurrentViewModel<PickerVm>()!.Pick("blue");
        await router.PopAsync(Ct);
        var result = await waiting;

        Assert.True(result.OpenResult.Success);
        Assert.True(result.Returned);
        Assert.Equal("blue", result.Value);
        Assert.NotNull(router.GetCurrentViewModel<AboutVm>());
    }

    [Fact]
    public async Task A_page_left_without_a_value_returns_nothing()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/about", cancellationToken: Ct);

        var waiting = router.GoToForResultAsync<string>("/picker", cancellationToken: Ct);
        await router.GoBackAsync(Ct);
        var result = await waiting;

        Assert.False(result.Returned);
        Assert.Null(result.Value);
        Assert.Equal("fallback", result.ValueOr("fallback"));
    }

    [Fact]
    public async Task A_page_that_opens_another_one_on_top_is_not_left_yet()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/about", cancellationToken: Ct);

        var waiting = router.GoToForResultAsync<string>("/picker", cancellationToken: Ct);
        router.GetCurrentViewModel<PickerVm>()!.Pick("red");
        await router.GoToAsync("/child", cancellationToken: Ct);
        Assert.False(waiting.IsCompleted);

        await router.PopAsync(Ct);      // back to the picker: still not left
        Assert.False(waiting.IsCompleted);
        await router.PopAsync(Ct);      // the picker is left
        Assert.Equal("red", (await waiting).Value);
    }

    [Fact]
    public async Task A_page_that_cannot_be_opened_completes_at_once()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToForResultAsync<string>("/nowhere", cancellationToken: Ct);

        Assert.False(result.OpenResult.Success);
        Assert.False(result.Returned);
    }

    [Fact]
    public async Task The_wait_can_be_cancelled()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/about", cancellationToken: Ct);
        using var cts = new CancellationTokenSource();

        var waiting = router.GoToForResultAsync<string>("/picker", cancellationToken: cts.Token);
        cts.Cancel();
        var result = await waiting;

        Assert.True(result.Cancelled);
        Assert.False(result.Returned);
        Assert.NotNull(router.GetCurrentViewModel<PickerVm>());     // the page itself stays
    }

    [Fact]
    public async Task The_caller_can_route_again_as_soon_as_it_has_the_result()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/about", cancellationToken: Ct);

        var follow = Task.Run(async () =>
        {
            var result = await router.GoToForResultAsync<string>("/picker", cancellationToken: Ct);
            await router.GoToAsync("/child", cancellationToken: Ct);   // would deadlock if it ran under the gate
            return result;
        }, Ct);
        while (router.GetCurrentViewModel<PickerVm>() is null) await Task.Delay(5, Ct);
        router.GetCurrentViewModel<PickerVm>()!.Pick("green");
        await router.PopAsync(Ct);

        Assert.Equal("green", (await follow.WaitAsync(TimeSpan.FromSeconds(5), Ct)).Value);
        Assert.NotNull(router.GetCurrentViewModel<ChildVm>());
    }

    [Fact]
    public async Task It_works_on_the_router_of_a_region()
    {
        using var provider = Build();
        var side = provider.GetRouter("side");
        await side.GoToAsync("/about", cancellationToken: Ct);

        var waiting = side.GoToForResultAsync<string>("/picker", cancellationToken: Ct);
        side.GetCurrentViewModel<PickerVm>()!.Pick("side");
        await side.PopAsync(Ct);

        Assert.Equal("side", (await waiting).Value);
        Assert.Null(provider.GetRequiredService<IRouter>().CurrentViewModel);
    }
}
