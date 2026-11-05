
//using Microsoft.AspNetCore.Mvc;
//using Routeables.AspNetCore;
//using Routeables.AspNetCore.Rendering;
//using XAnimals.Common;
//using XAnimals.Models;

//namespace XAnimals.WebApplication.Controllers;
////[ApiController]
//public sealed class RouteablesController(IRouteableRequestExecutor executor) : Controller
//{
//    [HttpGet]
//    public async Task<IActionResult> Index(string? route, string? template)
//    {
//        var target = string.IsNullOrWhiteSpace(route) ? "/cats" : "/" + route.TrimStart('/');
//        target += Request.QueryString.Value;
//        var result = await executor.NavigateAsync(target, template ?? target, HttpContext.RequestAborted);
//        return result.ToActionResult(ControllerContext);
//    }

//    //[HttpPost("/animals/select")]
//    //[ValidateAntiForgeryToken]
//    //public async Task<IActionResult> SelectAnimal(
//    //[FromRouteableViewModel] AnimalsViewModel service,
//    //[FromForm] string species,
//    //[FromForm] string name)
//    //{
//    //    //var vm = router.CurrentViewModel;

//    //    var animal = XAnimals.Data.CatData.Cats
//    //        .FirstOrDefault(c => c.Name == name);

//    //    if (animal is null)
//    //        return NotFound();

//    //    if (service != null)
//    //        await service.OnSelectedAnimalAsync(animal);

//    //    return Redirect(
//    //        $"/{species.ToLowerInvariant()}/{Uri.EscapeDataString(name)}");
//    //}

//    [HttpPost("/animals/select")]
//    [ValidateAntiForgeryToken]
//    public async Task<IActionResult> SelectAnimal(
//    [FromRouteableViewModel] AnimalsViewModel service,
//    [FromForm] string species,
//    [FromForm] string name)
//    {
//        var animal = XAnimals.Data.CatData.Cats.FirstOrDefault(c => c.Name == name);
//        if (animal is null)
//            return NotFound();

//        if (service != null)
//            await service.OnSelectedAnimalAsync(animal);

//        // Navigation dans le router singleton, puis rendu direct : pas de redirection.
//        var target = $"/{species.ToLowerInvariant()}/{Uri.EscapeDataString(name)}";
//        var result = await executor.NavigateAsync(target, "", HttpContext.RequestAborted);
//        return result.ToActionResult(ControllerContext);
//    }

//    [HttpPost("/animals/back"), ValidateAntiForgeryToken]
//    public IActionResult Back([FromForm] string species)
//    {
//        var allowed = new[] { "cats", "dogs", "monkeys", "elephants", "bears", "pandas" };
//        return Redirect(allowed.Contains(species, StringComparer.OrdinalIgnoreCase)
//            ? "/" + species.ToLowerInvariant() : "/cats");
//    }
//}
