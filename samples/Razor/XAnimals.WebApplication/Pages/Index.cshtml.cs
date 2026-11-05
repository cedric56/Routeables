using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace XAnimals.WebApplication.Pages
{
    public class ShellModel : PageModel
    {
        // L'adresse demandée ("/customers/42", "about"...) à passer au Routeables router.
        public string? Path => RouteData.Values.TryGetValue("path", out var value) ? value?.ToString() : null;

        public void OnGet() { }
    }
}
