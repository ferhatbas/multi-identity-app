using FSIdentity.Domain.Entities;
using FSIdentity.Domain.Interfaces;
using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Tests.Domain;

public class BaseEntityTests
{
    private sealed class Widget : BaseEntity, IStrictCompanyScoped { }
    private sealed class TenantWidget : BaseEntity, IStrictTenantScoped { }
    private sealed class Country : BaseEntityRoot { }

    [Fact]
    public void NewEntity_IsActive_DefaultsToTrue()
    {
        var widget = new Widget();

        Assert.True(widget.IsActive);
    }

    [Fact]
    public void NewEntity_IsDeleted_DefaultsToFalse()
    {
        var widget = new Widget();

        Assert.False(widget.IsDeleted);
    }

    [Fact]
    public void NewEntity_AuditFields_AreNullByDefault()
    {
        var widget = new Widget();

        Assert.Null(widget.CreatedAt);
        Assert.Null(widget.CreatedBy);
        Assert.Null(widget.LastModifiedAt);
        Assert.Null(widget.LastModifiedBy);
        Assert.Null(widget.DeletedAt);
        Assert.Null(widget.DeletedBy);
    }

    [Fact]
    public void BaseEntity_ImplementsIBaseEntityRoot()
    {
        var widget = new Widget();

        Assert.IsAssignableFrom<IBaseEntityRoot>(widget);
        Assert.IsAssignableFrom<IBaseEntity>(widget);
    }

    [Fact]
    public void BaseEntityRoot_HasNoOwnershipFields()
    {
        var type = typeof(Country);

        Assert.Null(type.GetProperty(nameof(BaseEntity.IdentityCompanyId)));
        Assert.Null(type.GetProperty(nameof(BaseEntity.IdentityTenantId)));
    }

    [Fact]
    public void StrictCompanyScoped_IsAlsoCompanyScoped()
    {
        var widget = new Widget();

        Assert.IsAssignableFrom<IStrictCompanyScoped>(widget);
        Assert.IsAssignableFrom<ICompanyScoped>(widget);
    }

    [Fact]
    public void StrictTenantScoped_IsAlsoTenantScoped()
    {
        var widget = new TenantWidget();

        Assert.IsAssignableFrom<IStrictTenantScoped>(widget);
        Assert.IsAssignableFrom<ITenantScoped>(widget);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var now = DateTime.UtcNow;
        var widget = new Widget
        {
            Id = 42,
            IdentityCompanyId = 7,
            IdentityTenantId = 3,
            CreatedAt = now,
            CreatedBy = 5
        };

        Assert.Equal(42, widget.Id);
        Assert.Equal(7, widget.IdentityCompanyId);
        Assert.Equal(3, widget.IdentityTenantId);
        Assert.Equal(now, widget.CreatedAt);
        Assert.Equal(5, widget.CreatedBy);
    }
}