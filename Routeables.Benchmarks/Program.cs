using BenchmarkDotNet.Running;
using Routeables.Benchmarks;

if (args.Contains("--leak"))
    return await LeakCheck.Run();

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
