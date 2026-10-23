using Microsoft.Extensions.DependencyInjection;
using Routeables.Maui;
using XAminals.Models;

namespace XAnimals.Maui
{
    public partial class App : Application
    {
        IServiceProvider provider;
        public App(IServiceProvider provider)
        {
            InitializeComponent();
            this.provider = provider;

            // The images of the lists load while the application starts: a page finds them in memory.
            _ = WebImageConverter.PreloadAsync(AnimalImages.Thumbnails);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new RouteablesShell(provider));
        }
    }
}