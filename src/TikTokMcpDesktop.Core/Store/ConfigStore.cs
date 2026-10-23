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
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_path, json);
        }
    }

    public string? GetApiKey() => _secrets.Load().OpenAiApiKey;

    public void SetApiKey(string? apiKey)
    {
        _secrets.Update(p => p.OpenAiApiKey = apiKey);
    }
}
