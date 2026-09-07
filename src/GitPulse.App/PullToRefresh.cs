namespace GitPulse.App;

internal static class PullToRefresh
{
    public static void WindowsOff(RefreshView? view)
    {
        if (view is null)
            return;
#if WINDOWS
        view.IsEnabled = false;
#endif
    }
}
