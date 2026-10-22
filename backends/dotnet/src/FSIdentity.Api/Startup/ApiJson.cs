using System.Text.Json;

namespace FSIdentity.Api.Startup;

public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    }
}