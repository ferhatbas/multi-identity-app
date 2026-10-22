using System.Security.Cryptography;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Identity.Auth;
using FSIdentity.Identity.Security;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Users;

public interface IUserProvisioning
{
    Task<long> ResolveTenantAsync(long? requestedTenantId, CancellationToken cancellationToken);

    Task<User> NewUserAsync(UserProfile profile, long tenantId, string password, bool mustChangePassword, CancellationToken cancellationToken);

    string GenerateTemporaryPassword();
}

public class UserProvisioning(
    IUnitOfWork uow, IMultiTenantAccessor tenant, IPasswordHasher hasher, ISecurityPolicyService policies, TimeProvider clock)
    : IUserProvisioning
{
    private const string Letters = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+?";

    public async Task<long> ResolveTenantAsync(long? requestedTenantId, CancellationToken cancellationToken)
    {
        var active = tenant.TenantId;

        if (active is not null && requestedTenantId is not null && requestedTenantId != active)
            throw new ForbiddenException("A user can only be created inside your own tenant");

        var tenantId = active ?? requestedTenantId
            ?? throw new BadRequestException("tenantId is required when no tenant is active");

        var exists = await uow.Repository<Tenant>().Query().AnyAsync(t => t.Id == tenantId, cancellationToken);
        return exists ? tenantId : throw new BadRequestException($"Tenant {tenantId} does not exist");
    }

    public async Task<User> NewUserAsync(
        UserProfile profile, long tenantId, string password, bool mustChangePassword, CancellationToken cancellationToken)
    {
        hasher.EnsureAcceptable(password, await policies.ForTenantAsync(tenantId, cancellationToken));

        return new User
        {
            UserName = profile.UserName.Trim().ToLowerInvariant(),
            Email = profile.Email.Trim().ToLowerInvariant(),
            FirstName = profile.FirstName.Trim(),
            LastName = profile.LastName.Trim(),
            PhoneNumber = profile.PhoneNumber?.Trim(),
            LanguageCode = profile.LanguageCode?.Trim().ToLowerInvariant(),
            IdentityTenantId = tenantId,
            PasswordHash = hasher.Hash(password),
            LastPasswordChangeAt = clock.GetUtcNow().UtcDateTime,
            ShouldChangePasswordOnNextLogin = mustChangePassword,
        };
    }

    public string GenerateTemporaryPassword()
    {
        var chars = new List<char>
        {
            Pick(Letters.ToLowerInvariant()), Pick(Letters.ToUpperInvariant()), Pick(Digits), Pick(Symbols),
        };
        while (chars.Count < 20)
            chars.Add(Pick(Letters + Digits));

        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    private static char Pick(string from) => from[RandomNumberGenerator.GetInt32(from.Length)];
}
