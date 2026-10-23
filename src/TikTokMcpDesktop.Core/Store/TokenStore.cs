using TikTokMcpDesktop.Core.Config;

namespace TikTokMcpDesktop.Core.Store;

public sealed class TokenStore
{
    private readonly SecretStore _secrets;

    public TokenStore(SecretStore secrets)
    {
        _secrets = secrets;
    }

    private static string Key(McpVariant variant) => variant.ToString().ToLowerInvariant();

    public OAuthTokenSet? Get(McpVariant variant)
    {
        var secrets = _secrets.Load();
        return secrets.TokensByVariant.TryGetValue(Key(variant), out var token) ? token : null;
    }

    public void Save(McpVariant variant, OAuthTokenSet token)
    {
        _secrets.Update(p => p.TokensByVariant[Key(variant)] = token);
    }

    public void Clear(McpVariant variant)
    {
        _secrets.Update(p => p.TokensByVariant.Remove(Key(variant)));
    }

    public bool IsExpiringSoon(McpVariant variant, TimeSpan skew)
    {
        var token = Get(variant);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            return true;
        return token.ExpiresAt <= DateTimeOffset.UtcNow + skew;
    }

    public DynamicClientRegistration? GetDynamicClient(McpVariant variant)
    {
        var secrets = _secrets.Load();
        return secrets.DynamicClientsByVariant.TryGetValue(Key(variant), out var c) ? c : null;
    }

    public void SaveDynamicClient(McpVariant variant, DynamicClientRegistration client)
    {
        _secrets.Update(p => p.DynamicClientsByVariant[Key(variant)] = client);
    }

    public void ClearDynamicClient(McpVariant variant)
    {
        _secrets.Update(p => p.DynamicClientsByVariant.Remove(Key(variant)));
    }

    /// <summary>Configured app id/client id wins; otherwise reuse DCR-issued client_id for this variant.</summary>
    public string? ResolveClientId(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.EffectiveClientId))
            return settings.EffectiveClientId;
        var dcr = GetDynamicClient(settings.McpVariant);
        return string.IsNullOrWhiteSpace(dcr?.ClientId) ? null : dcr!.ClientId;
    }
}
