using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace Routeables.Comparison.Prism;

/// <summary>The view of a page: a control with a text, built by the container with its view model.</summary>
public sealed class PrismPage : UserControl
{
    public PrismPage(PrismPageViewModel viewModel)
    {
        DataContext = viewModel;
        Content = new TextBlock { Text = "page" };
    }
}

/// <summary>The view model of a page. Prism navigation hands it nothing the scenario needs: the name of the page is read from the journal.</summary>
public sealed class PrismPageViewModel(IClock clock)
{
    public IClock Clock { get; } = clock;
}

public sealed class PrismEditPage : UserControl
{
    public PrismEditPage(PrismEditViewModel viewModel)
    {
        DataContext = viewModel;
        Content = new TextBlock { Text = "Edit" };
    }
}

/// <summary>The edit page refuses to be left while it holds changes: Prism asks it (<c>IConfirmNavigationRequest</c>).</summary>
public sealed class PrismEditViewModel(AppState state) : IConfirmNavigationRequest
{
    public void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback) => continuationCallback(!state.Dirty);

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
    }
}

/// <summary>The application: Fluent, then Prism with the pages registered for navigation and one region in the window.</summary>
public sealed class PrismBenchApp : PrismApplication
{
    public const string Region = "Main";

    /// <summary>The container of Prism, set once the application is initialised.</summary>
    public static IContainerProvider Services { get; private set; } = null!;

    public static Window Shell { get; private set; } = null!;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        base.Initialize();   // starts Prism: required
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<AppState>();
        containerRegistry.RegisterSingleton<IClock, Clock>();
        containerRegistry.Register<PrismPageViewModel>();
        containerRegistry.Register<PrismEditViewModel>();
        containerRegistry.RegisterForNavigation<PrismPage>("P00");
        containerRegistry.RegisterForNavigation<PrismPage>("P01");
        containerRegistry.RegisterForNavigation<PrismPage>("P02");
        containerRegistry.RegisterForNavigation<PrismPage>("P03");
        containerRegistry.RegisterForNavigation<PrismPage>("P04");
        containerRegistry.RegisterForNavigation<PrismPage>("P05");
        containerRegistry.RegisterForNavigation<PrismPage>("P06");
        containerRegistry.RegisterForNavigation<PrismPage>("P07");
        containerRegistry.RegisterForNavigation<PrismPage>("P08");
        containerRegistry.RegisterForNavigation<PrismPage>("P09");
        containerRegistry.RegisterForNavigation<PrismPage>("P10");
        containerRegistry.RegisterForNavigation<PrismPage>("P11");
        containerRegistry.RegisterForNavigation<PrismPage>("P12");
        containerRegistry.RegisterForNavigation<PrismPage>("P13");
        containerRegistry.RegisterForNavigation<PrismPage>("P14");
        containerRegistry.RegisterForNavigation<PrismPage>("P15");
        containerRegistry.RegisterForNavigation<PrismPage>("P16");
        containerRegistry.RegisterForNavigation<PrismPage>("P17");
        containerRegistry.RegisterForNavigation<PrismPage>("P18");
        containerRegistry.RegisterForNavigation<PrismPage>("P19");
        containerRegistry.RegisterForNavigation<PrismPage>("P20");
        containerRegistry.RegisterForNavigation<PrismPage>("P21");
        containerRegistry.RegisterForNavigation<PrismPage>("P22");
        containerRegistry.RegisterForNavigation<PrismPage>("P23");
        containerRegistry.RegisterForNavigation<PrismPage>("P24");
        containerRegistry.RegisterForNavigation<PrismPage>("P25");
        containerRegistry.RegisterForNavigation<PrismPage>("P26");
        containerRegistry.RegisterForNavigation<PrismPage>("P27");
        containerRegistry.RegisterForNavigation<PrismPage>("P28");
        containerRegistry.RegisterForNavigation<PrismPage>("P29");
        containerRegistry.RegisterForNavigation<PrismPage>("P30");
        containerRegistry.RegisterForNavigation<PrismPage>("P31");
        containerRegistry.RegisterForNavigation<PrismPage>("P32");
        containerRegistry.RegisterForNavigation<PrismPage>("P33");
        containerRegistry.RegisterForNavigation<PrismPage>("P34");
        containerRegistry.RegisterForNavigation<PrismPage>("P35");
        containerRegistry.RegisterForNavigation<PrismPage>("P36");
        containerRegistry.RegisterForNavigation<PrismPage>("P37");
        containerRegistry.RegisterForNavigation<PrismPage>("P38");
        containerRegistry.RegisterForNavigation<PrismPage>("P39");
        containerRegistry.RegisterForNavigation<PrismPage>("C0");
        containerRegistry.RegisterForNavigation<PrismPage>("C1");
        containerRegistry.RegisterForNavigation<PrismPage>("C2");
        containerRegistry.RegisterForNavigation<PrismPage>("C3");
        containerRegistry.RegisterForNavigation<PrismPage>("C4");
        containerRegistry.RegisterForNavigation<PrismPage>("C5");
        containerRegistry.RegisterForNavigation<PrismPage>("C6");
        containerRegistry.RegisterForNavigation<PrismPage>("C7");
        containerRegistry.RegisterForNavigation<PrismPage>("C8");
        containerRegistry.RegisterForNavigation<PrismPage>("C9");
        containerRegistry.RegisterForNavigation<PrismPage>("List");
        containerRegistry.RegisterForNavigation<PrismPage>("Detail");
        containerRegistry.RegisterForNavigation<PrismPage>("SignIn");
        containerRegistry.RegisterForNavigation<PrismEditPage>("Edit");
    }

    protected override AvaloniaObject CreateShell()
    {
        Services = Container;
        var host = new ContentControl();
        RegionManager.SetRegionName(host, Region);
        Shell = new Window { Width = 400, Height = 300, Content = host };
        return Shell;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<PrismBenchApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
