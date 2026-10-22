using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FSIdentity.Domain.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NotWhileImpersonatingAttribute : Attribute;

public sealed class ImpersonationGate : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.ImpersonatorId() is null)
            return Task.CompletedTask;

        return context.ActionDescriptor.EndpointMetadata.OfType<NotWhileImpersonatingAttribute>().Any()
            ? throw new ForbiddenException("This is not allowed while acting as another user")
            : Task.CompletedTask;
    }
}
