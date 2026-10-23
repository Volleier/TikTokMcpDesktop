using System.Security.Cryptography;
using System.Text;

namespace TikTokMcpDesktop.Core.OAuth;

public static class Pkce
{
    public static string CreateVerifier(int size = 64)
    {
        var bytes = RandomNumberGenerator.GetBytes(size);
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
