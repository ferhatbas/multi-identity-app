using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Localization;
using FSIdentity.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Localization;

public class LanguageRules(IUnitOfWork uow) : CrudRules<Language, LanguageCreate, LanguageUpdate>
{
    public override async Task OnCreateAsync(LanguageCreate create, Language entity, CancellationToken cancellationToken)
    {
        if (create.IsFallback)
            await ClearFallbackAsync(exceptId: null, cancellationToken);
    }

    public override async Task OnUpdateAsync(Language entity, LanguageUpdate update, CancellationToken cancellationToken)
    {
        if (entity.IsFallback && !update.IsFallback)
            throw new ConflictException("A fallback language is required; mark another language as the fallback instead");

        if (entity.IsFallback && update.IsActive == false)
            throw new ConflictException("The fallback language cannot be deactivated");

        if (update.IsFallback && !entity.IsFallback)
            await ClearFallbackAsync(entity.Id, cancellationToken);
    }

    public override Task OnDeleteAsync(Language entity, CancellationToken cancellationToken)
        => entity.IsFallback
            ? throw new ConflictException("The fallback language cannot be deleted")
            : Task.CompletedTask;

    public override Task OnRestoreAsync(Language entity, CancellationToken cancellationToken)
    {
        entity.IsFallback = false;
        return Task.CompletedTask;
    }

    private async Task ClearFallbackAsync(long? exceptId, CancellationToken cancellationToken)
    {
        var others = await uow.Repository<Language>().Query()
            .Where(l => l.IsFallback && l.Id != exceptId)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
            other.IsFallback = false;
    }
}
