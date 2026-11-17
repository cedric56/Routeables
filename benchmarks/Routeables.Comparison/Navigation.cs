namespace Routeables.Comparison;

/// <summary>What the application knows, whatever the navigation library: who is signed in, whether the edit page holds changes.</summary>
public sealed class AppState
{
    public bool SignedIn { get; set; }
    public bool Dirty { get; set; }

    /// <summary>The page the guard turned away, to go to once the user has signed in.</summary>
    public string? Requested { get; set; }

    /// <summary>The pages that need a signed-in user: P10 to P14.</summary>
    public static bool IsGuarded(string target) => target.Length == 3 && target[0] == 'P' && int.TryParse(target.AsSpan(1), out var n) && n is >= 10 and <= 14;

    /// <summary>The name a page gives itself: the target, with its parameter after a colon (<c>Detail/3</c> is <c>Detail:3</c>).</summary>
    public static string NameOf(string target) => target.Replace('/', ':');
}

public interface IClock { DateTime Now { get; } }

public sealed class Clock : IClock { public DateTime Now => DateTime.UtcNow; }

/// <summary>
/// What a navigation library has to do for the scenario. A target is <c>P07</c> (a page of the menu), <c>C3/7</c> (a page with a route
/// parameter), <c>List</c>, <c>Detail/3</c>, <c>Edit/3</c> or <c>SignIn</c>. The implementation follows the documentation of its library:
/// what the library does not have (a guard, a way to refuse leaving) is written in the application, in the way the library suggests.
/// </summary>
public interface INavigationDriver : IDisposable
{
    string Name { get; }

    AppState State { get; }

    /// <summary>Whether the library has a Forward: ReactiveUI routing has a stack, not a history.</summary>
    bool SupportsForward { get; }

    /// <summary>The name of the page that is shown.</summary>
    string? Current { get; }

    /// <summary>The view model that is shown, for the memory checks.</summary>
    object? CurrentObject { get; }

    Task GoAsync(string target);

    Task BackAsync();

    Task ForwardAsync();

    /// <summary>Lets go of the pages the library keeps behind the current one. <see langword="false"/> when it has no way to.</summary>
    Task<bool> ClearHistoryAsync();
}

public enum Op { Go, Back, Forward, SignedIn, Dirty, Continue }

/// <summary>One step of the script: what to do, and the page that must be shown afterwards.</summary>
public sealed record Step(Op Op, string? Target, bool Flag, string Expected);

/// <summary>
/// The script of about 200 steps, with the page each step must end on. The pages are computed by a small model of history, written once
/// here and not by any of the libraries: an implementation that ends on another page has failed the run.
/// </summary>
public static class Scenario
{
    public const int Steps = 200;

    public static IReadOnlyList<Step> Build()
    {
        var steps = new List<Step>();
        var history = new List<string>();
        var index = -1;
        var state = new AppState();
        string Cur() => history[index];

        void Add(Op op, string? target = null, bool flag = false)
        {
            switch (op)
            {
                case Op.SignedIn: state.SignedIn = flag; break;
                case Op.Dirty: state.Dirty = flag; break;
                case Op.Go: Push(target!); break;
                case Op.Continue: Push(state.Requested!); break;
                case Op.Back:
                    if (index > 0 && !Refused()) index--;
                    break;
                case Op.Forward:
                    if (index < history.Count - 1 && !Refused()) index++;
                    break;
            }

            steps.Add(new Step(op, target, flag, index < 0 ? "" : Cur()));
        }

        bool Refused() => index >= 0 && Cur().StartsWith("Edit", StringComparison.Ordinal) && state.Dirty;

        void Push(string target)
        {
            if (Refused())
                return;
            if (AppState.IsGuarded(target) && !state.SignedIn)
            {
                state.Requested = target;
                target = "SignIn";
            }

            history.RemoveRange(index + 1, history.Count - index - 1);
            history.Add(AppState.NameOf(target));
            index++;
        }

        string Page(int n) => $"P{n:00}";

        Add(Op.SignedIn, flag: true);
        for (var n = 0; n < 40; n++) Add(Op.Go, Page(n));                       // the menu: 40 pages
        for (var n = 0; n < 10; n++) Add(Op.Go, $"C{n}/{(n + 1) * 3}");          // 10 pages with a route parameter
        Add(Op.Go, "List"); Add(Op.Go, "Detail/3"); Add(Op.Go, "Edit/3");       // the chain, and back out of it
        Add(Op.Back); Add(Op.Back); Add(Op.Back);
        Add(Op.Go, "Edit/3"); Add(Op.Dirty, flag: true);                          // the edit page refuses to be left
        Add(Op.Go, Page(0)); Add(Op.Dirty, flag: false); Add(Op.Go, Page(0));
        for (var n = 10; n < 15; n++)                                             // the guard: turned away, signs in, goes on
        {
            Add(Op.SignedIn, flag: false);
            Add(Op.Go, Page(n));
            Add(Op.SignedIn, flag: true);
            Add(Op.Continue);
        }

        // The rest of the 200 steps, minus the deep history below (20 pushes, 20 Back, 20 Forward), is the menu again, in a fixed order.
        var random = new Random(42);
        while (steps.Count < Steps - 60)
        {
            var next = Page(random.Next(40));
            if (AppState.NameOf(next) != Cur())
                Add(Op.Go, next);
        }

        for (var n = 20; n < 40; n++) Add(Op.Go, Page(n));
        for (var i = 0; i < 20; i++) Add(Op.Back);
        for (var i = 0; i < 20; i++) Add(Op.Forward);
        return steps;
    }

    /// <summary>Does one step on <paramref name="driver"/>.</summary>
    public static async Task RunAsync(INavigationDriver driver, Step step)
    {
        switch (step.Op)
        {
            case Op.Go: await driver.GoAsync(step.Target!); break;
            case Op.Back: await driver.BackAsync(); break;
            case Op.Forward: await driver.ForwardAsync(); break;
            case Op.SignedIn: driver.State.SignedIn = step.Flag; break;
            case Op.Dirty: driver.State.Dirty = step.Flag; break;
            case Op.Continue: await driver.GoAsync(driver.State.Requested!); break;
        }
    }
}
