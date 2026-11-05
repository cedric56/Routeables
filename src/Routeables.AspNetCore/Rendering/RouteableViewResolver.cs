
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Routeables.AspNetCore.Rendering
{
    public static class RouteableViewModelHtmlExtensions
    {
        public static IHtmlContent RouteableViewModelToken(
            this IHtmlHelper html,
            string parameterName = "service")
        {
            ArgumentNullException.ThrowIfNull(html);

            var viewModel = html.ViewData.Model
                ?? throw new InvalidOperationException(
                    "The current view has no ViewModel.");

            var httpContext = html.ViewContext.HttpContext;

            var owner = httpContext.RequestServices
                .GetRequiredService<RouteableViewModelOwner>();

            var store = httpContext.RequestServices
                .GetRequiredService<IRouteableViewModelStore>();

            var ownerId = owner.GetOrCreate(httpContext);

            var token = store.Store(viewModel, ownerId);

            return new HtmlString(
                $"<input type=\"hidden\" name=\"{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(parameterName)}\" value=\"{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(token)}\" />");
        }
    }
    public sealed class RouteableViewModelModelBinder(
        IRouteableViewModelStore store,
        RouteableViewModelOwner owner)
        : IModelBinder
    {
        public Task BindModelAsync(
            ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var tokenResult = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);

            if (tokenResult == ValueProviderResult.None)
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    "Routeable ViewModel token is missing.");

                return Task.CompletedTask;
            }

            var token = tokenResult.FirstValue;

            var ownerId = owner.GetOrCreate(
                bindingContext.HttpContext);

            var method = typeof(IRouteableViewModelStore)
                .GetMethod(nameof(IRouteableViewModelStore.TryGet))!
                .MakeGenericMethod(bindingContext.ModelType);

            var arguments = new object?[]
            {
            token,
            ownerId,
            null
            };

            var found = (bool)method.Invoke(store, arguments)!;

            if (!found || arguments[2] is null)
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    "Routeable ViewModel token is invalid or expired.");

                return Task.CompletedTask;
            }

            bindingContext.Result = ModelBindingResult.Success(
                arguments[2]);

            return Task.CompletedTask;
        }
    }

    public sealed class RouteableViewModelOwner
    {
        private const string CookieName =
            "__Routeables_ViewModelOwner";

        public string GetOrCreate(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue(
                CookieName,
                out var ownerId) &&
                !string.IsNullOrWhiteSpace(ownerId))
            {
                return ownerId;
            }

            ownerId = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            context.Response.Cookies.Append(
                CookieName,
                ownerId,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromMinutes(30),
                    Path = "/"
                });

            return ownerId;
        }
}
    [AttributeUsage(
        AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class FromRouteableViewModelAttribute
        : ModelBinderAttribute
    {
        public FromRouteableViewModelAttribute()
            : base(typeof(RouteableViewModelModelBinder))
        {
        }
    }

    public interface IRouteableViewModelStore
    {
        string Store(object viewModel, string sessionId);

        bool TryGet<T>(
            string token,
            string sessionId,
            out T? viewModel);
    }

    public sealed class RouteableViewModelStore(
        IMemoryCache cache) : IRouteableViewModelStore
    {
        private static readonly TimeSpan Expiration =
            TimeSpan.FromMinutes(30);

        public string Store(object viewModel, string ownerId)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

            var token = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            cache.Set(
                token,
                new Entry(viewModel, ownerId),
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = Expiration
                });

            return token;
        }

        public bool TryGet<T>(
            string token,
            string ownerId,
            out T? viewModel)
        {
            viewModel = default;

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(ownerId))
                return false;

            if (!cache.TryGetValue(token, out Entry? entry) ||
                entry is null)
                return false;

            if (!string.Equals(
                entry.OwnerId,
                ownerId,
                StringComparison.Ordinal))
                return false;

            if (entry.ViewModel is not T typedViewModel)
                return false;

            viewModel = typedViewModel;
            return true;
        }

        private sealed record Entry(
            object ViewModel,
            string OwnerId);
    }
    public interface IRouteableViewResolver
    {
        Task<RouteableViewDescriptor?> ResolveAsync(
            Type viewModelType,
            CancellationToken cancellationToken = default);
    }

    public sealed class RouteableViewResolver : IRouteableViewResolver
    {
        private readonly IRazorViewEngine _viewEngine;

        public RouteableViewResolver(IRazorViewEngine viewEngine)
        {
            _viewEngine = viewEngine;
        }

        public Task<RouteableViewDescriptor?> ResolveAsync(
            Type viewModelType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(viewModelType);

            cancellationToken.ThrowIfCancellationRequested();

            // HomeViewModel => Home
            var viewFolder = viewModelType.Name;

            const string suffix = "ViewModel";

            if (viewFolder.EndsWith(
                suffix,
                StringComparison.Ordinal))
            {
                viewFolder = viewFolder[..^suffix.Length];
            }

            // ~/Views/Home/Index.cshtml
            var viewPath =
                $"~/Views/{viewFolder}/Index.cshtml";

            var result = _viewEngine.GetView(
                executingFilePath: null,
                viewPath: viewPath,
                isMainPage: true);

            if (!result.Success)
            {
                return Task.FromResult<RouteableViewDescriptor?>(
                    null);
            }

            var descriptor = new RouteableViewDescriptor(
                result.View.Path,
                viewModelType);

            return Task.FromResult<RouteableViewDescriptor?>(
                descriptor);
        }
    }
}
