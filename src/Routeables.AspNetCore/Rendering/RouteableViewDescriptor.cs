using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore.Rendering
{
    public sealed record RouteableViewDescriptor(
    string ViewPath,
    Type ViewModelType);
}
