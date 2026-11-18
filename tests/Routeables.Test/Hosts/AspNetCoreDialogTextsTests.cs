using Routeables.AspNetCore;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

/// <summary>The buttons of a task dialog of a web page say what the translator of the application says, as in the other hosts.</summary>
[Collection("Routeables texts")]
public class AspNetCoreDialogTextsTests
{
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
            await interactions.AnswerAsync(0);
            Assert.Equal(RouteableTaskDialogResult.Yes, await result);
        }
        finally { RouteableTexts.ButtonTranslator = before; }
    }
}
