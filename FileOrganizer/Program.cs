using System.Runtime.InteropServices;
using FileOrganizer.Core;
using FileOrganizer.Forms;
using FileOrganizer.Utils;

namespace FileOrganizer;

static class Program
{
    private const string MutexName = "FileOrganizer-SingleInstance";
    private const string SignalName = "FileOrganizer-Signal";

    private static Logger? _logger;
    private static ConfigManager? _config;
    private static FloatingWindow? _mainForm;
    private static EventWaitHandle? _signalHandle;

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            // Another instance exists — pass files to it
            if (args.Length > 0)
            {
                var pendingDir = Path.Combine(Path.GetTempPath(), "FileOrganizer");
                Directory.CreateDirectory(pendingDir);
                File.WriteAllLines(Path.Combine(pendingDir, "pending.txt"), args);
                using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
                signal.Set();
            }
            else
            {
                BringExistingToFront();
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _logger = new Logger();
        _logger.Info("程序启动");

        _config = new ConfigManager();
        _config.Load();

        if (_config.Rules.Count == 0)
        {
            _config.AddRule(new Rule
            {
                Name = "图片",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Organized")
            });
            _config.AddRule(new Rule
            {
                Name = "文档",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Organized")
            });
            _config.AddRule(new Rule
            {
                Name = "下载",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "Organized")
            });
        }

        // Register right-click menu if enabled
        if (_config.Settings.ContextMenuEnabled)
        {
            try { ShellExtensions.Register(); _logger.Info("右键菜单已注册"); }
            catch (Exception ex) { _logger.Error($"右键菜单注册失败: {ex.Message}"); }
        }

        // Start IPC watcher for receiving files from other instances
        _signalHandle = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        var watcher = new Thread(WatchForPendingFiles)
        {
            IsBackground = true,
            Name = "IPC-Watcher"
        };
        watcher.Start();

        // Create form (force handle creation so Invoke works from IPC thread)
        _mainForm = new FloatingWindow(_config);
        _ = _mainForm.Handle;

        // If launched with file args (from right-click), process them immediately
        if (args.Length > 0)
        {
            _mainForm.BeginInvoke(() =>
            {
                using var dialog = new OrganizeDialog(args.ToList(), _config!);
                dialog.ShowDialog(_mainForm);
            });
        }

        Application.Run(_mainForm);

        _signalHandle?.Dispose();
        _logger?.Info("程序退出");
    }

    private static void WatchForPendingFiles()
    {
        try
        {
            while (_signalHandle != null)
            {
                _signalHandle.WaitOne();

                var pendingPath = Path.Combine(Path.GetTempPath(), "FileOrganizer", "pending.txt");
                if (!File.Exists(pendingPath)) continue;

                var lines = File.ReadAllLines(pendingPath);
                File.Delete(pendingPath);

                if (lines.Length > 0 && _mainForm != null && !_mainForm.IsDisposed)
                {
                    Thread.Sleep(200); // Wait for form to be ready
                    _mainForm.Invoke(() =>
                    {
                        using var dialog = new OrganizeDialog(lines.ToList(), _config!);
                        dialog.ShowDialog(_mainForm);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"IPC 监听异常: {ex.Message}");
        }
    }

    private static void BringExistingToFront()
    {
        try
        {
            var existing = System.Diagnostics.Process.GetProcessesByName("FileOrganizer")
                .FirstOrDefault(p => p.Id != Environment.ProcessId);
            if (existing?.MainWindowHandle != IntPtr.Zero && existing?.MainWindowHandle != null)
            {
                NativeMethods.SetForegroundWindow(existing.MainWindowHandle);
            }
        }
        catch { }
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);
}
