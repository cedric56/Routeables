using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Routeables.AspNetCore.Rendering;

namespace Routeables.AspNetCore;

public sealed class RouteableRequestResult
{
    public bool Success { get; private init; }

    public RouteableViewDescriptor? View { get; private init; }

    public object? ViewModel { get; private init; }

    public Exception? Exception { get; private init; }

    public static RouteableRequestResult Succeeded(
        RouteableViewDescriptor view,
        object viewModel)
    {
        return new RouteableRequestResult
        {
            Success = true,
            View = view,
            ViewModel = viewModel
        };
    }

    public static RouteableRequestResult Failed(
        Exception? exception = null)
    {
        return new RouteableRequestResult
        {
            Exception = exception
        };
    }

    public static RouteableRequestResult NotFound()
    {
        return new RouteableRequestResult();
    }

    public static RouteableRequestResult ViewNotFound(Type viewModelType)
    {
        return new RouteableRequestResult
        {
            Exception = new InvalidOperationException(
                $"No view was found for ViewModel '{viewModelType.FullName}'.")
        };
    }

    public IActionResult ToActionResult(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Success)
        {
            if (Exception is not null)
            {
                // Une erreur de résolution ou d'exécution.
                return new StatusCodeResult(
                    StatusCodes.Status500InternalServerError);
            }

            return new NotFoundResult();
        }

        if (View is null || ViewModel is null)
            return new NotFoundResult();

        var metadataProvider =
            context.HttpContext.RequestServices
                .GetRequiredService<IModelMetadataProvider>();

        var viewData = new ViewDataDictionary(
            metadataProvider,
            context.ModelState)
        {
            Model = ViewModel
        };

        return new ViewResult
        {
            ViewName = View.ViewPath,
            ViewData = viewData
        };
    }
}