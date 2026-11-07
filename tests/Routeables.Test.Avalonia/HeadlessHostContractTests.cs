using Avalonia.Threading;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The contract every host honours (see <see cref="Routeables.Test.HostContract"/>), run on the Avalonia container.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessHostContractTests(HeadlessApp app)
{
    /// <summary>Back, Forward, Pop, refusals and Routing/Routed pairing behave as they do on every other host.</summary>
    [Fact]
    public Task Avalonia_honours_the_host_contract() => app.RunAsync(() =>
        Routeables.Test.HostContract.RunAsync(app.Provider, () =>
        {
            Dispatcher.UIThread.RunJobs();
            return Task.CompletedTask;
        }));
}
