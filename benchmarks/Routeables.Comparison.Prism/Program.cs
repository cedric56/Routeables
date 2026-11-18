using Avalonia.Headless;
using Routeables.Comparison;
using Routeables.Comparison.Avalonia;
using Routeables.Comparison.Prism;

using var session = HeadlessUnitTestSession.StartNew(typeof(PrismBenchApp));
var code = 1;
await session.Dispatch(async () => code = await Runner.RunAsync(args, [() => new PrismDriver(), () => new ReactiveUiAvaloniaDriver()], console: false), CancellationToken.None);
return code;
