using FSIdentity.Domain.Entities.Geography;
using FSIdentity.Domain.Entities.Localization;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Geography;

public class GeographySeeder(AppDbContext db)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedLanguagesAsync(cancellationToken);
        var countries = await SeedCountriesAsync(cancellationToken);

        if (countries.TryGetValue("TR", out var turkey))
        {
            await SeedStatesAndCitiesAsync(
                turkey,
                GeographySeedData.TurkeyRegions,
                GeographySeedData.TurkeyProvinces.Select(p => ($"TR-{p.Plate}", p.Name, p.Region)),
                "Bölge", "İl", cancellationToken);
        }

        if (countries.TryGetValue("US", out var unitedStates))
        {
            await SeedStatesAndCitiesAsync(
                unitedStates,
                GeographySeedData.UsStates,
                GeographySeedData.UsCities,
                "State", "City", cancellationToken);
        }
    }

    private async Task SeedLanguagesAsync(CancellationToken cancellationToken)
    {
        var live = await db.Languages.ToDictionaryAsync(l => l.Code, cancellationToken);
        var hasFallback = live.Values.Any(l => l.IsFallback);

        foreach (var seed in GeographySeedData.Languages)
        {
            if (!live.TryGetValue(seed.Code, out var language))
            {
                language = new Language { Code = seed.Code };
                db.Languages.Add(language);
            }

            language.Name = seed.Name;
            language.NativeName = seed.NativeName;
            language.IsRtl = seed.IsRtl;
            language.DisplayOrder = seed.DisplayOrder;
            language.IsActive = true;

            if (language.Id == 0 && seed.IsFallback && !hasFallback)
            {
                language.IsFallback = true;
                hasFallback = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, long>> SeedCountriesAsync(CancellationToken cancellationToken)
    {
        var live = await db.Countries.ToDictionaryAsync(c => c.Code, cancellationToken);

        foreach (var seed in GeographySeedData.Countries)
        {
            if (!live.TryGetValue(seed.Code, out var country))
            {
                country = new Country { Code = seed.Code };
                db.Countries.Add(country);
                live[seed.Code] = country;
            }

            country.Name = seed.Name;
            country.FormatCode = seed.FormatCode;
            country.LanguageCode = seed.LanguageCode;
            country.DateFormat = seed.DateFormat;
            country.DateTimeFormat = seed.DateTimeFormat;
            country.CurrencyCode = seed.CurrencyCode;
            country.CurrencySymbol = seed.CurrencySymbol;
            country.PriceFormat = seed.PriceFormat;
            country.TimeZone = seed.TimeZone;
            country.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        return live.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
    }

    private async Task SeedStatesAndCitiesAsync(
        long countryId,
        IEnumerable<(string Code, string Name)> states,
        IEnumerable<(string Code, string Name, string StateCode)> cities,
        string stateDescription, string cityDescription,
        CancellationToken cancellationToken)
    {
        var liveStates = await db.States.ToDictionaryAsync(s => s.Code, cancellationToken);

        foreach (var (code, name) in states)
        {
            if (!liveStates.TryGetValue(code, out var state))
            {
                state = new State { Code = code };
                db.States.Add(state);
                liveStates[code] = state;
            }

            state.Name = name;
            state.CountryId = countryId;
            state.Description = stateDescription;
            state.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        var liveCities = await db.Cities.ToDictionaryAsync(c => c.Code, cancellationToken);

        foreach (var (code, name, stateCode) in cities)
        {
            if (!liveCities.TryGetValue(code, out var city))
            {
                city = new City { Code = code };
                db.Cities.Add(city);
            }

            city.Name = name;
            city.StateId = liveStates[stateCode].Id;
            city.Description = cityDescription;
            city.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}