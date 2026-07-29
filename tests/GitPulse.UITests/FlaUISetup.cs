using System.Diagnostics;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

using NUnit.Framework;

using Application = FlaUI.Core.Application;

namespace GitPulse.UITests;

/// <summary>
///   Launches the unpackaged MAUI Windows app once per assembly and exposes a
///   FlaUI UIA3 main window. Prefer FlaUI over Appium/WinAppDriver on Windows:
///   Shell ContentPage bodies are missing from WinAppDriver's tree but present
///   under UIA3 descendants.
/// </summary>
[SetUpFixture]
public sealed class FlaUISetup
{
    private static Application? _application;
    private static UIA3Automation? _automation;
    private static Window? _mainWindow;

    public static Window MainWindow =>
        _mainWindow ?? throw new InvalidOperationException("FlaUI main window is not initialized.");

    public static UIA3Automation Automation =>
        _automation ?? throw new InvalidOperationException("FlaUI automation is not initialized.");

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        string? pat = Environment.GetEnvironmentVariable("GITPULSE_UI_TEST_PAT");
        if (string.IsNullOrWhiteSpace(pat))
        {
            Assert.Ignore(
                "GITPULSE_UI_TEST_PAT is not set. Configure a spare-account PAT in the User " +
                "environment (see docs/DEVELOPMENT.md) before running GitPulse.UITests.");
        }

        string appPath = ResolveAppPath();
        if (!File.Exists(appPath))
        {
            Assert.Fail(
                $"UI app under test not found at '{appPath}'. Publish Windows first " +
                "(artifacts/publish/win-x64/GitPulse.App.exe) or set GITPULSE_UI_APP_PATH.");
        }

        // Avoid attaching to a leftover manual-smoke instance.
        foreach (Process leftover in Process.GetProcessesByName("GitPulse.App"))
        {
            try
            {
                leftover.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort.
            }
            finally
            {
                leftover.Dispose();
            }
        }

        // Prefer TabbedPage host so ContentPage controls appear in UIA.
        // Must be in the parent process env (UseShellExecute=true does not apply
        // ProcessStartInfo.Environment on Windows).
        Environment.SetEnvironmentVariable("GITPULSE_UI_TEST_HOST", "1");

        _automation = new UIA3Automation();
        _application = Application.Launch(new ProcessStartInfo
        {
            FileName = appPath,
            WorkingDirectory = Path.GetDirectoryName(appPath)!,
            UseShellExecute = true,
        });

        _mainWindow = Retry.WhileNull(
            () =>
            {
                _application.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(30));
                Window? window = _application.GetMainWindow(_automation);
                return window is { IsAvailable: true } ? window : null;
            },
            timeout: TimeSpan.FromSeconds(60),
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: true,
            timeoutMessage: "Timed out waiting for GitPulse main window via FlaUI UIA3.").Result!;

        _mainWindow.SetForeground();

        // UiTestHost swaps the placeholder on first Appearing; wait for tabs or Settings body.
        RetryResult<bool> ready = Retry.WhileFalse(
            () =>
            {
                if (_application.HasExited)
                {
                    return false;
                }

                // Refresh main window in case the page swap changed the HWND.
                Window? window = _application.GetMainWindow(_automation);
                if (window is { IsAvailable: true })
                {
                    _mainWindow = window;
                }

                if (_mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("UiTestHostError")) is not null)
                {
                    throw new InvalidOperationException(
                        "UiTestHost failed to load. Tree:" + Environment.NewLine + DescribeMainWindow());
                }

                return _mainWindow.FindFirstDescendant(cf => cf.ByName("Settings")) is not null
                    || _mainWindow.FindFirstDescendant(cf => cf.ByName("Save Token")) is not null
                    || _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SettingsTokenEntry")) is not null;
            },
            timeout: TimeSpan.FromSeconds(45),
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: false);

        if (!ready.Result)
        {
            Assert.Fail(
                "Timed out waiting for UiTestHost tabs/Settings after deferred load." +
                Environment.NewLine +
                DescribeMainWindow());
        }
    }

    static string DescribeMainWindow()
    {
        if (_mainWindow is null)
        {
            return "(no main window)";
        }

        try
        {
            AutomationElement[] all = _mainWindow.FindAllDescendants();
            return string.Join(
                Environment.NewLine,
                all.Take(80).Select(e =>
                {
                    string id = string.Empty;
                    string name = string.Empty;
                    try { id = e.Properties.AutomationId.ValueOrDefault ?? string.Empty; } catch { /* ignore */ }
                    try { name = e.Properties.Name.ValueOrDefault ?? string.Empty; } catch { /* ignore */ }
                    return $"id='{id}' name='{name}'";
                }));
        }
        catch (Exception ex)
        {
            return $"(enumerate failed: {ex.Message})";
        }
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        try
        {
            _application?.Close();
        }
        catch
        {
            try
            {
                _application?.Kill();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
        finally
        {
            _application?.Dispose();
            _application = null;
            _mainWindow = null;
            _automation?.Dispose();
            _automation = null;
        }
    }

    internal static string ResolveAppPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("GITPULSE_UI_APP_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath.Trim().Trim('"'));
        }

        string root = FindRepoRoot();
        string[] candidates =
        [
            Path.Combine(root, "artifacts", "publish", "win-x64", "GitPulse.App.exe"),
            Path.Combine(
                root,
                "src",
                "GitPulse.App",
                "bin",
                "Release",
                "net10.0-windows10.0.19041.0",
                "win-x64",
                "GitPulse.App.exe"),
            Path.Combine(
                root,
                "src",
                "GitPulse.App",
                "bin",
                "Debug",
                "net10.0-windows10.0.19041.0",
                "win-x64",
                "GitPulse.App.exe"),
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
}
