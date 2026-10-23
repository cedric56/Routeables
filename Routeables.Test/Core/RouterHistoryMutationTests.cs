using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
namespace Routeables.Tests.Core;

/// <summary>
/// The history operations used by PopAsync / BackToRootAsync / MoveToAsync and the removed-entry lists that
/// feed DestroyAsync, on the real <see cref="RouterHistory"/>.
/// </summary>
public class RouterHistoryMutationTests
{
    private static RouterEntry Entry(string name) => new(name, new TestView(), RouteableArgs.Create());

    private static RouterHistory With(params string[] names)
    {
        var history = new RouterHistory();
        foreach (var name in names)
            history.Push(Entry(name));
        return history;
    }

    private static List<string> Names(IEnumerable<RouterEntry> entries) => entries.Select(e => (string)e.ViewModel).ToList();

    [Fact]
    public void Push_ReturnsNothing_WhenNothingWasRemoved()
    {
        var history = new RouterHistory();

        Assert.Empty(history.Push(Entry("a")));
        Assert.Empty(history.Push(Entry("b")));
    }

    [Fact]
    public void Push_ReturnsTheForwardEntriesItCutOff()
    {
        var history = With("a", "b", "c");
        history.MoveBack();                                          // current = b

        var removed = history.Push(Entry("d"));

        Assert.Equal(["c"], Names(removed));
        Assert.Equal(["a", "b", "d"], Names(history.Snapshot()));
    }

    [Fact]
    public void Push_ReturnsTheOldestEntry_WhenTheHistoryOverflows()
    {
        var history = With(Enumerable.Range(0, RouterHistory.MaxSize).Select(i => $"e{i}").ToArray());

        var removed = history.Push(Entry("extra"));

        Assert.Equal(["e0"], Names(removed));
        Assert.Equal(RouterHistory.MaxSize, history.Count);
        Assert.Equal("e1", history.Root!.ViewModel);
        Assert.Equal("extra", history.Current!.ViewModel);
    }

    [Fact]
    public void Pop_RemovesTheCurrentEntryAndTheForwardEntries()
    {
        var history = With("a", "b", "c", "d");
        history.MoveBack();                                          // current = c, forward = d

        var removed = history.Pop();

        Assert.Equal(["c", "d"], Names(removed));
        Assert.Equal("b", history.Current!.ViewModel);
        Assert.Equal(2, history.Count);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Pop_DoesNothing_WhenThereIsNoPreviousEntry()
    {
        var empty = new RouterHistory();
        var single = With("a");

        Assert.Empty(empty.Pop());
        Assert.Empty(single.Pop());
        Assert.Equal(1, single.Count);
        Assert.Equal("a", single.Current!.ViewModel);
    }

    [Fact]
    public void MoveToRoot_RemovesEveryEntryAfterTheRoot()
    {
        var history = With("a", "b", "c");

        var removed = history.MoveToRoot();

        Assert.Equal(["b", "c"], Names(removed));
        Assert.Equal("a", history.Current!.ViewModel);
        Assert.Equal(1, history.Count);
        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void MoveToRoot_DoesNothing_WhenEmptyOrAlreadyAlone()
    {
        Assert.Empty(new RouterHistory().MoveToRoot());

        var single = With("a");
        Assert.Empty(single.MoveToRoot());
        Assert.Equal("a", single.Current!.ViewModel);
    }

    [Fact]
    public void MoveTo_ChangesTheCurrentEntry_WithoutRemovingAnything()
    {
        var history = With("a", "b", "c");

        history.MoveTo(0);
        Assert.Equal("a", history.Current!.ViewModel);
        Assert.Equal(3, history.Count);
        Assert.True(history.CanGoForward);

        history.MoveTo(2);
        Assert.Equal("c", history.Current!.ViewModel);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void MoveTo_IgnoresAnInvalidOrCurrentIndex()
    {
        var history = With("a", "b", "c");

        history.MoveTo(-1);
        history.MoveTo(3);
        history.MoveTo(2);                                           // already there

        Assert.Equal(2, history.CurrentIndex);
    }

    [Fact]
    public void At_ReturnsTheEntryOrNull()
    {
        var history = With("a", "b");

        Assert.Equal("a", history.At(0)!.ViewModel);
        Assert.Equal("b", history.At(1)!.ViewModel);
        Assert.Null(history.At(-1));
        Assert.Null(history.At(2));
    }

    [Fact]
    public void ReplaceCurrent_SwapsTheCurrentEntry_WithoutChangingTheCount()
    {
        var history = With("a", "b", "c");
        history.MoveBack();                                          // current = b

        history.ReplaceCurrent(Entry("x"));

        Assert.Equal(["a", "x", "c"], Names(history.Snapshot()));
        Assert.Equal("x", history.Current!.ViewModel);
        Assert.Equal(1, history.CurrentIndex);
    }

    [Fact]
    public void ReplaceCurrent_OnAnEmptyHistory_PushesTheEntry()
    {
        var history = new RouterHistory();

        history.ReplaceCurrent(Entry("a"));

        Assert.Equal("a", history.Current!.ViewModel);
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void Clear_ReturnsEveryEntryItRemoved()
    {
        var history = With("a", "b", "c");

        var removed = history.Clear();

        Assert.Equal(["a", "b", "c"], Names(removed));
        Assert.Equal(0, history.Count);
        Assert.Equal(-1, history.CurrentIndex);
        Assert.Null(history.Root);
    }

    [Fact]
    public void Changed_IsRaisedOncePerChange_AndNotForNoOps()
    {
        var history = new RouterHistory();
        var changes = 0;
        history.Changed += (_, _) => changes++;

        void Expect(int expected, Action act)
        {
            changes = 0;
            act();
            Assert.Equal(expected, changes);
        }

        Expect(0, () => history.MoveBack());                         // empty
        Expect(0, () => history.Pop());
        Expect(0, () => history.MoveToRoot());
        Expect(1, () => history.Push(Entry("a")));
        Expect(1, () => history.Push(Entry("b")));
        Expect(1, () => history.Push(Entry("c")));
        Expect(1, () => history.MoveBack());
        Expect(1, () => history.MoveForward());
        Expect(0, () => history.MoveForward());                      // nothing forward
        Expect(1, () => history.MoveTo(0));
        Expect(0, () => history.MoveTo(0));                          // already there
        Expect(0, () => history.MoveTo(7));                          // out of range
        Expect(1, () => history.ReplaceCurrent(Entry("x")));
        Expect(0, () => history.Pop());                              // at the root: nothing to go back to
        Expect(1, () => history.MoveTo(2));
        Expect(1, () => history.Pop());
        Expect(1, () => history.MoveToRoot());
        Expect(1, () => history.Clear());
    }
}
