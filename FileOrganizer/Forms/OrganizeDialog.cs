using FileOrganizer.Core;

namespace FileOrganizer.Forms;

public class OrganizeDialog : Form
{
    private readonly List<string> _sourceFiles;
    private readonly ConfigManager _config;
    private readonly bool _isMultiFile;
    private TextBox? _nameBox;
    private TextBox? _templateBox;
    private ListBox? _fileListBox;
    private ComboBox _tagCombo = null!;
    private Button _moveButton = null!;
    private ProgressBar? _progressBar;
    private Label? _progressLabel;
    private Panel? _customPathPanel;
    private TextBox? _customNameBox;
    private TextBox? _customPathBox;
    private Panel _titleBar = null!;
    private bool _titleDragging;
    private Point _titleDragStart;
    private Point _formStartPos;

    private class TagItem
    {
        public string Text { get; }
        public Rule? Rule { get; }
        public TagItem(string text, Rule? rule) { Text = text; Rule = rule; }
        public override string ToString() => Text;
    }

    public OrganizeDialog(List<string> sourceFiles, ConfigManager config)
    {
        _sourceFiles = sourceFiles;
        _config = config;
        _isMultiFile = sourceFiles.Count > 1;
        InitializeComponent();
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

    private void InitializeComponent()
    {
        var width = _isMultiFile ? 600 : 540;
        var height = _isMultiFile ? 560 : 450;
        var titleText = _isMultiFile ? $"整理 {_sourceFiles.Count} 个文件" : "整理文件";

        Text = "";
        Size = new Size(width, height);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        DoubleBuffered = true;

        // --- Custom titlebar ---
        _titleBar = new Panel
        {
            Height = 36, Dock = DockStyle.Top,
            BackColor = Color.FromArgb(40, 43, 50),
            Cursor = Cursors.SizeAll
        };
        var titleLabel = new Label
        {
            Text = titleText, Left = 14, Top = 8,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Theme.Fg, BackColor = Color.Transparent,
            AutoSize = true
        };
        var closeBtn = new Button
        {
            Text = "×", Size = new Size(28, 28),
            Top = 4, Left = _titleBar.Width - 36,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 14f),
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.FlatAppearance.MouseOverBackColor = Theme.Danger;
        closeBtn.Click += (s, e) => Close();
        _titleBar.Controls.Add(titleLabel);
        _titleBar.Controls.Add(closeBtn);

        // Titlebar drag
        _titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _titleDragging = true;
                _titleDragStart = Cursor.Position;
                _formStartPos = Location;
            }
        };
        _titleBar.MouseMove += (s, e) =>
        {
            if (!_titleDragging) return;
            var screenPos = Cursor.Position;
            Location = new Point(
                _formStartPos.X + screenPos.X - _titleDragStart.X,
                _formStartPos.Y + screenPos.Y - _titleDragStart.Y);
        };
        _titleBar.MouseUp += (s, e) => _titleDragging = false;

        Controls.Add(_titleBar);

        // --- Body ---
        var body = new Panel
        {
            Top = 36, Left = 0,
            Width = width, Height = height - 36,
            Padding = new Padding(24, 16, 24, 16),
            BackColor = Theme.Surface
        };
        var contentWidth = _isMultiFile ? 500 : 440;
        var content = new Panel
        {
            Width = contentWidth,
            Height = body.Height - 20,
            Left = (body.Width - contentWidth) / 2,
            Top = 8,
            BackColor = Color.Transparent
        };
        var y = 8;

        if (_isMultiFile)
        {
            // File list label
            var fileLabel = new Label
            {
                Text = "文件列表",
                Top = y, Left = 0, AutoSize = true
            };
            Theme.StyleLabel(fileLabel, muted: true);
            content.Controls.Add(fileLabel);
            y += 20;

            // File list box
            _fileListBox = new ListBox
            {
                Top = y, Left = 0,
                Width = content.Width, Height = 100,
                SelectionMode = SelectionMode.None
            };
            Theme.StyleListBox(_fileListBox);
            foreach (var f in _sourceFiles)
                _fileListBox.Items.Add(Path.GetFileName(f));
            content.Controls.Add(_fileListBox);
            y += 108;

            // Rename template
            var templateLabel = new Label
            {
                Text = "重命名模板",
                Top = y, Left = 0, AutoSize = true
            };
            Theme.StyleLabel(templateLabel, muted: true);
            content.Controls.Add(templateLabel);
            y += 20;

            _templateBox = new TextBox
            {
                Top = y, Left = 0, Width = content.Width,
                PlaceholderText = "留空保持原名  {n}=序号  {name}=原名  {ext}=扩展名"
            };
            Theme.StyleTextBox(_templateBox, mono: true);
            content.Controls.Add(_templateBox);
            y += 34;
        }
        else
        {
            // File name field
            var fileLabel = new Label
            {
                Text = "文件",
                Top = y, Left = 0, AutoSize = true
            };
            Theme.StyleLabel(fileLabel, muted: true);
            content.Controls.Add(fileLabel);
            y += 20;

            _nameBox = new TextBox
            {
                Top = y, Left = 0, Width = content.Width,
                Text = _sourceFiles.Count > 0 ? Path.GetFileName(_sourceFiles[0]) : ""
            };
            Theme.StyleTextBox(_nameBox, mono: true);
            content.Controls.Add(_nameBox);
            y += 34;
        }

        // --- Tag dropdown ---
        var tagLabel = new Label
        {
            Text = "目标标签",
            Top = y, Left = 0, AutoSize = true
        };
        Theme.StyleLabel(tagLabel, muted: true);
        content.Controls.Add(tagLabel);
        y += 20;

        _tagCombo = new ComboBox
        {
            Top = y, Left = 0, Width = content.Width,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Theme.StyleComboBox(_tagCombo);
        _tagCombo.SelectedIndexChanged += (s, e) =>
        {
            if (_tagCombo.SelectedItem is TagItem item)
            {
                if (item.Rule != null)
                {
                    if (_customPathPanel != null) _customPathPanel.Visible = false;
                    if (_moveButton != null) _moveButton.Enabled = true;
                }
                else
                {
                    if (_customPathPanel != null) _customPathPanel.Visible = true;
                    if (_moveButton != null)
                        _moveButton.Enabled = _customPathBox != null
                            && !string.IsNullOrWhiteSpace(_customPathBox.Text)
                            && _customNameBox != null
                            && !string.IsNullOrWhiteSpace(_customNameBox.Text);
                }
            }
        };
        LoadTags();
        content.Controls.Add(_tagCombo);
        y += 34;

        // --- Custom path panel (hidden until "新增路径" is selected) ---
        _customPathPanel = new Panel
        {
            Top = y, Left = 0, Width = content.Width, Height = 120,
            Visible = false
        };

        var cpNameLabel = new Label { Text = "标签名", Top = 0, Left = 0, AutoSize = true };
        Theme.StyleLabel(cpNameLabel, muted: true);
        _customNameBox = new TextBox { Top = 20, Left = 0, Width = _customPathPanel.Width };
        Theme.StyleTextBox(_customNameBox);
        _customNameBox.TextChanged += OnCustomFieldChanged;

        var cpPathLabel = new Label { Text = "路径", Top = 50, Left = 0, AutoSize = true };
        Theme.StyleLabel(cpPathLabel, muted: true);
        _customPathBox = new TextBox { Top = 68, Left = 0, Width = _customPathPanel.Width - 140 };
        Theme.StyleTextBox(_customPathBox, mono: true);
        _customPathBox.TextChanged += OnCustomFieldChanged;

        var browseBtn = new Button { Text = "浏览...", Top = 67, Left = _customPathBox.Width + 8, Width = 60, Height = 25 };
        Theme.StyleButton(browseBtn);
        browseBtn.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _customPathBox!.Text, ShowNewFolderButton = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _customPathBox.Text = dlg.SelectedPath;
        };

        var newFolderBtn = new Button { Text = "新建", Top = 67, Left = _customPathBox.Width + 74, Width = 55, Height = 25 };
        Theme.StyleButton(newFolderBtn);
        newFolderBtn.Click += (s, e) =>
        {
            var basePath = _customPathBox!.Text.Trim();
            if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var inputForm = new Form
            {
                Text = "新建文件夹",
                Size = new Size(320, 130),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            };
            var inputBox = new TextBox { Top = 15, Left = 15, Width = 270, Text = "新建文件夹" };
            var okBtn = new Button { Text = "确定", Top = 50, Left = 130, Width = 70 };
            var cancelBtn = new Button { Text = "取消", Top = 50, Left = 210, Width = 70 };
            okBtn.Click += (s2, e2) => { inputForm.DialogResult = DialogResult.OK; inputForm.Close(); };
            cancelBtn.Click += (s2, e2) => { inputForm.DialogResult = DialogResult.Cancel; inputForm.Close(); };
            inputForm.Controls.AddRange(new Control[] { inputBox, okBtn, cancelBtn });

            if (inputForm.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(inputBox.Text))
            {
                try
                {
                    var newPath = Path.Combine(basePath, inputBox.Text.Trim());
                    Directory.CreateDirectory(newPath);
                    _customPathBox.Text = newPath;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建文件夹失败:\n{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        _customPathPanel.Controls.AddRange(new Control[] {
            cpNameLabel, _customNameBox,
            cpPathLabel, _customPathBox, browseBtn, newFolderBtn
        });
        content.Controls.Add(_customPathPanel);
        y += 128;

        // --- Progress area (multi-file only) ---
        if (_isMultiFile)
        {
            _progressBar = new ProgressBar
            {
                Top = y, Left = 0, Width = content.Width - 92, Height = 6
            };
            Theme.StyleProgressBar(_progressBar);
            _progressBar.Visible = false;
            content.Controls.Add(_progressBar);

            _progressLabel = new Label
            {
                Top = y - 2, Left = content.Width - 82, Width = 78, Height = 14,
                TextAlign = ContentAlignment.MiddleRight
            };
            Theme.StyleLabel(_progressLabel, muted: true, mono: true);
            _progressLabel.Visible = false;
            content.Controls.Add(_progressLabel);
            y += 18;
        }

        // --- Buttons (fixed to bottom) ---
        var btnTop = content.Height - 46;
        _moveButton = new Button
        {
            Text = _isMultiFile ? "移动全部" : "移动",
            Top = btnTop, Left = content.Width - 158, Width = 80, Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Enabled = false
        };
        Theme.StyleButton(_moveButton, primary: true);
        _moveButton.Click += MoveButton_Click;
        content.Controls.Add(_moveButton);

        var cancelButton = new Button
        {
            Text = "取消",
            Top = btnTop, Left = content.Width - 70, Width = 70, Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        Theme.StyleButton(cancelButton);
        cancelButton.Click += (s, e) => Close();
        content.Controls.Add(cancelButton);

        body.Controls.Add(content);
        Controls.Add(body);

        // Accept button + initial state
        AcceptButton = _moveButton;
        if (_tagCombo.SelectedIndex >= 0 && _tagCombo.SelectedItem is TagItem tag && tag.Rule != null)
            _moveButton.Enabled = true;

        // Paint border
        Paint += (s, e) =>
        {
            using var pen = new Pen(Theme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    private void OnCustomFieldChanged(object? sender, EventArgs e)
    {
        if (_moveButton == null) return;
        _moveButton.Enabled = !string.IsNullOrWhiteSpace(_customPathBox!.Text)
                           && !string.IsNullOrWhiteSpace(_customNameBox!.Text);
    }

    private void LoadTags()
    {
        _tagCombo.Items.Clear();
        int selectIndex = -1;
        var lastId = _config.Settings.LastUsedRuleId;

        for (int i = 0; i < _config.Rules.Count; i++)
        {
            var rule = _config.Rules[i];
            _tagCombo.Items.Add(new TagItem($"{rule.Name}  →  {rule.Path}", rule));
            if (rule.Id == lastId) selectIndex = i;
        }
        _tagCombo.Items.Add(new TagItem("📁 新增路径...", null));

        if (selectIndex >= 0)
        {
            _tagCombo.SelectedIndex = selectIndex;
        }
        else if (_config.Rules.Count > 0)
        {
            // Default to the first existing rule so the primary action is available immediately.
            _tagCombo.SelectedIndex = 0;
        }
    }

    private async void MoveButton_Click(object? sender, EventArgs e)
    {
        if (_tagCombo.SelectedItem is not TagItem item) return;

        Rule rule;
        if (item.Rule != null)
        {
            rule = item.Rule;
        }
        else
        {
            var name = _customNameBox!.Text.Trim();
            var path = _customPathBox!.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("请输入标签名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _customNameBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("请输入目标路径", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _customPathBox.Focus();
                return;
            }
            if (!Directory.Exists(path))
            {
                var dr = MessageBox.Show($"路径不存在:\n{path}\n\n是否创建该目录？", "路径不存在",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No) return;
                try { Directory.CreateDirectory(path); }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建目录失败:\n{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            rule = new Rule { Name = name, Path = path };
            _config.AddRule(rule);
        }

        _config.Settings.LastUsedRuleId = rule.Id;
        _config.SaveSettings();

        var mover = new FileMover(_config);
        var conflictAction = _config.Settings.DefaultConflictAction switch
        {
            "autoRename" => ConflictAction.AutoRename,
            "overwrite" => ConflictAction.Overwrite,
            "skip" => ConflictAction.Skip,
            _ => ConflictAction.Prompt
        };

        SetMovingState(true);
        try
        {
            if (_isMultiFile)
                await MoveAllFilesAsync(mover, rule, conflictAction);
            else
                await MoveSingleFileAsync(mover, rule, conflictAction);
        }
        finally
        {
            SetMovingState(false);
        }
    }

    private async Task MoveSingleFileAsync(FileMover mover, Rule rule, ConflictAction conflictAction)
    {
        var fileName = _nameBox!.Text.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show("文件名不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        while (true)
        {
            var result = await Task.Run(() => mover.MoveFile(_sourceFiles[0], rule.Path, fileName, conflictAction));

            if (result.Success)
            {
                FileMover.CleanupSourceDirIfEmpty(_sourceFiles[0], _config);
                Close();
                return;
            }

            if (result.ErrorMessage == "CONFLICT_PROMPT" && result.DestinationPath != null)
            {
                var dr = MessageBox.Show(
                    $"目标文件已存在:\n{Path.GetFileName(result.DestinationPath)}\n\n要覆盖吗？\n(选「否」自动加序号)",
                    "文件冲突",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (dr == DialogResult.Yes) conflictAction = ConflictAction.Overwrite;
                else if (dr == DialogResult.No) conflictAction = ConflictAction.AutoRename;
                else return;
            }
            else
            {
                MessageBox.Show($"移动失败:\n{result.ErrorMessage}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }

    private async Task MoveAllFilesAsync(FileMover mover, Rule rule, ConflictAction conflictAction)
    {
        _progressBar!.Visible = true;
        _progressLabel!.Visible = true;
        _progressBar.Maximum = _sourceFiles.Count;
        _progressBar.Value = 0;

        int success = 0, fail = 0;
        var results = new List<string>();

        for (int i = 0; i < _sourceFiles.Count; i++)
        {
            var file = _sourceFiles[i];
            string? fileName = null;

            if (_templateBox != null && !string.IsNullOrWhiteSpace(_templateBox.Text))
            {
                var template = _templateBox.Text;
                var origName = Path.GetFileNameWithoutExtension(file);
                var ext = Path.GetExtension(file);
                fileName = template
                    .Replace("{n}", (i + 1).ToString())
                    .Replace("{name}", origName)
                    .Replace("{ext}", ext);
            }

            var result = await Task.Run(() => mover.MoveFile(file, rule.Path, fileName, conflictAction));
            if (result.Success)
            {
                success++;
                FileMover.CleanupSourceDirIfEmpty(file, _config);
                results.Add($"✓ {Path.GetFileName(file)} → {result.DestinationPath}");
            }
            else if (result.ErrorMessage == "CONFLICT_PROMPT")
            {
                var autoResult = await Task.Run(() => mover.MoveFile(file, rule.Path, fileName, ConflictAction.AutoRename));
                if (autoResult.Success)
                {
                    success++;
                    FileMover.CleanupSourceDirIfEmpty(file, _config);
                    results.Add($"✓ {Path.GetFileName(file)} → {autoResult.DestinationPath}");
                }
                else
                {
                    fail++;
                    results.Add($"✗ {Path.GetFileName(file)} → {autoResult.ErrorMessage}");
                }
            }
            else
            {
                fail++;
                results.Add($"✗ {Path.GetFileName(file)} → {result.ErrorMessage}");
            }

            _progressBar.Value = i + 1;
            _progressLabel.Text = $"{i + 1}/{_sourceFiles.Count}";
            await Task.Yield();
        }

        if (fail > 0)
        {
            MessageBox.Show(
                $"整理完成!\n成功: {success} 个\n失败: {fail} 个",
                "整理结果",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        Close();
    }

    private void SetMovingState(bool moving)
    {
        if (IsDisposed) return;
        _moveButton.Enabled = !moving;
        _tagCombo.Enabled = !moving;
        if (_nameBox != null) _nameBox.Enabled = !moving;
        if (_templateBox != null) _templateBox.Enabled = !moving;
        if (_customNameBox != null) _customNameBox.Enabled = !moving;
        if (_customPathBox != null) _customPathBox.Enabled = !moving;
        UseWaitCursor = moving;
    }
}
