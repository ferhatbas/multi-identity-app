using FSIdentity.Domain.Common;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Interfaces;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Domain.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public abstract class CrudController<TEntity, TRead, TCreate, TUpdate>(
    ICrudService<TEntity, TRead, TCreate, TUpdate> service,
    string entityName,
    string entityNamePlural) : ControllerBase, IAsyncActionFilter
    where TEntity : class, IBaseEntityRoot, new()
{
    protected virtual string? ReadPermission => null;

    protected abstract string WritePermission { get; }

    async Task IAsyncActionFilter.OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var required = HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method)
            ? ReadPermission
            : WritePermission;

        if (required is not null)
            await PermissionCheck.RequireAsync(context.HttpContext, required);

        await next();
    }

    [HttpGet]
    public virtual async Task<ActionResult<ApiResponse<IReadOnlyList<TRead>>>> GetAll(CancellationToken cancellationToken)
    {
        var rows = await service.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TRead>>.Success(rows,
            rows.Count > 0 ? $"{entityNamePlural} retrieved successfully" : $"No {entityNamePlural} found"));
    }

    [HttpGet("paged")]
    public virtual async Task<ActionResult<ApiResponse<PaginatedResult<TRead>>>> GetPaged(
        [FromQuery] PageQuery page, CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(page, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<TRead>>.Success(result,
            result.Data.Count > 0 ? $"{entityNamePlural} page retrieved successfully" : $"No {entityNamePlural} found"));
    }

    [HttpGet("deleted")]
    public virtual async Task<ActionResult<ApiResponse<IReadOnlyList<TRead>>>> GetDeleted(CancellationToken cancellationToken)
    {
        var rows = await service.GetDeletedAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TRead>>.Success(rows, $"{entityNamePlural} (deleted)"));
    }

    [HttpGet("{id:long}")]
    public virtual async Task<ActionResult<ApiResponse<TRead>>> GetById(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<TRead>.Success(
            await service.GetByIdAsync(id, cancellationToken), $"{entityName} retrieved successfully"));

    [HttpPost]
    public virtual async Task<ActionResult<ApiResponse<TRead>>> Create(
        [FromBody] TCreate body, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<TRead>.Created(created, $"{entityName} created successfully"));
    }

    [HttpPut("{id:long}")]
    public virtual async Task<ActionResult<ApiResponse<TRead>>> Update(
        long id, [FromBody] TUpdate body, CancellationToken cancellationToken)
        => Ok(ApiResponse<TRead>.Success(
            await service.UpdateAsync(id, body, cancellationToken), $"{entityName} updated successfully"));

    [HttpDelete("{id:long}")]
    public virtual async Task<ActionResult<ApiResponse<bool>>> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, $"{entityName} deleted successfully"));
    }

    [HttpPost("{id:long}/restore")]
    public virtual async Task<ActionResult<ApiResponse<TRead>>> Restore(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<TRead>.Success(
            await service.RestoreAsync(id, cancellationToken), $"{entityName} restored"));
}