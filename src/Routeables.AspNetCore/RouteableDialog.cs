/// <summary>A dialog the visitor has to answer before anything else: the page shows it, the next request answers it.</summary>
public sealed class RouteableDialog(string title, string? header, string? body, IReadOnlyList<string> buttons)
{
    /// <summary>The dialog title.</summary>
    public string Title { get; } = title;

    /// <summary>Optional header text shown above the body.</summary>
    public string? Header { get; } = header;

    /// <summary>Optional body content of the dialog.</summary>
    public string? Body { get; init; } = body;

    /// <summary>Optional footer text shown below the body.</summary>
    public string? Footer { get; init; }

    /// <summary>Whether a progress indicator should be shown.</summary>
    public bool ShowProgress { get; init; }

    /// <summary>The labels of the buttons the visitor can choose from.</summary>
    public IReadOnlyList<string> Buttons { get; } = buttons;

    /// <summary>The view model of a modal dialog (the note); a message has none.</summary>
    public object? ViewModel { get; init; }

    /// <summary>Completes with the zero-based index of the button the visitor selected.</summary>
    public TaskCompletionSource<int> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}