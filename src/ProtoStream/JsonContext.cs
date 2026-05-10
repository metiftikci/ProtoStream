using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProtoStream;

public static class JsonHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
