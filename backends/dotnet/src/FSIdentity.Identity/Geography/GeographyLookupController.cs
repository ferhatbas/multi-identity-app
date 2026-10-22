using FSIdentity.Domain.Common;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Geography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Geography;

public record GeoOption(long Id, string Code, string Name, long ParentId);

[ApiController]
[Route("api/geography")]
public class GeographyLookupController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("states")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GeoOption>>>> States(
        [FromQuery] string? countryCode = null, CancellationToken cancellationToken = default)
    {
        var query = uow.Repository<State>().Query().Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var countryId = await FindCountryIdAsync(countryCode, cancellationToken);

            query = query.Where(s => countryId != null && s.CountryId == countryId);
        }

        var rows = await query
            .OrderBy(s => s.Code)
            .Select(s => new GeoOption(s.Id, s.Code, s.Name, s.CountryId))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<GeoOption>>.Success(rows));
    }

    [HttpGet("cities")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GeoOption>>>> Cities(
        [FromQuery] long? stateId = null, [FromQuery] string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = uow.Repository<City>().Query().Where(c => c.IsActive);

        if (stateId is not null)
            query = query.Where(c => c.StateId == stateId);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var countryId = await FindCountryIdAsync(countryCode, cancellationToken);
            var stateIds = uow.Repository<State>().Query()
                .Where(s => countryId != null && s.CountryId == countryId)
                .Select(s => s.Id);

            query = query.Where(c => stateIds.Contains(c.StateId));
        }

        var rows = await query
            .OrderBy(c => c.Name)
            .Select(c => new GeoOption(c.Id, c.Code, c.Name, c.StateId))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<GeoOption>>.Success(rows));
    }

    private Task<long?> FindCountryIdAsync(string countryCode, CancellationToken cancellationToken)
    {
        var code = countryCode.Trim().ToUpperInvariant();

        return uow.Repository<Country>().Query()
            .Where(c => c.Code == code)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}