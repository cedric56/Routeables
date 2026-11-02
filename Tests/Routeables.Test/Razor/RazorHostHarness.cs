using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Routeables.Tests.Razor;

/// <summary>
/// Renders a dialog or notification host component with <see cref="HtmlRenderer"/> against a scoped service
/// provider, so a test can show something through the service and read back the markup.
/// </summary>
internal sealed class RazorHostHarness<THost> : IAsyncDisposable where THost : IComponent
{
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;
    private readonly HtmlRenderer renderer;
    private readonly Func<string> toHtml;

    private RazorHostHarness(ServiceProvider provider, IServiceScope scope, HtmlRenderer renderer, Func<string> toHtml)
    {
        this.provider = provider;
        this.scope = scope;
        this.renderer = renderer;
        this.toHtml = toHtml;
    }

    public static async Task<RazorHostHarness<THost>> StartAsync(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        register(services);

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<THost>());
        return new RazorHostHarness<THost>(provider, scope, renderer, () => root.ToHtmlString());
    }

    public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public Task<string> HtmlAsync() => renderer.Dispatcher.InvokeAsync(toHtml);

    public async ValueTask DisposeAsync()
    {
        await renderer.DisposeAsync();
        scope.Dispose();
        await provider.DisposeAsync();
    }
}
