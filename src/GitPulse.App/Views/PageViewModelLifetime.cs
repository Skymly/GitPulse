namespace GitPulse.App.Views;

/// <summary>
/// Disposes a Transient page ViewModel only when the page leaves the back
/// stack (Page ViewModel lifetime). Tab switch, peek, and push keep Parent
/// set, so they do not Dispose.
/// </summary>
internal static class PageViewModelLifetime
{
    public static void OnParentSet(
        Page page,
        IDisposable viewModel,
        ref bool hadParent,
        ref bool disposed,
        Action? extraDispose = null)
    {
        if (page.Parent is not null)
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
