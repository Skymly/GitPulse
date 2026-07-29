using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

using NUnit.Framework;

namespace GitPulse.UITests;

public abstract class BaseTest
{
    protected Window MainWindow => FlaUISetup.MainWindow;

    protected AutomationElement WaitForAutomationId(string automationId, TimeSpan? timeout = null)
    {
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(30);
        RetryResult<AutomationElement?> result = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout: wait,
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: false);

        if (result.Result is null)
        {
            throw new TimeoutException(
                $"Timed out after {wait.TotalSeconds:0}s waiting for AutomationId '{automationId}'." +
                Environment.NewLine +
                DescribeTree());
        }

        return result.Result;
    }

    protected AutomationElement WaitForName(string name, TimeSpan? timeout = null)
    {
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(30);
        RetryResult<AutomationElement?> result = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByName(name)),
            timeout: wait,
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: false);

        if (result.Result is null)
        {
            throw new TimeoutException(
                $"Timed out after {wait.TotalSeconds:0}s waiting for Name '{name}'." +
                Environment.NewLine +
                DescribeTree());
        }

        return result.Result;
    }

    protected void TapAutomationId(string automationId)
    {
        AutomationElement element = WaitForAutomationId(automationId);
        element.Focus();
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            element.Click();
        }

        Thread.Sleep(300);
    }

    protected void TapName(string name)
    {
        AutomationElement element = WaitForName(name);
        element.Focus();
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            element.Click();
        }

        Thread.Sleep(300);
    }

    protected void SelectShellTab(string tabTitle)
    {
        // AppShell → NavigationView TabItem Name; UiTestHost → TabbedPage tab Title.
        AutomationElement tab = WaitForName(tabTitle);
        tab.Focus();
        tab.Click();
        Thread.Sleep(500);
    }

    protected void EnterText(string automationId, string text)
    {
        AutomationElement element = WaitForAutomationId(automationId);
        element.Focus();
        element.Click();

        // Password Entry often refuses ValuePattern; use keyboard.
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(VirtualKeyShort.DELETE);
        Keyboard.Type(text);
    }

    protected static string RequirePat()
    {
        string? pat = Environment.GetEnvironmentVariable("GITPULSE_UI_TEST_PAT");
        if (string.IsNullOrWhiteSpace(pat))
        {
            Assert.Fail("GITPULSE_UI_TEST_PAT is not set.");
        }

        string value = pat ?? string.Empty;
        return value.Trim().Trim('"').Trim('\'');
    }

    protected string DescribeTree(int maxElements = 120)
    {
        try
        {
            AutomationElement[] all = MainWindow.FindAllDescendants();
            IEnumerable<string> lines = all
                .Take(maxElements)
                .Select(e =>
                {
                    string id = Safe(() => e.Properties.AutomationId.ValueOrDefault) ?? string.Empty;
                    string name = Safe(() => e.Properties.Name.ValueOrDefault) ?? string.Empty;
                    ControlType type = Safe(() => e.Properties.ControlType.ValueOrDefault);
                    return $"[{type}] id='{id}' name='{name}'";
                });

            return $"UIA3 descendants ({Math.Min(all.Length, maxElements)}/{all.Length}):" +
                   Environment.NewLine +
                   string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            return $"(failed to enumerate tree: {ex.Message})";
        }
    }

    static T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }
}
