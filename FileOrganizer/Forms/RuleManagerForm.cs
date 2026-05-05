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

    public RuleManagerForm(ConfigManager config)
    {
        _config = config;
        InitializeComponent();
        LoadRules();
    }

    private void InitializeComponent()
    {
        Text = "规则管理";
        Size = new Size(700, 450);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;

        _grid = new DataGridView
        {
            Top = 10,
            Left = 10,
            Width = 660,
            Height = 300,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _grid.Columns.Add("Name", "标签名");
        _grid.Columns.Add("Path", "目标路径");
        _grid.Columns[0].FillWeight = 30;
        _grid.Columns[1].FillWeight = 70;

        _addButton = new Button { Text = "添加", Top = 325, Left = 10, Width = 80, Height = 30 };
        _addButton.Click += AddButton_Click;

        _editButton = new Button { Text = "编辑", Top = 325, Left = 100, Width = 80, Height = 30 };
        _editButton.Click += EditButton_Click;

        _deleteButton = new Button { Text = "删除", Top = 325, Left = 190, Width = 80, Height = 30 };
        _deleteButton.Click += DeleteButton_Click;

        _importButton = new Button { Text = "导入", Top = 325, Left = 310, Width = 80, Height = 30 };
        _importButton.Click += ImportButton_Click;

        _exportButton = new Button { Text = "导出", Top = 325, Left = 400, Width = 80, Height = 30 };
        _exportButton.Click += ExportButton_Click;

        Controls.AddRange(new Control[] { _grid, _addButton, _editButton, _deleteButton, _importButton, _exportButton });
    }

    private void LoadRules()
    {
        _grid.Rows.Clear();
        foreach (var rule in _config.Rules)
        {
            _grid.Rows.Add(rule.Name, rule.Path);
            _grid.Rows[^1].Tag = rule;
        }
    }

    private class RuleDialogResult
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
    }

    private static RuleDialogResult? ShowRuleDialog(string title, string initialName, string initialPath, IWin32Window owner)
    {
        RuleDialogResult? result = null;

        var form = new Form
        {
            Text = title,
            Size = new Size(450, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };

        var nameLabel = new Label { Text = "标签名:", Top = 20, Left = 15, Width = 60 };
        var nameBox = new TextBox { Top = 18, Left = 80, Width = 335, Text = initialName };

        var pathLabel = new Label { Text = "路径:", Top = 55, Left = 15, Width = 60 };
        var pathBox = new TextBox { Top = 53, Left = 80, Width = 250, Text = initialPath };
        var browseBtn = new Button { Text = "浏览...", Top = 52, Left = 335, Width = 80, Height = 25 };
        browseBtn.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = pathBox.Text };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                pathBox.Text = dlg.SelectedPath;
        };

        var okBtn = new Button { Text = "确定", Top = 100, Left = 260, Width = 70, Height = 28 };
        okBtn.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show(form, "标签名不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(pathBox.Text))
            {
                MessageBox.Show(form, "路径不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            result = new RuleDialogResult { Name = nameBox.Text.Trim(), Path = pathBox.Text.Trim() };
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
        };

        var cancelBtn = new Button { Text = "取消", Top = 100, Left = 340, Width = 70, Height = 28 };
        cancelBtn.Click += (s, e) => form.DialogResult = System.Windows.Forms.DialogResult.Cancel;

        form.Controls.AddRange(new Control[] { nameLabel, nameBox, pathLabel, pathBox, browseBtn, okBtn, cancelBtn });
        return form.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK ? result : null;
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        var result = ShowRuleDialog("添加规则", "", "", this);
        if (result != null)
        {
            _config.AddRule(new Rule { Name = result.Name!, Path = result.Path! });
            LoadRules();
        }
    }

    private void EditButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        var rule = (Rule)_grid.SelectedRows[0].Tag!;

        var result = ShowRuleDialog("编辑规则", rule.Name, rule.Path, this);
        if (result != null)
        {
            rule.Name = result.Name!;
            rule.Path = result.Path!;
            _config.UpdateRule(rule);
            LoadRules();
        }
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        var rule = (Rule)_grid.SelectedRows[0].Tag!;
        var dr = MessageBox.Show($"确定删除标签「{rule.Name}」吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (dr == System.Windows.Forms.DialogResult.Yes)
        {
            _config.RemoveRule(rule.Id);
            LoadRules();
        }
    }

    private void ImportButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "导入规则",
            Filter = "JSON 文件|*.json",
            DefaultExt = "json"
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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
                    MessageBox.Show($"已导入 {imported.Count} 条规则", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "导出规则",
            Filter = "JSON 文件|*.json",
            DefaultExt = "json",
            FileName = "rules.json"
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                var json = JsonSerializer.Serialize(_config.Rules.ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show($"已导出 {_config.Rules.Count} 条规则", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}