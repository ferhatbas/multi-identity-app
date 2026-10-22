namespace FSIdentity.Domain.Common;

public interface IApiResponse
{
    string? TraceId { get; set; }
}