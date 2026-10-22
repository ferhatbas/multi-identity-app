using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Auth;

public class SecurityOptions
{
    public const string SectionName = "Security";

    public string? DataKey { get; set; }
}

public interface ISecretProtector
{
    string Protect(string plain);

    string Unprotect(string protectedText);
}

public class SecretProtector : ISecretProtector
{
    private const string Prefix = "v1:";
    private readonly byte[] _key;

    public SecretProtector(IOptions<SecurityOptions> security, IOptions<JwtOptions> jwt)
    {
        var material = string.IsNullOrEmpty(security.Value.DataKey) ? jwt.Value.Key : security.Value.DataKey;
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(material), 32, info: "fsidentity-secrets-v1"u8.ToArray());
    }

    public string Protect(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, data, cipher, tag);

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string protectedText)
    {
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("Not a protected secret");

        var all = Convert.FromBase64String(protectedText[Prefix.Length..]);
        var nonce = all[..12];
        var tag = all[12..28];
        var cipher = all[28..];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
