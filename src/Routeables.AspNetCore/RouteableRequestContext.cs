using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore
{
    public sealed class RouteableRequestContext
    {
        public required HttpContext HttpContext { get; init; }

        public required string Route { get; init; }

        public required string Template { get; init; }

        public IReadOnlyDictionary<string, object?>
            RouteValues
        { get; init; }
            = new Dictionary<string, object?>();

        public CancellationToken CancellationToken
            => HttpContext.RequestAborted;
    }
}
