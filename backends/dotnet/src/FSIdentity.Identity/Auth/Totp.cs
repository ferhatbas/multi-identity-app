using System.Security.Cryptography;
using System.Text;

namespace FSIdentity.Identity.Auth;

public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;

    public static string NewSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(20));

    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    public static string Code(string secret, long step)
    {
        var counter = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(Base32.Decode(secret), counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    public static long? Verify(string secret, string code, DateTimeOffset now, long? lastUsedStep)
    {
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
            return null;

        var current = StepAt(now);
        for (var step = current - 1; step <= current + 1; step++)
        {
            if (lastUsedStep is { } last && step <= last)
                continue;

            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(secret, step)), Encoding.ASCII.GetBytes(code)))
                return step;
        }

        return null;
    }

    public static string Uri(string issuer, string account, string secret)
        => $"otpauth://totp/{System.Uri.EscapeDataString(issuer)}:{System.Uri.EscapeDataString(account)}" +
           $"?secret={secret}&issuer={System.Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
            result.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return result.ToString();
    }

    public static byte[] Decode(string text)
    {
        var bytes = new List<byte>(text.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in text.TrimEnd('=').ToUpperInvariant())
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0)
                throw new FormatException("Not a base32 character");

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
