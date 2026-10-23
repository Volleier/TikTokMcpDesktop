using System.Security.Cryptography;
using System.Text;

namespace TikTokMcpDesktop.Core.OAuth;

/// <summary>PKCE helpers aligned with AIEcomPlex TikTokAdsOAuthService (32-byte verifier, S256).</summary>
public static class Pkce
{
    /// <param name="byteLength">AIEcomPlex uses 32 random bytes → URL-safe Base64 verifier.</param>
    public static string CreateVerifier(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64Url(bytes);
    }

    public static string CreateChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(24));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
