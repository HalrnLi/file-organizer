using FileOrganizer.Core;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FileOrganizer.Forms;

public class FloatingWindow : Form
{
    private readonly ConfigManager _config;
    private NotifyIcon _trayIcon = null!;
    private const int WindowSize = 88;
    private bool _isDragging;
    private bool _isDragOver;
    private Point _dragScreenStart;
    private Point _dragFormStart;
    private Label _textLabel = null!;
    private Panel _contentPanel = null!;

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
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("规则管理", null, (s, e) => ShowRuleManager());
        trayMenu.Items.Add("设置", null, (s, e) => ShowSettings());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (s, e) => ExitApp());
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (s, e) => ToggleVisible();
    }

    private void ToggleVisible()
    {
        if (Visible) { Hide(); }
        else { Show(); WindowState = FormWindowState.Normal; BringToFront(); }
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
        Text = "";
        Size = new Size(WindowSize, WindowSize);
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        AllowDrop = true;
        BackColor = Color.FromArgb(35, 38, 47);
        Opacity = 0.85;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;

        // Restore saved position
        if (_config.Settings.FloatingWindowX != 0 || _config.Settings.FloatingWindowY != 0)
            Location = new Point(_config.Settings.FloatingWindowX, _config.Settings.FloatingWindowY);

        // Apply rounded region
        UpdateRegion();

        // Content panel (covers entire window)
        _contentPanel = new Panel
        {
            Size = new Size(WindowSize, WindowSize),
            Location = Point.Empty,
            BackColor = Color.Transparent,
            AllowDrop = true,
            Cursor = Cursors.Hand
        };

        // Text label (centered in panel)
        _textLabel = new Label
        {
            Text = "DROP",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(WindowSize, WindowSize),
            Location = Point.Empty,
            BackColor = Color.Transparent,
            AllowDrop = true,
            Cursor = Cursors.Hand
        };

        _contentPanel.Controls.Add(_textLabel);
        Controls.Add(_contentPanel);

        // Drag-drop events → whole window
        _contentPanel.DragEnter += OnDragEnter;
        _contentPanel.DragLeave += OnDragLeave;
        _contentPanel.DragDrop += OnDragDrop;
        _textLabel.DragEnter += OnDragEnter;
        _textLabel.DragLeave += OnDragLeave;
        _textLabel.DragDrop += OnDragDrop;
        DragEnter += OnDragEnter;
        DragLeave += OnDragLeave;
        DragDrop += OnDragDrop;

        // Drag-move: attach to contentPanel and textLabel
        SetupDragEvents(_contentPanel);
        SetupDragEvents(_textLabel);

        // Double-click: flash drag-over effect
        _contentPanel.DoubleClick += (s, e) => FlashDragOver();

        // Right-click context menu
        _contentPanel.ContextMenuStrip = BuildContextMenu();

        // Minimize to tray on close
        FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };

        // Repaint on resize (region)
        Resize += (s, e) => UpdateRegion();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("规则管理", null, (s, e) => ShowRuleManager());
        menu.Items.Add("设置", null, (s, e) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (s, e) => ExitApp());
        return menu;
    }

    private async void FlashDragOver()
    {
        SetDragOver(true);
        await Task.Delay(800);
        SetDragOver(false);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Move;
            SetDragOver(true);
        }
    }

    private void OnDragLeave(object? sender, EventArgs e)
    {
        SetDragOver(false);
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        SetDragOver(false);

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

    private void SetDragOver(bool on)
    {
        if (_isDragOver == on) return;
        _isDragOver = on;

        if (on)
        {
            BackColor = Color.FromArgb(45, 60, 50);
            _textLabel.ForeColor = Theme.Accent;
        }
        else
        {
            BackColor = Color.FromArgb(35, 38, 47);
            _textLabel.ForeColor = Theme.Muted;
        }
        Invalidate();
    }

    private void UpdateRegion()
    {
        var corner = 16;
        using var path = RoundedRectPath(ClientRectangle, corner);
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Border
        var borderColor = _isDragOver ? Theme.Accent : Theme.Border;
        var borderWidth = _isDragOver ? 2f : 1f;
        using var pen = new Pen(borderColor, borderWidth);
        using var path = RoundedRectPath(
            new Rectangle(1, 1, ClientSize.Width - 2, ClientSize.Height - 2), 15);
        g.DrawPath(pen, path);
    }

    private void SetupDragEvents(Control ctrl)
    {
        ctrl.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragScreenStart = Cursor.Position;
                _dragFormStart = Location;
            }
        };
        ctrl.MouseMove += (s, e) =>
        {
            if (!_isDragging) return;
            var screenPos = Cursor.Position;
            Location = new Point(
                _dragFormStart.X + screenPos.X - _dragScreenStart.X,
                _dragFormStart.Y + screenPos.Y - _dragScreenStart.Y);
        };
        ctrl.MouseUp += (s, e) =>
        {
            if (!_isDragging) return;
            _isDragging = false;
            var screen = Screen.FromPoint(Location).WorkingArea;
            Left = Math.Clamp(Left, screen.Left, screen.Right - Width);
            Top = Math.Clamp(Top, screen.Top, screen.Bottom - Height);
            _config.Settings.FloatingWindowX = Left;
            _config.Settings.FloatingWindowY = Top;
            Task.Run(() => _config.SaveSettings());
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _trayIcon?.Dispose();
        base.Dispose(disposing);
    }
}
