using Microsoft.Extensions.DependencyInjection;
using XAnimals.Common;
using XAnimals.ViewModels;

namespace XAnimals.Blazor;

public static class Extensions
{
    public static void UseSharedLibrary(this IServiceCollection services)
    {
        AnimalIcons.Register();
        services.RegisterViewModels();
    }
}