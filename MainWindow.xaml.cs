using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Math2Tex.Backend;
using Math2Tex.Storage;
using Wpf.Ui.Controls;
using Forms = System.Windows.Forms;

namespace Math2Tex;

public partial class MainWindow : FluentWindow
{
    private BackendSettings _settings = new();
    private ObservableCollection<HistoryItem> _history = new();

    private uint _hotkeyMods;
    private uint _hotkeyKey;

    private bool _autoConvert = true;
    private int _maxChars = 600;
    private bool _converting;
    private bool _loadingAll;
    private bool _settingsLoaded;
    private string _lastSelfWrite = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadAll();
    }

    private void LoadAll()
    {
        _loadingAll = true;
        try { LoadAllCore(); } finally { _loadingAll = false; }
    }

    private void LoadAllCore()
    {
        _settings = BackendSettings.Load();
        BaseUrlBox.Text = _settings.BaseUrl;
        ApiKeyBox.Password = _settings.ApiKey;
        ModelBox.Text = _settings.Model;
        TemperatureBox.Text = _settings.Temperature.ToString(CultureInfo.InvariantCulture);
        MaxTokensBox.Text = _settings.MaxTokens.ToString(CultureInfo.InvariantCulture);
        MaxCharsBox.Text = _settings.AutoConvertMaxChars.ToString(CultureInfo.InvariantCulture);
        SystemPromptBox.Text = string.IsNullOrWhiteSpace(_settings.SystemPrompt)
            ? BackendSettings.DefaultSystemPrompt
            : _settings.SystemPrompt;
        ExtraBodyBox.Text = string.IsNullOrWhiteSpace(_settings.ExtraRequestBodyJson)
            ? BackendSettings.DefaultExtraRequestBodyJson
            : _settings.ExtraRequestBodyJson;

        _hotkeyMods = _settings.HotkeyModifiers;
        _hotkeyKey = _settings.HotkeyKey;
        HotkeyBox.Text = FormatHotkey(_hotkeyMods, _hotkeyKey);

        _autoConvert = _settings.AutoConvert;
        _maxChars = _settings.AutoConvertMaxChars;
        UpdateAutoConvertChip();
        UpdateHeroStatus(_autoConvert ? "待命" : "已暂停", _autoConvert ? "#10B981" : "#9CA3AF");

        _history = HistoryStore.Load();
        var historyView = CollectionViewSource.GetDefaultView(_history);
        historyView.Filter = HistoryFilter;
        HistoryList.ItemsSource = historyView;
        _history.CollectionChanged += (_, _) => UpdateHistoryUi();

        UpdateHistoryUi();
        UpdateAuthState();
        UpdateStatus("已加载");
        HotkeyHintText.Text = string.IsNullOrEmpty(HotkeyBox.Text) ? "" : $"切换 {HotkeyBox.Text}";
        _settingsLoaded = true;
    }

    // ===== Hero status =====

    private void UpdateHeroStatus(string text, string colorHex)
    {
        HeroStatusText.Text = text;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            StatusDot.Background = new SolidColorBrush(color);
        }
        catch { }
    }

    private void AutoConvertChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _autoConvert = !_autoConvert;
        UpdateAutoConvertChip();
        UpdateHeroStatus(_autoConvert ? "待命" : "已暂停", _autoConvert ? "#10B981" : "#9CA3AF");
        if (!_settingsLoaded || _loadingAll) return;

        try
        {
            var fresh = BackendSettings.Load();
            fresh.AutoConvert = _autoConvert;
            fresh.Save();
        }
        catch { /* best-effort persistence */ }
        _settings.AutoConvert = _autoConvert;
    }

    private void UpdateAutoConvertChip()
    {
        AutoConvertText.Text = _autoConvert ? "已开启" : "已暂停";
        var color = _autoConvert
            ? Color.FromRgb(0x10, 0xB9, 0x81)
            : Color.FromRgb(0x9C, 0xA3, 0xAF);
        AutoConvertDot.Background = new SolidColorBrush(color);
    }

    // ===== History =====

    private bool HistoryFilter(object obj)
    {
        if (obj is not HistoryItem item) return false;
        var q = HistorySearchBox?.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return true;
        return (item.Source?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.Latex?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void UpdateHistoryUi()
    {
        HistorySubtitle.Text = $"{_history.Count} 条";
        HistoryEmptyState.Visibility = _history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HistorySearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (HistoryList?.ItemsSource is null) return;
        CollectionViewSource.GetDefaultView(_history).Refresh();
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HistoryDetailBox.Text = HistoryList.SelectedItem is HistoryItem item ? item.Latex : string.Empty;
    }

    private void HistoryCopy_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item && !string.IsNullOrEmpty(item.Latex))
        {
            _lastSelfWrite = item.Latex;
            Clipboard.SetText(item.Latex);
            UpdateStatus("已复制");
        }
    }

    private void HistoryDelete_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item)
        {
            _history.Remove(item);
            HistoryStore.Save(_history);
            UpdateStatus("已删除");
        }
    }

    private void HistoryClear_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count == 0) return;
        var confirm = System.Windows.MessageBox.Show("Clear all history?", "Math2Tex", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.OK) return;
        _history.Clear();
        HistoryStore.Save(_history);
        UpdateStatus("已清空");
    }

    // ===== Settings =====

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var error))
        {
            UpdateStatus(error);
            ShowTestResult(error, InfoBarSeverity.Error);
            return;
        }
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            UpdateStatus("写盘失败");
            ShowTestResult($"保存失败：{Truncate(ex.Message, 200)}\n路径：{BackendSettings.SettingsPath}", InfoBarSeverity.Error);
            return;
        }

        // Verify what landed on disk and show it back to the user.
        try
        {
            var verified = BackendSettings.Load();
            ShowTestResult(
                $"已写入 {BackendSettings.SettingsPath}\n" +
                $"BaseUrl = {verified.BaseUrl}\n" +
                $"Model = {verified.Model}\n" +
                $"ApiKey = {(string.IsNullOrEmpty(verified.ApiKey) ? "(空)" : "*** " + verified.ApiKey.Length + " 字符")}\n" +
                $"AutoConvert = {verified.AutoConvert}, MaxChars = {verified.AutoConvertMaxChars}\n" +
                $"Hotkey = {FormatHotkey(verified.HotkeyModifiers, verified.HotkeyKey)}",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowTestResult("已保存但回读失败：" + ex.Message, InfoBarSeverity.Warning);
        }

        if (Application.Current is App app)
        {
            var ok = app.ApplyToggleHotkey(_hotkeyMods, _hotkeyKey);
            if (!ok && _hotkeyKey != 0)
            {
                UpdateStatus("热键被占用，已保存但未生效");
                return;
            }
        }
        HotkeyHintText.Text = string.IsNullOrEmpty(HotkeyBox.Text) ? "" : $"切换 {HotkeyBox.Text}";
        _maxChars = _settings.AutoConvertMaxChars;
        UpdateStatus("已保存");
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ResetToDefaults();
        BaseUrlBox.Text = _settings.BaseUrl;
        ApiKeyBox.Password = _settings.ApiKey;
        ModelBox.Text = _settings.Model;
        TemperatureBox.Text = _settings.Temperature.ToString(CultureInfo.InvariantCulture);
        MaxTokensBox.Text = _settings.MaxTokens.ToString(CultureInfo.InvariantCulture);
        MaxCharsBox.Text = _settings.AutoConvertMaxChars.ToString(CultureInfo.InvariantCulture);
        SystemPromptBox.Text = _settings.SystemPrompt;
        ExtraBodyBox.Text = _settings.ExtraRequestBodyJson;
        _hotkeyMods = _settings.HotkeyModifiers;
        _hotkeyKey = _settings.HotkeyKey;
        HotkeyBox.Text = FormatHotkey(_hotkeyMods, _hotkeyKey);
        UpdateStatus("已重置");
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e) => UpdateAuthState();

    private void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = DebugLog.FilePath;
            if (!System.IO.File.Exists(path))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, $"{DateTime.Now:HH:mm:ss.fff}  log file created\r\n");
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            UpdateStatus($"打开日志失败: {ex.Message}");
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BaseUrlBox.Text))
        {
            ShowTestResult("Base URL 不能为空", InfoBarSeverity.Warning);
            return;
        }
        if (!double.TryParse(TemperatureBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp)) temp = 0.1;
        if (!int.TryParse(MaxTokensBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxTok)) maxTok = 32;

        var options = new LlmBackendOptions
        {
            BaseUrl = BaseUrlBox.Text.Trim(),
            ApiKey = ApiKeyBox.Password.Trim(),
            Model = string.IsNullOrWhiteSpace(ModelBox.Text) ? "gpt-4o-mini" : ModelBox.Text.Trim(),
            Temperature = temp,
            MaxTokens = Math.Min(32, maxTok),
            TopP = _settings.TopP,
            PresencePenalty = _settings.PresencePenalty,
            FrequencyPenalty = _settings.FrequencyPenalty,
            ExtraRequestBodyJson = ExtraBodyBox?.Text?.Trim() ?? ""
        };

        TestButton.IsEnabled = false;
        var originalContent = TestButton.Content;
        TestButton.Content = "测试中…";
        ShowTestResult("正在连接…", InfoBarSeverity.Informational);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var backend = new OpenAiCompatibleBackend(options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await backend.ConvertAsync(
                "Reply with only the two characters: OK",
                "ping",
                cts.Token);
            sw.Stop();
            var preview = Truncate((result.Content ?? "").Trim(), 80);
            ShowTestResult(
                $"连接成功 · {sw.ElapsedMilliseconds} ms · model={result.Model}\n返回：{preview}",
                InfoBarSeverity.Success);
        }
        catch (TaskCanceledException)
        {
            ShowTestResult("超时（>20s），检查网络或 Base URL 是否可达", InfoBarSeverity.Error);
        }
        catch (HttpRequestException ex)
        {
            ShowTestResult($"HTTP 失败：{Truncate(ex.Message, 200)}", InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowTestResult($"失败：{Truncate(ex.Message, 200)}", InfoBarSeverity.Error);
        }
        finally
        {
            TestButton.IsEnabled = true;
            TestButton.Content = originalContent;
        }
    }

    private void ShowTestResult(string message, InfoBarSeverity severity)
    {
        TestInfoBar.Severity = severity;
        TestInfoBar.Title = severity switch
        {
            InfoBarSeverity.Success => "测试通过",
            InfoBarSeverity.Error => "测试失败",
            InfoBarSeverity.Warning => "无法测试",
            _ => "测试中"
        };
        TestInfoBar.Message = message;
        TestInfoBar.IsOpen = true;
    }

    private bool TryReadSettings(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(BaseUrlBox.Text)) { error = "Base URL 不能为空"; return false; }
        if (!double.TryParse(TemperatureBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp)) { error = "Temperature 必须是数字"; return false; }
        if (!int.TryParse(MaxTokensBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxTok)) { error = "Max Tokens 必须是整数"; return false; }
        if (!int.TryParse(MaxCharsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxChars) || maxChars < 1) { error = "长度上限必须是正整数"; return false; }

        _settings.BaseUrl = BaseUrlBox.Text.Trim();
        _settings.ApiKey = ApiKeyBox.Password.Trim();
        _settings.Model = string.IsNullOrWhiteSpace(ModelBox.Text) ? "gpt-4o-mini" : ModelBox.Text.Trim();
        _settings.Temperature = temp;
        _settings.MaxTokens = maxTok;
        _settings.AutoConvertMaxChars = maxChars;
        _settings.AutoConvert = _autoConvert;
        _settings.HotkeyModifiers = _hotkeyMods;
        _settings.HotkeyKey = _hotkeyKey;
        _settings.SystemPrompt = string.IsNullOrWhiteSpace(SystemPromptBox.Text)
            ? BackendSettings.DefaultSystemPrompt
            : SystemPromptBox.Text;

        var extra = ExtraBodyBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(extra))
        {
            try { System.Text.Json.Nodes.JsonNode.Parse(extra); }
            catch (Exception ex) { error = $"附加参数 JSON 不合法: {ex.Message}"; return false; }
        }
        _settings.ExtraRequestBodyJson = string.IsNullOrWhiteSpace(extra) ? "{}" : extra;
        return true;
    }

    private void UpdateAuthState()
    {
        var missing = string.IsNullOrWhiteSpace(ApiKeyBox.Password);
        AuthInfoBar.Title = missing ? "未配置 API Key" : "API Key 已设置";
        AuthInfoBar.Severity = missing ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
        PathText.Text = BackendSettings.SettingsPath;
        UpdateAuthState();
    }

    // ===== Hotkey capture (Esc clears) =====

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e) => HotkeyBox.Text = "按下组合键…（Esc 取消）";
    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e) => HotkeyBox.Text = FormatHotkey(_hotkeyMods, _hotkeyKey);

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _hotkeyMods = 0;
            _hotkeyKey = 0;
            HotkeyBox.Text = string.Empty;
            Keyboard.ClearFocus();
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
                or Key.System or Key.None)
        {
            return;
        }

        var mods = (uint)Keyboard.Modifiers;
        if (mods == 0)
        {
            HotkeyBox.Text = "需要修饰键（Esc 清除）";
            return;
        }

        _hotkeyMods = mods;
        _hotkeyKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        HotkeyBox.Text = FormatHotkey(_hotkeyMods, _hotkeyKey);
        Keyboard.ClearFocus();
    }

    private static string FormatHotkey(uint mods, uint vk)
    {
        if (vk == 0) return string.Empty;
        var sb = new StringBuilder();
        if ((mods & 0x0002) != 0) sb.Append("Ctrl+");
        if ((mods & 0x0001) != 0) sb.Append("Alt+");
        if ((mods & 0x0004) != 0) sb.Append("Shift+");
        if ((mods & 0x0008) != 0) sb.Append("Win+");
        try { sb.Append(KeyInterop.KeyFromVirtualKey((int)vk).ToString()); }
        catch { sb.Append('?'); }
        return sb.ToString();
    }

    public void ToggleVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
            HideToTray();
        else
            ShowFromTray();
    }

    // ===== Clipboard auto-convert =====

    private int _clipEventCount;

    public void ReportWatcherState(string msg) => UpdateStatus(msg);


    public void OnClipboardChanged()
    {
        _clipEventCount++;
        UpdateStatus($"剪贴板事件 #{_clipEventCount}");
        DebugLog.Write($"--- clipboard event #{_clipEventCount}, autoConvert={_autoConvert}");
        if (!_autoConvert)
        {
            UpdateHeroStatus("已暂停（开关未打开）", "#9CA3AF");
            return;
        }
        Dispatcher.BeginInvoke(new Action(async () => await RunClipboardConversionAsync(manual: false)));
    }

    private const string SkipToken = "__SKIP__";

    public async Task RunClipboardConversionAsync(bool manual)
    {
        DebugLog.Write($"RunClipboardConversionAsync(manual={manual}) entry, _converting={_converting}");
        if (_converting) { UpdateStatus("跳过：上一次还在进行中"); DebugLog.Write("skip: already converting"); return; }

        string? input = null;
        try { if (Clipboard.ContainsText()) input = Clipboard.GetText(); }
        catch (Exception ex) { UpdateStatus($"跳过：读剪贴板异常 {ex.Message}"); DebugLog.Write($"skip: read clipboard threw {ex.GetType().Name}: {ex.Message}"); return; }

        DebugLog.Write($"input length = {input?.Length ?? 0}");

        if (string.IsNullOrWhiteSpace(input))
        {
            UpdateStatus("跳过：剪贴板不是文本或为空");
            DebugLog.Write("skip: empty/non-text");
            if (manual) Notify("剪贴板为空", "复制一段公式后再试", Forms.ToolTipIcon.Warning);
            return;
        }

        if (string.Equals(input, _lastSelfWrite, StringComparison.Ordinal))
        { UpdateStatus("跳过：内容是我们刚写入的"); DebugLog.Write("skip: matches _lastSelfWrite"); return; }
        if (!manual && input.Length > _maxChars)
        { UpdateHeroStatus($"已忽略（>{_maxChars} 字）", "#9CA3AF"); UpdateStatus($"跳过：长度 {input.Length} > 上限 {_maxChars}"); DebugLog.Write($"skip: too long ({input.Length} > {_maxChars})"); return; }

        if (!manual && !LooksLikeMath(input))
        {
            UpdateHeroStatus("非公式（本地预筛）", "#9CA3AF");
            UpdateStatus("跳过：本地预筛判定无数学字符");
            DebugLog.Write("skip: pre-filter says no math signals");
            return;
        }


        var systemPrompt = string.IsNullOrWhiteSpace(_settings.SystemPrompt)
            ? BackendSettings.DefaultSystemPrompt
            : _settings.SystemPrompt;

        _converting = true;
        UpdateHeroStatus("正在转换…", "#F59E0B");
        DebugLog.Write($"calling LLM, model={_settings.Model}, baseUrl={_settings.BaseUrl}");

        var llmStart = DateTime.Now;
        var elapsedTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        elapsedTimer.Tick += (_, _) => UpdateHeroStatus($"正在转换… {(DateTime.Now - llmStart).TotalSeconds:F1}s", "#F59E0B");
        elapsedTimer.Start();

        try
        {
            string output;
            string modelUsed;
            try
            {
                using var backend = new OpenAiCompatibleBackend(_settings.ToBackendOptions());
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                var result = await backend.ConvertAsync(systemPrompt, input, cts.Token);
                output = (result.Content ?? string.Empty).Trim();
                modelUsed = result.Model;
                DebugLog.Write($"LLM ok, latency={(DateTime.Now - llmStart).TotalMilliseconds:F0}ms, model={modelUsed}, output_len={output.Length}");
            }
            catch (Exception ex)
            {
                DebugLog.Write($"LLM failed after {(DateTime.Now - llmStart).TotalMilliseconds:F0}ms: {ex.GetType().Name}: {ex.Message}");
                UpdateHeroStatus("失败", "#EF4444");
                UpdateStatus($"转换失败: {Truncate(ex.Message, 120)}");
                if (manual) Notify("LLM 调用失败", Truncate(ex.Message, 200), Forms.ToolTipIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(output) || output.Equals(SkipToken, StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.Write("LLM returned __SKIP__");
                UpdateHeroStatus("非公式，已忽略", "#9CA3AF");
                return;
            }

            output = StripCodeFences(output);

            _lastSelfWrite = output;
            var item = new HistoryItem { Source = input, Latex = output };
            _history.Insert(0, item);
            HistoryStore.Save(_history);
            DebugLog.Write("history saved, attempting clipboard write");

            try
            {
                await SetClipboardWithRetryAsync(output);
                DebugLog.Write("clipboard write OK");
                UpdateHeroStatus("已替换为 LaTeX", "#10B981");
                UpdateStatus("已替换为 LaTeX");
            }
            catch (Exception ex)
            {
                DebugLog.Write($"clipboard write failed: {ex.GetType().Name}: {ex.Message}");
                // Write failed; clipboard may now be empty due to EmptyClipboard
                // running before SetClipboardData failed. Restore the original
                // input so the user doesn't lose what they had on the clipboard.
                try
                {
                    await SetClipboardWithRetryAsync(input);
                    DebugLog.Write("restored original clipboard content after failed write");
                }
                catch (Exception ex2)
                {
                    DebugLog.Write($"restore also failed: {ex2.GetType().Name}: {ex2.Message}");
                }
                UpdateHeroStatus("写入剪贴板失败", "#EF4444");
                UpdateStatus($"写剪贴板失败: {Truncate(ex.Message, 120)}（已记入历史，原内容已恢复）");
                if (manual) Notify("无法写入剪贴板", Truncate(ex.Message, 200), Forms.ToolTipIcon.Error);
            }
        }
        finally
        {
            elapsedTimer.Stop();
            _converting = false;
        }
    }

    private static Task SetClipboardWithRetryAsync(string text)
    {
        // 3 attempts on a background STA thread, no outer timeout. Each attempt
        // is bounded by Win32 OpenClipboard's own behavior (~tens of ms typical).
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new System.Threading.Thread(() =>
        {
            Exception? last = null;
            for (int i = 0; i < 3; i++)
            {
                DebugLog.Write($"clipboard write attempt {i + 1}/3");
                try
                {
                    if (Win32Clipboard.TrySetText(text))
                    {
                        DebugLog.Write($"clipboard write attempt {i + 1} succeeded");
                        tcs.TrySetResult(true);
                        return;
                    }
                    DebugLog.Write($"clipboard write attempt {i + 1} returned false (likely contended)");
                }
                catch (Exception ex)
                {
                    last = ex;
                    DebugLog.Write($"clipboard write attempt {i + 1} threw {ex.GetType().Name}: {ex.Message}");
                }
                if (i < 2) System.Threading.Thread.Sleep(80);
            }
            tcs.TrySetException(last ?? new InvalidOperationException("3 次写入剪贴板均失败"));
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    /// <summary>
    /// Local pre-filter: skip the LLM round-trip when the input has zero
    /// math signals. Conservative — any signal at all and we still send it.
    /// </summary>
    private static bool LooksLikeMath(string s)
    {
        if (s.Length < 2) return false;
        if (s.Contains('\\')) return true; // already-LaTeX

        const string mathSignals =
            "+-*/=<>^_{}[]" +
            "∑∫∏√∇∂∞→⟶⊤⊥⊕⊗⋅·∈∉⊂⊃⊆⊇∪∩∅" +
            "≤≥≠≈≡±÷×↦⟨⟩‖∥⌊⌋⌈⌉" +
            "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ" +
            "αβγδεζηθικλμνξοπρστυφχψω";

        foreach (var ch in s)
        {
            if (char.IsDigit(ch)) return true;
            if (mathSignals.IndexOf(ch) >= 0) return true;
        }
        return false;
    }

    private static string StripCodeFences(string s)
    {
        s = s.Trim();
        if (s.StartsWith("```"))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline > 0) s = s[(firstNewline + 1)..];
            if (s.EndsWith("```")) s = s[..^3];
            s = s.Trim();
        }
        return s;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private void Notify(string title, string message, Forms.ToolTipIcon icon)
    {
        if (Application.Current is App app) app.ShowTrayBalloon(title, message, icon);
    }

    // ===== Tray behavior =====

    private void Window_StateChanged(object sender, EventArgs e)
    {
        // No-op: let minimize behave as standard Windows minimize. Hide-to-tray
        // happens only on Close (X) or tray menu Exit.
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (Application.Current is App app && !app.ShuttingDown)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    public void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }
}
