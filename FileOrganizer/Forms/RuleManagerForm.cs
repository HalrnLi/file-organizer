using FileOrganizer.Core;
using System.Text.Json;

namespace FileOrganizer.Forms;

public class RuleManagerForm : Form
{
    private readonly ConfigManager _config;
    private DataGridView _grid = null!;
    private Button _addButton = null!;
    private Button _editButton = null!;
    private Button _deleteButton = null!;
    private Button _importButton = null!;
    private Button _exportButton = null!;

    // Inline dialog controls
    private Panel _inlineOverlay = null!;
    private TextBox _inlineNameBox = null!;
    private TextBox _inlinePathBox = null!;
    private string? _editingRuleId;

    // Titlebar drag
    private bool _titleDragging;
    private Point _titleDragStart, _formStartPos;

    public RuleManagerForm(ConfigManager config)
    {
        _config = config;
        InitializeComponent();
        LoadRules();
    }

    private void InitializeComponent()
    {
        Text = "";
        Size = new Size(640, 450);
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
            Text = "规则管理", Left = 14, Top = 8,
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
        titleBar.Controls.Add(titleLabel);
        titleBar.Controls.Add(closeBtn);
        void LayoutTitleButtons()
        {
            closeBtn.Left = titleBar.ClientSize.Width - closeBtn.Width - 8;
        }
        Shown += (s, e) => BeginInvoke(LayoutTitleButtons);
        titleBar.Resize += (s, e) => LayoutTitleButtons();
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

        // --- Table ---
        var contentLeft = 40;
        var contentWidth = 560;

        _grid = new DataGridView
        {
            Top = 44, Left = contentLeft, Width = contentWidth, Height = 300,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _grid.Columns.Add("Name", "标签名");
        _grid.Columns.Add("Path", "目标路径");
        _grid.Columns[0].Width = 120;
        _grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        Theme.StyleDataGridView(_grid);
        Controls.Add(_grid);

        // --- Buttons ---
        var btnY = 358;
        var gap = 10;
        var totalButtonWidth = 75 * 5 + gap * 4;
        var startX = (Width - totalButtonWidth) / 2;
        _addButton = CreateToolButton("添加", startX, btnY);
        _editButton = CreateToolButton("编辑", startX + 75 + gap, btnY);
        _deleteButton = CreateToolButton("删除", startX + (75 + gap) * 2, btnY, danger: true);
        _importButton = CreateToolButton("导入", startX + (75 + gap) * 3, btnY);
        _exportButton = CreateToolButton("导出", startX + (75 + gap) * 4, btnY);
        foreach (var button in new[] { _addButton, _editButton, _deleteButton, _importButton, _exportButton })
            button.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        _addButton.Click += (s, e) => ShowInlineDialog();
        _editButton.Click += (s, e) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var rule = (Rule)_grid.SelectedRows[0].Tag!;
            _editingRuleId = rule.Id;
            ShowInlineDialog("编辑规则", rule.Name, rule.Path);
        };
        _deleteButton.Click += (s, e) => DeleteRule();
        _importButton.Click += (s, e) => ImportRules();
        _exportButton.Click += (s, e) => ExportRules();

        Controls.AddRange(new Control[] { _addButton, _editButton, _deleteButton, _importButton, _exportButton });

        // --- Inline dialog overlay ---
        BuildInlineDialog();

        // Paint border
        Paint += (s, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    private static Button CreateToolButton(string text, int x, int y, bool danger = false)
    {
        var btn = new Button
        {
            Text = text, Top = y, Left = x, Width = 75, Height = 28
        };
        Theme.StyleButton(btn, danger: danger);
        return btn;
    }

    private void BuildInlineDialog()
    {
        _inlineOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 0, 0, 0),
            Visible = false
        };
        _inlineOverlay.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            e.Graphics.FillRectangle(brush, _inlineOverlay.ClientRectangle);
        };

        var box = new Panel
        {
            Size = new Size(360, 200),
            BackColor = Theme.Surface,
            BorderStyle = BorderStyle.None
        };
        box.Paint += (s, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
        };

        var dialogTitle = new Label
        {
            Text = "添加规则", Top = 16, Left = 16,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Theme.Fg, BackColor = Color.Transparent,
            AutoSize = true, Name = "dlgTitle"
        };

        var nameLabel = new Label { Text = "标签名", Top = 48, Left = 16, AutoSize = true };
        Theme.StyleLabel(nameLabel, muted: true);

        _inlineNameBox = new TextBox { Top = 66, Left = 16, Width = 328 };
        Theme.StyleTextBox(_inlineNameBox);

        var pathLabel = new Label { Text = "目标路径", Top = 100, Left = 16, AutoSize = true };
        Theme.StyleLabel(pathLabel, muted: true);

        _inlinePathBox = new TextBox { Top = 118, Left = 16, Width = 240 };
        Theme.StyleTextBox(_inlinePathBox, mono: true);

        var browseBtn = new Button { Text = "浏览...", Top = 117, Left = 262, Width = 82, Height = 25 };
        Theme.StyleButton(browseBtn);
        browseBtn.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _inlinePathBox.Text };
            if (dlg.ShowDialog() == DialogResult.OK)
                _inlinePathBox.Text = dlg.SelectedPath;
        };

        var cancelBtn = new Button { Text = "取消", Top = 158, Left = 220, Width = 60, Height = 28 };
        Theme.StyleButton(cancelBtn);
        cancelBtn.Click += (s, e) => _inlineOverlay.Visible = false;

        var okBtn = new Button { Text = "确定", Top = 158, Left = 286, Width = 60, Height = 28 };
        Theme.StyleButton(okBtn, primary: true);
        okBtn.Click += (s, e) => CommitInlineDialog();

        box.Controls.AddRange(new Control[] {
            dialogTitle, nameLabel, _inlineNameBox,
            pathLabel, _inlinePathBox, browseBtn,
            cancelBtn, okBtn
        });
        _inlineOverlay.Controls.Add(box);

        // Center the box
        box.Location = new Point(
            (Width - box.Width) / 2,
            (Height - box.Height) / 2 - 20);

        _inlineOverlay.Resize += (s, e) =>
        {
            box.Location = new Point(
                (_inlineOverlay.Width - box.Width) / 2,
                (_inlineOverlay.Height - box.Height) / 2 - 20);
        };

        Controls.Add(_inlineOverlay);
        _inlineOverlay.BringToFront();
    }

    private void ShowInlineDialog(string title = "添加规则", string name = "", string path = "")
    {
        _inlineNameBox.Text = name;
        _inlinePathBox.Text = path;
        var titleLbl = (Label)_inlineOverlay.Controls[0].Controls["dlgTitle"]!;
        titleLbl.Text = title;
        _inlineOverlay.Visible = true;
        _inlineOverlay.BringToFront();
        _inlineNameBox.Focus();
    }

    private void CommitInlineDialog()
    {
        var name = _inlineNameBox.Text.Trim();
        var path = _inlinePathBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "标签名不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(this, "路径不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_editingRuleId != null)
        {
            var rule = _config.GetRuleById(_editingRuleId);
            if (rule != null)
            {
                rule.Name = name;
                rule.Path = path;
                _config.UpdateRule(rule);
            }
            _editingRuleId = null;
        }
        else
        {
            _config.AddRule(new Rule { Name = name, Path = path });
        }

        LoadRules();
        _inlineOverlay.Visible = false;
    }

    private void LoadRules()
    {
        _grid.Rows.Clear();
        foreach (var rule in _config.Rules)
        {
            var rowIdx = _grid.Rows.Add(rule.Name, rule.Path);
            _grid.Rows[rowIdx].Tag = rule;
        }
    }

    private void DeleteRule()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var rule = (Rule)_grid.SelectedRows[0].Tag!;
        var dr = MessageBox.Show($"确定删除标签「{rule.Name}」吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (dr == DialogResult.Yes)
        {
            _config.RemoveRule(rule.Id);
            LoadRules();
        }
    }

    private void ImportRules()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "导入规则",
            Filter = "JSON 文件|*.json",
            DefaultExt = "json"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var imported = JsonSerializer.Deserialize<List<Rule>>(json);
                if (imported != null && imported.Count > 0)
                {
                    foreach (var rule in imported)
                        _config.AddRule(rule);
                    LoadRules();
                    MessageBox.Show($"已导入 {imported.Count} 条规则", "完成",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportRules()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "导出规则",
            Filter = "JSON 文件|*.json",
            DefaultExt = "json",
            FileName = "rules.json"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var json = JsonSerializer.Serialize(_config.Rules.ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show($"已导出 {_config.Rules.Count} 条规则", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Keyboard: close inline dialog with Escape
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            if (_inlineOverlay.Visible)
            {
                _inlineOverlay.Visible = false;
                _editingRuleId = null;
            }
            else Close();
        }
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
