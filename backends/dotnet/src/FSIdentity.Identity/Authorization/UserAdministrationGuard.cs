using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Authorization;

public class CallerAccess(AppDbContext db, IHttpContextAccessor http) : ICallerAccess
{
    private UserType? _rank;

    public async Task<UserType> RankAsync(CancellationToken cancellationToken = default)
    {
        if (_rank is { } known)
            return known;

        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return (_rank = UserType.Reader).Value;
        if (principal.IsSuperAdmin())
            return (_rank = UserType.PlatformAdmin).Value;

        UserType? membership = null;
        if (principal.UserId() is { } userId && principal.ActiveCompanyId() is { } companyId)
        {
            membership = await db.UserCompanies.IgnoreQueryFilters([FSDbContext.TenantFilter])
                .Where(m => m.UserId == userId && m.CompanyId == companyId && m.IsActive)
                .Select(m => (UserType?)m.MembershipType)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return (_rank = membership ?? UserType.Reader).Value;
    }
}

public interface IUserAdministrationGuard
{
    Task EnsureCanAdministerAsync(long targetUserId, bool allowSelf, CancellationToken cancellationToken = default);

    Task EnsureCanGrantAsync(UserType membershipType, CancellationToken cancellationToken = default);
}

public class UserAdministrationGuard(AppDbContext db, ICallerAccess caller, IMultiTenantAccessor current) : IUserAdministrationGuard
{
    public async Task EnsureCanAdministerAsync(long targetUserId, bool allowSelf, CancellationToken cancellationToken = default)
    {
        var callerRank = await caller.RankAsync(cancellationToken);
        if (callerRank == UserType.PlatformAdmin)
            return;
        if (allowSelf && current.UserId == targetUserId)
            return;

        var targetRank = await RankOfAsync(targetUserId, cancellationToken);
        if (targetRank >= callerRank)
            throw new ForbiddenException("You can only manage people who rank below you");
    }

    public async Task EnsureCanGrantAsync(UserType membershipType, CancellationToken cancellationToken = default)
    {
        if (membershipType == UserType.PlatformAdmin)
            throw new ForbiddenException("Platform administrator is not a membership");

        var callerRank = await caller.RankAsync(cancellationToken);
        if (callerRank == UserType.PlatformAdmin)
            return;

        if (membershipType >= callerRank)
            throw new ForbiddenException("You can only give a rank below your own");
    }

    private async Task<UserType> RankOfAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId).Select(u => new { u.IsPlatformAdmin }).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            throw new NotFoundException($"User {userId} not found");
        if (user.IsPlatformAdmin)
            return UserType.PlatformAdmin;

        var highest = await db.UserCompanies.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(m => m.UserId == userId && m.IsActive)
            .MaxAsync(m => (UserType?)m.MembershipType, cancellationToken);

        return highest ?? UserType.Standard;
    }
}
