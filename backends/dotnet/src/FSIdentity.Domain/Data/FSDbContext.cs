using System.Reflection;
using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Interfaces;
using FSIdentity.Domain.Interfaces.MultiTenant;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Domain.Data;

public abstract class FSDbContext : DbContext
{
    public const string SoftDeleteFilter = "SoftDelete";
    public const string CompanyFilter = "Company";
    public const string TenantFilter = "Tenant";
    public const string CompanySharedFilter = "CompanyShared";

    private readonly IMultiTenantAccessor _mtx;    
    private readonly TimeProvider _clock;
    private readonly IAuditContext? _auditContext;

    protected FSDbContext(
        DbContextOptions options, IMultiTenantAccessor mtx, TimeProvider? clock = null, IAuditContext? auditContext = null)
        : base(options)
    {
        _mtx = mtx;
        _clock = clock ?? TimeProvider.System;
        _auditContext = auditContext;
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAudit();
        var pending = AuditTrail.Collect(ChangeTracker);
        if (pending.Count == 0)
            return base.SaveChanges(acceptAllChangesOnSuccess);

        using var own = Database.CurrentTransaction is null ? Database.BeginTransaction() : null;
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        AuditLogs.AddRange(AuditRows(pending));
        base.SaveChanges(acceptAllChangesOnSuccess);
        own?.Commit();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        var pending = AuditTrail.Collect(ChangeTracker);
        if (pending.Count == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        await using var own = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        AuditLogs.AddRange(AuditRows(pending));
        await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (own is not null)
            await own.CommitAsync(cancellationToken);
        return result;
    }

    private List<AuditLog> AuditRows(List<PendingAudit> pending)
    {
        var rows = pending.Select(AuditTrail.ToRow).ToList();
        if (AuditNote.Value is { } note)
            rows.Add(AuditTrail.NoteRow(note, pending.Count));

        foreach (var row in rows)
            Stamp(row);

        return rows;
    }

    private void Stamp(AuditLog row)
    {
        row.CreatedAt = _clock.GetUtcNow().UtcDateTime;
        row.UserId = _mtx.UserId;
        row.AuditTenantId = _mtx.TenantId;
        row.AuditCompanyId = _mtx.CompanyId;
        row.UserName = _auditContext?.UserName;
        row.ImpersonatedBy = _auditContext?.ImpersonatorId;
        row.IpAddress = _auditContext?.IpAddress;
        row.UserAgent = _auditContext?.UserAgent;
        row.CorrelationId = _auditContext?.CorrelationId;
    }

    public void RecordAudit(string entityName, string? entityId, string action, string? details = null)
    {
        var row = new AuditLog { EntityName = entityName, EntityId = entityId, Action = action, NewValues = details };
        Stamp(row);
        AuditLogs.Add(row);
    }

    private void ApplyAudit()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _mtx.UserId;

        foreach (var entry in ChangeTracker.Entries<IBaseEntityRoot>())
        {
            var entity = entry.Entity;

            switch (entry.State)
            {
                case EntityState.Added:
                    entity.CreatedAt = now;
                    entity.CreatedBy = userId;
                    if (entity is IBaseEntity owned)
                    {
                        owned.IdentityCompanyId ??= _mtx.CompanyId;
                        owned.IdentityTenantId ??= _mtx.TenantId;
                    }
                    break;

                case EntityState.Modified:
                    entity.LastModifiedAt = now;
                    entity.LastModifiedBy = userId;
                    if (!entity.IsDeleted && entry.Property(nameof(ISoftDelete.IsDeleted)).OriginalValue is true)
                    {
                        entity.DeletedAt = null;
                        entity.DeletedBy = null;
                    }
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    entity.IsDeleted = true;
                    entity.DeletedAt = now;
                    entity.DeletedBy = userId;
                    break;
            }
        }
    }

    protected long? CurrentCompanyId => _mtx.CompanyId;
    protected long? CurrentTenantId => _mtx.TenantId;
    protected bool BypassSoftDelete => _mtx.BypassSoftDelete;
    protected bool BypassCompany => _mtx.Bypass || _mtx.BypassCompany;
    protected bool BypassTenant => _mtx.Bypass || _mtx.BypassTenant;

    protected virtual void ConfigureModel(ModelBuilder modelBuilder)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        ConfigureModel(modelBuilder);
        ApplyQueryFilters(modelBuilder);
    }

    private void ApplyQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.BaseType is not null || entityType.IsOwned())
                continue;

            var clr = entityType.ClrType;

            if (typeof(ISoftDelete).IsAssignableFrom(clr))
                Invoke(nameof(SoftDelete), clr, modelBuilder);

            if (typeof(IStrictCompanyScoped).IsAssignableFrom(clr))
                Invoke(nameof(StrictCompany), clr, modelBuilder);
            else if (typeof(ICompanyScoped).IsAssignableFrom(clr))
                Invoke(nameof(Company), clr, modelBuilder);

            if (typeof(IStrictTenantScoped).IsAssignableFrom(clr))
                Invoke(nameof(StrictTenant), clr, modelBuilder);
            else if (typeof(ITenantScoped).IsAssignableFrom(clr))
                Invoke(nameof(Tenant), clr, modelBuilder);

            if (typeof(ICompanySharedScoped).IsAssignableFrom(clr))
                Invoke(nameof(CompanyShared), clr, modelBuilder);
        }
    }

    private void Invoke(string method, Type clr, ModelBuilder modelBuilder)
        => typeof(FSDbContext)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(clr)
            .Invoke(this, [modelBuilder]);

    private void SoftDelete<T>(ModelBuilder mb) where T : class
        => mb.Entity<T>().HasQueryFilter(SoftDeleteFilter,
            e => BypassSoftDelete || EF.Property<bool>(e, nameof(ISoftDelete.IsDeleted)) == false);

    private void Company<T>(ModelBuilder mb) where T : class
        => mb.Entity<T>().HasQueryFilter(CompanyFilter,
            e => CurrentCompanyId == null || BypassCompany
                 || EF.Property<long?>(e, nameof(IBaseEntity.IdentityCompanyId)) == CurrentCompanyId);

    private void StrictCompany<T>(ModelBuilder mb) where T : class
        => mb.Entity<T>().HasQueryFilter(CompanyFilter,
            e => CurrentCompanyId == null
                 || EF.Property<long?>(e, nameof(IBaseEntity.IdentityCompanyId)) == CurrentCompanyId);

    private void Tenant<T>(ModelBuilder mb) where T : class
        => mb.Entity<T>().HasQueryFilter(TenantFilter,
            e => CurrentTenantId == null || BypassTenant
                 || EF.Property<long?>(e, nameof(IBaseEntity.IdentityTenantId)) == CurrentTenantId);

    private void StrictTenant<T>(ModelBuilder mb) where T : class
        => mb.Entity<T>().HasQueryFilter(TenantFilter,
            e => CurrentTenantId == null
                 || EF.Property<long?>(e, nameof(IBaseEntity.IdentityTenantId)) == CurrentTenantId);

    private void CompanyShared<T>(ModelBuilder mb) where T : class
        => mb.Entity<T>().HasQueryFilter(CompanySharedFilter,
            e => CurrentCompanyId == null
                 || EF.Property<long?>(e, nameof(IBaseEntity.IdentityCompanyId)) == null
                 || EF.Property<long?>(e, nameof(IBaseEntity.IdentityCompanyId)) == CurrentCompanyId);
}