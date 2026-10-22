using System.Net;
using System.Text.Json;
using FSIdentity.Api.Middleware;
using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace FSIdentity.Tests.Startup;

public class MiddlewareTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AllowedOrigin = "http://localhost:3040";

    private HttpClient ClientThatThrows(Exception exception)
        => factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new ThrowingStartupFilter(exception))))
            .CreateClient();

    private static HttpRequestMessage Request(HttpMethod method, string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(method, path);
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        return request;
    }

    [Fact]
    public async Task Response_gets_a_generated_request_id()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        var id = Assert.Single(response.Headers.GetValues(RequestIdMiddleware.HeaderName));
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public async Task Incoming_request_id_is_echoed()
    {
        var response = await factory.CreateClient().SendAsync(
            Request(HttpMethod.Get, "/api/health", (RequestIdMiddleware.HeaderName, "abc-123_X.9")));

        Assert.Equal("abc-123_X.9", Assert.Single(response.Headers.GetValues(RequestIdMiddleware.HeaderName)));
    }

    [Theory]
    [InlineData("has spaces in it")]
    [InlineData("semi;colon")]
    public async Task Invalid_incoming_request_id_is_replaced(string sent)
    {
        var response = await factory.CreateClient().SendAsync(
            Request(HttpMethod.Get, "/api/health", (RequestIdMiddleware.HeaderName, sent)));

        var id = Assert.Single(response.Headers.GetValues(RequestIdMiddleware.HeaderName));
        Assert.NotEqual(sent, id);
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public async Task Too_long_incoming_request_id_is_replaced()
    {
        var sent = new string('a', 65);

        var response = await factory.CreateClient().SendAsync(
            Request(HttpMethod.Get, "/api/health", (RequestIdMiddleware.HeaderName, sent)));

        Assert.NotEqual(sent, Assert.Single(response.Headers.GetValues(RequestIdMiddleware.HeaderName)));
    }

    [Fact]
    public async Task Error_response_keeps_the_request_id_header_and_uses_it_as_trace_id()
    {
        var client = ClientThatThrows(new NotFoundException("missing"));

        var response = await client.SendAsync(
            Request(HttpMethod.Get, "/anything", (RequestIdMiddleware.HeaderName, "rid-42")));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("rid-42", Assert.Single(response.Headers.GetValues(RequestIdMiddleware.HeaderName)));
        Assert.Equal("rid-42", doc.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Preflight_from_an_allowed_origin_is_accepted()
    {
        var response = await factory.CreateClient().SendAsync(Request(
            HttpMethod.Options, "/api/health",
            ("Origin", AllowedOrigin),
            ("Access-Control-Request-Method", "GET")));

        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task Request_from_an_unknown_origin_gets_no_cors_headers()
    {
        var response = await factory.CreateClient().SendAsync(
            Request(HttpMethod.Get, "/api/health", ("Origin", "http://evil.example")));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Request_id_header_is_exposed_to_the_browser()
    {
        var response = await factory.CreateClient().SendAsync(
            Request(HttpMethod.Get, "/api/health", ("Origin", AllowedOrigin)));

        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains(RequestIdMiddleware.HeaderName, exposed);
    }

    [Fact]
    public async Task Error_response_still_carries_cors_headers()
    {
        var client = ClientThatThrows(new Exception("boom"));

        var response = await client.SendAsync(
            Request(HttpMethod.Get, "/anything", ("Origin", AllowedOrigin)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Access_log_records_method_path_status_and_request_id()
    {
        var logger = new FakeLogger<AccessLogMiddleware>();
        var middleware = new AccessLogMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        }, logger);
        var context = new DefaultHttpContext { TraceIdentifier = "rid-7" };
        context.Request.Method = "GET";
        context.Request.Path = "/patients/5";

        await middleware.InvokeAsync(context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.StartsWith("GET /patients/5 -> 404", record.Message);
        Assert.EndsWith("rid=rid-7", record.Message);
    }

    [Fact]
    public async Task Access_log_records_500_when_an_exception_escapes()
    {
        var logger = new FakeLogger<AccessLogMiddleware>();
        var middleware = new AccessLogMiddleware(_ => throw new InvalidOperationException("x"), logger);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/boom";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Contains("POST /boom -> 500", Assert.Single(logger.Collector.GetSnapshot()).Message);
    }
}