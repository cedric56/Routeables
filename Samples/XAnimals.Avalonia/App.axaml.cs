using AsyncImageLoader;
using AsyncImageLoader.Core.Caching;
using AsyncImageLoader.Core.Pipeline;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using Routeables.Generated;
using System;
using System.Net.Http;
using XAminals.Models;
using XAnimals.Avalonia.Views;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class App : Application
{
    public static AppBuilder CreateApp(
        IMessageBoxService? messageBoxService = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IServiceProvider>? afterBuildProvider = null,
        Action? onAppDispose = null)
    {
        // The usual Avalonia shape: the application has its own constructor, Routeables is added to the builder.
        return AppBuilder.Configure<App>().UseRouteables<MainViewModel>(
           configure: services =>
           {
               services.UseRouteablesServices("avares://XAnimals.Avalonia/Assets/icon.ico", messageBoxService);
               services.AddGeneratedRoutes();
               AnimalIcons.Register();
               services.RegisterViewModels<CatDetailView, DogDetailView>();
               configureServices?.Invoke(services);
           },
           afterBuild: afterBuildProvider,
           onDispose: onAppDispose);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Wikimedia refuses requests that carry no User-Agent (HTTP 403), which is what the default HttpClient sends:
        // the loader gets one that identifies the application.
        var http = new HttpClient(new CachedImageHandler(new HttpClientHandler()));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("XAnimals/1.0 (Routeables sample; https://github.com/cedric56/Routeables)");
        ImageLoader.AsyncImageLoader = ImageLoaderPipelineBuilder.RamCached(new MemoryImageCacheOptions()).UseHttpClient(http).Build();

        // The images of the lists are loaded now, while the application starts: a page finds them in memory.
        _ = CachedImageHandler.PreloadAsync(http, AnimalImages.Thumbnails);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // The window on a desktop, the view on a single-view platform; both get the menu as their DataContext.
        this.OpenMainView(ApplicationLifetime is ISingleViewApplicationLifetime ? new MainView() : new MainWindow());

        base.OnFrameworkInitializationCompleted();
    }
}