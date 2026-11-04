using Microsoft.Playwright;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Routeables.Test.Playwright;

/// <summary>
/// Starts the Blazor Server sample on a free port and one Chromium for all the tests; each test opens its own browser context, so each has
/// its own circuit (its own router, history and sign-in) and the tests do not see one another.
/// </summary>
public sealed class BlazorApp : IAsyncLifetime
{
    /// <summary>Path of the sample project relative to the solution root.</summary>
    private const string Project = "samples/Xanimals.BlazorApp/Xanimals.BlazorApp.csproj";

    private readonly List<string> _output = [];
    private Process? _process;
    private IPlaywright? _playwright;

    /// <summary>The address of the sample, with a trailing slash.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>The browser shared by the tests.</summary>
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Starts the sample process, waits until it answers HTTP, and launches Chromium.</summary>
    public async ValueTask InitializeAsync()
    {
        Assertions.SetDefaultExpectTimeout(15_000);

        var root = FindRoot();
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}/";

        // The sample was built by the project reference, in the configuration of this project (bin/<configuration>/<framework>).
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('/', '\\')).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "run", "--project", Project, "--no-build", "-c", configuration, "--no-launch-profile", "--urls", BaseUrl.TrimEnd('/') })
            start.ArgumentList.Add(argument);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";   // serves the static files of the libraries (Routeables.Razor, MudBlazor)

        _process = Process.Start(start) ?? throw new InvalidOperationException("The sample could not be started.");
        _process.OutputDataReceived += (_, e) => Remember(e.Data);
        _process.ErrorDataReceived += (_, e) => Remember(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitUntilReadyAsync();

        // Idempotent: does nothing when the browser is already there. CI installs it in a step of its own as well.
        Microsoft.Playwright.Program.Main(["install", "chromium"]);
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("ROUTEABLES_HEADED") != "1",
        });
    }

    /// <summary>Closes the browser and kills the sample process.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();
        _playwright?.Dispose();

        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);
        _process?.Dispose();
    }

    /// <summary>Remembers a line of process output, keeping only the last 80 lines.</summary>
    private void Remember(string? line)
    {
        if (line is null) return;
        lock (_output)
        {
            _output.Add(line);
            if (_output.Count > 80) _output.RemoveAt(0);
        }
    }

    /// <summary>Returns the remembered process output as a single string.</summary>
    private string Output()
    {
        lock (_output) return string.Join(Environment.NewLine, _output);
    }

    /// <summary>Polls the sample until it answers HTTP 200 or a 120-second timeout elapses.</summary>
    private async Task WaitUntilReadyAsync()
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
                throw new InvalidOperationException($"The sample stopped with code {_process.ExitCode}:{Environment.NewLine}{Output()}");
            try
            {
                using var response = await http.GetAsync(BaseUrl);
                if (response.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            await Task.Delay(500);
        }
        throw new TimeoutException($"The sample did not answer in 120 seconds:{Environment.NewLine}{Output()}");
    }

    /// <summary>Walks up from the test base directory until <c>Routeables.slnx</c> is found.</summary>
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Routeables.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Routeables.slnx was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Returns an unused TCP port on the loopback interface.</summary>
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

/// <summary>xUnit collection that shares a single <see cref="BlazorApp"/> instance across tests.</summary>
[CollectionDefinition("Blazor")]
public sealed class BlazorCollection : ICollectionFixture<BlazorApp>;