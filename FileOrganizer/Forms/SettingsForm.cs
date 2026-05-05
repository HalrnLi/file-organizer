using Microsoft.Win32;
using FileOrganizer.Core;
using FileOrganizer.Utils;

namespace FileOrganizer.Forms;

public class SettingsForm : Form
{
    private readonly ConfigManager _config;
    private CheckBox _startupCheck;
    private CheckBox _contextMenuCheck;
    private CheckBox _deleteEmptyDirCheck;
    private ComboBox _conflictCombo;

    public SettingsForm(ConfigManager config)
    {
        _config = config;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "设置";
        Size = new Size(400, 280);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var y = 20;

        _startupCheck = new CheckBox
        {
            Text = "开机自启",
            Top = y, Left = 20, Width = 300,
            Checked = _config.Settings.StartWithWindows
        };

        y += 35;
        _contextMenuCheck = new CheckBox
        {
            Text = "鼠标右键菜单",
            Top = y, Left = 20, Width = 300,
            Checked = _config.Settings.ContextMenuEnabled
        };

        y += 35;
        _deleteEmptyDirCheck = new CheckBox
        {
            Text = "移动后删除空源文件夹",
            Top = y, Left = 20, Width = 300,
            Checked = _config.Settings.DeleteEmptySourceDir
        };

        y += 35;
        var conflictLabel = new Label { Text = "默认冲突处理:", Top = y + 3, Left = 20, Width = 100 };

        _conflictCombo = new ComboBox
        {
            Top = y, Left = 125, Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _conflictCombo.Items.AddRange(new object[] { "弹窗选择", "自动加序号", "覆盖", "跳过" });
        _conflictCombo.SelectedIndex = _config.Settings.DefaultConflictAction switch
        {
            "autoRename" => 1,
            "overwrite" => 2,
            "skip" => 3,
            _ => 0
        };

        y += 40;
        var saveBtn = new Button
        {
            Text = "保存",
            Top = y, Left = 220, Width = 70, Height = 28
        };
        saveBtn.Click += SaveButton_Click;

        var cancelBtn = new Button
        {
            Text = "取消",
            Top = y, Left = 300, Width = 70, Height = 28
        };
        cancelBtn.Click += (s, e) => Close();

        Controls.AddRange(new Control[] {
            _startupCheck, _contextMenuCheck, _deleteEmptyDirCheck,
            conflictLabel, _conflictCombo,
            saveBtn, cancelBtn
        });
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        // Startup
        var startupKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (startupKey != null)
        {
            if (_startupCheck.Checked)
                startupKey.SetValue("FileOrganizer", $"\"{Environment.ProcessPath}\"");
            else
                startupKey.DeleteValue("FileOrganizer", false);
            startupKey.Dispose();
        }

        // Context menu
        if (_contextMenuCheck.Checked != _config.Settings.ContextMenuEnabled)
        {
            if (_contextMenuCheck.Checked)
                ShellExtensions.Register();
            else
                ShellExtensions.Unregister();
        }

        _config.Settings.StartWithWindows = _startupCheck.Checked;
        _config.Settings.ContextMenuEnabled = _contextMenuCheck.Checked;
        _config.Settings.DeleteEmptySourceDir = _deleteEmptyDirCheck.Checked;
        _config.Settings.DefaultConflictAction = _conflictCombo.SelectedIndex switch
        {
            1 => "autoRename",
            2 => "overwrite",
            3 => "skip",
            _ => "prompt"
        };
        _config.SaveSettings();

        Close();
    }
}
