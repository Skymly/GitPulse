namespace GitPulse.App;

/// <summary>
/// Shared GITPULSE_UI_TEST_HOST flag for UiTestHostPage and the in-memory
/// credential store. Android copies the launch intent extra into this variable
/// in MainActivity.OnCreate, after MauiProgram has already run.
/// </summary>
internal static class UiTestHost
{
    internal const string EnvironmentName = "GITPULSE_UI_TEST_HOST";
    internal const string EnabledValue = "1";

    internal static bool IsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentName),
            EnabledValue,
            StringComparison.Ordinal);

    internal static void Enable() =>
        Environment.SetEnvironmentVariable(EnvironmentName, EnabledValue);
}
