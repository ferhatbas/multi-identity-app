using System.Text.Json;
using FSIdentity.Api.Startup;
using FSIdentity.Domain.Data;

namespace FSIdentity.Tests.Common;

public class PaginatedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    public void TotalPages_rounds_up(int totalRecords, int pageSize, int expectedPages)
    {
        var page = new PaginatedResult<int>([], 1, pageSize, totalRecords);

        Assert.Equal(expectedPages, page.TotalPages);
    }

    [Fact]
    public void Keeps_the_values_it_was_given()
    {
        var page = new PaginatedResult<string>(["a", "b"], 2, 2, 5);

        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalRecords);
        Assert.Equal(["a", "b"], page.Data);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    public void Rejects_invalid_page_arguments(int pageNumber, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PaginatedResult<int>([], pageNumber, pageSize, 0));
    }

    [Fact]
    public void Json_has_the_contract_fields()
    {
        var options = new JsonSerializerOptions();
        ApiJson.Configure(options);

        var json = JsonSerializer.Serialize(new PaginatedResult<int>([1], 1, 10, 1), options);
        using var doc = JsonDocument.Parse(json);

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(
            new[] { "data", "pageNumber", "pageSize", "totalPages", "totalRecords" },
            names);
    }
}