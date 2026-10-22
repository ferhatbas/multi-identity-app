namespace FSIdentity.Domain.Data;

public class PaginatedResult<T>
{
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages { get; }
    public int TotalRecords { get; }
    public IReadOnlyList<T> Data { get; }

    public PaginatedResult(IEnumerable<T> data, int pageNumber, int pageSize, int totalRecords)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalRecords);

        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalRecords = totalRecords;
        TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        Data = data.ToList();
    }
}