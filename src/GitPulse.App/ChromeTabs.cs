namespace GitPulse.App;

/// <summary>
/// GitHub/Fluent destination tabs: transparent chrome, Accent text when
/// selected, muted when not. Not Domain Color, not filled Primary pills.
/// </summary>
internal static class ChromeTabs
{
    public static void Style(Button button, bool active)
    {
        var accent = Application.Current?.Resources["Primary"] as Color
            ?? Colors.DeepSkyBlue;
        var muted = Application.Current?.Resources["Gray500"] as Color
            ?? Colors.Gray;

        button.BackgroundColor = Colors.Transparent;
        button.TextColor = active ? accent : muted;
        button.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        button.BorderWidth = 0;
        button.CornerRadius = 0;
    }
}
