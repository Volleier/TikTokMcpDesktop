using System.Text.Json;
using System.Text.Json.Serialization;
using TikTokMcpDesktop.Core.Config;

namespace TikTokMcpDesktop.Core.Store;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly SecretStore _secrets;
    private readonly object _lock = new();

    public ConfigStore(AppPaths paths, SecretStore secrets)
    {
        _path = paths.SettingsPath;
        _secrets = secrets;
    }

    public AppSettings Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return new AppSettings();
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            // App secret lives in SecretStore, not plain settings.json
            settings.AppSecret = _secrets.Load().TikTokAppSecret ?? settings.AppSecret;
            return settings;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            // Persist non-secret settings without AppSecret plaintext
            var toWrite = new AppSettings
            {
                OpenAiBaseUrl = settings.OpenAiBaseUrl,
                OpenAiModel = settings.OpenAiModel,
                ClientId = settings.ClientId,
                AppId = settings.AppId,
                AppSecret = "",
                McpVariant = settings.McpVariant,
                TokenRequestFormat = settings.TokenRequestFormat,
                SyncIntervalMinutes = settings.SyncIntervalMinutes,
                AutoSyncEnabled = settings.AutoSyncEnabled,
                ScheduleMode = settings.ScheduleMode,
                DailyAtLocalTime = settings.DailyAtLocalTime,
                OAuthCallbackPort = settings.OAuthCallbackPort,
                AuthorizeUrlOverride = settings.AuthorizeUrlOverride,
                McpUrlOverride = settings.McpUrlOverride
            };
            var json = JsonSerializer.Serialize(toWrite, JsonOptions);
            File.WriteAllText(_path, json);
        }
    }

    public string? GetApiKey() => _secrets.Load().OpenAiApiKey;

    public void SetApiKey(string? apiKey)
    {
        _secrets.Update(p => p.OpenAiApiKey = apiKey);
    }

    public void SetAppSecret(string? secret)
    {
        _secrets.Update(p => p.TikTokAppSecret = string.IsNullOrWhiteSpace(secret) ? null : secret);
    }
}
