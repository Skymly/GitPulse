namespace GitPulse.App.Services;

/// <summary>
/// Confirm a destructive action. Returns <see langword="true"/> only when the
/// user chooses the destructive button. Escape, Back, tapping outside, and
/// Cancel all return <see langword="false"/>.
/// </summary>
/// <remarks>
/// MAUI <c>DisplayAlertAsync</c> cannot set the WinUI <c>DefaultButton</c>.
/// Its Primary button takes initial focus, so Enter would activate the
/// destructive action. Windows therefore uses a <c>ContentDialog</c> with
/// <c>DefaultButton = Secondary</c> (Cancel). Android uses the stock alert:
/// Back already maps to not-accepted once the buttons are in the standard
/// order.
/// </remarks>
internal static class DestructiveConfirm
{
    public static Task<bool> ShowAsync(
        Page page,
        string title,
        string message,
        string destructive)
    {
#if WINDOWS
        return ShowWindowsAsync(page, title, message, destructive);
#else
        return page.DisplayAlertAsync(title, message, destructive, "Cancel");
#endif
    }

#if WINDOWS
    private static async Task<bool> ShowWindowsAsync(
        Page page,
        string title,
        string message,
        string destructive)
    {
        var native = page.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (native?.Content is not Microsoft.UI.Xaml.FrameworkElement content
            || content.XamlRoot is null)
        {
            return await page.DisplayAlertAsync(title, message, destructive, "Cancel");
        }

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = destructive,
            SecondaryButtonText = "Cancel",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Secondary,
            XamlRoot = content.XamlRoot,
            RequestedTheme = content.RequestedTheme,
        };

        var result = await dialog.ShowAsync();
        return result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary;
    }
#endif
}
