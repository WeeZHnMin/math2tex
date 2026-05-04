using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Math2Tex.Backend;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Math2Tex;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Math2Tex_SingleInstance_{F2A91E2C-7B0D-4B2E-9D8C-1234567890AB}";

    private Forms.NotifyIcon? _trayIcon;
    private GlobalHotkey? _toggleHotkey;
    private ClipboardWatcher? _clipboardWatcher;
    private MainWindow? _main;
    private System.Threading.Mutex? _singleInstanceMutex;

    public bool ShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new System.Threading.Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Math2Tex 已在运行。请在系统托盘里找到现有实例。", "Math2Tex",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _main = new MainWindow();
        MainWindow = _main;

        // Hook SourceInitialized BEFORE Show — Show fires it synchronously.
        _main.SourceInitialized += (_, _) =>
        {
            var settings = BackendSettings.Load();
            InitHotkey(_main, settings.HotkeyModifiers, settings.HotkeyKey);
            InitClipboardWatcher(_main);
        };

        _main.Show();
        InitTray(_main);
    }

    private void InitTray(MainWindow main)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => main.ShowFromTray());
        menu.Items.Add("Convert clipboard now", null, async (_, _) => await main.RunClipboardConversionAsync(manual: true));
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) => RequestExit());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon() ?? Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Math2Tex",
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => main.ShowFromTray();
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) main.ShowFromTray();
        };
    }

    private void InitHotkey(MainWindow main, uint modifiers, uint key)
    {
        var helper = new WindowInteropHelper(main);
        var source = HwndSource.FromHwnd(helper.Handle);
        if (source is null) return;

        _toggleHotkey = new GlobalHotkey(helper.Handle, source, hotkeyId: 0xB001);
        _toggleHotkey.Pressed += () => main.ToggleVisibility();
        if (modifiers != 0 && key != 0) _toggleHotkey.Register(modifiers, key);
    }

    private void InitClipboardWatcher(MainWindow main)
    {
        var helper = new WindowInteropHelper(main);
        var source = HwndSource.FromHwnd(helper.Handle);
        if (source is null)
        {
            main.ReportWatcherState("HwndSource 为空，监听未启用");
            return;
        }

        _clipboardWatcher = new ClipboardWatcher(helper.Handle, source);
        _clipboardWatcher.Updated += () => main.OnClipboardChanged();
        var ok = _clipboardWatcher.Start();
        main.ReportWatcherState(ok ? "剪贴板监听已启用" : "AddClipboardFormatListener 调用失败");
    }

    private static Drawing.Icon? LoadAppIcon()
    {
        try
        {
            var info = GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info?.Stream is null) return null;
            using var s = info.Stream;
            return new Drawing.Icon(s);
        }
        catch { return null; }
    }

    public bool ApplyToggleHotkey(uint modifiers, uint key)
    {
        if (_toggleHotkey is null) return false;
        if (modifiers == 0 || key == 0) { _toggleHotkey.Unregister(); return true; }
        return _toggleHotkey.Register(modifiers, key);
    }

    public void ShowTrayBalloon(string title, string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        if (_trayIcon is null) return;
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(2000);
    }

    private void RequestExit()
    {
        ShuttingDown = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _toggleHotkey?.Dispose();
        _clipboardWatcher?.Dispose();
        _toggleHotkey = null;
        _clipboardWatcher = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _toggleHotkey?.Dispose();
        _clipboardWatcher?.Dispose();
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

internal sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly int _hotkeyId;
    private bool _registered;

    public event Action? Pressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public GlobalHotkey(IntPtr hwnd, HwndSource source, int hotkeyId)
    {
        _hwnd = hwnd;
        _source = source;
        _hotkeyId = hotkeyId;
        _source.AddHook(WndProc);
    }

    public bool Register(uint modifiers, uint key)
    {
        if (_registered) UnregisterHotKey(_hwnd, _hotkeyId);
        _registered = RegisterHotKey(_hwnd, _hotkeyId, modifiers, key);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, _hotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _hotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }
}

internal sealed class ClipboardWatcher : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private bool _started;

    public event Action? Updated;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public ClipboardWatcher(IntPtr hwnd, HwndSource source)
    {
        _hwnd = hwnd;
        _source = source;
    }

    public bool Start()
    {
        if (_started) return true;
        _started = AddClipboardFormatListener(_hwnd);
        if (_started) _source.AddHook(WndProc);
        return _started;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            Updated?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_started)
        {
            RemoveClipboardFormatListener(_hwnd);
            _source.RemoveHook(WndProc);
            _started = false;
        }
    }
}
