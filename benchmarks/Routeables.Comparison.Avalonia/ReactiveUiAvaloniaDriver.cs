using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Unit = System.Reactive.Unit;

namespace Routeables.Comparison.Avalonia;

/// <summary>A routable view model: the name it gives itself, and the screen that hosts it.</summary>
public sealed class RxPage(string name, IScreen host) : ReactiveObject, IRoutableViewModel
{
    public string Name { get; } = name;

    public string? UrlPathSegment => Name;

    public IScreen HostScreen { get; } = host;

    /// <summary>ReactiveUI routing has no "can I leave": the application asks the page before it navigates.</summary>
    public Func<bool> CanLeave { get; set; } = static () => true;
}

/// <summary>The view of a page: a control with a text, built with the page it shows.</summary>
public sealed class RxView : UserControl
{
    public RxView(RxPage page)
    {
        DataContext = page;
        Content = new TextBlock { Text = page.Name };
        ViewCount.Built(this);
    }
}

public sealed class RxScreen : ReactiveObject, IScreen
{
    // The immediate scheduler: the application's thread is already the UI thread the commands run on.
    public RoutingState Router { get; } = new(ImmediateScheduler.Instance);
}

/// <summary>
/// Whether the page that a step shows was laid out: after each step, the view in the host must have a size. A driver that shows a view
/// the window never laid out would pay nothing for it, and its numbers would not be those of a page shown. Each driver adds what it
/// sees, and the program writes the counts at the end of the run (to the output, which the job copies into its summary).
/// </summary>
public static class ViewCount
{
    private static readonly Dictionary<string, int> Counts = [];
    private static readonly List<string> Notes = [];
    private static string s_owner = "";
    private static List<WeakReference> s_views = [];

    /// <summary>The driver that is running: the views that get built are counted for it (and the ones kept in view are those it has built).</summary>
    public static string Owner
    {
        get => s_owner;
        set
        {
            s_owner = value;
            s_views = [];
        }
    }

    public static void Built(object view)
    {
        Counts.TryGetValue(Owner, out var count);
        Counts[Owner] = count + 1;
        s_views.Add(new WeakReference(view));
    }

    public static int BuiltBy(string driver) => Counts.TryGetValue(driver, out var count) ? count : 0;

    /// <summary>A line for the end of the run (the tables are written while the drivers run: a line written then would be inside one).</summary>
    public static void Note(string text) => Notes.Add(text);

    /// <summary>How many of the views this driver has built are still alive, after what the application has queued has run and the memory has been collected.</summary>
    public static void NoteAlive(string when)
    {
        Dispatcher.UIThread.RunJobs();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Notes.Add($"{Owner}, {when}: {s_views.Count(view => view.IsAlive)} of the {s_views.Count} views built since the driver was created are alive.");
    }

    public static void Clear()
    {
        Counts.Clear();
        Notes.Clear();
    }

    public static void WriteNotes()
    {
        foreach (var note in Notes)
            Console.WriteLine(note);
    }
}

public static class LayoutCount
{
    private static readonly Dictionary<string, (int Shown, int Unsized)> Counts = [];

    public static void Observe(string driver, object? shown)
    {
        if (shown is not global::Avalonia.Visual visual)
            return;
        Counts.TryGetValue(driver, out var count);
        var unsized = visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0;
        Counts[driver] = (count.Shown + 1, count.Unsized + (unsized ? 1 : 0));
    }

    public static void Report()
    {
        Console.WriteLine();
        foreach (var (driver, count) in Counts)
        {
            var built = ViewCount.BuiltBy(driver);
            Console.WriteLine($"Layout, {driver}: {count.Shown} views seen after a step, {count.Unsized} of them with no size ({(count.Shown == 0 ? 0 : 100.0 * count.Unsized / count.Shown):F1} %); {built} views built ({(count.Shown == 0 ? 0 : 100.0 * built / count.Shown):F1} % of the steps).");
        }

        ViewCount.WriteNotes();
        Counts.Clear();
        ViewCount.Clear();
    }
}

/// <summary>
/// The scenario on <c>RoutingState</c> of ReactiveUI, shown by a view host written here: the host shows the view of the page the router
/// gives (<c>RoutedViewHost</c> of ReactiveUI.Avalonia does the same, with its view locator; its API could not be checked, so it is not used). The host is a
/// <c>TransitioningContentControl</c>, the control that <c>RouteableRegion</c> is, so that the two libraries pay the same for the control.
/// The library has a navigation stack, not a history (no Forward), no route table and no guard: the guard and the refusal to leave are
/// written in the application, in front of <c>Navigate</c>. Pages and views are built through the container, as Routeables builds its own.
/// </summary>
public sealed class ReactiveUiAvaloniaDriver : INavigationDriver
{
    private readonly RxScreen _screen = new();
    private readonly ServiceProvider _provider = new ServiceCollection().AddSingleton<IClock, Clock>().BuildServiceProvider();
    private readonly Window _window;
    private readonly ContentControl _content;
    private readonly IDisposable _host;

    public ReactiveUiAvaloniaDriver()
    {
        State = new AppState();
        ViewCount.Owner = Name;
        // The control of Routeables' region (RouteableRegion is a TransitioningContentControl): two presenters and the transition machinery, with none
        // declared. A plain ContentControl would leave that cost to Routeables alone.
        var content = _content = new TransitioningContentControl();
        _host = _screen.Router.CurrentViewModel.Subscribe(
            vm => content.Content = vm is RxPage page ? ActivatorUtilities.CreateInstance<RxView>(_provider, page) : null);
        _window = new Window { Width = 400, Height = 300, Content = content };
        _window.Show();
        Flush();
    }

    public string Name => "ReactiveUI routing";

    public AppState State { get; }

    public bool SupportsForward => false;

    public string? Current => (_screen.Router.NavigationStack.LastOrDefault() as RxPage)?.Name;

    public object? CurrentObject => _screen.Router.NavigationStack.LastOrDefault();

    public async Task GoAsync(string target)
    {
        if (!CanLeave())
            return;
        if (AppState.IsGuarded(target) && !State.SignedIn)
        {
            State.Requested = target;
            target = "SignIn";
        }

        await _screen.Router.Navigate.Execute(Create(target.Split('/')));
        Flush();
    }

    public async Task BackAsync()
    {
        if (_screen.Router.NavigationStack.Count > 1 && CanLeave())
            await _screen.Router.NavigateBack.Execute(Unit.Default);
        Flush();
    }

    public Task ForwardAsync() => throw new NotSupportedException("ReactiveUI routing has no Forward.");

    public Task<bool> ClearHistoryAsync()
    {
        ViewCount.NoteAlive("before the history is cleared");
        var current = _screen.Router.NavigationStack.LastOrDefault();
        _screen.Router.NavigationStack.Clear();
        if (current is not null)
            _screen.Router.NavigationStack.Add(current);
        Flush();
        ViewCount.NoteAlive("after the history is cleared");
        return Task.FromResult(true);
    }

    private bool CanLeave() => _screen.Router.NavigationStack.LastOrDefault() is not RxPage page || page.CanLeave();

    private RxPage Create(string[] parts) => parts[0] switch
    {
        ['C', >= '0' and <= '9', ..] when parts.Length > 1 => Page($"{parts[0]}:{parts[1]}"),
        "Detail" => Page($"Detail:{parts[1]}"),
        "Edit" => Page($"Edit:{parts[1]}", () => !State.Dirty),
        ['P', '3', '0' or '1' or '2' or '3' or '4'] => PageWithDependency(parts[0]),
        _ => Page(parts[0]),
    };

    private RxPage PageWithDependency(string name)
    {
        _ = _provider.GetRequiredService<IClock>().Now; // the dependency of the five pages that have one
        return Page(name);
    }

    private RxPage Page(string name, Func<bool>? canLeave = null)
    {
        var page = ActivatorUtilities.CreateInstance<RxPage>(_provider, name, _screen);
        if (canLeave is not null)
            page.CanLeave = canLeave;
        return page;
    }

    private void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        LayoutCount.Observe(Name, _content.Content);
    }

    public void Dispose()
    {
        _host.Dispose();
        _window.Close();
        Dispatcher.UIThread.RunJobs(); // not counted: a closed window has no size
        _provider.Dispose();
    }
}
