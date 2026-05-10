using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProtoStream;

public class MessageEnvelope
{
    [JsonPropertyName("t")]
    public MessageType Type { get; set; }

    [JsonPropertyName("m")]
    public string? Method { get; set; }

    [JsonPropertyName("c")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("d")]
    public string? Data { get; set; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonHelper.Options);
    }

    public static MessageEnvelope? FromJson(string json)
    {
        return JsonSerializer.Deserialize<MessageEnvelope>(json, JsonHelper.Options);
    }

    public static MessageEnvelope CreateCommand(string method, string correlationId, string data)
    {
        return new MessageEnvelope { Type = MessageType.Command, Method = method, CorrelationId = correlationId, Data = data };
    }

    public static MessageEnvelope CreateResponse(string correlationId, string data)
    {
        return new MessageEnvelope { Type = MessageType.CommandResponse, CorrelationId = correlationId, Data = data };
    }

    public static MessageEnvelope CreateEvent(string method, string data)
    {
        return new MessageEnvelope { Type = MessageType.Event, Method = method, Data = data };
    }
}
