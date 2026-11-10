namespace Routeables.Abstraction.Locking;

/// <summary>
/// What a form (or any page that holds work the user would lose) asks to keep the menu from leaving it: it locks the entries of the menu
/// while it is being edited, and the menu is free again when the last form lets go. A view model depends on this contract only; the
/// implementation comes with <c>AddLocking()</c> of <c>Routeables.Locking</c>.
/// <code>
/// using var editing = formLock.Lock();   // the menu is locked until editing is disposed
/// </code>
/// Several forms can lock at the same time: the menu is unlocked when every one of them has disposed its lock. It is the lock of the menu, not a
/// guard: a page that must also refuse a route change by code implements <c>CanLeaveAsync</c>.
/// </summary>
public interface IRouteableFormLock
{
    /// <summary>Whether at least one form holds the lock.</summary>
    bool IsLocked { get; }

    /// <summary>Locks the menu until the returned token is disposed. Disposing it twice releases the lock once.</summary>
    /// <returns>The token of this lock.</returns>
    IDisposable Lock();
}
