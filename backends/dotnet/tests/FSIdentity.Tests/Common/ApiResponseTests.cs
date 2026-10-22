using System.Text.Json;
using FSIdentity.Api.Startup;
using FSIdentity.Domain.Common;
using FSIdentity.Domain.Enums;

namespace FSIdentity.Tests.Common;

public class ApiResponseTests
{
    [Fact]
    public void Success_has_code_200_and_success_type()
    {
        var response = ApiResponse<string>.Success("hello");

        Assert.Equal("200", response.Code);
        Assert.Equal(ApiResponseTypes.SUCCESS, response.Types);
        Assert.Equal("hello", response.Data);
        Assert.True(response.IsSuccess);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Created_has_code_201()
    {
        var response = ApiResponse<int>.Created(5);

        Assert.Equal("201", response.Code);
        Assert.Equal(ApiResponseTypes.SUCCESS, response.Types);
    }

    [Theory]
    [InlineData(400, ApiResponseTypes.CLIENT_ERROR)]
    [InlineData(401, ApiResponseTypes.CLIENT_ERROR)]
    [InlineData(403, ApiResponseTypes.CLIENT_ERROR)]
    [InlineData(404, ApiResponseTypes.CLIENT_ERROR)]
    [InlineData(409, ApiResponseTypes.CLIENT_ERROR)]
    [InlineData(422, ApiResponseTypes.CLIENT_ERROR)]
    [InlineData(500, ApiResponseTypes.SERVER_ERROR)]
    [InlineData(503, ApiResponseTypes.SERVER_ERROR)]
    public void Failure_type_follows_the_status_code(int status, ApiResponseTypes expected)
    {
        var response = ApiResponse<string>.Failure(status, "nope");

        Assert.Equal(status.ToString(), response.Code);
        Assert.Equal(expected, response.Types);
        Assert.False(response.IsSuccess);
        Assert.Equal(status, response.StatusCode);
    }

    [Fact]
    public void Named_factories_use_the_expected_status()
    {
        Assert.Equal("400", ApiResponse<string>.BadRequest("x").Code);
        Assert.Equal("401", ApiResponse<string>.Unauthorized().Code);
        Assert.Equal("403", ApiResponse<string>.Forbidden().Code);
        Assert.Equal("404", ApiResponse<string>.NotFound().Code);
        Assert.Equal("409", ApiResponse<string>.Conflict().Code);
        Assert.Equal("422", ApiResponse<string>.ValidationError([]).Code);
        Assert.Equal("500", ApiResponse<string>.InternalError().Code);
    }

    [Fact]
    public void AsErrorResponse_keeps_code_type_errors_and_trace()
    {
        var failing = ApiResponse<string>.Failure(404, "missing", [new ApiError("X", "boom")]);
        failing.TraceId = "t-1";

        var converted = failing.AsErrorResponse<int>();

        Assert.Equal("404", converted.Code);
        Assert.Equal(ApiResponseTypes.CLIENT_ERROR, converted.Types);
        Assert.Equal("t-1", converted.TraceId);
        Assert.Single(converted.Errors);
    }

    [Fact]
    public void Json_has_exactly_the_contract_fields_in_camel_case()
    {
        var options = new JsonSerializerOptions();
        ApiJson.Configure(options);

        var json = JsonSerializer.Serialize(ApiResponse<string>.Success("x"), options);
        using var doc = JsonDocument.Parse(json);

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(
            new[] { "code", "data", "errors", "message", "timestamp", "traceId", "types" },
            names);
    }

    [Fact]
    public void Json_types_is_a_number_and_code_is_a_string()
    {
        var options = new JsonSerializerOptions();
        ApiJson.Configure(options);

        var json = JsonSerializer.Serialize(ApiResponse<string>.NotFound("x"), options);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("types").ValueKind);
        Assert.Equal(1, doc.RootElement.GetProperty("types").GetInt32());
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("code").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("data").ValueKind);
    }
}