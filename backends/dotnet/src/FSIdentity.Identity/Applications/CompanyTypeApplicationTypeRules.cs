using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Applications;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Applications;

public class CompanyTypeApplicationTypeRules(IUnitOfWork uow)
    : CrudRules<CompanyTypeApplicationType, CompanyTypeApplicationTypeCreate, CompanyTypeApplicationTypeUpdate>
{
    public override async Task OnCreateAsync(
        CompanyTypeApplicationTypeCreate create, CompanyTypeApplicationType entity, CancellationToken cancellationToken)
    {
        if (create.IsDefault)
            await ClearDefaultAsync(create.CompanyTypeId, exceptId: null, cancellationToken);
    }

    public override async Task OnUpdateAsync(
        CompanyTypeApplicationType entity, CompanyTypeApplicationTypeUpdate update, CancellationToken cancellationToken)
    {
        var becomesDefault = update.IsDefault && (!entity.IsDefault || entity.CompanyTypeId != update.CompanyTypeId);
        if (becomesDefault)
            await ClearDefaultAsync(update.CompanyTypeId, entity.Id, cancellationToken);
    }

    public override Task OnRestoreAsync(CompanyTypeApplicationType entity, CancellationToken cancellationToken)
    {
        entity.IsDefault = false;
        return Task.CompletedTask;
    }

    private async Task ClearDefaultAsync(long companyTypeId, long? exceptId, CancellationToken cancellationToken)
    {
        var others = await uow.Repository<CompanyTypeApplicationType>().Query()
            .Where(m => m.CompanyTypeId == companyTypeId && m.IsDefault && m.Id != exceptId)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
            other.IsDefault = false;
    }
}