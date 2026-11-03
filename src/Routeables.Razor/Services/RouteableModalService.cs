using Microsoft.AspNetCore.Components;

namespace Routeables.Razor.Services;

/// <summary>A modal currently shown by <see cref="RouteableModalHost"/>.</summary>
internal sealed class ModalRequest(string title, RenderFragment content)
{
    /// <summary>The text shown in the modal header.</summary>
    public string Title { get; } = title;

    /// <summary>The view displayed in the modal body.</summary>
    public RenderFragment Content { get; } = content;

    /// <summary>The modal element, captured by the host so the focus can be moved into it.</summary>
    public ElementReference Element { get; set; }

    /// <summary>Whether the focus has already been moved into the modal.</summary>
    public bool Focused { get; set; }
}

/// <summary>
/// Shows views in modal dialogs without any UI library. The service only keeps the stack of open modals;
/// <see cref="RouteableModalHost"/>, placed once in the application's layout, renders them.
/// A modal opened while another one is open is stacked on top of it, and the one underneath keeps its state.
/// </summary>
internal sealed class RouteableModalService
{
    private readonly List<ModalRequest> stack = [];
    private readonly object gate = new();

    /// <summary>Raised when the stack changes. The host subscribes to it to re-render.</summary>
    public event Action? Changed;

    /// <summary>The open modals, the oldest first.</summary>
    public IReadOnlyList<ModalRequest> Items
    {
        get
        {
            lock (gate) return stack.ToArray();
        }
    }

    /// <summary>Opens a modal on top of the ones already open.</summary>
    /// <exception cref="InvalidOperationException">No <see cref="RouteableModalHost"/> is displayed.</exception>
    public ModalRequest Show(string title, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (Changed is null)
            throw new InvalidOperationException(
                "No modal host is displayed. Add <RouteableModalHost /> to your layout so modals can be shown.");

        var request = new ModalRequest(title ?? string.Empty, content);
        lock (gate) stack.Add(request);

        Changed?.Invoke();
        return request;
    }

    /// <summary>Closes <paramref name="request"/>. Closing a modal that is already closed does nothing.</summary>
    public void Close(ModalRequest request)
    {
        bool removed;
        lock (gate) removed = stack.Remove(request);

        if (removed) Changed?.Invoke();
    }
}