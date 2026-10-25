using Microsoft.Extensions.DependencyInjection;
using Routeables.Maui;
using XAminals.Models;
using XAnimals.ViewModels;

namespace XAnimals.Maui
{
    public partial class App : Application
    {
        IServiceProvider provider;
        public App(IServiceProvider provider)
        {
            InitializeComponent();
            this.provider = provider;
            LinkInbox.Instance.Attach(provider);   // the links received by the platform are followed once the first page is shown

            // The images of the lists load while the application starts: a page finds them in memory.
            _ = WebImageConverter.PreloadAsync(AnimalImages.Thumbnails);
        }

        // false: the shell generated from the entries of MainViewModel (RouteablesShell).
        // true: the shell written in XamlShell.xaml, whose pages are the routes of Routeables (ShellItems.AttachRouteables).
        private static readonly bool UseXamlShell = false;

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (UseXamlShell)
            {
                var xamlShell = new XamlShell();
                xamlShell.AttachRouteables(provider);
                return new Window(xamlShell);
            }

            var shell = new RouteablesShell(provider);
            var main = provider.GetRequiredService<MainViewModel>();

            // The button of the app bar: every page of the shell has its own toolbar, so each one gets the item when it is shown
            // (the entries of a module loaded later included).
            void AddThemeButton()
            {
                if (shell.CurrentPage is not { } page || page.ToolbarItems.Any(item => item.AutomationId == "ThemeButton"))
                    return;

                page.ToolbarItems.Add(new ToolbarItem
                {
                    Text = "\u25D0",
                    AutomationId = "ThemeButton",
                    Command = new Command(() => main.ToggleTheme(Application.Current?.RequestedTheme == AppTheme.Dark))
                });
            }

            shell.Navigated += (_, _) => AddThemeButton();
            shell.Loaded += (_, _) => AddThemeButton();
            return new Window(shell);
        }
    }
}