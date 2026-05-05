namespace FileOrganizer.Core;

public class FileMoveResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DestinationPath { get; set; }
}

public class FileMover
{
    private readonly ConfigManager _config;

    public FileMover(ConfigManager config)
    {
        _config = config;
    }

    public FileMoveResult MoveFile(
        string sourcePath,
        string targetDir,
        string? newFileName = null,
        ConflictAction? conflictAction = null)
    {
        try
        {
            var resolvedTarget = Environment.ExpandEnvironmentVariables(targetDir);
            Directory.CreateDirectory(resolvedTarget);

            var fileName = newFileName ?? Path.GetFileName(sourcePath);
            var destPath = Path.Combine(resolvedTarget, fileName);

            var action = conflictAction ?? ParseConflictAction(_config.Settings.DefaultConflictAction);

            if (File.Exists(destPath))
            {
                switch (action)
                {
                    case ConflictAction.Skip:
                        return new FileMoveResult
                        {
                            Success = false,
                            ErrorMessage = "文件已存在，已跳过"
                        };
                    case ConflictAction.AutoRename:
                        destPath = GetUniqueFilePath(destPath);
                        break;
                    case ConflictAction.Overwrite:
                        File.Delete(destPath);
                        break;
                    case ConflictAction.Prompt:
                        return new FileMoveResult
                        {
                            Success = false,
                            ErrorMessage = "CONFLICT_PROMPT",
                            DestinationPath = destPath
                        };
                }
            }

            File.Move(sourcePath, destPath);

            return new FileMoveResult
            {
                Success = true,
                DestinationPath = destPath
            };
        }
        catch (Exception ex)
        {
            return new FileMoveResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string GetUniqueFilePath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        for (int i = 1; ; i++)
        {
            var newPath = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(newPath))
                return newPath;
        }
    }

    public static void CleanupSourceDirIfEmpty(string sourcePath, ConfigManager config)
    {
        if (!config.Settings.DeleteEmptySourceDir) return;
        var dir = Path.GetDirectoryName(sourcePath);
        if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            Directory.Delete(dir);
        }
    }

    internal static ConflictAction ParseConflictAction(string value) => value switch
    {
        "autoRename" => ConflictAction.AutoRename,
        "overwrite" => ConflictAction.Overwrite,
        "skip" => ConflictAction.Skip,
        _ => ConflictAction.Prompt
    };
}
