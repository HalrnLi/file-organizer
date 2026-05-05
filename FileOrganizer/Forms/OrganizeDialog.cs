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
    private FlowLayoutPanel _tagPanel;
    private ComboBox _conflictCombo;
    private Button _moveButton;
    private ProgressBar? _progressBar;
    private Label? _progressLabel;
    private RadioButton? _selectedTag;

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
        Size = new Size(520, 450);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var y = 15;

        if (_isMultiFile)
        {
            // --- File list ---
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

            // --- Rename template ---
            var templateLabel = new Label
            {
                Text = "重命名模板 (留空保持原名, {n}=序号, {name}=原名):",
                Top = y, Left = 15, Width = 480
            };
            Controls.Add(templateLabel);
            y += 20;

            _templateBox = new TextBox
            {
                Top = y, Left = 15, Width = 480, Text = ""
            };
            Controls.Add(_templateBox);
            y += 30;
        }
        else
        {
            // --- Single file name ---
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

        // --- Tag selection ---
        var tagLabel = new Label { Text = "目标标签:", Top = y, Left = 15, Width = 80 };
        Controls.Add(tagLabel);
        y += 22;

        _tagPanel = new FlowLayoutPanel
        {
            Top = y, Left = 15, Width = 480, Height = 120,
            FlowDirection = FlowDirection.TopDown,
            AutoScroll = true,
            BorderStyle = BorderStyle.Fixed3D
        };
        LoadTags();
        Controls.Add(_tagPanel);
        y += 128;

        // --- Conflict action ---
        var conflictLabel = new Label { Text = "冲突时:", Top = y + 2, Left = 15, Width = 60 };
        Controls.Add(conflictLabel);

        _conflictCombo = new ComboBox
        {
            Top = y, Left = 75, Width = 200,
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
        Controls.Add(_conflictCombo);
        y += 30;

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
    }

    private void LoadTags()
    {
        foreach (var rule in _config.Rules)
        {
            var rb = new RadioButton
            {
                Text = $"{rule.Name}  →  {rule.Path}",
                Tag = rule,
                AutoSize = true,
                Padding = new Padding(5, 3, 5, 3)
            };
            rb.CheckedChanged += (s, e) =>
            {
                if (rb.Checked)
                {
                    _selectedTag = rb;
                    _moveButton.Enabled = true;
                }
            };
            _tagPanel.Controls.Add(rb);
        }
    }

    private ConflictAction GetSelectedConflictAction() => _conflictCombo.SelectedIndex switch
    {
        1 => ConflictAction.AutoRename,
        2 => ConflictAction.Overwrite,
        3 => ConflictAction.Skip,
        _ => ConflictAction.Prompt
    };

    private void MoveButton_Click(object? sender, EventArgs e)
    {
        if (_selectedTag?.Tag is not Rule rule) return;

        var mover = new FileMover(_config);
        var conflictAction = GetSelectedConflictAction();

        if (_isMultiFile)
        {
            MoveAllFiles(mover, rule, conflictAction);
        }
        else
        {
            MoveSingleFile(mover, rule, conflictAction);
        }
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
                MessageBox.Show($"文件已移动到:\n{result.DestinationPath}", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                // For multi-file, auto-rename on conflict instead of prompting per file
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

        MessageBox.Show(
            $"整理完成!\n成功: {success} 个\n失败: {fail} 个",
            "整理结果",
            MessageBoxButtons.OK,
            fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        Close();
    }
}
