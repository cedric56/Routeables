using System;
using System.Collections.Generic;
using System.Text;

namespace Routeables.AspNetCore;

/// <summary>A dialog the visitor has to answer before anything else: the page shows it, the next request answers it.</summary>
public sealed class RouteableDialog(string title, string? header, string? body, IReadOnlyList<string> buttons)
{
    public string Title { get; } = title;

    public string? Header { get; } = header;

    public string? Body { get; init; } = body;

    public string? Footer { get; init; }

    public bool ShowProgress { get; init; }

    public IReadOnlyList<string> Buttons { get; } = buttons;

    /// <summary>The view model of a modal dialog (the note); a message has none.</summary>
    public object? ViewModel { get; init; }

    public TaskCompletionSource<int> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
