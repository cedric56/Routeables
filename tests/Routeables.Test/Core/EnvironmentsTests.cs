using Microsoft.Extensions.DependencyInjection;
using Routeables.Core;

namespace Routeables.Tests.Core;

/// <summary>
/// The identifiers of the runtime, the desktop and the distribution (strongly typed strings, equal by value), what the application detects on the
/// machine it runs on, and how they are registered.
/// </summary>
public class EnvironmentsTests
{
    // ------------------------------------------------------------------------------------------------ the identifiers

    private static readonly string[] DesktopList =
        ["Gnome", "Unknown", "KDE", "Xfce", "Mate", "Cinnamon", "WSL", "Macos", "Windows", "iOS", "Android", "Unity", "LXDE", "TvOS", "WatchOS"];

    public static TheoryData<string> DesktopNames => new(DesktopList);

    private static Desktop Desktops(string name) => name switch
    {
        "Gnome" => Desktop.Gnome, "Unknown" => Desktop.Unknown, "KDE" => Desktop.KDE, "Xfce" => Desktop.Xfce, "Mate" => Desktop.Mate,
        "Cinnamon" => Desktop.Cinnamon, "WSL" => Desktop.WSL, "Macos" => Desktop.Macos, "Windows" => Desktop.Windows, "iOS" => Desktop.iOS,
        "Android" => Desktop.Android, "Unity" => Desktop.Unity, "LXDE" => Desktop.LXDE, "TvOS" => Desktop.TvOS, "WatchOS" => Desktop.WatchOS,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    [Theory]
    [MemberData(nameof(DesktopNames))]
    public void A_desktop_is_named_as_its_property_and_equals_the_one_created_with_that_name(string name)
    {
        var desktop = Desktops(name);

        Assert.Equal(name, desktop.ToString());
        Assert.Equal(desktop, Desktop.Create(name));
        Assert.True(desktop == Desktop.Create(name));
        Assert.False(desktop != Desktop.Create(name));
        Assert.Equal(desktop.GetHashCode(), Desktop.Create(name).GetHashCode());
        Assert.True(desktop.Equals((object)Desktop.Create(name)));
    }

    [Fact]
    public void The_desktops_are_all_different()
    {
        var all = DesktopList.Select(Desktops).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void A_custom_desktop_is_equal_by_value_and_differs_from_the_others()
    {
        var custom = Desktop.Create("Sway");

        Assert.Equal("Sway", custom.ToString());
        Assert.NotEqual(Desktop.Gnome, custom);
        Assert.True(custom != Desktop.Gnome);
        Assert.False(custom.Equals((object?)"Sway"));
        Assert.False(custom.Equals((object?)null));
    }

    [Fact]
    public void A_desktop_that_was_never_set_is_empty()
    {
        Desktop none = default;

        Assert.Equal(string.Empty, none.ToString());
        Assert.Equal(0, none.GetHashCode());
        Assert.NotEqual(Desktop.Unknown, none);
    }

    [Fact]
    public void A_desktop_needs_a_name()
    {
        Assert.Throws<ArgumentNullException>(() => Desktop.Create(null!));
        Assert.Throws<ArgumentException>(() => Desktop.Create(string.Empty));
    }

    private static readonly string[] DistributionList =
        [
            "Ubuntu", "Fedora", "ArchLinux", "Debian", "OpenSUSE", "LinuxMint", "Manjaro", "PopOS", "Unknown", "Windows11", "Windows10", "Vista",
            "WindowsXP", "Windows81", "Windows8", "Windows7", "Android16", "Android15", "Android14", "Android13", "Android12L", "Android12",
            "Android11", "Android10", "Android9", "Android8_1", "Android8", "Android7_1", "Android7", "Android6", "Android5_1", "Android5"
        ];

    public static TheoryData<string> DistributionNames => new(DistributionList);

    private static Distribution Named(string name) => (Distribution)typeof(Distribution).GetProperty(name)!.GetValue(null)!;

    [Theory]
    [MemberData(nameof(DistributionNames))]
    public void A_distribution_is_named_as_its_property_and_equals_the_one_created_with_that_name(string name)
    {
        var distribution = Named(name);

        Assert.Equal(name, distribution.ToString());
        Assert.Equal(distribution, Distribution.Create(name));
        Assert.True(distribution == Distribution.Create(name));
        Assert.False(distribution != Distribution.Create(name));
        Assert.Equal(distribution.GetHashCode(), Distribution.Create(name).GetHashCode());
        Assert.True(distribution.Equals((object)Distribution.Create(name)));
    }

    [Fact]
    public void The_distributions_are_all_different()
    {
        var all = DistributionList.Select(Named).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void A_distribution_that_was_never_set_is_empty_and_one_needs_a_name()
    {
        Distribution none = default;

        Assert.Equal(string.Empty, none.ToString());
        Assert.Equal(0, none.GetHashCode());
        Assert.Throws<ArgumentNullException>(() => Distribution.Create(null!));
        Assert.Throws<ArgumentException>(() => Distribution.Create(string.Empty));
        Assert.NotEqual(Distribution.Ubuntu, Distribution.Create("Gentoo"));
        Assert.False(Distribution.Ubuntu.Equals((object?)"Ubuntu"));
    }

    [Fact]
    public void A_runtime_is_equal_by_value_and_the_runtimes_are_all_different()
    {
        Runtime[] all = [Runtime.Avalonia, Runtime.Uno, Runtime.Wpf, Runtime.Maui, Runtime.MauiHybrid, Runtime.BlazorServer, Runtime.BlazorWebAssembly, Runtime.Unknown];

        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.Equal(Runtime.Wpf, Runtime.Create("Wpf"));
        Assert.True(Runtime.Wpf == Runtime.Create("Wpf"));
        Assert.True(Runtime.Wpf != Runtime.Uno);
        Assert.Equal("BlazorServer", Runtime.BlazorServer.ToString());
        Assert.Equal(Runtime.Wpf.GetHashCode(), Runtime.Create("Wpf").GetHashCode());
        Runtime none = default;
        Assert.Equal(string.Empty, none.ToString());
        Assert.Throws<ArgumentNullException>(() => Runtime.Create(null!));
        Assert.Throws<ArgumentException>(() => Runtime.Create(string.Empty));
    }

    // ------------------------------------------------------------------------------------------------ what the machine is

    [Fact]
    public void The_desktop_is_detected_once_and_fits_the_operating_system()
    {
        var detector = new DesktopImplementation();
        var current = detector.Current;

        Assert.Equal(current, detector.Current);
        if (OperatingSystem.IsWindows()) Assert.Equal(Desktop.Windows, current);
        else if (OperatingSystem.IsMacOS()) Assert.Equal(Desktop.Macos, current);
        else if (OperatingSystem.IsLinux())
            Assert.Contains(current, new[] { Desktop.Gnome, Desktop.KDE, Desktop.Xfce, Desktop.Mate, Desktop.Cinnamon, Desktop.Unity, Desktop.LXDE, Desktop.WSL, Desktop.Unknown });
    }

    [Fact]
    public void The_distribution_fits_the_operating_system()
    {
        var current = DistributionImplementation.Detect();

        if (OperatingSystem.IsWindows())
            Assert.Contains(current, new[] { Distribution.Windows11, Distribution.Windows10, Distribution.Windows81, Distribution.Windows8, Distribution.Windows7, Distribution.Vista, Distribution.WindowsXP, Distribution.Unknown });
        else if (OperatingSystem.IsLinux())
            Assert.Contains(current, new[] { Distribution.Ubuntu, Distribution.Fedora, Distribution.ArchLinux, Distribution.Debian, Distribution.OpenSUSE, Distribution.LinuxMint, Distribution.Manjaro, Distribution.PopOS, Distribution.Unknown });
        else
            Assert.Equal(Distribution.Unknown, current);

        Assert.Equal(current, new DistributionImplementation().Current);
    }

    [Fact]
    public void A_linux_system_is_recognised_by_its_os_release_when_the_file_is_there()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/etc/os-release"))
            return;

        var text = string.Join('\n', File.ReadAllLines("/etc/os-release")).ToLowerInvariant();
        var current = DistributionImplementation.Detect();

        // The ids the file names: the distribution detected is the first of the families that one of them belongs to.
        if (text.Contains("id=ubuntu") || text.Contains("id_like=ubuntu") || text.Contains("id_like=\"ubuntu"))
            Assert.Equal(Distribution.Ubuntu, current);
        else if (text.Contains("id=debian"))
            Assert.Equal(Distribution.Debian, current);
        else if (text.Contains("id=fedora"))
            Assert.Equal(Distribution.Fedora, current);
    }

    // ------------------------------------------------------------------------------------------------ registration

    [Fact]
    public void The_environments_are_registered_as_singletons()
    {
        var services = new ServiceCollection();
        services.RegisterEnvironments(Runtime.BlazorServer);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(Runtime.BlazorServer, provider.GetRequiredService<IRuntime>().Current);
        Assert.Same(provider.GetRequiredService<IDesktop>(), provider.GetRequiredService<IDesktop>());
        Assert.Same(provider.GetRequiredService<IDistribution>(), provider.GetRequiredService<IDistribution>());
        Assert.Equal(new DesktopImplementation().Current, provider.GetRequiredService<IDesktop>().Current);
    }

    [Theory]
    [InlineData("BlazorServer", true)]
    [InlineData("BlazorWebAssembly", true)]
    [InlineData("MauiHybrid", true)]
    [InlineData("Maui", false)]
    [InlineData("Avalonia", false)]
    [InlineData("Uno", false)]
    [InlineData("Wpf", false)]
    [InlineData("Unknown", false)]
    public void Only_the_blazor_and_hybrid_runtimes_are_razor_runtimes(string name, bool razor)
    {
        var services = new ServiceCollection();
        services.RegisterEnvironments(Runtime.Create(name));
        using var provider = services.BuildServiceProvider();

        Assert.Equal(razor, provider.GetRequiredService<IRuntime>().IsRazorRuntime());
    }
}
