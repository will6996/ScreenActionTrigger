using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Persistence.Serializers;

public static class JsonProfileSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<ExecutionProfile?> DeserializeAsync(Stream stream)
    {
        return await JsonSerializer.DeserializeAsync<ExecutionProfile>(stream, Options);
    }

    public static async Task SerializeAsync(ExecutionProfile profile, Stream stream)
    {
        await JsonSerializer.SerializeAsync(stream, profile, Options);
    }

    public static string Serialize(ExecutionProfile profile)
        => JsonSerializer.Serialize(profile, Options);

    public static ExecutionProfile? Deserialize(string json)
        => JsonSerializer.Deserialize<ExecutionProfile>(json, Options);
}
