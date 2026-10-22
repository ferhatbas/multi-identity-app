using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Domain.Security;

public interface IPermissionEvaluator
{
    Task<bool> HasAsync(string permissionCode, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetAllAsync(CancellationToken cancellationToken = default);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permissionCode) : Attribute, IAsyncAuthorizationFilter
{
    public string PermissionCode { get; } = permissionCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        await PermissionCheck.RequireAsync(context.HttpContext, PermissionCode);
    }
}

public static class PermissionCheck
{
    public static async Task RequireAsync(Microsoft.AspNetCore.Http.HttpContext http, string permissionCode)
    {
        var evaluator = http.RequestServices.GetRequiredService<IPermissionEvaluator>();
        if (!await evaluator.HasAsync(permissionCode, http.RequestAborted))
            throw new PermissionDeniedException($"The '{permissionCode}' permission is required");
    }
}
