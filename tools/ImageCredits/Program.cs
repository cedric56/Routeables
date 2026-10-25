using System.Text;
using ImageCredits;

// dotnet run --project tools/ImageCredits [path to THIRD-PARTY-NOTICES.md] [--refresh]
var path = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "THIRD-PARTY-NOTICES.md";
var refresh = args.Contains("--refresh");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"{path} not found: run the tool from the root of the repository, or give the path.");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

// Wikimedia asks every client to say who it is.
http.DefaultRequestHeaders.UserAgent.ParseAdd("RouteablesImageCredits/1.0 (https://github.com/cedric56/Routeables)");

var tool = new CreditsTool(http, Console.Out);
var text = await tool.UpdateAsync(File.ReadAllText(path), refresh);
File.WriteAllText(path, text.TrimEnd('\n') + "\n", new UTF8Encoding(false));
Console.WriteLine($"{path} written.");
return 0;
