using Microsoft.Win32;
using System.Diagnostics;

namespace FileOrganizer.Utils;

public static class ShellExtensions
{
    private const string FileKey = @"*\shell\FileOrganizer";
    private const string DirKey = @"Directory\shell\FileOrganizer";
    private const string MenuText = "整理(File Organizer)";

    public static void Register()
    {
        try
        {
            var exePath = $"\"{Process.GetCurrentProcess().MainModule?.FileName}\" \"%1\"";

            // Register for files (*)
            using var fileKey = Registry.CurrentUser.CreateSubKey(FileKey);
            fileKey?.SetValue("", MenuText);
            using var fileCmd = Registry.CurrentUser.CreateSubKey($@"{FileKey}\command");
            fileCmd?.SetValue("", exePath);

            // Register for directories
            using var dirKey = Registry.CurrentUser.CreateSubKey(DirKey);
            dirKey?.SetValue("", MenuText);
            using var dirCmd = Registry.CurrentUser.CreateSubKey($@"{DirKey}\command");
            dirCmd?.SetValue("", exePath);

            // Also register for background (right-click in empty space)
            using var bgKey = Registry.CurrentUser.CreateSubKey(@"Directory\Background\shell\FileOrganizer");
            bgKey?.SetValue("", MenuText);
            using var bgCmd = Registry.CurrentUser.CreateSubKey(@"Directory\Background\shell\FileOrganizer\command");
            bgCmd?.SetValue("", exePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"注册右键菜单失败: {ex.Message}",
                "注册失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"*\shell\FileOrganizer", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Directory\shell\FileOrganizer", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Directory\Background\shell\FileOrganizer", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"卸载右键菜单失败: {ex.Message}",
                "卸载失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
