using Microsoft.Maui.Dispatching;
using NSubstitute;

namespace Routeables.Tests.Maui;

[CollectionDefinition("MAUI UI", DisableParallelization = true)]
public sealed class MauiUiTestCollection;

internal sealed class MauiDispatcherTestScope : IDisposable
{
    private readonly IDispatcherProvider? _originalProvider = DispatcherProvider.Current;

    public MauiDispatcherTestScope()
    {
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()();
            return true;
        });

        var provider = Substitute.For<IDispatcherProvider>();
        provider.GetForCurrentThread().Returns(dispatcher);
        DispatcherProvider.SetCurrent(provider);
    }

    public void Dispose()
    {
        DispatcherProvider.SetCurrent(_originalProvider!);
    }
}
