using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Common;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Audit;

public class AuditLogRead
{
    public long Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string Action { get; init; } = string.Empty;
    public long? UserId { get; init; }
    public string? UserName { get; init; }
    public long? AuditTenantId { get; init; }
    public long? AuditCompanyId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? CorrelationId { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? ChangedFields { get; init; }
}

public class AuditLogQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? EntityName { get; init; }
    public string? Action { get; init; }
    public long? UserId { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public string? Search { get; init; }
}

[ApiController]
[Route("api/[controller]")]
[RequirePermission(Permissions.AuditLogView)]
public class AuditLogController(AppDbContext db, IMultiTenantAccessor mtx) : ControllerBase
{
    public const int MaxPageSize = 500;

    [HttpGet("paged")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<AuditLogRead>>>> GetPaged(
        [FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        if (query.PageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1");
        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
            throw new ArgumentException($"pageSize must be between 1 and {MaxPageSize}");

        var rows = Visible();

        if (!string.IsNullOrWhiteSpace(query.EntityName))
            rows = rows.Where(a => a.EntityName == query.EntityName);
        if (!string.IsNullOrWhiteSpace(query.Action))
            rows = rows.Where(a => a.Action == query.Action);
        if (query.UserId is { } userId)
            rows = rows.Where(a => a.UserId == userId);
        if (query.From is { } from)
        {
            var start = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            rows = rows.Where(a => a.CreatedAt >= start);
        }
        if (query.To is { } to)
        {
            var end = DateTime.SpecifyKind(to, DateTimeKind.Utc);
            var exclusive = end.TimeOfDay == TimeSpan.Zero ? end.AddDays(1) : end.AddTicks(1);
            rows = rows.Where(a => a.CreatedAt < exclusive);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(a => a.EntityName.Contains(term)
                                   || (a.UserName != null && a.UserName.Contains(term))
                                   || (a.EntityId != null && a.EntityId.Contains(term))
                                   || (a.ChangedFields != null && a.ChangedFields.Contains(term)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Read)
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<AuditLogRead>(page, query.PageNumber, query.PageSize, total);
        return Ok(ApiResponse<PaginatedResult<AuditLogRead>>.Success(result,
            page.Count > 0 ? "Audit log page retrieved successfully" : "No audit log found"));
    }

    [HttpGet("actions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> Actions(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> actions = await Visible().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Success(actions, "Audit actions retrieved successfully"));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AuditLogRead>>> GetById(long id, CancellationToken cancellationToken)
    {
        var row = await Visible().Where(a => a.Id == id).Select(Read).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException($"Audit log {id} not found");

        return Ok(ApiResponse<AuditLogRead>.Success(row, "Audit log retrieved successfully"));
    }

    [HttpGet("entity/{entityName}/{entityId}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditLogRead>>>> EntityHistory(
        string entityName, string entityId, [FromQuery] int max = 200, CancellationToken cancellationToken = default)
    {
        if (max < 1 || max > 1000)
            throw new ArgumentException("max must be between 1 and 1000");

        var history = await Visible()
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(max)
            .Select(Read)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<AuditLogRead>>.Success(history,
            history.Count > 0 ? "Audit history retrieved successfully" : "No audit history found"));
    }

    private IQueryable<AuditLog> Visible()
    {
        var rows = db.AuditLogs.AsNoTracking();
        return !mtx.Bypass && mtx.TenantId is { } tenantId
            ? rows.Where(a => a.AuditTenantId == tenantId)
            : rows;
    }

    private static readonly System.Linq.Expressions.Expression<Func<AuditLog, AuditLogRead>> Read = a => new AuditLogRead
    {
        Id = a.Id,
        CreatedAt = a.CreatedAt,
        EntityName = a.EntityName,
        EntityId = a.EntityId,
        Action = a.Action,
        UserId = a.UserId,
        UserName = a.UserName,
        AuditTenantId = a.AuditTenantId,
        AuditCompanyId = a.AuditCompanyId,
        IpAddress = a.IpAddress,
        UserAgent = a.UserAgent,
        CorrelationId = a.CorrelationId,
        OldValues = a.OldValues,
        NewValues = a.NewValues,
        ChangedFields = a.ChangedFields,
    };
}
