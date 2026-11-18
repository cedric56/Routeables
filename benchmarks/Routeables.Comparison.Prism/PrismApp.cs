using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Routeables.Comparison.Avalonia;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace Routeables.Comparison.Prism;

/// <summary>The view of a page: a control with a text, built by the container with its view model. One class for each page (PrismPages.cs).</summary>
public abstract class PrismPage : UserControl
{
    protected PrismPage(PrismPageViewModel viewModel, string text)
    {
        DataContext = viewModel;
        Content = new TextBlock { Text = text };
        ViewCount.Built();
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
        ViewCount.Built();
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

    /// <summary>The control the region shows its view in.</summary>
    public static ContentControl Host { get; private set; } = null!;

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
        containerRegistry.RegisterForNavigation<P00Page>("P00");
        containerRegistry.RegisterForNavigation<P01Page>("P01");
        containerRegistry.RegisterForNavigation<P02Page>("P02");
        containerRegistry.RegisterForNavigation<P03Page>("P03");
        containerRegistry.RegisterForNavigation<P04Page>("P04");
        containerRegistry.RegisterForNavigation<P05Page>("P05");
        containerRegistry.RegisterForNavigation<P06Page>("P06");
        containerRegistry.RegisterForNavigation<P07Page>("P07");
        containerRegistry.RegisterForNavigation<P08Page>("P08");
        containerRegistry.RegisterForNavigation<P09Page>("P09");
        containerRegistry.RegisterForNavigation<P10Page>("P10");
        containerRegistry.RegisterForNavigation<P11Page>("P11");
        containerRegistry.RegisterForNavigation<P12Page>("P12");
        containerRegistry.RegisterForNavigation<P13Page>("P13");
        containerRegistry.RegisterForNavigation<P14Page>("P14");
        containerRegistry.RegisterForNavigation<P15Page>("P15");
        containerRegistry.RegisterForNavigation<P16Page>("P16");
        containerRegistry.RegisterForNavigation<P17Page>("P17");
        containerRegistry.RegisterForNavigation<P18Page>("P18");
        containerRegistry.RegisterForNavigation<P19Page>("P19");
        containerRegistry.RegisterForNavigation<P20Page>("P20");
        containerRegistry.RegisterForNavigation<P21Page>("P21");
        containerRegistry.RegisterForNavigation<P22Page>("P22");
        containerRegistry.RegisterForNavigation<P23Page>("P23");
        containerRegistry.RegisterForNavigation<P24Page>("P24");
        containerRegistry.RegisterForNavigation<P25Page>("P25");
        containerRegistry.RegisterForNavigation<P26Page>("P26");
        containerRegistry.RegisterForNavigation<P27Page>("P27");
        containerRegistry.RegisterForNavigation<P28Page>("P28");
        containerRegistry.RegisterForNavigation<P29Page>("P29");
        containerRegistry.RegisterForNavigation<P30Page>("P30");
        containerRegistry.RegisterForNavigation<P31Page>("P31");
        containerRegistry.RegisterForNavigation<P32Page>("P32");
        containerRegistry.RegisterForNavigation<P33Page>("P33");
        containerRegistry.RegisterForNavigation<P34Page>("P34");
        containerRegistry.RegisterForNavigation<P35Page>("P35");
        containerRegistry.RegisterForNavigation<P36Page>("P36");
        containerRegistry.RegisterForNavigation<P37Page>("P37");
        containerRegistry.RegisterForNavigation<P38Page>("P38");
        containerRegistry.RegisterForNavigation<P39Page>("P39");
        containerRegistry.RegisterForNavigation<C0Page>("C0");
        containerRegistry.RegisterForNavigation<C1Page>("C1");
        containerRegistry.RegisterForNavigation<C2Page>("C2");
        containerRegistry.RegisterForNavigation<C3Page>("C3");
        containerRegistry.RegisterForNavigation<C4Page>("C4");
        containerRegistry.RegisterForNavigation<C5Page>("C5");
        containerRegistry.RegisterForNavigation<C6Page>("C6");
        containerRegistry.RegisterForNavigation<C7Page>("C7");
        containerRegistry.RegisterForNavigation<C8Page>("C8");
        containerRegistry.RegisterForNavigation<C9Page>("C9");
        containerRegistry.RegisterForNavigation<ListPage>("List");
        containerRegistry.RegisterForNavigation<DetailPage>("Detail");
        containerRegistry.RegisterForNavigation<SignInPage>("SignIn");
        containerRegistry.RegisterForNavigation<PrismEditPage>("Edit");
    }

    protected override AvaloniaObject CreateShell()
    {
        Services = Container;
        var host = new ContentControl();
        Host = host;
        RegionManager.SetRegionName(host, Region);
        Shell = new Window { Width = 400, Height = 300, Content = host };
        return Shell;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<PrismBenchApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
