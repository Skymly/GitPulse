namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Best-effort crash / fault logging for diagnosing native WinUI exits.
/// Writes to %TEMP%\GitPulse-crash.log.
/// </summary>
internal static class CrashLog
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "GitPulse-crash.log");

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:o} {message}";
            if (ex is not null)
                line += $"{Environment.NewLine}{ex}";

            File.AppendAllText(LogPath, line + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
            // Never throw from diagnostics.
        }
    }
}
