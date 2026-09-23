using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnitTests.Endpoints;

public static class PortalJson
{
    // Matches the api's own serializer: camelCase with string enums.
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
