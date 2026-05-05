namespace FileOrganizer.Core;

public enum ConflictAction
{
    Prompt,
    AutoRename,
    Overwrite,
    Skip
}

public class Rule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Settings
{
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ContextMenuEnabled { get; set; } = true;
    public string DefaultConflictAction { get; set; } = "prompt";
    public bool DeleteEmptySourceDir { get; set; } = false;
    public int FloatingWindowX { get; set; } = 100;
    public int FloatingWindowY { get; set; } = 100;
}
