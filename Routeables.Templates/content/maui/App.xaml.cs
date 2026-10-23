using Routeables.Maui;

namespace RouteablesApp;

public partial class App : Application
{
    private readonly IServiceProvider _provider;

    public App(IServiceProvider provider)
    {
        InitializeComponent();
        _provider = provider;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new RouteablesShell(_provider));
}
