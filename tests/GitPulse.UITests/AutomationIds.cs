namespace GitPulse.UITests;

/// <summary>
///   Stable AutomationId constants shared by App XAML and UITests.
///   Keep names identical to the XAML AutomationId values.
/// </summary>
public static class AutomationIds
{
    public const string TabRepos = "TabRepos";
    public const string TabNotifications = "TabNotifications";
    public const string TabSearch = "TabSearch";
    public const string TabSettings = "TabSettings";

    public const string SettingsTokenEntry = "SettingsTokenEntry";
    public const string SettingsSaveTokenButton = "SettingsSaveTokenButton";
    public const string SettingsClearTokenButton = "SettingsClearTokenButton";
    public const string SettingsTokenStoredBanner = "SettingsTokenStoredBanner";
    public const string SettingsStatusMessage = "SettingsStatusMessage";

    public const string ReposPageRoot = "ReposPageRoot";
    public const string ReposSearchBar = "ReposSearchBar";
    public const string ReposFirstItem = "ReposFirstItem";

    public const string NotificationsPageRoot = "NotificationsPageRoot";
    public const string NotificationsRefreshButton = "NotificationsRefreshButton";

    public const string SearchPageRoot = "SearchPageRoot";
    public const string SearchSubmitButton = "SearchSubmitButton";

    public const string RepoDetailPageRoot = "RepoDetailPageRoot";
    public const string RepoDetailIssuesButton = "RepoDetailIssuesButton";
    public const string RepoDetailPrsButton = "RepoDetailPrsButton";
    public const string RepoDetailActionsButton = "RepoDetailActionsButton";

    public const string IssuesPageRoot = "IssuesPageRoot";
    public const string IssuesOpenPrsButton = "IssuesOpenPrsButton";

    public const string PullRequestsPageRoot = "PullRequestsPageRoot";
    public const string PullRequestsBackButton = "PullRequestsBackButton";

    public const string WorkflowRunsPageRoot = "WorkflowRunsPageRoot";
}
