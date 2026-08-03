using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;

using NUnit.Framework;

using GitPulse.UITests;

namespace GitPulse.AndroidUITests;

/// <summary>
///   Launches GitPulse on a local Android emulator once per assembly via Appium 2
///   + UiAutomator2. Mirrors <c>FlaUISetup</c> for the Android Emulator UI Smoke seam.
/// </summary>
[SetUpFixture]
public sealed class AppiumSetup
{
    private static AndroidDriver? _driver;

    public static AndroidDriver Driver =>
        _driver ?? throw new InvalidOperationException("AndroidDriver is not initialized.");

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        string? pat = Environment.GetEnvironmentVariable("GITPULSE_UI_TEST_PAT");
        if (string.IsNullOrWhiteSpace(pat))
        {
            Assert.Ignore(
                "GITPULSE_UI_TEST_PAT is not set. Configure a spare-account PAT in the User " +
                "environment (see docs/DEVELOPMENT.md) before running GitPulse.AndroidUITests.");
        }

        string apkPath = ResolveApkPath();
        if (!File.Exists(apkPath))
        {
            Assert.Fail(
                $"Android APK under test not found at '{apkPath}'. Build/install first " +
                "(see docs/DEVELOPMENT.md) or set GITPULSE_ANDROID_APK.");
        }

        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
        {
            AutomationName = "UiAutomator2",
            PlatformName = "Android",
            App = apkPath,
        };

        options.AddAdditionalAppiumOption(MobileCapabilityType.NewCommandTimeout, 120);
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("appium:appWaitActivity", "com.skymly.gitpulse.MainActivity");
        // Enable UiTestHostPage (host env vars do not reach the Android process).
        options.AddAdditionalAppiumOption(
            "appium:optionalIntentArguments",
            "--es GITPULSE_UI_TEST_HOST 1");

        string? avd = Environment.GetEnvironmentVariable("GITPULSE_ANDROID_AVD");
        if (string.IsNullOrWhiteSpace(avd))
        {
            avd = "GitPulse_API34_Phone";
        }

        // Boot default AVD when no device is already connected.
        if (!HasConnectedDevice())
        {
            options.AddAdditionalAppiumOption("appium:avd", avd.Trim());
            options.AddAdditionalAppiumOption("appium:avdLaunchTimeout", 180_000);
            options.AddAdditionalAppiumOption("appium:avdReadyTimeout", 180_000);
        }

        string? udid = Environment.GetEnvironmentVariable("GITPULSE_ANDROID_UDID");
        if (!string.IsNullOrWhiteSpace(udid))
        {
            options.AddAdditionalAppiumOption(MobileCapabilityType.Udid, udid.Trim());
        }

        _driver = new AndroidDriver(
            new Uri($"http://{AppiumServerHelper.DefaultHostAddress}:{AppiumServerHelper.DefaultHostPort}/"),
            options,
            TimeSpan.FromSeconds(180));

        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        WaitForUiTestHostReady();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        try
        {
            _driver?.Quit();
        }
        catch
        {
            // Best-effort cleanup.
        }
        finally
        {
            _driver?.Dispose();
            _driver = null;
            AppiumServerHelper.DisposeAppiumLocalServer();
        }
    }

    static void WaitForUiTestHostReady()
    {
        AndroidDriver driver = Driver;
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (driver.FindElements(MobileBy.Id($"{PackageId}:id/UiTestHostError")).Count > 0
                    || driver.FindElements(MobileBy.AccessibilityId("UiTestHostError")).Count > 0)
                {
                    Assert.Fail(
                        "UiTestHost failed to load." + Environment.NewLine +
                        CaptureDiagnostics("uitesthost-error"));
                }

                // Tabs expose title-case content-desc; page roots use resource-id.
                bool tabsReady =
                    driver.FindElements(MobileBy.AccessibilityId("Settings")).Count > 0
                    || driver.FindElements(MobileBy.AccessibilityId("Repos")).Count > 0;
                bool pageReady =
                    driver.FindElements(MobileBy.Id($"{PackageId}:id/{AutomationIds.ReposPageRoot}")).Count > 0
                    || driver.FindElements(MobileBy.Id($"{PackageId}:id/{AutomationIds.SettingsTokenEntry}")).Count > 0;

                if (tabsReady || pageReady)
                {
                    return;
                }
            }
            catch
            {
                // Keep polling while the activity settles.
            }

            Thread.Sleep(500);
        }

        Assert.Fail(
            "Timed out waiting for UiTestHost tabs/Settings after launch." + Environment.NewLine +
            CaptureDiagnostics("uitesthost-timeout"));
    }

    const string PackageId = "com.skymly.gitpulse";

    internal static string CaptureDiagnostics(string label)
    {
        try
        {
            string root = Path.Combine(FindRepoRoot(), "artifacts", "uitest-diagnostics");
            Directory.CreateDirectory(root);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string prefix = Path.Combine(root, $"android-{label}-{stamp}");

            if (_driver is not null)
            {
                try
                {
                    Screenshot shot = _driver.GetScreenshot();
                    shot.SaveAsFile(prefix + ".png");
                }
                catch
                {
                    // Best-effort.
                }

                try
                {
                    File.WriteAllText(prefix + "-page-source.xml", _driver.PageSource);
                }
                catch
                {
                    // Best-effort.
                }
            }

            return $"Diagnostics written under {root} (prefix android-{label}-{stamp}).";
        }
        catch (Exception ex)
        {
            return $"(failed to write diagnostics: {ex.Message})";
        }
    }

    internal static string ResolveApkPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("GITPULSE_ANDROID_APK");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath.Trim().Trim('"'));
        }

        string root = FindRepoRoot();
        string[] candidates =
        [
            Path.Combine(root, "artifacts", "GitPulse-android.apk"),
            Path.Combine(
                root,
                "src",
                "GitPulse.App",
                "bin",
                "Release",
                "net10.0-android",
                "com.skymly.gitpulse-Signed.apk"),
            Path.Combine(
                root,
                "src",
                "GitPulse.App",
                "bin",
                "Debug",
                "net10.0-android",
                "com.skymly.gitpulse-Signed.apk"),
        ];

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    internal static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GitPulse.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }

    static bool HasConnectedDevice()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "devices",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5_000);
            return output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"\tdevice$"));
        }
        catch
        {
            return false;
        }
    }
}
