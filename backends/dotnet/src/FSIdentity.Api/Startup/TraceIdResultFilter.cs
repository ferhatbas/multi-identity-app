using FSIdentity.Domain.Common;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FSIdentity.Api.Startup;

public class TraceIdResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is Microsoft.AspNetCore.Mvc.ObjectResult { Value: IApiResponse response }
            && response.TraceId is null)
        {
            response.TraceId = context.HttpContext.TraceIdentifier;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}