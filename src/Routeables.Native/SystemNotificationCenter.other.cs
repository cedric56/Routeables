#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

namespace Routeables.Native;

// The plain net10.0 target has no notification center: nothing can be created.
public sealed partial class SystemNotificationCenter
{
    private partial Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    private partial void Present(SystemNotification notification)
    {
    }

    private partial void Cancel(uint id)
    {
    }

    private partial void CancelAll()
    {
    }
}
#endif
