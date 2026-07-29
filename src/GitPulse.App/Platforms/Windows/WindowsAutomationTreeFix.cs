#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using WinUIAutomation = Microsoft.UI.Xaml.Automation;
using WinUIAccessibilityView = Microsoft.UI.Xaml.Automation.Peers.AccessibilityView;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
///   Workaround for https://github.com/dotnet/maui/issues/4715 — Layouts / Pages /
///   ContentViews with AutomationId are often omitted from the Windows UIA tree,
///   which breaks FlaUI / Appium. Force Name + Content accessibility view when
///   AutomationId is set.
/// </summary>
public static class WindowsAutomationTreeFix
{
    public static MauiAppBuilder UseWindowsAutomationTreeFix(this MauiAppBuilder builder)
    {
        ViewHandler.ViewMapper.AppendToMapping(
            nameof(IView.AutomationId),
            static (handler, view) => Apply(handler.PlatformView as FrameworkElement, view));

        PageHandler.Mapper.AppendToMapping(
            nameof(IView.AutomationId),
            static (handler, view) => Apply(handler.PlatformView as FrameworkElement, view));

        return builder;
    }

    static void Apply(FrameworkElement? platformView, IView view)
    {
        if (platformView is null || string.IsNullOrEmpty(view.AutomationId))
        {
            return;
        }

        bool isContainer = view is Microsoft.Maui.ILayout
            or Page
            or ContentView
            or Border;
        if (!isContainer)
        {
            WinUIAutomation.AutomationProperties.SetAutomationId(platformView, view.AutomationId);
            return;
        }

        WinUIAutomation.AutomationProperties.SetAutomationId(platformView, view.AutomationId);
        WinUIAutomation.AutomationProperties.SetName(platformView, view.AutomationId);
        WinUIAutomation.AutomationProperties.SetAccessibilityView(platformView, WinUIAccessibilityView.Content);
    }
}
#endif
