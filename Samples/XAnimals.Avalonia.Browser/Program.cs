using Avalonia;
using Avalonia.Browser;
using Avalonia.Labs.Notifications;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using System;
using System.Threading.Tasks;
using XAnimals.Avalonia;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
            .WithInterFont()
            // The browser gives the application no system font: without this one, the emoji of the menu draw as empty squares.
            // The font is a collection (as Inter is): "fonts:Emoji#Noto Emoji" names it, the font manager asks it for the glyphs Inter lacks.
            .ConfigureFonts(fonts => fonts.AddFontCollection(new EmbeddedFontCollection(
                new Uri("fonts:Emoji", UriKind.Absolute),
                new Uri("avares://XAnimals.Avalonia.Browser/Assets/Fonts", UriKind.Absolute))))
            .With(new FontManagerOptions
            {
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily("fonts:Emoji#Noto Emoji") }]
            })
            .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => App.CreateApp()
              .WithAppNotifications();
}
