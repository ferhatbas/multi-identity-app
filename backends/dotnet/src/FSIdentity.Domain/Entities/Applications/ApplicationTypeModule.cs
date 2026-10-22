using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Applications;

[Unique(nameof(AppModuleId), Within = nameof(ApplicationTypeId))]
public class ApplicationTypeModule : BaseEntityRoot
{
    [Parent(typeof(ApplicationType), InUseText = "still grants modules")]
    public long ApplicationTypeId { get; set; }

    public ApplicationType? ApplicationType { get; set; }

    [Parent(typeof(AppModule), InUseText = "is still granted to applications")]
    public long AppModuleId { get; set; }

    public AppModule? AppModule { get; set; }
}