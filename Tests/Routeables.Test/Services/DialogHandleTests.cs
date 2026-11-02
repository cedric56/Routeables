using Routeables.Services;

namespace Routeables.Tests.Services;

public class DialogHandleTests
{
    [Fact]
    public void Complete_closes_the_surface_once()
    {
        var handle = new RouteableDialogHandle();
        var closes = 0;
        handle.CloseSurface = () => closes++;

        handle.Complete();
        handle.Complete();

        Assert.True(handle.Completed);
        Assert.Equal(1, closes);
    }

    [Fact]
    public void A_dismissal_can_be_vetoed()
    {
        var handle = new RouteableDialogHandle { TryDismiss = () => false };
        var closes = 0;
        handle.CloseSurface = () => closes++;

        Assert.False(handle.RequestDismiss());
        Assert.False(handle.AcceptDismiss());

        Assert.False(handle.Completed);
        Assert.Equal(0, closes);
    }

    [Fact]
    public void An_accepted_dismissal_completes_the_dialog_and_closes_the_surface()
    {
        var handle = new RouteableDialogHandle { TryDismiss = () => true };
        var closes = 0;
        handle.CloseSurface = () => closes++;

        Assert.True(handle.RequestDismiss());

        Assert.True(handle.Completed);
        Assert.Equal(1, closes);
    }

    [Fact]
    public void Once_completed_a_dismissal_is_accepted_without_asking_again()
    {
        var asked = 0;
        var handle = new RouteableDialogHandle { TryDismiss = () => { asked++; return false; } };
        handle.Complete();

        // This is what a window's Closing handler sees after a button closed it.
        Assert.True(handle.AcceptDismiss());
        Assert.Equal(0, asked);
    }

    [Fact]
    public void A_dialog_completed_before_it_is_on_screen_closes_as_soon_as_it_opens()
    {
        var handle = new RouteableDialogHandle();
        var opened = 0;
        var closes = 0;
        handle.Opened = () => opened++;

        handle.Complete();
        handle.CloseSurface = () => closes++;
        Assert.Equal(0, closes);

        handle.NotifyOpened();
        handle.NotifyOpened();

        Assert.Equal((2, 1), (opened, closes));
    }
}
