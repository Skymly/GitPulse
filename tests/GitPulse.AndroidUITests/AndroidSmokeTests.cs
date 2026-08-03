using OpenQA.Selenium.Appium;

using NUnit.Framework;

using GitPulse.UITests;

namespace GitPulse.AndroidUITests;

/// <summary>
///   Android Emulator UI Smoke via Appium UiAutomator2.
///   Mirrors Windows FlaUI short smoke first-class path.
/// </summary>
public sealed class AndroidSmokeTests : BaseTest
{
    [Test]
    [Order(0)]
    public void AppLaunches_ShowsMainChrome()
    {
        WaitForText("Settings", TimeSpan.FromSeconds(45));
        WaitForText("Repos", TimeSpan.FromSeconds(5));
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
            WaitForAutomationId(AutomationIds.SettingsTokenStoredBanner, TimeSpan.FromSeconds(45)).Displayed,
            Is.True,
            "Expected stored-token banner after saving PAT.");
    }

    [Test]
    [Order(3)]
    public void ReposTab_LoadsWithoutCrash()
    {
        SelectShellTab("Repos");
        Assert.That(WaitForAutomationId(AutomationIds.ReposPageRoot).Displayed, Is.True);
        Assert.That(WaitForAutomationId(AutomationIds.ReposSearchBar).Displayed, Is.True);
    }

    [Test]
    [Order(4)]
    public void NotificationsTab_LoadsWithoutCrash()
    {
        SelectShellTab("Notifications");
        Assert.That(WaitForAutomationId(AutomationIds.NotificationsPageRoot).Displayed, Is.True);
        Assert.That(WaitForAutomationId(AutomationIds.NotificationsRefreshButton).Displayed, Is.True);
    }

    [Test]
    [Order(5)]
    public void SearchTab_LoadsWithoutCrash()
    {
        SelectShellTab("Search");
        Assert.That(WaitForAutomationId(AutomationIds.SearchPageRoot).Displayed, Is.True);
        Assert.That(WaitForAutomationId(AutomationIds.SearchSubmitButton).Displayed, Is.True);
    }

    [Test]
    [Order(6)]
    public void OpenRepo_ThenIssuesPrsActions_LoadWithoutCrash()
    {
        SelectShellTab("Repos");
        WaitForAutomationId(AutomationIds.ReposPageRoot);

        // UiTestHost may have Appeared Repos before PAT was saved (_loaded once).
        // Explicit reload matches the on-screen "Load Repositories" control.
        TapText("Load Repositories");

        AppiumElement? firstRepo = TryFindAutomationId(AutomationIds.ReposFirstItem, TimeSpan.FromSeconds(60));
        Assert.That(
            firstRepo,
            Is.Not.Null,
            "No repository row found. Ensure the spare account can list at least one repo.");

        firstRepo!.Click();
        Assert.That(WaitForAutomationId(AutomationIds.RepoDetailPageRoot).Displayed, Is.True);

        TapAutomationId(AutomationIds.RepoDetailIssuesButton);
        Assert.That(WaitForAutomationId(AutomationIds.IssuesPageRoot).Displayed, Is.True);

        // Stack nav: one back returns to RepoDetail (do not jump Issues → PRs).
        NavigateBack();
        Assert.That(WaitForAutomationId(AutomationIds.RepoDetailPageRoot).Displayed, Is.True);

        TapAutomationId(AutomationIds.RepoDetailPrsButton);
        Assert.That(WaitForAutomationId(AutomationIds.PullRequestsPageRoot).Displayed, Is.True);

        TapAutomationId(AutomationIds.PullRequestsBackButton);
        Assert.That(WaitForAutomationId(AutomationIds.RepoDetailPageRoot).Displayed, Is.True);

        TapAutomationId(AutomationIds.RepoDetailActionsButton);
        Assert.That(WaitForAutomationId(AutomationIds.WorkflowRunsPageRoot).Displayed, Is.True);
    }
}
