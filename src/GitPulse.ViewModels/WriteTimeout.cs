namespace GitPulse.ViewModels;

internal static class WriteTimeout
{
    public const string MaybeSubmitted =
        "Request may have been submitted. Refresh to confirm.";

    public static bool IsCanceled(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is OperationCanceledException)
                return true;
        }

        return ex is HttpRequestException
            && ex.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase);
    }
}

