using ProtoStream;
using Xunit;

namespace ProtoStream.Tests.Core;

public class MessageEnvelopeTests
{
    [Fact]
    public void Command_Envelope_Roundtrips()
    {
        var env = MessageEnvelope.CreateCommand("TestMethod", "corr-123", "{\"key\":\"value\"}");
        var json = env.ToJson();
        var deserialized = MessageEnvelope.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal(MessageType.Command, deserialized.Type);
        Assert.Equal("TestMethod", deserialized.Method);
        Assert.Equal("corr-123", deserialized.CorrelationId);
        Assert.Equal("{\"key\":\"value\"}", deserialized.Data);
    }

    [Fact]
    public void Response_Envelope_Roundtrips()
    {
        var env = MessageEnvelope.CreateResponse("corr-456", "{\"result\":\"ok\"}");
        var json = env.ToJson();
        var deserialized = MessageEnvelope.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal(MessageType.CommandResponse, deserialized.Type);
        Assert.Equal("corr-456", deserialized.CorrelationId);
        Assert.Equal("{\"result\":\"ok\"}", deserialized.Data);
    }

    [Fact]
    public void Event_Envelope_Roundtrips()
    {
        var env = MessageEnvelope.CreateEvent("UserJoined", "{\"name\":\"alice\"}");
        var json = env.ToJson();
        var deserialized = MessageEnvelope.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal(MessageType.Event, deserialized.Type);
        Assert.Equal("UserJoined", deserialized.Method);
        Assert.Equal("{\"name\":\"alice\"}", deserialized.Data);
    }

    [Fact]
    public void ToJson_FieldNames_AreCompressed()
    {
        var env = MessageEnvelope.CreateCommand("Test", "id", "data");
        var json = env.ToJson();

        Assert.Contains("\"t\"", json);
        Assert.Contains("\"m\"", json);
        Assert.Contains("\"c\"", json);
        Assert.Contains("\"d\"", json);
        Assert.DoesNotContain("\"type\"", json);
    }

    [Fact]
    public void Null_Method_IsAllowed()
    {
        var env = new MessageEnvelope { Type = MessageType.Event, Data = "{}" };
        var json = env.ToJson();
        var deserialized = MessageEnvelope.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal(MessageType.Event, deserialized.Type);
        Assert.Null(deserialized.Method);
    }
}
