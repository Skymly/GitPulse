using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;

namespace GitPulse.App;

/// <summary>
///   Android entry activity. Stable Java name + optional UI Test Host intent for Appium.
/// </summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density,
    WindowSoftInputMode = SoftInput.AdjustResize)]
[Register("com.skymly.gitpulse.MainActivity")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        ApplyUiTestHostFromIntent();
        base.OnCreate(savedInstanceState);
    }

    /// <summary>
    ///   Host env vars do not reach the Android process. Appium passes
    ///   <c>--es GITPULSE_UI_TEST_HOST 1</c> via optionalIntentArguments so
    ///   <see cref="App"/> can enable <c>UiTestHostPage</c> the same way as Windows.
    /// </summary>
    void ApplyUiTestHostFromIntent()
    {
        string? flag = Intent?.GetStringExtra("GITPULSE_UI_TEST_HOST");
        if (string.Equals(flag, "1", StringComparison.Ordinal))
        {
            System.Environment.SetEnvironmentVariable("GITPULSE_UI_TEST_HOST", "1");
        }
    }
}
