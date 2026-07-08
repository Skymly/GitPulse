using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Windows-specific extension methods for applying system backdrops
/// (Mica or Acrylic) to a WinUI <see cref="Microsoft.UI.Xaml.Window"/>.
/// Ported from the official WinUI Gallery SystemBackdrops sample and
/// Lance McCarthy's MAUI adaptation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mica</b> is the preferred backdrop on Windows 11 22H2+ — a subtle
/// translucent material that tints the desktop wallpaper. <b>Acrylic</b>
/// is the fallback for older Windows 11 builds. If neither is supported,
/// the window keeps its default solid background.
/// </para>
/// <para>
/// For the backdrop to be visible, the MAUI page backgrounds must be
/// transparent. This is handled by the global page style in
/// <c>Resources/Styles/Styles.xaml</c> (Windows-conditional).
/// </para>
/// </remarks>
public static class WindowHelpers
{
    /// <summary>
    /// Apply Mica (preferred) or Acrylic (fallback) system backdrop to
    /// the window. No-ops if neither is supported on the current OS.
    /// </summary>
    public static void TryMicaOrAcrylic(this Microsoft.UI.Xaml.Window window)
    {
        var dispatcherHelper = new WindowsSystemDispatcherQueueHelper();
        dispatcherHelper.EnsureWindowsSystemDispatcherQueueController();

        var configurationSource = new SystemBackdropConfiguration
        {
            IsInputActive = true,
        };

        // Match the backdrop theme to the window content theme.
        configurationSource.Theme = ((FrameworkElement)window.Content).ActualTheme switch
        {
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            ElementTheme.Light => SystemBackdropTheme.Light,
            _ => SystemBackdropTheme.Default,
        };

        // Try Mica first — preferred on Windows 11 22H2+.
        if (MicaController.IsSupported())
        {
            var micaController = new MicaController();
            micaController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
            micaController.SetSystemBackdropConfiguration(configurationSource);

            // Update backdrop theme when the window theme changes.
            window.Activated += (sender, args) =>
            {
                configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            };
        }
        // Fallback to Acrylic on older Windows 11 builds.
        else if (DesktopAcrylicController.IsSupported())
        {
            var acrylicController = new DesktopAcrylicController();
            acrylicController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
            acrylicController.SetSystemBackdropConfiguration(configurationSource);

            window.Activated += (sender, args) =>
            {
                configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            };
        }
        // Neither supported — keep default solid background.
    }
}
