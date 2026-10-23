using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Razor;

/// <summary>
/// A Razor component container that dynamically renders a component of type <typeparamref name="T"/>,
/// optionally passing child content and parameter bags.
/// </summary>
/// <typeparam name="T">The Blazor component type to instantiate and render.</typeparam>
/// <param name="content">Optional child content to render inside the component's <c>ChildContent</c> parameter.</param>
/// <param name="parameters">Optional dictionary of parameter names and values to supply to the component.</param>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public partial class RouteableContentFor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
    object? content = null,
    Dictionary<string, object>? parameters = null)
    : RouteableFor
{
    protected override void OpenComponent(RenderTreeBuilder builder)
    {
        builder.OpenComponent(0, typeof(T));
    }

    /// <summary>
    /// Builds and returns the <see cref="RenderFragment"/> required to render the specified component <typeparamref name="T"/>
    /// along with its attributes and child content.
    /// </summary>
    /// <returns>A <see cref="RenderFragment"/> delegate if the view is a valid Blazor component; otherwise, <see langword="null"/>.</returns>
    public override RenderFragment? Render()
    {
        if (this is not IComponent component)
        {
            return null;
        }

        return builder =>
        {
            OpenComponent(builder);

            if (content is not null)
            {
                builder.AddAttribute(
                    1,
                    "ChildContent",
                    (RenderFragment)(childBuilder =>
                        childBuilder.AddContent(0, content)));
            }

            if (parameters is not null)
            {
                foreach (var parameter in parameters)
                {
                    builder.AddAttribute(
                        2,
                        parameter.Key,
                        parameter.Value);
                }
            }

            builder.CloseComponent();
        };
    }
}