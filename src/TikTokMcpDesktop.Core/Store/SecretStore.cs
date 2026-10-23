using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TikTokMcpDesktop.Core.Store;

public sealed class SecretsPayload
{
    public string? OpenAiApiKey { get; set; }
    /// <summary>Optional app secret for token JSON field "secret" (rarely needed for Open MCP PKCE).</summary>
    public string? TikTokAppSecret { get; set; }
    public Dictionary<string, OAuthTokenSet> TokensByVariant { get; set; } = new();
    /// <summary>Per-variant client_id from Dynamic Client Registration (no preconfigured app required).</summary>
    public Dictionary<string, DynamicClientRegistration> DynamicClientsByVariant { get; set; } = new();
}

public sealed class DynamicClientRegistration
{
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OAuthTokenSet
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public string? TokenType { get; set; }
    public string? Scope { get; set; }
    /// <summary>From token response advertiser_ids when present (AIEcomPlex parity).</summary>
    public List<string> AdvertiserIds { get; set; } = new();
}

/// <summary>
/// Encrypts secrets with Windows DPAPI when available; falls back to AES keyed by machine+user on other OS (dev/test only).
/// </summary>
public sealed class SecretStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public SecretStore(AppPaths paths)
    {
        _path = paths.SecretsPath;
    }

    public SecretsPayload Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return new SecretsPayload();

            var blob = File.ReadAllBytes(_path);
            var json = Encoding.UTF8.GetString(Unprotect(blob));
            return JsonSerializer.Deserialize<SecretsPayload>(json) ?? new SecretsPayload();
        }
    }

    public void Save(SecretsPayload payload)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(payload);
            var blob = Protect(Encoding.UTF8.GetBytes(json));
            File.WriteAllBytes(_path, blob);
        }
    }

    public void Update(Action<SecretsPayload> mutate)
    {
        lock (_lock)
        {
            var payload = LoadUnlocked();
            mutate(payload);
            var json = JsonSerializer.Serialize(payload);
            File.WriteAllBytes(_path, Protect(Encoding.UTF8.GetBytes(json)));
        }
    }

    private SecretsPayload LoadUnlocked()
    {
        if (!File.Exists(_path))
            return new SecretsPayload();
        var blob = File.ReadAllBytes(_path);
        var json = Encoding.UTF8.GetString(Unprotect(blob));
        return JsonSerializer.Deserialize<SecretsPayload>(json) ?? new SecretsPayload();
    }

    private static byte[] Protect(byte[] data)
    {
        if (OperatingSystem.IsWindows())
            return ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);

        return AesProtect(data);
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (OperatingSystem.IsWindows())
            return ProtectedData.Unprotect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);

        return AesUnprotect(data);
    }

    private static byte[] DeriveKey()
    {
        var material = Encoding.UTF8.GetBytes(
            $"TikTokMcpDesktop|{Environment.UserName}|{Environment.MachineName}|v1");
        return SHA256.HashData(material);
    }

    private static byte[] AesProtect(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
        var output = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, output, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, output, aes.IV.Length, cipher.Length);
        return output;
    }

    private static byte[] AesUnprotect(byte[] data)
    {
        if (data.Length <= 16)
            throw new CryptographicException("Secrets blob is truncated or corrupt.");
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 16, data.Length - 16);
    }
}
