using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Routeables.Comparison;

/// <summary>
/// The three parts of phase 1, the same for every library: <c>--check</c> (the script ends every step on the page the model expects),
/// <c>--measure</c> (time and bytes of the script, fresh driver each run) and <c>--memory</c> (1 000 route changes, the heap, and the pages
/// still alive). With no argument all three run. Exit code 0 only when every check passed.
/// </summary>
public static class Runner
{
    private const int Warmups = 5;
    private const int Runs = 30;
    private const int RouteChanges = 1_000;
    private const int Tracked = 100;

    public static IReadOnlyList<Func<INavigationDriver>> Drivers(IEnumerable<Func<INavigationDriver>>? extra = null)
    {
        List<Func<INavigationDriver>> list = [() => new RouteablesDriver()];
        if (extra is not null)
            list.AddRange(extra);
        list.Add(() => new HomeGrownDriver());
        return list;
    }

    public static async Task<int> RunAsync(string[] args, IEnumerable<Func<INavigationDriver>>? extra = null)
    {
        var all = args.Length == 0;
        var drivers = Drivers(extra);
        var failures = 0;
        var script = Scenario.Build();
        Console.WriteLine($".NET {Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, " +
                          $"{Environment.ProcessorCount} cores, {(GCSettings.IsServerGC ? "server" : "workstation")} GC, {script.Count} steps, " +
                          $"{Runs} runs after {Warmups} warm-ups");
        Console.WriteLine();

        if (all || args.Contains("--check"))
            foreach (var make in drivers)
                failures += await CheckAsync(make, script);

        if (all || args.Contains("--measure"))
        {
            Console.WriteLine();
            Console.WriteLine("| Library | median ms / run | µs / step | KiB allocated / run | B / step |");
            Console.WriteLine("|---|---:|---:|---:|---:|");
            foreach (var make in drivers)
                await MeasureAsync(make, script);
        }

        if (all || args.Contains("--memory"))
        {
            Console.WriteLine();
            Console.WriteLine($"| Library | heap after {RouteChanges} changes (KiB) | first {Tracked} pages alive | alive after the history is cleared |");
            Console.WriteLine("|---|---:|---:|---:|");
            foreach (var make in drivers)
                await MemoryAsync(make);
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static async Task<int> CheckAsync(Func<INavigationDriver> make, IReadOnlyList<Step> script)
    {
        using var driver = make();
        var skipped = 0;
        for (var i = 0; i < script.Count; i++)
        {
            var step = script[i];
            if (step.Op == Op.Forward && !driver.SupportsForward)
            {
                skipped++;
                continue;
            }

            await Scenario.RunAsync(driver, step);
            if ((driver.Current ?? "") != step.Expected)
            {
                Console.WriteLine($"FAIL  {driver.Name}: step {i + 1} ({step.Op} {step.Target}) shows '{driver.Current}', expected '{step.Expected}'");
                return 1;
            }
        }

        Console.WriteLine($"PASS  {driver.Name}: {script.Count - skipped} of {script.Count} steps ended on the expected page" +
                          (skipped > 0 ? $" ({skipped} Forward steps skipped: no Forward in this library)" : ""));
        return 0;
    }

    private static async Task MeasureAsync(Func<INavigationDriver> make, IReadOnlyList<Step> script)
    {
        var times = new double[Runs];
        var bytes = new long[Runs];
        var name = "";
        for (var run = -Warmups; run < Runs; run++)
        {
            using var driver = make();
            name = driver.Name;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var before = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();
            foreach (var step in script)
            {
                if (step.Op == Op.Forward && !driver.SupportsForward)
                    continue;
                await Scenario.RunAsync(driver, step);
            }

            watch.Stop();
            if (run >= 0)
            {
                times[run] = watch.Elapsed.TotalMilliseconds;
                bytes[run] = GC.GetTotalAllocatedBytes(true) - before;
            }
        }

        Array.Sort(times);
        Array.Sort(bytes);
        var ms = times[Runs / 2];
        var allocated = bytes[Runs / 2];
        Console.WriteLine($"| {name} | {ms:F2} | {ms * 1000 / script.Count:F1} | {allocated / 1024.0:F0} | {allocated / script.Count} |");
    }

    private static async Task MemoryAsync(Func<INavigationDriver> make)
    {
        using var driver = make();
        driver.State.SignedIn = true;
        Collect();
        var baseline = GC.GetTotalMemory(true);
        var weak = await ChangeRoutesAsync(driver);
        Collect();
        var heap = GC.GetTotalMemory(true) - baseline;
        var kept = weak.Count(w => w.IsAlive);
        var cleared = await driver.ClearHistoryAsync();
        Collect();
        var after = cleared ? weak.Count(w => w.IsAlive).ToString() : "n/a";
        Console.WriteLine($"| {driver.Name} | {heap / 1024.0:F0} | {kept} of {weak.Count} | {after} |");
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>Kept apart so that no local of the caller keeps a page alive.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<List<WeakReference>> ChangeRoutesAsync(INavigationDriver driver)
    {
        var weak = new List<WeakReference>();
        for (var i = 0; i < RouteChanges; i++)
        {
            await driver.GoAsync($"P{i % 40:00}");
            if (i < Tracked && driver.CurrentObject is { } page)
                weak.Add(new WeakReference(page));
        }

        return weak;
    }
}
