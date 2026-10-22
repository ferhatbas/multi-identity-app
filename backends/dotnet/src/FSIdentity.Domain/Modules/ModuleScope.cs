using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Domain.Modules;

public class ModuleScopeOptions
{
    public const string SectionName = "Modules";

    public bool StrictMode { get; set; }

    public int CacheSeconds { get; set; } = 60;
}

public interface IModuleScopeAccessor
{
    Task<HashSet<string>> GetGrantedCodesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsGrantedAsync(string moduleCode, CancellationToken cancellationToken = default);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireModuleAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequireModuleAttribute(params string[] moduleCodes)
    {
        if (moduleCodes.Length == 0)
            throw new ArgumentException("At least one module code is required.", nameof(moduleCodes));

        ModuleCodes = moduleCodes;
    }

    public IReadOnlyList<string> ModuleCodes { get; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        var accessor = context.HttpContext.RequestServices.GetService<IModuleScopeAccessor>();
        if (accessor is null)
            return;

        foreach (var code in ModuleCodes)
            if (await accessor.IsGrantedAsync(code, context.HttpContext.RequestAborted))
                return;

        throw new ModuleNotGrantedException(
            $"The active application does not grant the '{string.Join("' or '", ModuleCodes)}' module. " +
            "Switch to an application that includes it.");
    }
}