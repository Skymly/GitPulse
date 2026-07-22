namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Best-effort crash / fault logging for diagnosing native WinUI exits.
/// Writes to %TEMP%\GitPulse-crash.log and %LOCALAPPDATA%\GitPulse\crash.log.
/// </summary>
internal static class CrashLog
{
    private static readonly string TempLogPath =
        Path.Combine(Path.GetTempPath(), "GitPulse-crash.log");

    private static readonly string AppDataLogPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitPulse",
            "crash.log");

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:o} {message}";
            if (ex is not null)
                line += $"{Environment.NewLine}{ex}";

            line += Environment.NewLine + Environment.NewLine;

            File.AppendAllText(TempLogPath, line);

            var dir = Path.GetDirectoryName(AppDataLogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(AppDataLogPath, line);
        }
        catch
        {
            // Never throw from diagnostics.
        }
    }
}
