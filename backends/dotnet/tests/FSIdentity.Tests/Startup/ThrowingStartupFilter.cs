using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace FSIdentity.Tests.Startup;

internal sealed class ThrowingStartupFilter(Exception exception) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            context.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("UserId", "1")], "test"));
            await nextMiddleware();
        });

        next(app);
        app.Run(_ => throw exception);
    };
}