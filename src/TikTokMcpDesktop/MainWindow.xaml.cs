using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Jobs;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Mcp;
using TikTokMcpDesktop.Core.OAuth;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop;

public partial class MainWindow : Window
{
    public string LocalApiStatus { get; private set; } = "";

    private readonly ObservableCollection<ChatRow> _chatRows = new();
    private string _webRoute = "/";
    private DispatcherTimer? _scheduleUiTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LstChat.ItemsSource = _chatRows;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) =>
        {
            App.Services.Scheduler.StatusChanged -= OnSchedulerStatus;
            AppLog.OnEntry -= OnLog;
            _scheduleUiTimer?.Stop();
        };
        AppLog.OnEntry += OnLog;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LocalApiStatus = $"LocalApi: {App.LocalApi.BaseUrl}";
        LoadSettingsToUi();
        RefreshAuthStatus();
        RefreshSyncUi();
        App.Services.Scheduler.StatusChanged += OnSchedulerStatus;
        _scheduleUiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _scheduleUiTimer.Tick += (_, _) => RefreshScheduleStatus();
        _scheduleUiTimer.Start();
        RefreshScheduleStatus();
        try
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        }
        catch (Exception ex)
        {
            TxtWebHint.Text = $"WebView2 不可用: {ex.Message}。请安装 Microsoft Edge WebView2 Runtime。";
            AppLog.Warn(TxtWebHint.Text);
        }
    }

    private void OnSchedulerStatus(SchedulerStatus _) =>
        Dispatcher.Invoke(RefreshScheduleStatus);

    private void OnLog(LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            if (PageSync.Visibility == Visibility.Visible)
                RefreshSyncUi();
        });
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        PageSettings.Visibility = Visibility.Collapsed;
        PageConnect.Visibility = Visibility.Collapsed;
        PageSync.Visibility = Visibility.Collapsed;
        PageChat.Visibility = Visibility.Collapsed;
        PageWeb.Visibility = Visibility.Collapsed;

        switch (tag)
        {
            case "settings":
                PageSettings.Visibility = Visibility.Visible;
                LoadSettingsToUi();
                break;
            case "connect":
                PageConnect.Visibility = Visibility.Visible;
                RefreshAuthStatus();
                break;
            case "sync":
                PageSync.Visibility = Visibility.Visible;
                RefreshSyncUi();
                break;
            case "chat":
                PageChat.Visibility = Visibility.Visible;
                break;
            case "table":
                PageWeb.Visibility = Visibility.Visible;
                _webRoute = "/#/tables";
                NavigateWeb();
                break;
            case "dashboard":
                PageWeb.Visibility = Visibility.Visible;
                _webRoute = "/#/dashboards";
                NavigateWeb();
                break;
        }
    }

    private async void NavigateWeb()
    {
        try
        {
            if (WebView.CoreWebView2 is null)
                await WebView.EnsureCoreWebView2Async();
            WebView.Source = new Uri($"{App.LocalApi.BaseUrl}{_webRoute}");
            TxtWebHint.Text = $"嵌入页: {App.LocalApi.BaseUrl}{_webRoute}";
        }
        catch (Exception ex)
        {
            TxtWebHint.Text = $"无法打开嵌入页: {ex.Message}";
        }
    }

    private void LoadSettingsToUi()
    {
        var s = App.Services.Config.Load();
        TxtBaseUrl.Text = s.OpenAiBaseUrl;
        TxtModel.Text = s.OpenAiModel;
        TxtClientId.Text = s.ClientId;
        TxtAppId.Text = s.AppId;
        TxtAppSecret.Password = s.AppSecret ?? "";
        TxtSyncInterval.Text = s.SyncIntervalMinutes.ToString();
        TxtDailyAt.Text = s.DailyAtLocalTime;
        ChkAutoSync.IsChecked = s.AutoSyncEnabled;
        TxtApiKey.Password = App.Services.Config.GetApiKey() ?? "";
        SelectCombo(CmbVariant, s.McpVariant.ToString());
        SelectCombo(CmbTokenFormat, s.TokenRequestFormat.ToString());
        SelectCombo(CmbScheduleMode, s.ScheduleMode.ToString());
        UpdateScheduleModePanels();
    }

    private void ScheduleMode_Changed(object sender, SelectionChangedEventArgs e) =>
        UpdateScheduleModePanels();

    private void UpdateScheduleModePanels()
    {
        if (PanelInterval is null || PanelDailyAt is null) return;
        var daily = CmbScheduleMode.SelectedItem is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), "DailyAt", StringComparison.OrdinalIgnoreCase);
        PanelInterval.Visibility = daily ? Visibility.Collapsed : Visibility.Visible;
        PanelDailyAt.Visibility = daily ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SelectCombo(ComboBox cmb, string tag)
    {
        foreach (ComboBoxItem item in cmb.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                cmb.SelectedItem = item;
                return;
            }
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var s = App.Services.Config.Load();
            var previousVariant = s.McpVariant;
            s.OpenAiBaseUrl = TxtBaseUrl.Text.Trim();
            s.OpenAiModel = TxtModel.Text.Trim();
            s.ClientId = TxtClientId.Text.Trim();
            s.AppId = TxtAppId.Text.Trim();
            s.SyncIntervalMinutes = int.TryParse(TxtSyncInterval.Text.Trim(), out var m) ? Math.Clamp(m, 5, 1440) : 60;
            s.AutoSyncEnabled = ChkAutoSync.IsChecked == true;
            if (CmbScheduleMode.SelectedItem is ComboBoxItem sm)
                s.ScheduleMode = Enum.Parse<ScheduleMode>(sm.Tag!.ToString()!);
            if (s.ScheduleMode == ScheduleMode.DailyAt &&
                !string.IsNullOrWhiteSpace(TxtDailyAt.Text) &&
                !SchedulePlanner.TryParseDailyAt(TxtDailyAt.Text, out _, out _))
            {
                TxtSettingsStatus.Text = "每日时间格式无效，请使用 HH:mm（如 09:30）。";
                return;
            }
            s.DailyAtLocalTime = SchedulePlanner.NormalizeDailyAt(TxtDailyAt.Text);
            TxtDailyAt.Text = s.DailyAtLocalTime;
            if (CmbVariant.SelectedItem is ComboBoxItem v)
                s.McpVariant = Enum.Parse<McpVariant>(v.Tag!.ToString()!);
            if (CmbTokenFormat.SelectedItem is ComboBoxItem f)
                s.TokenRequestFormat = Enum.Parse<TokenRequestFormat>(f.Tag!.ToString()!);

            App.Services.Config.Save(s);
            App.Services.Config.SetApiKey(string.IsNullOrWhiteSpace(TxtApiKey.Password) ? null : TxtApiKey.Password);
            App.Services.Config.SetAppSecret(string.IsNullOrWhiteSpace(TxtAppSecret.Password) ? null : TxtAppSecret.Password);
            App.Services.Scheduler.Restart();
            RefreshScheduleStatus();
            if (previousVariant != s.McpVariant)
                TxtSettingsStatus.Text = "已保存。MCP 变体已切换，请重新授权。调度已按新设置重启。";
            else
                TxtSettingsStatus.Text = "已保存；定时调度已按新设置生效。";
        }
        catch (Exception ex)
        {
            TxtSettingsStatus.Text = $"保存失败: {ex.Message}";
        }
    }

    private void RefreshAuthStatus()
    {
        var s = App.Services.Config.Load();
        var token = App.Services.Tokens.Get(s.McpVariant);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            TxtAuthStatus.Text = "未连接";
            TxtAuthStatus.Foreground = System.Windows.Media.Brushes.DarkRed;
            TxtTokenExpiry.Text = "点击「授权登录」即可（可无 client_id，将自动动态注册客户端）。";
        }
        else if (token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            TxtAuthStatus.Text = "令牌可能已过期（将尝试 refresh）";
            TxtAuthStatus.Foreground = System.Windows.Media.Brushes.DarkOrange;
            TxtTokenExpiry.Text = $"Access 过期时间(UTC): {token.ExpiresAt:u}";
        }
        else
        {
            TxtAuthStatus.Text = "已连接";
            TxtAuthStatus.Foreground = System.Windows.Media.Brushes.Teal;
            var ads = token.AdvertiserIds.Count > 0
                ? $" · advertisers: {string.Join(", ", token.AdvertiserIds.Take(5))}"
                : "";
            TxtTokenExpiry.Text = $"变体 {s.McpVariant} · resource={s.GetMcpUrl()} · Access 过期(UTC): {token.ExpiresAt:u}{ads}";
        }
    }

    private async void Authorize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtAuthStatus.Text = "等待浏览器授权…";
            var settings = App.Services.Config.Load();
            await App.Services.OAuth.AuthorizeInteractiveAsync(settings);
            RefreshAuthStatus();
            MessageBox.Show("授权成功。", "TikTok MCP", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            RefreshAuthStatus();
            MessageBox.Show($"授权失败: {ex.Message}", "TikTok MCP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ListTools_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtTools.Text = "加载中…";
            var settings = App.Services.Config.Load();
            var tools = await App.Services.Mcp.ListToolsAsync(settings);
            var allowed = ToolWhitelist.FilterForModel(tools);
            TxtTools.Text = string.Join(Environment.NewLine,
                tools.Select(t =>
                {
                    var flag = ToolWhitelist.IsWriteTool(t.Name) ? "[写/禁用]" : "[只读]";
                    return $"{flag} {t.Name} — {t.Description}";
                }));
            TxtTools.Text += $"{Environment.NewLine}{Environment.NewLine}模型可见: {allowed.Count} / 全部 {tools.Count}";
        }
        catch (ReauthRequiredException ex)
        {
            TxtTools.Text = ex.Message;
            MessageBox.Show(ex.Message, "需要重新授权", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            TxtTools.Text = ex.Message;
        }
    }

    private void RefreshSyncUi()
    {
        var latest = App.Services.Db.GetLatestSync();
        TxtSyncSummary.Text = latest is null
            ? "尚未同步。授权后点击「立即同步」，或开启下方定时调度。"
            : latest.ToString();
        RefreshScheduleStatus();
        LstSyncLogs.Items.Clear();
        foreach (var line in App.Services.Db.GetSyncLogs(200))
            LstSyncLogs.Items.Add(line.ToString());
        if (LstSyncLogs.Items.Count > 0)
            LstSyncLogs.ScrollIntoView(LstSyncLogs.Items[^1]);
    }

    private void RefreshScheduleStatus()
    {
        if (TxtScheduleStatus is null || BtnToggleSchedule is null) return;
        var st = App.Services.Scheduler.GetStatus();
        var running = st.IsJobRunning ? " · 正在执行拉数…" : "";
        TxtScheduleStatus.Text = st.Summary + running;
        BtnToggleSchedule.Content = st.Enabled ? "关闭调度" : "开启调度";
    }

    private void ToggleSchedule_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var s = App.Services.Config.Load();
            s.AutoSyncEnabled = !s.AutoSyncEnabled;
            App.Services.Config.Save(s);
            ChkAutoSync.IsChecked = s.AutoSyncEnabled;
            App.Services.Scheduler.Restart();
            RefreshScheduleStatus();
            TxtSyncSummary.Text = s.AutoSyncEnabled
                ? "已开启定时调度（详见下方状态）。"
                : "已关闭定时调度。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "调度", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtSyncSummary.Text = "同步中…";
            var result = await App.Services.Pull.RunAsync();
            App.Services.Scheduler.MarkRunCompleted();
            RefreshSyncUi();
            MessageBox.Show(result.Message ?? (result.Success ? "完成" : "失败"),
                "同步", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (ReauthRequiredException ex)
        {
            MessageBox.Show(ex.Message, "需要重新授权", MessageBoxButton.OK, MessageBoxImage.Warning);
            ShowPage("connect");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "同步失败", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshSyncUi();
        }
    }

    private async void SendChat_Click(object sender, RoutedEventArgs e) => await SendChatAsync();

    private async void TxtChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await SendChatAsync();
        }
    }

    private async Task SendChatAsync()
    {
        var text = TxtChatInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        TxtChatInput.Text = "";
        _chatRows.Add(new ChatRow("你", text));
        var thinking = new ChatRow("助手", "思考中…");
        _chatRows.Add(thinking);
        try
        {
            var reply = await App.Services.Chat.SendAsync(text);
            thinking.Content = reply;
            LstChat.Items.Refresh();
        }
        catch (ReauthRequiredException ex)
        {
            thinking.Content = ex.Message;
            LstChat.Items.Refresh();
            MessageBox.Show(ex.Message, "需要重新授权", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            thinking.Content = $"错误: {ex.Message}";
            LstChat.Items.Refresh();
        }
        ChatScroll.ScrollToEnd();
    }

    public sealed class ChatRow
    {
        public ChatRow(string role, string content)
        {
            Role = role;
            Content = content;
        }
        public string Role { get; }
        public string Content { get; set; }
    }
}
