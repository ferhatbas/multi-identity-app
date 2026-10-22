using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Applications;

[Unique(nameof(Code))]
public class AppModule : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsCore { get; set; }
}

public static class AppModuleCodes
{
    public const string Core = "CORE";
    public const string Admin = "ADMIN";
}