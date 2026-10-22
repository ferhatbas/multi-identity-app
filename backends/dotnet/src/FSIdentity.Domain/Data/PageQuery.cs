namespace FSIdentity.Domain.Data;

public record PageQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? OrderBy = null,
    bool Desc = false,
    string? Search = null);