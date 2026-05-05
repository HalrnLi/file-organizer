namespace FileOrganizer.Utils;

public class Logger
{
    private readonly string _logPath;

    public Logger(string? logDir = null)
    {
        logDir ??= Path.GetDirectoryName(Environment.ProcessPath)!;
        _logPath = Path.Combine(logDir, "file-organizer.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message) => Write("ERROR", message);
    public void Warn(string message) => Write("WARN", message);

    private void Write(string level, string message)
    {
        try
        {
            File.AppendAllText(_logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
