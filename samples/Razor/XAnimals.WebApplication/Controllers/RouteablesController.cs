using Microsoft.AspNetCore.Mvc;
using Routeables.AspNetCore;
namespace XAnimals.WebApplication.Controllers;
public sealed class RouteablesController(IRouteableRequestExecutor executor) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? route)
    {
        var target = string.IsNullOrWhiteSpace(route) ? "/cats" : "/" + route.TrimStart('/');
        target += Request.QueryString.Value;
        var result = await executor.NavigateAsync(target, HttpContext.RequestAborted);
        return result.ToActionResult(ControllerContext);
    }
    [HttpPost("/animals/select"), ValidateAntiForgeryToken]
    public IActionResult SelectAnimal([FromForm] string species, [FromForm] string name)
    {
        var allowed = new[] { "cats", "dogs", "monkeys", "elephants", "bears", "pandas" };
        if (string.IsNullOrWhiteSpace(name) || !allowed.Contains(species, StringComparer.OrdinalIgnoreCase)) return BadRequest();
        return Redirect("/" + species.ToLowerInvariant() + "/" + Uri.EscapeDataString(name));
    }
    [HttpPost("/animals/back"), ValidateAntiForgeryToken]
    public IActionResult Back([FromForm] string species)
    {
        var allowed = new[] { "cats", "dogs", "monkeys", "elephants", "bears", "pandas" };
        return Redirect(allowed.Contains(species, StringComparer.OrdinalIgnoreCase) ? "/" + species.ToLowerInvariant() : "/cats");
    }
}
