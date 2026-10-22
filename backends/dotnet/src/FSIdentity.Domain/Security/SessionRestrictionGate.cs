using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FSIdentity.Domain.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AccountSelfServiceAttribute : Attribute;

public sealed class SessionRestrictionGate : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (!user.MustChangePassword() && !user.MfaSetupRequired() && !user.EmailConfirmationRequired())
            return Task.CompletedTask;

        if (context.ActionDescriptor.EndpointMetadata.OfType<AccountSelfServiceAttribute>().Any())
            return Task.CompletedTask;

        if (user.MustChangePassword())
            throw new PasswordChangeRequiredException("The password must be changed before anything else");
        if (user.MfaSetupRequired())
            throw new MfaSetupRequiredException("Two-factor authentication must be set up before anything else");
        throw new EmailConfirmationRequiredException("The e-mail address must be confirmed before anything else");
    }
}
