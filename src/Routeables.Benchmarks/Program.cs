using BenchmarkDotNet.Running;
using Routeables.Benchmarks;

if (args.Contains("--leak"))
    return await LeakCheck.Run();

if (args.Contains("--perf"))
    return await PerfCheck.Run(args);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
