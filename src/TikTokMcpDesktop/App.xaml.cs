using System.Windows;
using TikTokMcpDesktop.Core;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.LocalApi;

namespace TikTokMcpDesktop;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;
    public static LocalApiHost LocalApi { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            Services = new AppServices();
            LocalApi = new LocalApiHost();
            await LocalApi.StartAsync(Services);
            Services.Scheduler.Start();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Startup failed: {ex}");
            MessageBox.Show($"启动失败: {ex.Message}", "TikTok MCP Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (LocalApi is not null)
                await LocalApi.DisposeAsync();
            if (Services is not null)
                await Services.DisposeAsync();
        }
        catch { /* ignore */ }
        base.OnExit(e);
    }
}
