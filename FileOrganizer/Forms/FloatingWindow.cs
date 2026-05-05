using FileOrganizer.Core;
using System.Drawing;
using System.Windows.Forms;

namespace FileOrganizer.Forms;

public class FloatingWindow : Form
{
    private readonly ConfigManager _config;
    private NotifyIcon _trayIcon = null!;
    private const int WindowSize = 80;
    private const int HoverOpacity = 90;
    private Point _dragStart;
    private bool _isDragging;

    public FloatingWindow(ConfigManager config)
    {
        _config = config;
        InitializeComponent();
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "File Organizer",
            Visible = true
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示/隐藏悬浮窗", null, (s, e) => ToggleVisible());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("规则管理", null, (s, e) => ShowRuleManager());
        trayMenu.Items.Add("设置", null, (s, e) => ShowSettings());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("退出", null, (s, e) => ExitApp());
        _trayIcon.ContextMenuStrip = trayMenu;

        _trayIcon.DoubleClick += (s, e) => ToggleVisible();
    }

    private void ToggleVisible()
    {
        if (Visible)
        {
            Hide();
        }
        else
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
        }
    }

    private void ShowRuleManager()
    {
        using var form = new RuleManagerForm(_config);
        form.ShowDialog(this);
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_config);
        form.ShowDialog(this);
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    private void InitializeComponent()
    {
        Text = "File Organizer";
        Size = new Size(WindowSize, WindowSize);
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        AllowDrop = true;
        Opacity = 0.5;

        BackColor = Color.FromArgb(52, 73, 94);
        var iconLabel = new Label
        {
            Text = "📁",
            Font = new Font("Segoe UI", 28),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AllowDrop = true,
            Cursor = Cursors.Hand
        };
        Controls.Add(iconLabel);

        // Restore saved position
        if (_config.Settings.FloatingWindowX != 0 || _config.Settings.FloatingWindowY != 0)
        {
            Location = new Point(_config.Settings.FloatingWindowX, _config.Settings.FloatingWindowY);
        }

        // File drag-drop on form
        DragEnter += FloatingWindow_DragEnter;
        DragLeave += FloatingWindow_DragLeave;
        DragDrop += FloatingWindow_DragDrop;

        // File drag-drop on iconLabel (this is what gets triggered when dropping files)
        iconLabel.DragEnter += FloatingWindow_DragEnter;
        iconLabel.DragLeave += FloatingWindow_DragLeave;
        iconLabel.DragDrop += FloatingWindow_DragDrop;

        // Window drag-move on iconLabel
        iconLabel.MouseDown += IconLabel_MouseDown;
        iconLabel.MouseMove += IconLabel_MouseMove;
        iconLabel.MouseUp += IconLabel_MouseUp;

        // Right-click context menu on iconLabel
        var ctxMenu = new ContextMenuStrip();
        ctxMenu.Items.Add("规则管理", null, (s, e) => ShowRuleManager());
        ctxMenu.Items.Add("设置", null, (s, e) => ShowSettings());
        ctxMenu.Items.Add("-");
        ctxMenu.Items.Add("退出", null, (s, e) => ExitApp());
        iconLabel.ContextMenuStrip = ctxMenu;

        // Minimize to tray on close
        FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    private void IconLabel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStart = e.Location;
        }
    }

    private void IconLabel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            Left += e.X - _dragStart.X;
            Top += e.Y - _dragStart.Y;
        }
    }

    private void IconLabel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            var screen = Screen.FromPoint(Location).WorkingArea;
            Left = Math.Clamp(Left, 0, screen.Width - Width);
            Top = Math.Clamp(Top, 0, screen.Height - Height);
            _config.Settings.FloatingWindowX = Left;
            _config.Settings.FloatingWindowY = Top;
            Task.Run(() => _config.SaveSettings());
        }
    }

    private void FloatingWindow_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Move;
            Opacity = HoverOpacity / 100.0;
            BackColor = Color.FromArgb(41, 128, 185);
        }
    }

    private void FloatingWindow_DragLeave(object? sender, EventArgs e)
    {
        Opacity = 0.5;
        BackColor = Color.FromArgb(52, 73, 94);
    }

    private void FloatingWindow_DragDrop(object? sender, DragEventArgs e)
    {
        Opacity = 0.5;
        BackColor = Color.FromArgb(52, 73, 94);

        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0)
            {
                try
                {
                    using var dialog = new OrganizeDialog(files.ToList(), _config);
                    dialog.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"错误: {ex}", "异常",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}