namespace GitPulse.App.Views;

/// <summary>
/// Disposes a Transient page ViewModel only when the page leaves the back
/// stack (Page ViewModel lifetime). Tab switch, peek, and push keep Parent
/// set, so they do not Dispose. Call from <c>OnParentSet</c> after
/// <c>base.OnParentSet()</c> and pass <c>Parent is not null</c> — a live
/// read, not a cached flag.
/// </summary>
internal static class PageViewModelLifetime
{
    public static void OnParentSet(
        bool hasParent,
        IDisposable viewModel,
        ref bool hadParent,
        ref bool disposed,
        Action? extraDispose = null)
    {
        if (hasParent)
        {
            hadParent = true;
            return;
        }

        if (!hadParent || disposed)
            return;

        disposed = true;
        extraDispose?.Invoke();
        viewModel.Dispose();
    }
}
