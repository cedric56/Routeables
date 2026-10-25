using Avalonia;
using Avalonia.Browser;
using System.Threading.Tasks;
using XAnimals.Avalonia;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => App.CreateApp();
}
