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
        Size = new Size(540, 500);
        StartPosition = FormStartPosition.CenterScreen;
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
            Text = "×", Size = new Size(32, 28),
            Top = 4,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 14f),
            ForeColor = Theme.Fg, BackColor = Color.FromArgb(60, 63, 70),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand, TabStop = false
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.FlatAppearance.MouseOverBackColor = Theme.Danger;
        closeBtn.Click += (s, e) => Close();
        void LayoutTitleButtons()
        {
            closeBtn.Left = titleBar.ClientSize.Width - closeBtn.Width - 8;
        }
        Shown += (s, e) => BeginInvoke(LayoutTitleButtons);
        titleBar.Resize += (s, e) => LayoutTitleButtons();
        titleBar.Controls.Add(titleLabel);
        titleBar.Controls.Add(closeBtn);
        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _titleDragging = true;
                _titleDragStart = Cursor.Position;
                _formStartPos = Location;
            }
        };
        titleBar.MouseMove += (s, e) =>
        {
            if (!_titleDragging) return;
            var screenPos = Cursor.Position;
            Location = new Point(
                _formStartPos.X + screenPos.X - _titleDragStart.X,
                _formStartPos.Y + screenPos.Y - _titleDragStart.Y);
        };
        titleBar.MouseUp += (s, e) => _titleDragging = false;
        Controls.Add(titleBar);

        // --- Body ---
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(36, 24, 28, 24),
            BackColor = Theme.Surface
        };
        var contentWidth = 420;
        var content = new Panel
        {
            Width = contentWidth,
            Height = 388,
            Left = (540 - contentWidth) / 2,
            Top = 28,
            BackColor = Color.Transparent
        };
        var y = 12;

        _startupCheck = new CheckBox
        {
            Text = "开机自启",
            Top = y, Left = 0, Width = 380,
            Checked = _config.Settings.StartWithWindows
        };
        Theme.StyleCheckBox(_startupCheck);
        content.Controls.Add(_startupCheck);
        y += 40;

        _contextMenuCheck = new CheckBox
        {
            Text = "鼠标右键菜单 — 在资源管理器中显示\"整理(File Organizer)\"",
            Top = y, Left = 0, Width = 380,
            Checked = _config.Settings.ContextMenuEnabled
        };
        Theme.StyleCheckBox(_contextMenuCheck);
        content.Controls.Add(_contextMenuCheck);
        y += 40;

        _deleteEmptyDirCheck = new CheckBox
        {
            Text = "移动后删除空的源文件夹",
            Top = y, Left = 0, Width = 380,
            Checked = _config.Settings.DeleteEmptySourceDir
        };
        Theme.StyleCheckBox(_deleteEmptyDirCheck);
        content.Controls.Add(_deleteEmptyDirCheck);
        y += 44;

        var conflictLabel = new Label
        {
            Text = "默认冲突处理",
            Top = y + 3, Left = 0, AutoSize = true
        };
        Theme.StyleLabel(conflictLabel, muted: true);
        content.Controls.Add(conflictLabel);

        _conflictCombo = new ComboBox
        {
            Top = y + 22, Left = 0, Width = 200,
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
        content.Controls.Add(_conflictCombo);
        y += 60;

        // --- Buttons ---
        var saveBtn = new Button { Text = "保存", Top = 344, Left = contentWidth - 148, Width = 70, Height = 28 };
        Theme.StyleButton(saveBtn, primary: true);
        saveBtn.Click += SaveButton_Click;
        saveBtn.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

        var cancelBtn = new Button { Text = "取消", Top = 344, Left = contentWidth - 70, Width = 70, Height = 28 };
        Theme.StyleButton(cancelBtn);
        cancelBtn.Click += (s, e) => Close();
        cancelBtn.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

        content.Controls.Add(saveBtn);
        content.Controls.Add(cancelBtn);
        body.Controls.Add(content);
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

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0201) // WM_LBUTTONDOWN
            {
                var pt = PointToClient(new Point((int)m.LParam & 0xFFFF, (int)m.LParam >> 16));
                if (pt.Y < 36 && pt.X < Width - 44) // title bar except close button
                {
                    Capture = false;
                    m.Msg = 0x00A1;       // WM_NCLBUTTONDOWN
                    m.WParam = (IntPtr)2; // HTCAPTION
                    DefWndProc(ref m);
                    return;
                }
            }
            base.WndProc(ref m);
        }
}
