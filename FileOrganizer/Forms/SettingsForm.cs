using Microsoft.Win32;
using FileOrganizer.Core;
using FileOrganizer.Utils;

namespace FileOrganizer.Forms;

public class SettingsForm : Form
{
    private readonly ConfigManager _config;
    private CheckBox _startupCheck = null!;
    private CheckBox _contextMenuCheck = null!;
    private CheckBox _deleteEmptyDirCheck = null!;
    private ComboBox _conflictCombo = null!;

    // Titlebar drag
    private bool _titleDragging;
    private Point _titleDragStart, _formStartPos;

    public SettingsForm(ConfigManager config)
    {
        _config = config;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "";
        Size = new Size(380, 280);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        DoubleBuffered = true;

        // --- Titlebar ---
        var titleBar = new Panel
        {
            Height = 36, Dock = DockStyle.Top,
            BackColor = Color.FromArgb(40, 43, 50),
            Cursor = Cursors.SizeAll
        };
        var titleLabel = new Label
        {
            Text = "设置", Left = 14, Top = 8,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Theme.Fg, BackColor = Color.Transparent,
            AutoSize = true
        };
        var closeBtn = new Button
        {
            Text = "×", Size = new Size(28, 28),
            Top = 4, Left = Width - 36,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 14f),
            ForeColor = Theme.Muted, BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand, TabStop = false
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.FlatAppearance.MouseOverBackColor = Theme.Danger;
        closeBtn.Click += (s, e) => Close();
        titleBar.Controls.Add(titleLabel);
        titleBar.Controls.Add(closeBtn);
        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _titleDragging = true;
                _titleDragStart = e.Location;
                _formStartPos = Location;
            }
        };
        titleBar.MouseMove += (s, e) =>
        {
            if (!_titleDragging) return;
            Location = new Point(
                _formStartPos.X + e.X - _titleDragStart.X,
                _formStartPos.Y + e.Y - _titleDragStart.Y);
        };
        titleBar.MouseUp += (s, e) => _titleDragging = false;
        Controls.Add(titleBar);

        // --- Body ---
        var body = new Panel
        {
            Top = 36, Left = 0, Width = Width, Height = Height - 36,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = Theme.Surface
        };
        var y = 4;

        _startupCheck = new CheckBox
        {
            Text = "开机自启",
            Top = y, Left = 0, Width = 300,
            Checked = _config.Settings.StartWithWindows
        };
        Theme.StyleCheckBox(_startupCheck);
        body.Controls.Add(_startupCheck);
        y += 32;

        _contextMenuCheck = new CheckBox
        {
            Text = "鼠标右键菜单 — 在资源管理器中显示\"整理(File Organizer)\"",
            Top = y, Left = 0, Width = 340,
            Checked = _config.Settings.ContextMenuEnabled
        };
        Theme.StyleCheckBox(_contextMenuCheck);
        body.Controls.Add(_contextMenuCheck);
        y += 32;

        _deleteEmptyDirCheck = new CheckBox
        {
            Text = "移动后删除空的源文件夹",
            Top = y, Left = 0, Width = 300,
            Checked = _config.Settings.DeleteEmptySourceDir
        };
        Theme.StyleCheckBox(_deleteEmptyDirCheck);
        body.Controls.Add(_deleteEmptyDirCheck);
        y += 36;

        var conflictLabel = new Label
        {
            Text = "默认冲突处理",
            Top = y + 3, Left = 0, AutoSize = true
        };
        Theme.StyleLabel(conflictLabel, muted: true);
        body.Controls.Add(conflictLabel);

        _conflictCombo = new ComboBox
        {
            Top = y + 20, Left = 0, Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Theme.StyleComboBox(_conflictCombo);
        _conflictCombo.Items.AddRange(new object[] { "弹窗选择", "自动加序号", "覆盖", "跳过" });
        _conflictCombo.SelectedIndex = _config.Settings.DefaultConflictAction switch
        {
            "autoRename" => 1,
            "overwrite" => 2,
            "skip" => 3,
            _ => 0
        };
        body.Controls.Add(_conflictCombo);

        // --- Buttons ---
        var saveBtn = new Button { Text = "保存", Top = body.Height - 46, Left = body.Width - 172, Width = 70, Height = 28 };
        Theme.StyleButton(saveBtn, primary: true);
        saveBtn.Click += SaveButton_Click;

        var cancelBtn = new Button { Text = "取消", Top = body.Height - 46, Left = body.Width - 94, Width = 70, Height = 28 };
        Theme.StyleButton(cancelBtn);
        cancelBtn.Click += (s, e) => Close();

        body.Controls.Add(saveBtn);
        body.Controls.Add(cancelBtn);
        Controls.Add(body);

        // Paint border
        Paint += (s, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
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
