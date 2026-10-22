using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Domain.Security;

public interface ICallerAccess
{
    Task<UserType> RankAsync(CancellationToken cancellationToken = default);
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowReaderAttribute : Attribute;

public sealed class ReaderGuard : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true || http.User.IsSuperAdmin())
            return;

        var method = http.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return;
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowReaderAttribute>().Any())
            return;

        var access = http.RequestServices.GetRequiredService<ICallerAccess>();
        if (await access.RankAsync(http.RequestAborted) == UserType.Reader)
            throw new ReaderReadOnlyException("A read-only membership cannot change anything");
    }
}
