using FlaUI.Core.AutomationElements;

using NUnit.Framework;

namespace GitPulse.UITests;

/// <summary>
///   Windows UI smoke via FlaUI UIA3.
/// </summary>
public sealed class WindowsSmokeTests : BaseTest
{
    [Test]
    [Order(0)]
    public void AppLaunches_ShowsMainChrome()
    {
        WaitForName("Settings", TimeSpan.FromSeconds(45));
        WaitForName("Repos", TimeSpan.FromSeconds(5));
        Assert.Pass();
    }

    [Test]
    [Order(1)]
    public void ShellTabs_CanSelectEachFirstClassTab()
    {
        SelectShellTab("Repos");
        SelectShellTab("Notifications");
        SelectShellTab("Search");
        SelectShellTab("Settings");
        Assert.Pass();
    }

    [Test]
    [Order(2)]
    public void SavePat_FromSettings_ShowsStoredToken()
    {
        SelectShellTab("Settings");
        EnterText(AutomationIds.SettingsTokenEntry, RequirePat());
        TapAutomationId(AutomationIds.SettingsSaveTokenButton);

        Assert.That(
            WaitForAutomationId(AutomationIds.SettingsTokenStoredBanner, TimeSpan.FromSeconds(45)).IsAvailable,
            Is.True,
            "Expected stored-token banner after saving PAT.");
    }

    [Test]
    [Order(3)]
    public void ReposTab_LoadsWithoutCrash()
    {
        SelectShellTab("Repos");
        Assert.That(WaitForAutomationId(AutomationIds.ReposPageRoot).IsAvailable, Is.True);
        Assert.That(WaitForAutomationId(AutomationIds.ReposSearchBar).IsAvailable, Is.True);
    }

    [Test]
    [Order(4)]
    public void NotificationsTab_LoadsWithoutCrash()
    {
        SelectShellTab("Notifications");
        Assert.That(WaitForAutomationId(AutomationIds.NotificationsPageRoot).IsAvailable, Is.True);
        Assert.That(WaitForAutomationId(AutomationIds.NotificationsRefreshButton).IsAvailable, Is.True);
    }

    [Test]
    [Order(5)]
    public void SearchTab_LoadsWithoutCrash()
    {
        SelectShellTab("Search");
        Assert.That(WaitForAutomationId(AutomationIds.SearchPageRoot).IsAvailable, Is.True);
        Assert.That(WaitForAutomationId(AutomationIds.SearchSubmitButton).IsAvailable, Is.True);
    }

    [Test]
    [Order(6)]
    public void OpenRepo_ThenIssuesPrsActions_LoadWithoutCrash()
    {
        SelectShellTab("Repos");
        WaitForAutomationId(AutomationIds.ReposPageRoot);

        AutomationElement? firstRepo = TryFind(AutomationIds.ReposFirstItem, TimeSpan.FromSeconds(45));
        Assert.That(
            firstRepo,
            Is.Not.Null,
            "No repository row found. Ensure the spare account can list at least one repo.");

        firstRepo!.Click();
        Assert.That(WaitForAutomationId(AutomationIds.RepoDetailPageRoot).IsAvailable, Is.True);

        TapAutomationId(AutomationIds.RepoDetailIssuesButton);
        Assert.That(WaitForAutomationId(AutomationIds.IssuesPageRoot).IsAvailable, Is.True);

        // Stack nav: one back returns to RepoDetail (do not jump Issues → PRs).
        TapName("← Repo");
        Assert.That(WaitForAutomationId(AutomationIds.RepoDetailPageRoot).IsAvailable, Is.True);

        TapAutomationId(AutomationIds.RepoDetailPrsButton);
        Assert.That(WaitForAutomationId(AutomationIds.PullRequestsPageRoot).IsAvailable, Is.True);

        TapAutomationId(AutomationIds.PullRequestsBackButton);
        Assert.That(WaitForAutomationId(AutomationIds.RepoDetailPageRoot).IsAvailable, Is.True);

        TapAutomationId(AutomationIds.RepoDetailActionsButton);
        Assert.That(WaitForAutomationId(AutomationIds.WorkflowRunsPageRoot).IsAvailable, Is.True);
    }

    AutomationElement? TryFind(string automationId, TimeSpan timeout)
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
}
