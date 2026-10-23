using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.OAuth;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.Tests;

public class OAuthContractTests
{
    [Fact]
    public void EffectiveClientId_Prefers_AppId()
    {
        var s = new AppSettings { ClientId = "cid", AppId = "aid" };
        Assert.Equal("aid", s.EffectiveClientId);
        Assert.Equal(TikTokEndpoints.LayerMcpUrl, s.GetMcpUrl());
        Assert.Equal($"{TikTokEndpoints.LayerMcpUrl}/oauth/token", s.GetTokenUrl());
        Assert.Equal($"{TikTokEndpoints.LayerMcpUrl}/oauth/register", TikTokEndpoints.GetRegisterUrl(McpVariant.Layer));
    }

    [Fact]
    public void ResolveClientId_Falls_Back_To_Dcr_When_Settings_Empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "ttmcp-dcr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new AppPaths(root);
            var secrets = new SecretStore(paths);
            var tokens = new TokenStore(secrets);
            var settings = new AppSettings { ClientId = "", AppId = "" };
            Assert.Null(tokens.ResolveClientId(settings));

            tokens.SaveDynamicClient(McpVariant.Layer, new DynamicClientRegistration
            {
                ClientId = "dcr-client-xyz",
                RedirectUri = "http://127.0.0.1:17865/callback"
            });
            Assert.Equal("dcr-client-xyz", tokens.ResolveClientId(settings));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ParseTokenResponse_Reads_AdvertiserIds_And_Wrapped_Data()
    {
        var token = OAuthClient.ParseTokenResponse("""
            {"data":{"access_token":"a1","refresh_token":"r1","expires_in":7200,"advertiser_ids":["111","222"]}}
            """);
        Assert.Equal("a1", token.AccessToken);
        Assert.Equal("r1", token.RefreshToken);
        Assert.Equal(new[] { "111", "222" }, token.AdvertiserIds);
    }

    [Fact]
    public void Pkce_Uses_32_Byte_Verifier_Like_AIEcomPlex()
    {
        var v = Pkce.CreateVerifier(32);
        Assert.DoesNotContain("+", v);
        Assert.DoesNotContain("/", v);
        // 32 bytes → ~43 chars base64url
        Assert.InRange(v.Length, 40, 50);
    }
}
