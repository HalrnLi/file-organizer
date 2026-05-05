# Phase 1 — 文件整理工具 最小可用版 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现可拖放整理文件的最小可用版本，包含悬浮窗、整理弹窗、配置管理、文件移动和单实例守卫。

**Architecture:** C# .NET 8 WinForms 桌面程序，Core 层（Models/ConfigManager/FileMover）与 UI 层（FloatingWindow/OrganizeDialog）分离，Core 层可单元测试。配置存 JSON 文件，与 exe 同级目录。

**Tech Stack:** .NET 8 + WinForms + xUnit + System.Text.Json

**注意:** 本计划代码需在 Windows 上使用 .NET 8 SDK 构建。开发时在 Windows 上执行 `dotnet build` / `dotnet test`。

---

## 文件结构

```
FileOrganizer/
├── FileOrganizer.csproj
├── Program.cs                     # 入口 + 单实例守卫
├── Forms/
│   ├── FloatingWindow.cs          # 悬浮窗
│   └── OrganizeDialog.cs          # 整理弹窗
├── Core/
│   ├── Models.cs                  # 数据模型
│   ├── ConfigManager.cs           # JSON 配置读写
│   └── FileMover.cs               # 文件移动逻辑
└── tests/
    └── FileOrganizer.Tests/
        ├── FileOrganizer.Tests.csproj
        └── ConfigManagerTests.cs  # ConfigManager + FileMover 测试
```

---

### Task 1: 项目骨架 + 数据模型

**Files:**
- Create: `FileOrganizer/FileOrganizer.csproj`
- Create: `FileOrganizer/Core/Models.cs`
- Create: `FileOrganizer/tests/FileOrganizer.Tests/FileOrganizer.Tests.csproj`

- [ ] **Step 1: 创建主项目 csproj**

```xml
<!-- FileOrganizer/FileOrganizer.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: 创建测试项目 csproj**

```xml
<!-- FileOrganizer/tests/FileOrganizer.Tests/FileOrganizer.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\FileOrganizer.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: 创建 Models.cs**

```csharp
// FileOrganizer/Core/Models.cs
namespace FileOrganizer.Core;

public enum ConflictAction
{
    Prompt,
    AutoRename,
    Overwrite,
    Skip
}

public class Rule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Settings
{
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ContextMenuEnabled { get; set; } = true;
    public string DefaultConflictAction { get; set; } = "prompt";
    public bool DeleteEmptySourceDir { get; set; } = false;
    public int FloatingWindowX { get; set; } = 100;
    public int FloatingWindowY { get; set; } = 100;
}
```

- [ ] **Step 4: 验证项目可编译**

```bash
cd FileOrganizer
dotnet restore
dotnet build
```
Expected: Build 成功，无错误。

- [ ] **Step 5: 提交**

```bash
git init
git add .
git commit -m "feat: project scaffolding with data models"
```

---

### Task 2: ConfigManager（JSON 配置读写）

**Files:**
- Create: `FileOrganizer/Core/ConfigManager.cs`
- Modify: `FileOrganizer/tests/FileOrganizer.Tests/ConfigManagerTests.cs`

ConfigManager 职责：读取/写入 rules.json 和 settings.json，内存缓存，异常时返回默认值。

- [ ] **Step 1: 写第 1 个测试 — 加载时自动创建默认配置**

```csharp
// FileOrganizer/tests/FileOrganizer.Tests/ConfigManagerTests.cs
using FileOrganizer.Core;

namespace FileOrganizer.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _testDir;

    public ConfigManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FO_Test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Load_CreatesDefaultFilesWhenNotExist()
    {
        var mgr = new ConfigManager(_testDir);
        mgr.Load();

        Assert.True(File.Exists(Path.Combine(_testDir, "rules.json")), "rules.json should be created");
        Assert.True(File.Exists(Path.Combine(_testDir, "settings.json")), "settings.json should be created");
        Assert.Empty(mgr.Rules);
        Assert.NotNull(mgr.Settings);
    }
}
```

- [ ] **Step 2: 验证测试红**

```bash
dotnet test tests/FileOrganizer.Tests
```
Expected: 编译失败（ConfigManager 还不存在）。

- [ ] **Step 3: 创建 ConfigManager**

```csharp
// FileOrganizer/Core/ConfigManager.cs
using System.Text.Json;

namespace FileOrganizer.Core;

public class ConfigManager
{
    private readonly string _rulesPath;
    private readonly string _settingsPath;
    private List<Rule> _rules = new();
    private Settings _settings = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ConfigManager(string? configDir = null)
    {
        configDir ??= Path.GetDirectoryName(Environment.ProcessPath)!;
        _rulesPath = Path.Combine(configDir, "rules.json");
        _settingsPath = Path.Combine(configDir, "settings.json");
    }

    public IReadOnlyList<Rule> Rules => _rules.AsReadOnly();
    public Settings Settings => _settings;

    public void Load()
    {
        _rules = LoadFile(_rulesPath, () => new List<Rule>());
        _settings = LoadFile(_settingsPath, () => new Settings());
    }

    public void SaveRules()
    {
        var json = JsonSerializer.Serialize(_rules, JsonOptions);
        File.WriteAllText(_rulesPath, json);
    }

    public void SaveSettings()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public void AddRule(Rule rule)
    {
        _rules.Add(rule);
        SaveRules();
    }

    public bool RemoveRule(string id)
    {
        var removed = _rules.RemoveAll(r => r.Id == id);
        if (removed > 0) SaveRules();
        return removed > 0;
    }

    public Rule? GetRuleById(string id)
    {
        return _rules.FirstOrDefault(r => r.Id == id);
    }

    private T LoadFile<T>(string path, Func<T> defaultFactory)
    {
        try
        {
            if (!File.Exists(path))
            {
                var instance = defaultFactory();
                var json = JsonSerializer.Serialize(instance, JsonOptions);
                File.WriteAllText(path, json);
                return instance;
            }
            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(content) ?? defaultFactory();
        }
        catch
        {
            return defaultFactory();
        }
    }
}
```

- [ ] **Step 4: 验证测试绿**

```bash
dotnet test tests/FileOrganizer.Tests
```
Expected: 1 passed.

- [ ] **Step 5: 写第 2 个测试 — AddRule 持久化到文件**

```csharp
// 追加到 ConfigManagerTests.cs

[Fact]
public void AddRule_SavesToFile()
{
    var mgr = new ConfigManager(_testDir);
    mgr.Load();

    mgr.AddRule(new Rule { Name = "图片", Path = "D:\\Pictures" });

    var mgr2 = new ConfigManager(_testDir);
    mgr2.Load();
    Assert.Single(mgr2.Rules);
    Assert.Equal("图片", mgr2.Rules[0].Name);
}
```

- [ ] **Step 6: 验证测试绿**

```bash
dotnet test tests/FileOrganizer.Tests
```
Expected: 2 passed.

- [ ] **Step 7: 写第 3 个测试 — RemoveRule 生效**

```csharp
// 追加到 ConfigManagerTests.cs

[Fact]
public void RemoveRule_RemovesAndPersists()
{
    var mgr = new ConfigManager(_testDir);
    mgr.Load();
    var rule = new Rule { Name = "文档" };
    mgr.AddRule(rule);

    var removed = mgr.RemoveRule(rule.Id);
    Assert.True(removed);

    var mgr2 = new ConfigManager(_testDir);
    mgr2.Load();
    Assert.Empty(mgr2.Rules);
}
```

- [ ] **Step 8: 写第 4 个测试 — 损坏的 JSON 返回默认值**

```csharp
// 追加到 ConfigManagerTests.cs

[Fact]
public void Load_CorruptedJsonReturnsDefaults()
{
    File.WriteAllText(Path.Combine(_testDir, "rules.json"), "{bad json}");
    File.WriteAllText(Path.Combine(_testDir, "settings.json"), "{bad json}");

    var mgr = new ConfigManager(_testDir);
    mgr.Load();

    Assert.Empty(mgr.Rules);
    Assert.NotNull(mgr.Settings);
    Assert.Equal("prompt", mgr.Settings.DefaultConflictAction);
}
```

- [ ] **Step 9: 验证所有 ConfigManager 测试通过**

```bash
dotnet test tests/FileOrganizer.Tests
```
Expected: 4 passed.

- [ ] **Step 10: 提交**

```bash
git add .
git commit -m "feat: ConfigManager with JSON read/write and tests"
```

---

### Task 3: FileMover（文件移动逻辑）

**Files:**
- Create: `FileOrganizer/Core/FileMover.cs`
- Modify: `FileOrganizer/tests/FileOrganizer.Tests/ConfigManagerTests.cs`（追加 FileMover 测试到同一个文件）

- [ ] **Step 1: 写第 1 个 FileMover 测试 — 移动文件到目标目录**

```csharp
// 追加到 ConfigManagerTests.cs

public class FileMoverTests : IDisposable
{
    private readonly string _testDir;
    private readonly ConfigManager _config;
    private readonly FileMover _mover;

    public FileMoverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FO_MoveTest_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
        _config = new ConfigManager(_testDir);
        _config.Load();
        _mover = new FileMover(_config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void MoveFile_MovesToTargetDirectory()
    {
        var source = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(source, "hello");
        var targetDir = Path.Combine(_testDir, "Target");

        var result = _mover.MoveFile(source, targetDir);

        Assert.True(result.Success);
        Assert.False(File.Exists(source), "Source should not exist after move");
        Assert.True(File.Exists(result.DestinationPath!), "File should exist at destination");
        Assert.Contains("Target", result.DestinationPath!);
    }
}
```

- [ ] **Step 2: 验证测试红**

```bash
dotnet test tests/FileOrganizer.Tests --filter FileMoverTests
```
Expected: 编译失败（FileMover 还不存在）。

- [ ] **Step 3: 创建 FileMover**

```csharp
// FileOrganizer/Core/FileMover.cs
namespace FileOrganizer.Core;

public class FileMoveResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DestinationPath { get; set; }
}

public class FileMover
{
    private readonly ConfigManager _config;

    public FileMover(ConfigManager config)
    {
        _config = config;
    }

    public FileMoveResult MoveFile(
        string sourcePath,
        string targetDir,
        string? newFileName = null,
        ConflictAction? conflictAction = null)
    {
        try
        {
            var resolvedTarget = Environment.ExpandEnvironmentVariables(targetDir);
            Directory.CreateDirectory(resolvedTarget);

            var fileName = newFileName ?? Path.GetFileName(sourcePath);
            var destPath = Path.Combine(resolvedTarget, fileName);

            var action = conflictAction ?? ParseConflictAction(_config.Settings.DefaultConflictAction);

            if (File.Exists(destPath))
            {
                switch (action)
                {
                    case ConflictAction.Skip:
                        return new FileMoveResult
                        {
                            Success = false,
                            ErrorMessage = "文件已存在，已跳过"
                        };
                    case ConflictAction.AutoRename:
                        destPath = GetUniqueFilePath(destPath);
                        break;
                    case ConflictAction.Overwrite:
                        File.Delete(destPath);
                        break;
                    case ConflictAction.Prompt:
                        return new FileMoveResult
                        {
                            Success = false,
                            ErrorMessage = "CONFLICT_PROMPT",
                            DestinationPath = destPath
                        };
                }
            }

            File.Move(sourcePath, destPath);

            return new FileMoveResult
            {
                Success = true,
                DestinationPath = destPath
            };
        }
        catch (Exception ex)
        {
            return new FileMoveResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string GetUniqueFilePath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        for (int i = 1; ; i++)
        {
            var newPath = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(newPath))
                return newPath;
        }
    }

    internal static ConflictAction ParseConflictAction(string value) => value switch
    {
        "autoRename" => ConflictAction.AutoRename,
        "overwrite" => ConflictAction.Overwrite,
        "skip" => ConflictAction.Skip,
        _ => ConflictAction.Prompt
    };
}
```

- [ ] **Step 4: 验证测试绿**

```bash
dotnet test tests/FileOrganizer.Tests --filter FileMoverTests
```
Expected: 1 passed.

- [ ] **Step 5: 写第 2 个 FileMover 测试 — 重命名**

```csharp
// 追加到 FileMoverTests class

[Fact]
public void MoveFile_WithNewFileName()
{
    var source = Path.Combine(_testDir, "old.txt");
    File.WriteAllText(source, "data");
    var targetDir = Path.Combine(_testDir, "Target");

    var result = _mover.MoveFile(source, targetDir, "新文件.txt");

    Assert.True(result.Success);
    Assert.True(File.Exists(Path.Combine(targetDir, "新文件.txt")));
}
```

- [ ] **Step 6: 写第 3 个测试 — AutoRename 冲突处理**

```csharp
// 追加到 FileMoverTests class

[Fact]
public void MoveFile_AutoRenameOnConflict()
{
    var targetDir = Path.Combine(_testDir, "Target");
    Directory.CreateDirectory(targetDir);
    File.WriteAllText(Path.Combine(targetDir, "doc.txt"), "existing");

    var source = Path.Combine(_testDir, "doc.txt");
    File.WriteAllText(source, "new");

    var result = _mover.MoveFile(source, targetDir, null, ConflictAction.AutoRename);

    Assert.True(result.Success);
    Assert.NotEqual(Path.Combine(targetDir, "doc.txt"), result.DestinationPath);
    Assert.Contains("doc (1)", result.DestinationPath!);
}
```

- [ ] **Step 7: 写第 4 个测试 — Overwrite 冲突处理**

```csharp
// 追加到 FileMoverTests class

[Fact]
public void MoveFile_OverwriteOnConflict()
{
    var targetDir = Path.Combine(_testDir, "Target");
    Directory.CreateDirectory(targetDir);
    File.WriteAllText(Path.Combine(targetDir, "doc.txt"), "old");

    var source = Path.Combine(_testDir, "doc.txt");
    File.WriteAllText(source, "new content");

    var result = _mover.MoveFile(source, targetDir, null, ConflictAction.Overwrite);

    Assert.True(result.Success);
    Assert.Equal(Path.Combine(targetDir, "doc.txt"), result.DestinationPath);
    Assert.Equal("new content", File.ReadAllText(result.DestinationPath));
}
```

- [ ] **Step 8: 写第 5 个测试 — Skip 冲突处理**

```csharp
// 追加到 FileMoverTests class

[Fact]
public void MoveFile_SkipOnConflict()
{
    var targetDir = Path.Combine(_testDir, "Target");
    Directory.CreateDirectory(targetDir);
    File.WriteAllText(Path.Combine(targetDir, "doc.txt"), "existing");

    var source = Path.Combine(_testDir, "doc.txt");
    File.WriteAllText(source, "new");

    var result = _mover.MoveFile(source, targetDir, null, ConflictAction.Skip);

    Assert.False(result.Success);
    Assert.True(File.Exists(source)); // Source remains
    Assert.Equal("existing", File.ReadAllText(Path.Combine(targetDir, "doc.txt"))); // Target unchanged
}
```

- [ ] **Step 9: 写第 6 个测试 — 自动创建目标目录**

```csharp
// 追加到 FileMoverTests class

[Fact]
public void MoveFile_CreatesTargetDirectory()
{
    var source = Path.Combine(_testDir, "test.txt");
    File.WriteAllText(source, "data");
    var targetDir = Path.Combine(_testDir, "NewFolder", "SubFolder");

    var result = _mover.MoveFile(source, targetDir);

    Assert.True(result.Success);
    Assert.True(Directory.Exists(targetDir));
}
```

- [ ] **Step 10: 验证所有 FileMover 测试通过**

```bash
dotnet test tests/FileOrganizer.Tests
```
Expected: 10 passed（4 ConfigManager + 6 FileMover）。

- [ ] **Step 11: 提交**

```bash
git add .
git commit -m "feat: FileMover with move, rename, conflict handling and tests"
```

---

### Task 4: 悬浮窗（FloatingWindow）

**Files:**
- Create: `FileOrganizer/Forms/FloatingWindow.cs`

无需单元测试（WinForms UI），手动验证。

- [ ] **Step 1: 创建 FloatingWindow**

```csharp
// FileOrganizer/Forms/FloatingWindow.cs
using FileOrganizer.Core;

namespace FileOrganizer.Forms;

public class FloatingWindow : Form
{
    private readonly ConfigManager _config;
    private const int WindowSize = 80;
    private const int HoverOpacity = 90;
    private Point _dragStart;
    private bool _isDragging;

    public FloatingWindow(ConfigManager config)
    {
        _config = config;
        InitializeComponent();
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

        BackColor = Color.FromArgb(52, 73, 94); // Dark blue-gray
        var iconLabel = new Label
        {
            Text = "📁",
            Font = new Font("Segoe UI", 28),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AllowDrop = true
        };
        Controls.Add(iconLabel);

        // Restore saved position
        if (_config.Settings.FloatingWindowX != 0 || _config.Settings.FloatingWindowY != 0)
        {
            Location = new Point(_config.Settings.FloatingWindowX, _config.Settings.FloatingWindowY);
        }

        // File drag-drop events
        DragEnter += (s, e) =>
        {
            if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Move;
                Opacity = HoverOpacity / 100.0;
                BackColor = Color.FromArgb(41, 128, 185);
            }
        };

        DragLeave += (s, e) =>
        {
            Opacity = 0.5;
            BackColor = Color.FromArgb(52, 73, 94);
        };

        DragDrop += (s, e) =>
        {
            Opacity = 0.5;
            BackColor = Color.FromArgb(52, 73, 94);

            if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                if (files.Length > 0)
                {
                    using var dialog = new OrganizeDialog(files.ToList(), _config);
                    dialog.ShowDialog(this);
                }
            }
        };

        // Window drag-move
        MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragStart = e.Location;
            }
        };

        MouseMove += (s, e) =>
        {
            if (_isDragging)
            {
                Left += e.X - _dragStart.X;
                Top += e.Y - _dragStart.Y;
            }
        };

        MouseUp += (s, e) =>
        {
            if (_isDragging)
            {
                _isDragging = false;
                // Constrain to screen
                var screen = Screen.FromPoint(Location).WorkingArea;
                Left = Math.Clamp(Left, 0, screen.Width - Width);
                Top = Math.Clamp(Top, 0, screen.Height - Height);
                // Save position
                _config.Settings.FloatingWindowX = Left;
                _config.Settings.FloatingWindowY = Top;
                _config.SaveSettings();
            }
        };

        // Right-click context menu
        var ctxMenu = new ContextMenuStrip();
        ctxMenu.Items.Add("规则管理", null, (s, e) => { /* Phase 2 */ });
        ctxMenu.Items.Add("设置", null, (s, e) => { /* Phase 2 */ });
        ctxMenu.Items.Add("-");
        ctxMenu.Items.Add("退出", null, (s, e) => Application.Exit());
        ContextMenuStrip = ctxMenu;
    }
}
```

- [ ] **Step 2: 验证可编译**

```bash
dotnet build
```
Expected: Build 成功。

- [ ] **Step 3: 手动验证清单**（在 Windows 上运行程序）

| 验证项 | 预期行为 |
|--------|----------|
| 悬浮窗显示 | 80x80 半透明小窗口，居中（或上次位置） |
| 拖拽移动 | 鼠标按住拖动，窗口跟随，松手后保存位置 |
| 文件拖入 | 悬停时变蓝高亮（90% 透明度） |
| 文件移出 | 恢复暗色半透明 |
| 右键菜单 | 显示规则管理/设置(灰)/退出 |

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: FloatingWindow with drag-drop and drag-move"
```

---

### Task 5: 整理弹窗（OrganizeDialog）

**Files:**
- Create: `FileOrganizer/Forms/OrganizeDialog.cs`

- [ ] **Step 1: 创建 OrganizeDialog**

```csharp
// FileOrganizer/Forms/OrganizeDialog.cs
using FileOrganizer.Core;

namespace FileOrganizer.Forms;

public class OrganizeDialog : Form
{
    private readonly List<string> _sourceFiles;
    private readonly ConfigManager _config;
    private readonly string _sourcePath;
    private TextBox _nameBox;
    private FlowLayoutPanel _tagPanel;
    private ComboBox _conflictCombo;
    private Button _moveButton;
    private RadioButton? _selectedTag;

    public OrganizeDialog(List<string> sourceFiles, ConfigManager config)
    {
        _sourceFiles = sourceFiles;
        _sourcePath = sourceFiles[0];
        _config = config;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "整理文件";
        Size = new Size(500, 370);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        // --- File name ---
        var fileLabel = new Label { Text = "文件:", Top = 20, Left = 20, Width = 50 };

        _nameBox = new TextBox
        {
            Top = 18,
            Left = 70,
            Width = 400,
            Text = Path.GetFileName(_sourcePath)
        };

        // --- Tag selection ---
        var tagLabel = new Label { Text = "目标标签:", Top = 60, Left = 20, Width = 80 };

        _tagPanel = new FlowLayoutPanel
        {
            Top = 58,
            Left = 20,
            Width = 450,
            Height = 140,
            FlowDirection = FlowDirection.TopDown,
            AutoScroll = true,
            BorderStyle = BorderStyle.Fixed3D
        };
        LoadTags();

        // --- Conflict action ---
        var conflictLabel = new Label { Text = "冲突时:", Top = 215, Left = 20, Width = 60 };

        _conflictCombo = new ComboBox
        {
            Top = 213,
            Left = 80,
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _conflictCombo.Items.AddRange(new object[] { "弹窗选择", "自动加序号", "覆盖", "跳过" });
        _conflictCombo.SelectedIndex = GetConflictComboIndex(_config.Settings.DefaultConflictAction);

        // --- Buttons ---
        _moveButton = new Button
        {
            Text = "移动",
            Top = 260,
            Left = 300,
            Width = 80,
            Height = 30,
            Enabled = false
        };
        _moveButton.Click += MoveButton_Click;

        var cancelButton = new Button
        {
            Text = "取消",
            Top = 260,
            Left = 390,
            Width = 80,
            Height = 30
        };
        cancelButton.Click += (s, e) => Close();

        // --- Gather controls ---
        Controls.AddRange(new Control[] {
            fileLabel, _nameBox,
            tagLabel, _tagPanel,
            conflictLabel, _conflictCombo,
            _moveButton, cancelButton
        });
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

    private static int GetConflictComboIndex(string action) => action switch
    {
        "autoRename" => 1,
        "overwrite" => 2,
        "skip" => 3,
        _ => 0
    };

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
        var fileName = _nameBox.Text.Trim();
        var conflictAction = GetSelectedConflictAction();

        while (true)
        {
            var result = mover.MoveFile(_sourcePath, rule.Path, fileName, conflictAction);

            if (result.Success)
            {
                MessageBox.Show(
                    $"文件已移动到:\n{result.DestinationPath}",
                    "完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                MessageBox.Show(
                    $"移动失败:\n{result.ErrorMessage}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }
    }
}
```

- [ ] **Step 2: 验证可编译**

```bash
dotnet build
```
Expected: Build 成功。

- [ ] **Step 3: 手动验证清单**（在 Windows 上运行，需要先用拖放触发）

| 验证项 | 预期行为 |
|--------|----------|
| 弹窗显示 | 整理文件标题，显示源文件名 |
| 文件名编辑 | 文本框可修改，修改生效 |
| 标签列表 | 显示 rules.json 中的标签，单选 |
| 移动按钮 | 未选标签时禁用，选中后启用 |
| 冲突弹窗 | 目标文件已存在时弹出 Yes/No/Cancel 对话框 |
| 移动完成 | 显示成功消息，文件被移动到目标目录 |

- [ ] **Step 4: 提交**

```bash
git add .
git commit -m "feat: OrganizeDialog with tag selection and move execution"
```

---

### Task 6: Program.cs + 单实例守卫

**Files:**
- Create: `FileOrganizer/Program.cs`

- [ ] **Step 1: 创建 Program.cs**

```csharp
// FileOrganizer/Program.cs
using FileOrganizer.Core;
using FileOrganizer.Forms;

namespace FileOrganizer;

static class Program
{
    private const string MutexName = "FileOrganizer-SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            // Bring existing instance to foreground
            var existing = System.Diagnostics.Process.GetProcessesByName("FileOrganizer")
                .FirstOrDefault(p => p.Id != Environment.ProcessId);
            if (existing != null && existing.MainWindowHandle != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(existing.MainWindowHandle);
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var config = new ConfigManager();
        config.Load();

        // Add default rules if first run
        if (config.Rules.Count == 0)
        {
            config.AddRule(new Rule
            {
                Name = "图片",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Organized")
            });
            config.AddRule(new Rule
            {
                Name = "文档",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Organized")
            });
            config.AddRule(new Rule
            {
                Name = "下载",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "Organized")
            });
        }

        Application.Run(new FloatingWindow(config));
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);
}
```

- [ ] **Step 2: 验证可编译**

```bash
dotnet build
```
Expected: Build 成功。

- [ ] **Step 3: 手动验证**

| 验证项 | 预期行为 |
|--------|----------|
| 首次启动 | 自动创建 rules.json（含 3 条默认规则）和 settings.json |
| 重复启动 | 第二次启动时聚焦已有窗口，不创建新实例 |
| 默认规则 | 在 Pictures/Organized, Documents/Organized, Downloads/Organized 各有一条 |

- [ ] **Step 4: 设置启动项（Program.cs + app.manifest）**

创建 `FileOrganizer/app.manifest` 启用 Windows 视觉样式：

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{e2011457-1546-43c5-a5fe-008deee3d3f0}" /> <!-- Windows 8 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" /> <!-- Windows 8.1 -->
      <supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}" /> <!-- Windows 10 -->
      <supportedOS Id="{8f71e1f5-8b8b-4f8f-8a8f-7b9f5c8d7e9f}" /> <!-- Windows 11 -->
    </application>
  </compatibility>
</assembly>
```

更新 csproj 引用清单文件：

```bash
# 在 FileOrganizer.csproj 的 <PropertyGroup> 中添加:
# <ApplicationManifest>app.manifest</ApplicationManifest>
```

- [ ] **Step 5: 最终验证全部编译**

```bash
dotnet build
dotnet test tests/FileOrganizer.Tests
```
Expected: Build 成功，10 个测试全部通过。

- [ ] **Step 6: 提交**

```bash
git add .
git commit -m "feat: entry point with single instance guard and default rules"
```

---

## Phase 1 交付清单

| 模块 | 状态 | 说明 |
|------|------|------|
| 项目骨架 | ✅ | .NET 8 WinForms，xUnit 测试项目 |
| Models | ✅ | Rule、Settings、ConflictAction |
| ConfigManager | ✅ | JSON 读写 + 内存缓存 + 异常降级 |
| FileMover | ✅ | 移动/重命名/4 种冲突策略 |
| FloatingWindow | ✅ | 半透明悬浮窗、拖放触发、拖拽移动、右键菜单 |
| OrganizeDialog | ✅ | 文件名编辑、标签单选、冲突弹窗选择 |
| 单实例守卫 | ✅ | Mutex 防重复 + 激活已有窗口 |
| 测试覆盖 | ✅ | ConfigManager 4 个 + FileMover 6 个 |

## 构建发布

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

输出在 `bin/Release/net8.0-windows/win-x64/publish/FileOrganizer.exe`

Phase 1 不需要管理员权限运行（右键菜单注册在 Phase 3）。

---

## 后续 Phase 预告

- **Phase 2**: 规则管理窗口（CRUD）、设置窗口、系统托盘
- **Phase 3**: 右键菜单注册（需 UAC）、多文件批处理、导入/导出配置、错误日志
