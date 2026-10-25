using Uno.UI.Hosting;
using XAnimals.Uno;

namespace XAnimals.Desktop;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // An address given on the command line (xanimals xanimals://dogs/Rex) is followed once the first page is shown.
        if (args.Length > 0 && Uri.TryCreate(args[0], UriKind.Absolute, out var link))
            LinkInbox.Instance.Push(link);

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
