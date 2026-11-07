using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using System.Diagnostics;
using System.Text.Json;

namespace Routeables.Benchmarks;

/// <summary>
/// A gate against regressions, not a measurement: runs the main route changes and compares them to <c>perf-baseline.json</c>. Two numbers for each:
/// the bytes allocated by one operation (stable from one machine to another, so the limit is tight) and its time, as a multiple of the time
/// a fixed loop takes on the same machine (the machine speed cancels out, so the limit is loose: a runner is noisy). Run with <c>--perf</c>;
/// <c>--perf --update</c> writes the baseline (do it on purpose, in a commit of its own, when the library got slower or faster for a reason).
/// </summary>
internal static class PerfCheck
{
    // A change is a regression above these: allocations +10 % and 256 bytes, time 4 times (best of the runs, against the best of the baseline).
    private const double AllocationFactor = 1.10;
    private const long AllocationSlack = 256;
    private const double TimeFactor = 4.0;
    private const int Operations = 4_000;
    private const int Runs = 7;

    private sealed record Entry(double Bytes, double Relative);

    public static async Task<int> Run(string[] args)
    {
        var update = args.Contains("--update");
        var path = args.SkipWhile(a => a != "--perf").Skip(1).FirstOrDefault(a => !a.StartsWith("--"))
                   ?? Path.Combine(AppContext.BaseDirectory, "perf-baseline.json");

        var measured = await MeasureAsync();
        foreach (var (name, entry) in measured)
            Console.WriteLine($"{name,-28} {entry.Bytes,9:F0} B/op   {entry.Relative,8:F2} x reference");

        if (update)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(measured, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Baseline written to {path}");
            return 0;
        }

        if (!File.Exists(path))
        {
            Console.WriteLine($"FAIL  no baseline at {path}: run with --perf --update");
            return 1;
        }

        var baseline = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path)) ?? [];
        var failures = 0;
        foreach (var (name, now) in measured)
        {
            if (!baseline.TryGetValue(name, out var before))
            {
                Console.WriteLine($"FAIL  {name}: not in the baseline");
                failures++;
                continue;
            }

            var bytesOk = now.Bytes <= before.Bytes * AllocationFactor + AllocationSlack;
            var timeOk = now.Relative <= before.Relative * TimeFactor;
            Console.WriteLine($"{(bytesOk && timeOk ? "PASS" : "FAIL")}  {name}: {now.Bytes:F0} B (baseline {before.Bytes:F0}), {now.Relative:F2}x (baseline {before.Relative:F2}x)");
            if (!bytesOk || !timeOk) failures++;
        }

        Console.WriteLine(failures == 0 ? "No regression." : $"{failures} regression(s).");
        return failures == 0 ? 0 : 1;
    }

    private static async Task<Dictionary<string, Entry>> MeasureAsync()
    {
        var reference = Reference();
        var result = new Dictionary<string, Entry>();

        using var provider = Bench.Build(factories: true);
        var router = provider.GetRequiredService<IRouter>();

        async Task Scenario(string name, Func<Task> operation)
        {
            await router.EraseHistoryAsync();
            for (var i = 0; i < 200; i++) await operation();          // warm up: JIT, caches

            double bestTime = double.MaxValue, bytes = 0;
            for (var run = 0; run < Runs; run++)
            {
                await router.EraseHistoryAsync();
                GC.Collect();
                var before = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                for (var i = 0; i < Operations; i++) await operation();
                clock.Stop();
                bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Operations;
                bestTime = Math.Min(bestTime, clock.Elapsed.TotalMilliseconds * 1_000_000 / Operations);
            }

            result[name] = new Entry(Math.Round(bytes), Math.Round(bestTime / reference, 2));
        }

        var flip = false;
        await Scenario("goto_address", async () => Bench.Ok(await router.GoToAsync((flip = !flip) ? "/a" : "/b")));
        await Scenario("goto_with_dependency", async () => Bench.Ok(await router.GoToAsync((flip = !flip) ? "/dep" : "/a")));
        await Scenario("goto_guarded", async () => Bench.Ok(await router.GoToAsync((flip = !flip) ? "/guarded" : "/a")));
        await Scenario("goto_parameter", async () => Bench.Ok(await router.GoToAsync((flip = !flip) ? "/orders/42" : "/a")));
        await Scenario("back_and_forward", async () =>
        {
            if (router.History.Count < 2) { Bench.Ok(await router.GoToAsync("/a")); Bench.Ok(await router.GoToAsync("/b")); }
            Bench.Ok(await router.GoBackAsync());
            Bench.Ok(await router.ForwardAsync());
        });
        return result;
    }

    /// <summary>The time, in nanoseconds, of a fixed amount of work on this machine: what the times are divided by.</summary>
    private static double Reference()
    {
        static long Work()
        {
            long sum = 0;
            for (var i = 0; i < 2_000_000; i++) sum += (i ^ (i >> 3)) % 7;
            return sum;
        }

        Work();
        var best = double.MaxValue;
        for (var run = 0; run < 9; run++)
        {
            var clock = Stopwatch.StartNew();
            if (Work() == -1) Console.WriteLine();
            clock.Stop();
            best = Math.Min(best, clock.Elapsed.TotalMilliseconds * 1_000_000);
        }
        return best / 2_000_000;
    }
}
