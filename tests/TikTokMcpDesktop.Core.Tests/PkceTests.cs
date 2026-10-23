using TikTokMcpDesktop.Core.OAuth;

namespace TikTokMcpDesktop.Core.Tests;

public class PkceTests
{
    [Fact]
    public void Challenge_Is_S256_Base64Url()
    {
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = Pkce.CreateChallenge(verifier);
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void CreateVerifier_Is_UrlSafe()
    {
        var v = Pkce.CreateVerifier();
        Assert.DoesNotContain("+", v);
        Assert.DoesNotContain("/", v);
        Assert.DoesNotContain("=", v);
        Assert.True(v.Length >= 43);
    }
}
