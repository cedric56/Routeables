using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Routeables.Abstraction;

namespace Routeables.Razor;

/// <summary>
/// A base Razor component that implements <see cref="IRouteableFor"/> to dynamically render components 
/// with bound contexts, dictionary parameters, and CSS classes.
/// </summary>
public abstract class RouteableFor : ComponentBase, IRouteableFor
{
    /// <summary>
    /// Opens and defines the component type within the render tree builder. Must be overridden by derived classes.
    /// </summary>
    /// <param name="builder">The <see cref="RenderTreeBuilder"/> used to build the component.</param>
    /// <exception cref="NotImplementedException">Thrown if not overridden in a derived class.</exception>
    protected abstract void OpenComponent(RenderTreeBuilder builder);

    /// <summary>
    /// Gets or sets a dictionary of additional parameters passed dynamically to the component.
    /// </summary>
    [Parameter]
    public RouteablesParameters DictionaryParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the data context object passed to the rendered component.
    /// </summary>
    [Parameter]
    public object? Context { get; set; }

    /// <summary>
    /// Optional CSS class name(s) applied to the rendered component's root element.
    /// </summary>
    [Parameter]
    public virtual string Class { get; set; } = string.Empty;

    /// <summary>
    /// Builds and returns the <see cref="RenderFragment"/> for rendering the component, including its context and parameters.
    /// </summary>
    /// <returns>A <see cref="RenderFragment"/> delegate representing the rendered component markup.</returns>
    public virtual RenderFragment? Render()
    {
        return builder =>
        {
            OpenComponent(builder);
            builder.AddAttribute(1, nameof(Context), Context);
            foreach (var parameter in DictionaryParameters)
            {
                builder.AddAttribute(2,
                    parameter.Key,
                    parameter.Value);
            }
            builder.SetKey(this);
            builder.CloseComponent();
        };
    }
}