using System.Globalization;
using GitPulse.Core.Models;

namespace GitPulse.App.Converters;

internal static class ThemeColor
{
    public static Color Named(string lightKey, string darkKey)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var key = dark ? darkKey : lightKey;
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return dark ? Colors.White : Colors.Black;
    }
}

/// <summary>Visible GitHub PR state text: Open / Draft / Merged / Closed.</summary>
public sealed class PullRequestStateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Text(value as PullRequest);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static string Text(PullRequest? pr)
    {
        if (pr is null)
            return string.Empty;
        if (pr.Merged)
            return "Merged";
        if (pr.Draft && pr.State.Equals("open", StringComparison.OrdinalIgnoreCase))
            return "Draft";
        if (pr.State.Equals("closed", StringComparison.OrdinalIgnoreCase))
            return "Closed";
        return "Open";
    }
}

/// <summary>Domain Color for a pull request state. Color is never the only signal.</summary>
public sealed class PullRequestStateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pr = value as PullRequest;
        var text = PullRequestStateTextConverter.Text(pr);
        return text switch
        {
            "Merged" => ThemeColor.Named("DomainDone", "DomainDoneDark"),
            "Draft" => ThemeColor.Named("DomainDraft", "DomainDraftDark"),
            "Closed" => ThemeColor.Named("DomainDanger", "DomainDangerDark"),
            _ => ThemeColor.Named("DomainOpen", "DomainOpenDark"),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible issue state text. Not-planned is Closed until state_reason exists.</summary>
public sealed class IssueStateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Issue issue)
            return issue.State.Equals("closed", StringComparison.OrdinalIgnoreCase) ? "Closed" : "Open";
        if (value is string s)
            return s.Equals("closed", StringComparison.OrdinalIgnoreCase) ? "Closed" : "Open";
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Domain Color for an issue: open = success, closed-as-completed = done.</summary>
public sealed class IssueStateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var closed = value is Issue issue
            ? issue.State.Equals("closed", StringComparison.OrdinalIgnoreCase)
            : value is string s && s.Equals("closed", StringComparison.OrdinalIgnoreCase);
        return closed
            ? ThemeColor.Named("DomainDone", "DomainDoneDark")
            : ThemeColor.Named("DomainOpen", "DomainOpenDark");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>16px Domain Icon file for a notification subject type.</summary>
public sealed class NotificationIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var type = value as string;
        return type switch
        {
            "PullRequest" => "git_pull_request_16.png",
            "Issue" => "issue_opened_16.png",
            "Commit" => "git_commit_16.png",
            "Release" => "repo_16.png",
            "CheckSuite" => "check_circle_16.png",
            _ => "bell_16.png",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Trailing #number parsed from a notification subject API URL.</summary>
public sealed class NotificationNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var tail = url.TrimEnd('/').Split('/')[^1];
        return int.TryParse(tail, out var n) ? $"#{n}" : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Humanize a GitHub notification reason as Chrome Neutral caption text.</summary>
public sealed class NotificationReasonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string)?.Replace('_', ' ') ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Accent unread edge: 2px when true, 0 otherwise.</summary>
public sealed class UnreadEdgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 2d : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>16px Domain Icon for a pull request state.</summary>
public sealed class PullRequestIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pr = value as PullRequest;
        var text = PullRequestStateTextConverter.Text(pr);
        return text switch
        {
            "Merged" => "git_merge_16.png",
            "Draft" => "git_pull_request_draft_16.png",
            "Closed" => "git_pull_request_16.png",
            _ => "git_pull_request_16.png",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>16px Domain Icon for an issue state.</summary>
public sealed class IssueIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var closed = value is Issue issue
            ? issue.State.Equals("closed", StringComparison.OrdinalIgnoreCase)
            : value is string s && s.Equals("closed", StringComparison.OrdinalIgnoreCase);
        return closed ? "issue_closed_16.png" : "issue_opened_16.png";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>GitHub label hex (RRGGBB) to a Color. Name is always shown beside it.</summary>
public sealed class HexColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = (value as string)?.Trim().TrimStart('#');
        if (hex is { Length: 6 }
            && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return Color.FromRgb((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        return ThemeColor.Named("ChromeMuted", "ChromeMutedDark");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Check Run conclusion/status Domain Color. Color is never the only signal.</summary>
public sealed class CheckConclusionColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var state = value switch
        {
            CheckRun run => string.IsNullOrWhiteSpace(run.Conclusion) ? run.Status : run.Conclusion,
            string s => s,
            _ => string.Empty,
        };
        state = state.Trim().ToLowerInvariant();
        return state switch
        {
            "success" => ThemeColor.Named("DomainOpen", "DomainOpenDark"),
            "failure" or "timed_out" or "startup_failure" or "action_required" =>
                ThemeColor.Named("DomainDanger", "DomainDangerDark"),
            "cancelled" or "skipped" or "neutral" or "stale" =>
                ThemeColor.Named("DomainDraft", "DomainDraftDark"),
            _ => ThemeColor.Named("DomainAttention", "DomainAttentionDark"),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Check Run visible state: conclusion if present, otherwise status.</summary>
public sealed class CheckRunStateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CheckRun run)
            return string.IsNullOrWhiteSpace(run.Conclusion) ? run.Status : run.Conclusion;
        return value as string ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
