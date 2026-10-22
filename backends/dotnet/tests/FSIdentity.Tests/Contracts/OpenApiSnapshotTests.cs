using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FSIdentity.Tests.Api;

namespace FSIdentity.Tests.Contracts;

public class OpenApiSnapshotTests : ApiTestBase
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public async Task The_published_openapi_document_matches_the_committed_snapshot()
    {
        var live = Normalize(await Client.GetStringAsync("/openapi/v1.json"));
        var path = SnapshotPath();

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            File.WriteAllText(path, live);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing. Create it with UPDATE_OPENAPI=1 (see this file).");

        var committed = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.True(committed == live,
            "The API surface changed but contracts/openapi.json was not updated. " +
            "If the change is intended, regenerate it: UPDATE_OPENAPI=1 dotnet test --filter OpenApiSnapshotTests");
    }

    [Fact]
    public async Task The_snapshot_describes_the_standard_envelope()
    {
        var document = JsonNode.Parse(await Client.GetStringAsync("/openapi/v1.json"))!;
        var schemas = document["components"]!["schemas"]!.AsObject();

        var envelope = schemas["ApiResponseOfCountryRead"]!["properties"]!.AsObject().Select(p => p.Key).Order().ToArray();

        Assert.Equal(new[] { "code", "data", "errors", "message", "timestamp", "traceId", "types" }, envelope);
    }

    [Fact]
    public async Task The_document_declares_the_bearer_scheme()
    {
        var document = JsonNode.Parse(await Client.GetStringAsync("/openapi/v1.json"))!;

        var scheme = document["components"]!["securitySchemes"]!["Bearer"]!;
        Assert.Equal("http", (string?)scheme["type"]);
        Assert.Equal("bearer", (string?)scheme["scheme"]);
        Assert.Equal("JWT", (string?)scheme["bearerFormat"]);
        Assert.NotNull(document["security"]!.AsArray().Single()!["Bearer"]);
    }

    [Fact]
    public async Task The_swagger_page_points_at_the_document_and_is_not_itself_documented()
    {
        var response = await Anonymous.GetAsync("/swagger");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/openapi/v1.json", html);
        Assert.DoesNotContain("\"/swagger\"", await Client.GetStringAsync("/openapi/v1.json"));
    }

    private static string Normalize(string json)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("servers");
        return document.ToJsonString(Pretty).Replace("\r\n", "\n") + "\n";
    }

    private static string SnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !(Directory.Exists(Path.Combine(directory.FullName, "contracts"))
                    && Directory.Exists(Path.Combine(directory.FullName, "backends"))))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not find the repository root (a folder holding contracts/ and backends/).");
        return Path.Combine(directory!.FullName, "contracts", "openapi.json");
    }
}
