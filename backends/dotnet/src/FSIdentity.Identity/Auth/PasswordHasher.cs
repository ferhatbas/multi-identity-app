using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Security;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Auth;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);

    void BurnTime();

    void EnsureAcceptable(string password, EffectivePolicy? policy = null);
}

public class PasswordHasher(IOptions<PasswordOptions> options) : IPasswordHasher
{
    private readonly Lazy<string> _dummy = new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), options.Value.WorkFactor));

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, options.Value.WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
        {
            return false;
        }
    }

    public void BurnTime() => BCrypt.Net.BCrypt.Verify("not-the-password", _dummy.Value);

    public void EnsureAcceptable(string password, EffectivePolicy? policy = null)
    {
        var minLength = policy?.MinPasswordLength ?? options.Value.MinLength;
        var problems = new List<string>();
        if (password.Length < minLength)
            problems.Add($"at least {minLength} characters");
        if ((policy?.PasswordRequiresLetter ?? true) && !password.Any(char.IsLetter))
            problems.Add("a letter");
        if ((policy?.PasswordRequiresDigit ?? true) && !password.Any(char.IsDigit))
            problems.Add("a digit");
        if (policy?.PasswordRequiresSymbol == true && !password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            problems.Add("a symbol");
        if (policy?.PasswordRequiresMixedCase == true && !(password.Any(char.IsUpper) && password.Any(char.IsLower)))
            problems.Add("an upper-case and a lower-case letter");

        if (problems.Count > 0)
            throw new BadRequestException($"The password needs {string.Join(", ", problems)}");
    }
}
