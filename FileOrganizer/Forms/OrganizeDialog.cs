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
    private ComboBox _tagCombo;
    private Button _moveButton;
    private ProgressBar? _progressBar;
    private Label? _progressLabel;
    private Panel? _customPathPanel;
    private TextBox? _customNameBox;
    private TextBox? _customPathBox;

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

    private void InitializeComponent()
    {
        Text = _isMultiFile ? $"整理 {_sourceFiles.Count} 个文件" : "整理文件";
        Size = new Size(520, _isMultiFile ? 440 : 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var y = 15;

        if (_isMultiFile)
        {
            var fileLabel = new Label
            {
                Text = $"已选择 {_sourceFiles.Count} 个文件:",
                Top = y, Left = 15, Width = 480
            };
            Controls.Add(fileLabel);
            y += 22;

            _fileListBox = new ListBox
            {
                Top = y, Left = 15, Width = 480, Height = 100,
                SelectionMode = SelectionMode.None
            };
            foreach (var f in _sourceFiles)
                _fileListBox.Items.Add(Path.GetFileName(f));
            Controls.Add(_fileListBox);
            y += 108;

            var templateLabel = new Label
            {
                Text = "重命名模板 (留空保持原名, {n}=序号, {name}=原名):",
                Top = y, Left = 15, Width = 480
            };
            Controls.Add(templateLabel);
            y += 20;

            _templateBox = new TextBox { Top = y, Left = 15, Width = 480, Text = "" };
            Controls.Add(_templateBox);
            y += 30;
        }
        else
        {
            var fileLabel = new Label { Text = "文件:", Top = y, Left = 15, Width = 50 };
            Controls.Add(fileLabel);

            _nameBox = new TextBox
            {
                Top = y - 2, Left = 65, Width = 430,
                Text = Path.GetFileName(_sourceFiles[0])
            };
            Controls.Add(_nameBox);
            y += 30;
        }

        // --- Tag dropdown ---
        var tagLabel = new Label { Text = "目标标签:", Top = y, Left = 15, Width = 80 };
        Controls.Add(tagLabel);
        y += 22;

        _tagCombo = new ComboBox
        {
            Top = y, Left = 15, Width = 480,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
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
        Controls.Add(_tagCombo);
        y += 30;

        // --- Custom path panel (hidden until "新增路径" is selected) ---
        _customPathPanel = new Panel
        {
            Top = y, Left = 15, Width = 480, Height = 58,
            Visible = false
        };

        var cpNameLabel = new Label { Text = "标签名:", Top = 5, Left = 0, Width = 60 };
        _customNameBox = new TextBox { Top = 2, Left = 65, Width = 410 };
        _customNameBox.TextChanged += OnCustomFieldChanged;

        var cpPathLabel = new Label { Text = "路径:", Top = 33, Left = 0, Width = 60 };
        _customPathBox = new TextBox { Top = 30, Left = 65, Width = 285 };
        _customPathBox.TextChanged += OnCustomFieldChanged;

        var browseBtn = new Button { Text = "浏览...", Top = 29, Left = 355, Width = 60, Height = 25 };
        browseBtn.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                SelectedPath = _customPathBox!.Text,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _customPathBox.Text = dlg.SelectedPath;
        };

        var newFolderBtn = new Button { Text = "新建", Top = 29, Left = 420, Width = 55, Height = 25 };
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
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false
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
        Controls.Add(_customPathPanel);
        y += 62;

        // --- Progress bar (hidden initially) ---
        _progressBar = new ProgressBar
        {
            Top = y, Left = 15, Width = 380, Height = 20,
            Visible = false
        };
        Controls.Add(_progressBar);

        _progressLabel = new Label
        {
            Top = y, Left = 400, Width = 100, Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        Controls.Add(_progressLabel);
        y += 28;

        // --- Buttons ---
        _moveButton = new Button
        {
            Text = "移动",
            Top = y, Left = 340, Width = 75, Height = 30,
            Enabled = false
        };
        _moveButton.Click += MoveButton_Click;
        Controls.Add(_moveButton);

        var cancelButton = new Button
        {
            Text = "取消",
            Top = y, Left = 420, Width = 75, Height = 30
        };
        cancelButton.Click += (s, e) => Close();
        Controls.Add(cancelButton);

        AcceptButton = _moveButton;

        if (_tagCombo.SelectedIndex >= 0 && _tagCombo.SelectedItem is TagItem item && item.Rule != null)
            _moveButton.Enabled = true;
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
            if (rule.Id == lastId)
                selectIndex = i;
        }
        _tagCombo.Items.Add(new TagItem("📁 新增路径...", null));

        if (selectIndex >= 0)
            _tagCombo.SelectedIndex = selectIndex;
    }

    private void MoveButton_Click(object? sender, EventArgs e)
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

        if (_isMultiFile)
            MoveAllFiles(mover, rule, conflictAction);
        else
            MoveSingleFile(mover, rule, conflictAction);
    }

    private void MoveSingleFile(FileMover mover, Rule rule, ConflictAction conflictAction)
    {
        var fileName = _nameBox!.Text.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show("文件名不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        while (true)
        {
            var result = mover.MoveFile(_sourceFiles[0], rule.Path, fileName, conflictAction);

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

                if (dr == DialogResult.Yes)
                    conflictAction = ConflictAction.Overwrite;
                else if (dr == DialogResult.No)
                    conflictAction = ConflictAction.AutoRename;
                else
                    return;
            }
            else
            {
                MessageBox.Show($"移动失败:\n{result.ErrorMessage}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }

    private void MoveAllFiles(FileMover mover, Rule rule, ConflictAction conflictAction)
    {
        _moveButton.Enabled = false;
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

            var result = mover.MoveFile(file, rule.Path, fileName, conflictAction);
            if (result.Success)
            {
                success++;
                FileMover.CleanupSourceDirIfEmpty(file, _config);
                results.Add($"✓ {Path.GetFileName(file)} → {result.DestinationPath}");
            }
            else if (result.ErrorMessage == "CONFLICT_PROMPT")
            {
                var autoResult = mover.MoveFile(file, rule.Path, fileName, ConflictAction.AutoRename);
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
            Application.DoEvents();
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
}
