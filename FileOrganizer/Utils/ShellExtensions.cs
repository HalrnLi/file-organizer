using Microsoft.Win32;

namespace FileOrganizer.Utils;

public static class ShellExtensions
{
    private const string FileKey = @"Software\Classes\*\shell\FileOrganizer";
    private const string DirKey = @"Software\Classes\Directory\shell\FileOrganizer";
    private const string MenuText = "整理(File Organizer)";

    public static void Register()
    {
        try
        {
            var exePath = $"\"{Environment.ProcessPath}\" \"%1\"";

            SetRegistryKey(FileKey, MenuText);
            SetRegistryKey($@"{FileKey}\command", exePath);
            SetRegistryKey(DirKey, MenuText);
            SetRegistryKey($@"{DirKey}\command", exePath);
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                $"注册右键菜单失败: {ex.Message}\n\n请尝试以管理员身份运行。",
                "注册失败",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }

    public static void Unregister()
    {
        try
        {
            DeleteRegistryKey(FileKey);
            DeleteRegistryKey(DirKey);
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                $"卸载右键菜单失败: {ex.Message}",
                "卸载失败",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }

    private static void SetRegistryKey(string path, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue("", value);
    }

    private static void DeleteRegistryKey(string path)
    {
        var parent = Path.GetDirectoryName(path.Replace('\\', '/')).Replace('/', '\\');
        var name = Path.GetFileName(path);
        if (parent != null)
        {
            using var parentKey = Registry.CurrentUser.OpenSubKey(parent, writable: true);
            parentKey?.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        }
    }
}
