using Routeables.Services;

namespace Routeables.Tests.Services;

/// <summary>The words the library writes itself (the buttons of a message box, the title of an application) can be translated.</summary>
[CollectionDefinition("Routeables texts", DisableParallelization = true)]
public sealed class RouteableTextsCollection;

/// <summary>The texts are global: these tests run alone and put them back.</summary>
[Collection("Routeables texts")]
public class RouteableTextsTests
{
    private static void WithTranslator(Func<string, string?>? translator, Action body)
    {
        var before = RouteableTexts.ButtonTranslator;
        RouteableTexts.ButtonTranslator = translator;
        try { body(); }
        finally { RouteableTexts.ButtonTranslator = before; }
    }

    [Fact]
    public void Without_a_translator_the_buttons_say_the_english_words()
        => WithTranslator(null, () =>
        {
            Assert.Equal("Yes", RouteableTexts.Button("Yes"));
            Assert.True(new[] { "Yes", "No", "Cancel" }.SequenceEqual(RouteableButtons.Labels("YesNoCancel")));
        });

    [Fact]
    public void A_translator_changes_the_label_and_not_the_word()
        => WithTranslator(word => word switch { "Yes" => "Oui", "No" => "Non", _ => null }, () =>
        {
            Assert.True(new[] { "Oui", "Non", "Cancel" }.SequenceEqual(RouteableButtons.Labels("YesNoCancel")));
            Assert.True(new[] { "Yes", "No", "Cancel" }.SequenceEqual(RouteableButtons.Split("YesNoCancel")));
        });

    [Fact]
    public void A_blank_or_failing_translation_keeps_the_word()
    {
        WithTranslator(_ => "  ", () => Assert.Equal("Abort", RouteableTexts.Button("Abort")));
        WithTranslator(_ => throw new InvalidOperationException("no catalog"), () => Assert.Equal("Abort", RouteableTexts.Button("Abort")));
    }

    [Fact]
    public void The_buttons_of_a_task_dialog_are_translated_and_still_answer_with_their_result()
        => WithTranslator(word => word == "Yes" ? "Oui" : null, () =>
        {
            var completed = 0;
            var session = new RouteableTaskDialogSession(
                new RouteableTaskDialog(), [RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.No], () => completed++);

            Assert.Equal(["Oui", "No"], session.Buttons.Select(b => b.Text));
            Assert.True(session.Buttons[0].Press());
            Assert.Equal(RouteableTaskDialogResult.Yes, session.Result);
            Assert.Equal(1, completed);
        });

    [Fact]
    public void The_title_of_an_application_is_set_once_and_a_blank_one_puts_the_default_back()
    {
        var before = RouteableTexts.ApplicationTitle;
        try
        {
            RouteableTexts.ApplicationTitle = "Mon application";
            Assert.Equal("Mon application", RouteableTexts.ApplicationTitle);

            RouteableTexts.ApplicationTitle = " ";
            Assert.Equal("Application", RouteableTexts.ApplicationTitle);
        }
        finally
        {
            RouteableTexts.ApplicationTitle = before;
        }
    }
}
