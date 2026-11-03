using Routeables.Avalonia;

namespace Routeables.Tests.Avalonia;

public class TopLevelStateManagerTests
{
    [Fact]
    public void Default_provides_a_top_level_state_manager()
    {
        Assert.NotNull(TopLevelStateManager.Default);
    }
}
