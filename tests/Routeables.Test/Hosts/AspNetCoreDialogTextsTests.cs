using Routeables.AspNetCore;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

/// <summary>The buttons of a task dialog of a web page say what the translator of the application says, as in the other hosts.</summary>
[Collection("Routeables texts")]
public class AspNetCoreDialogTextsTests
{
    /// <summary>Answers the dialog that waits, naming it as the form of a page does.</summary>
    private static Task AnswerPending(RouteableInteractions interactions, int index)
        => interactions.Pending is { } dialog ? interactions.AnswerAsync(index, dialog.Id) : Task.CompletedTask;

    [Fact]
    public async Task The_buttons_of_a_task_dialog_are_translated()
    {
        var before = RouteableTexts.ButtonTranslator;
        RouteableTexts.ButtonTranslator = word => word == "Yes" ? "Oui" : null;
        try
        {
            using var interactions = new RouteableInteractions();
            var service = new RouteableDialogService(interactions);

            var result = service.ShowTaskDialogAsync(new RouteableTaskDialog { Title = "T", Content = "C" }, RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.No);

            Assert.Equal(["Oui", "No"], interactions.Pending!.Buttons);
            await AnswerPending(interactions, 0);
            Assert.Equal(RouteableTaskDialogResult.Yes, await result);
        }
        finally { RouteableTexts.ButtonTranslator = before; }
    }
}
