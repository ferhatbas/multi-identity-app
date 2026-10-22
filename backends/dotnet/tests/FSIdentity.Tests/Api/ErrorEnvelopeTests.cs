using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FSIdentity.Api.Startup;

namespace FSIdentity.Tests.Api;

public class ErrorEnvelopeTests : ApiTestBase
{
    private async Task AssertEnvelope(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await Read(response);
        var requestId = Assert.Single(response.Headers.GetValues("X-Request-ID"));

        Assert.Equal(((int)status).ToString(), body.GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("types").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
        Assert.Equal(errorCode, Assert.Single(body.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString());
        Assert.Equal(requestId, body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task An_unknown_address_is_a_404_envelope()
        => await AssertEnvelope(await Client.GetAsync("/nope"), HttpStatusCode.NotFound, "NOT_FOUND");

    [Fact]
    public async Task An_unknown_controller_is_a_404_envelope()
        => await AssertEnvelope(await Client.GetAsync("/api/Nope"), HttpStatusCode.NotFound, "NOT_FOUND");

    [Fact]
    public async Task An_id_that_is_not_a_number_is_a_404_envelope()
        => await AssertEnvelope(await Client.GetAsync("/api/Country/abc"), HttpStatusCode.NotFound, "NOT_FOUND");

    [Fact]
    public async Task A_wrong_http_method_is_a_405_envelope()
        => await AssertEnvelope(await Client.DeleteAsync("/api/Country"), HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");

    [Fact]
    public async Task A_wrong_content_type_is_a_415_envelope_not_problem_details()
    {
        var response = await Client.PostAsync("/api/Country", new StringContent("x", Encoding.UTF8, "text/plain"));

        await AssertEnvelope(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task A_response_that_already_has_a_body_is_left_alone()
    {
        var response = await Client.GetAsync("/api/Country/999");
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Country 999 not found", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Malformed_json_is_one_validation_error_for_the_whole_body()
    {
        var response = await Client.PostAsync("/api/Country",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));
        var body = await Read(response);

        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.Equal("", error.GetProperty("field").GetString());
    }

    [Fact]
    public async Task An_empty_body_is_a_400_envelope_with_one_error()
    {
        var response = await Client.PostAsync("/api/Country", new StringContent("", Encoding.UTF8, "application/json"));
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("400", body.GetProperty("code").GetString());
        Assert.Single(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task A_wrong_type_for_one_field_names_that_field()
    {
        var response = await Client.PostAsJsonAsync("/api/Language",
            new { code = "tr", name = "Turkish", displayOrder = "not a number" });
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("displayOrder", body.GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("\"just a string\"")]
    public async Task A_body_that_is_not_an_object_is_one_error(string json)
    {
        var response = await Client.PostAsync("/api/Country", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single((await Read(response)).GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task A_wrong_type_and_a_missing_required_field_are_both_reported_without_the_body_noise()
    {
        var response = await Client.PostAsJsonAsync("/api/Language", new { displayOrder = "abc" });
        var fields = (await Read(response)).GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString()).ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("body", fields);
        Assert.Contains("displayOrder", fields);
    }

    [Fact]
    public async Task These_responses_still_carry_cors_headers_for_an_allowed_origin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/nope");
        request.Headers.Add("Origin", "http://localhost:3040");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("http://localhost:3040", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData(400, "BAD_REQUEST")]
    [InlineData(401, "UNAUTHORIZED")]
    [InlineData(403, "FORBIDDEN")]
    [InlineData(404, "NOT_FOUND")]
    [InlineData(405, "METHOD_NOT_ALLOWED")]
    [InlineData(406, "NOT_ACCEPTABLE")]
    [InlineData(409, "CONFLICT")]
    [InlineData(415, "UNSUPPORTED_MEDIA_TYPE")]
    [InlineData(429, "TOO_MANY_REQUESTS")]
    [InlineData(418, "CLIENT_ERROR")]
    [InlineData(500, "INTERNAL_ERROR")]
    [InlineData(503, "INTERNAL_ERROR")]
    public void Every_status_code_has_an_error_code(int status, string expected)
        => Assert.Equal(expected, StatusCodeResponses.Describe(status).Code);
}
