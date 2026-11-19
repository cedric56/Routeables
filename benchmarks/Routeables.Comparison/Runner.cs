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

    public static IReadOnlyList<Func<INavigationDriver>> Drivers(IEnumerable<Func<INavigationDriver>>? extra = null, bool console = true)
    {
        List<Func<INavigationDriver>> list = [];
        if (console)
            list.Add(() => new RouteablesDriver());
        if (extra is not null)
            list.AddRange(extra);
        if (console)
            list.Add(() => new HomeGrownDriver());
        return list;
    }

    /// <param name="console">Whether the two console drivers (Routeables without a view, the service of your own) are part of the run: the Avalonia programs leave them out and bring their own, with a real view for every page.</param>
    public static async Task<int> RunAsync(string[] args, IEnumerable<Func<INavigationDriver>>? extra = null, bool console = true)
    {
        var all = args.Length == 0;
        var drivers = Drivers(extra, console);
        var failures = 0;
        var script = Scenario.Build();
        Console.WriteLine($".NET {Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, " +
                          $"{Environment.ProcessorCount} cores, {(GCSettings.IsServerGC ? "server" : "workstation")} GC, {script.Count} steps, " +
                          $"{Runs} runs after {Warmups} warm-ups");
        // Which machine measured, and when: the programs of one job run one after the other on the same runner, so their figures can be read together.
        Console.WriteLine($"Runner: {Environment.GetEnvironmentVariable("RUNNER_NAME") ?? "not a CI runner"}, process {Environment.ProcessId}, started {DateTime.UtcNow:HH:mm:ss} UTC");
        Console.WriteLine();

        if (all || args.Contains("--check"))
            foreach (var make in drivers)
                failures += await CheckAsync(make, script);

        if (all || args.Contains("--measure"))
        {
            Console.WriteLine();
            Console.WriteLine("| Library | median ms / run | µs / step | KiB allocated / run | B / step |");
            Console.WriteLine("|---|---:|---:|---:|---:|");
            await MeasureAsync(drivers, script);
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

    // The libraries are measured in turns, in the same process, and the order changes from one run to the next. They are not measured one library
    // after the other: machine noise, which can move a library's time by a factor of two from one job to another, then affects all of them alike,
    // and the ratio of two libraries is the median of the ratios within the same run.
    private static async Task MeasureAsync(IReadOnlyList<Func<INavigationDriver>> drivers, IReadOnlyList<Step> script)
    {
        var count = drivers.Count;
        var times = new double[count][];
        var bytes = new long[count][];
        var names = new string[count];
        var positions = new int[count][]; // where each library came in its run
        for (var d = 0; d < count; d++)
        {
            times[d] = new double[Runs];
            bytes[d] = new long[Runs];
            positions[d] = new int[Runs];
        }

        for (var run = -Warmups; run < Runs; run++)
        {
            for (var k = 0; k < count; k++)
            {
                var d = (k + run + Warmups) % count;
                using var driver = drivers[d]();
                names[d] = driver.Name;
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
                    times[d][run] = watch.Elapsed.TotalMilliseconds;
                    bytes[d][run] = GC.GetTotalAllocatedBytes(true) - before;
                    positions[d][run] = k;
                }
            }
        }

        for (var d = 0; d < count; d++)
        {
            var sorted = (double[])times[d].Clone();
            var sortedBytes = (long[])bytes[d].Clone();
            Array.Sort(sorted);
            Array.Sort(sortedBytes);
            var ms = sorted[Runs / 2];
            var allocated = sortedBytes[Runs / 2];
            Console.WriteLine($"| {names[d]} | {ms:F2} | {ms * 1000 / script.Count:F1} | {allocated / 1024.0:F0} | {allocated / script.Count} |");
        }

        if (count > 1)
        {
            Console.WriteLine();
            Console.WriteLine($"Time of {names[0]} over the time of each other library, run by run (median, and the range of the middle half of the {Runs} runs; above 1 means {names[0]} is slower):");
            Console.WriteLine();
            for (var d = 1; d < count; d++)
            {
                var ratios = new double[Runs];
                for (var run = 0; run < Runs; run++)
                    ratios[run] = times[0][run] / times[d][run];
                Array.Sort(ratios);
                // A library can be faster or slower depending on its place in the run (what the previous one left behind, the state of the framework's caches).
                // So the ratio is also given for the runs where the first library came before the other and for those where it came after. The geometric mean of
                // the two cancels an effect that multiplies the time of whoever comes second.
                var before = new List<double>();
                var after = new List<double>();
                for (var run = 0; run < Runs; run++)
                    (positions[0][run] < positions[d][run] ? before : after).Add(times[0][run] / times[d][run]);
                var medianOf0 = Median(times[0]);
                var medianOfD = Median(times[d]);
                // Both ways of comparing are given. They agree when the runs are alike. When they differ, the order of measures or machine noise weighs on the
                // ratio, and neither figure should be quoted alone.
                Console.WriteLine($"- against {names[d]}: {ratios[Runs / 2]:F2} (from {ratios[Runs / 4]:F2} to {ratios[3 * Runs / 4]:F2}); ratio of the two median times: {medianOf0 / medianOfD:F2}");
                if (before.Count > 0 && after.Count > 0)
                {
                    var b = Median([.. before]);
                    var a = Median([.. after]);
                    Console.WriteLine($"  - {names[0]} measured first: {b:F2}, measured second: {a:F2}; the order cancelled: {Math.Sqrt(b * a):F2}");
                }
            }
        }
    }

    private static double Median(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
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
