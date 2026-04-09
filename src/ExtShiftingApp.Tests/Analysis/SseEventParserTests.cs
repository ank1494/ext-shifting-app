using System.Text.Json;
using ExtShiftingApp.Analysis;

namespace ExtShiftingApp.Tests.Analysis;

public class SseEventParserTests
{
    [Fact]
    public void Parse_EventLine_ReturnsJsonWithoutPrefix()
    {
        const string line = "EVENT:{\"type\":\"item_started\",\"item\":\"0001\",\"depth\":0}";
        var json = SseEventParser.Parse(line);
        Assert.Equal("{\"type\":\"item_started\",\"item\":\"0001\",\"depth\":0}", json);
    }

    [Fact]
    public void Parse_PlainLine_WrapsAsM2Output()
    {
        const string line = "loading libraries...";
        var json = SseEventParser.Parse(line);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("m2_output", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(line, doc.RootElement.GetProperty("line").GetString());
    }

    [Fact]
    public void Parse_QueueStateLine_PreservesCountFields()
    {
        const string line = "EVENT:{\"type\":\"queue_state\",\"pendingCount\":5,\"doneCount\":3}";
        var json = SseEventParser.Parse(line);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("queue_state", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("pendingCount").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("doneCount").GetInt32());
    }

    [Fact]
    public void Parse_EmptyLine_WrapsAsM2Output()
    {
        var json = SseEventParser.Parse("");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("m2_output", doc.RootElement.GetProperty("type").GetString());
    }
}
