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
}

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
}
