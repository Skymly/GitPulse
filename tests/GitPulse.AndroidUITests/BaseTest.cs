using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

using NUnit.Framework;

using GitPulse.UITests;

namespace GitPulse.AndroidUITests;

public abstract class BaseTest
{
    protected const string AndroidPackageId = "com.skymly.gitpulse";

    protected AndroidDriver Driver => AppiumSetup.Driver;

    /// <summary>
    ///   MAUI maps AutomationId to Android <c>resource-id</c>
    ///   (<c>com.skymly.gitpulse:id/…</c>). Tab titles often expose the same
    ///   string as <c>content-desc</c> while displayed text is uppercased.
    /// </summary>
    protected AppiumElement WaitForAutomationId(string automationId, TimeSpan? timeout = null)
    {
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(30);
        DateTime deadline = DateTime.UtcNow + wait;

        while (DateTime.UtcNow < deadline)
        {
            AppiumElement? found = FindAutomationId(automationId);
            if (found is not null)
            {
                return found;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException(
            $"Timed out after {wait.TotalSeconds:0}s waiting for AutomationId '{automationId}'." +
            Environment.NewLine +
            AppiumSetup.CaptureDiagnostics($"wait-{automationId}"));
    }

    protected AppiumElement? FindAutomationId(string automationId)
    {
        try
        {
            // Prefer resource-id (MAUI AutomationId on Android).
            IReadOnlyCollection<AppiumElement> byId = Driver.FindElements(
                MobileBy.Id($"{AndroidPackageId}:id/{automationId}"));
            AppiumElement? match = byId.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }

            byId = Driver.FindElements(MobileBy.Id(automationId));
            match = byId.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }

            // Tabs / some chrome use content-desc instead.
            IReadOnlyCollection<AppiumElement> byA11y =
                Driver.FindElements(MobileBy.AccessibilityId(automationId));
            return byA11y.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    protected AppiumElement WaitForText(string text, TimeSpan? timeout = null)
    {
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(30);
        DateTime deadline = DateTime.UtcNow + wait;
        string escaped = EscapeUiAutomator(text);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                IReadOnlyCollection<AppiumElement> exact = Driver.FindElements(
                    MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{escaped}\")"));
                AppiumElement? first = exact.FirstOrDefault();
                if (first is not null)
                {
                    return first;
                }

                // Tab labels are often uppercased in the TextView while content-desc keeps title case.
                IReadOnlyCollection<AppiumElement> upper = Driver.FindElements(
                    MobileBy.AndroidUIAutomator(
                        $"new UiSelector().text(\"{EscapeUiAutomator(text.ToUpperInvariant())}\")"));
                first = upper.FirstOrDefault();
                if (first is not null)
                {
                    return first;
                }

                IReadOnlyCollection<AppiumElement> desc = Driver.FindElements(
                    MobileBy.AccessibilityId(text));
                first = desc.FirstOrDefault();
                if (first is not null)
                {
                    return first;
                }
            }
            catch
            {
                // Keep polling.
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException(
            $"Timed out after {wait.TotalSeconds:0}s waiting for text '{text}'." +
            Environment.NewLine +
            AppiumSetup.CaptureDiagnostics($"wait-text-{Sanitize(text)}"));
    }

    protected void TapAutomationId(string automationId)
    {
        AppiumElement element = WaitForAutomationId(automationId);
        element.Click();
        Thread.Sleep(300);
    }

    protected void TapText(string text)
    {
        AppiumElement element = WaitForText(text);
        element.Click();
        Thread.Sleep(300);
    }

    protected void SelectShellTab(string tabTitle)
    {
        // Prefer content-desc (title case) — displayed tab TextView is often UPPERCASE.
        AppiumElement tab = WaitForAutomationId(tabTitle);
        tab.Click();
        Thread.Sleep(500);
    }

    protected void EnterText(string automationId, string text)
    {
        AppiumElement element = WaitForAutomationId(automationId);
        element.Click();
        try
        {
            element.Clear();
        }
        catch
        {
            // Password Entry may not support Clear; fall through to overwrite.
        }

        element.SendKeys(text);
    }

    protected void NavigateBack()
    {
        AppiumElement? back = FindAutomationId(AutomationIds.PullRequestsBackButton);
        if (back is not null)
        {
            back.Click();
            Thread.Sleep(300);
            return;
        }

        Driver.Navigate().Back();
        Thread.Sleep(500);
    }

    protected static string RequirePat()
    {
        string? pat = Environment.GetEnvironmentVariable("GITPULSE_UI_TEST_PAT");
        if (string.IsNullOrWhiteSpace(pat))
        {
            Assert.Fail("GITPULSE_UI_TEST_PAT is not set.");
        }

        return (pat ?? string.Empty).Trim().Trim('"').Trim('\'');
    }

    protected AppiumElement? TryFindAutomationId(string automationId, TimeSpan timeout)
    {
        try
        {
            return WaitForAutomationId(automationId, timeout);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    [TearDown]
    public void CaptureOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status ==
            NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            AppiumSetup.CaptureDiagnostics(Sanitize(TestContext.CurrentContext.Test.Name));
        }
    }

    static string EscapeUiAutomator(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }
}
