using System.Net;
using System.Text.Json;
using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Startup;

public class ApiStartupTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient ClientThatThrows(Exception exception)
        => factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new ThrowingStartupFilter(exception))))
            .CreateClient();

    [Fact]
    public async Task App_starts_and_serves_a_request()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Handled_exception_comes_back_in_the_envelope()
    {
        var client = ClientThatThrows(new NotFoundException("Patient 5 not found"));

        var response = await client.GetAsync("/anything");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("404", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("Patient 5 not found", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("types").GetInt32());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_without_leaking_details()
    {
        var client = ClientThatThrows(new Exception("Server=prod;Password=secret"));

        var response = await client.GetAsync("/anything");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret", body);
        Assert.Contains("\"code\":\"500\"", body);
    }
}