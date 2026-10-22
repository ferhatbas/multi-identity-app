using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Geography;

[Unique(nameof(Code))]
public class Country : BaseEntityRoot
{
    public string Name { get; set; } = string.Empty;
    public string? NameGlobal { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string FormatCode { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string? DateFormat { get; set; }
    public string? DateTimeFormat { get; set; }
    public string? CurrencyCode { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? PriceFormat { get; set; }
    public string? TimeZone { get; set; }
}