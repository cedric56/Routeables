using Microsoft.AspNetCore.Mvc;

namespace XAnimals.WebApplication.Controllers;

[Route("about")]
public sealed class AboutController : Controller
{
    [HttpPost("polar-bears"), ValidateAntiForgeryToken]
    public IActionResult PolarBears() => Redirect("/bears?SearchText=polar");
}
