using System.Text;
using ArcadeDodger.Multiplayer;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class ProtocolTests
{
    private const string Join = """{"v":6,"type":"join","name":"  Pilot_7  ","room":"abcd0123"}""";
    private const string Input =
        """{"v":6,"type":"input","seq":1,"x":0,"y":1,"aim":0,"fire":false,"menu":true}""";

    private static bool Parse(string json, out ClientMessage? message) =>
        ClientProtocol.TryParse(Encoding.UTF8.GetBytes(json), out message);

    [Fact]
    public void ValidMessagesHaveOnlyIntentAndNormalizeNames()
    {
        Assert.True(Parse(Join, out var join));
        Assert.Equal(new JoinMessage("Pilot_7", "abcd0123"), join);
        Assert.True(Parse(Input, out var input));
        Assert.Equal(new InputMessage(1, 0, 1, 0, false, true), input);
        Assert.True(
            Parse("""{"v":6,"type":"choice","seq":2,"choice":"thruster.1"}""", out var choice)
        );
        Assert.Equal(new ChoiceMessage(2, "thruster.1"), choice);
        Assert.True(Parse("""{"v":6,"type":"respawn","seq":3}""", out var respawn));
        Assert.Equal(new RespawnMessage(3), respawn);
        Assert.True(Parse("""{"v":6,"type":"join","name":"   ","room":""}""", out join));
        Assert.Equal(new JoinMessage("Pilot", ""), join);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"v\":6")]
    [InlineData("{\"v\":6,\"type\":\"join\",\"name\":null,\"room\":\"\"}")]
    [InlineData("{\"v\":6,\"type\":\"respawn\",\"seq\":1,}")]
    [InlineData("{\"v\":6,/*comment*/\"type\":\"respawn\",\"seq\":1}")]
    [InlineData("{\"v\":2,\"type\":\"respawn\",\"seq\":1}")]
    [InlineData("{\"v\":6,\"type\":\"damage\",\"seq\":1}")]
    [InlineData("{\"v\":6,\"type\":\"respawn\",\"seq\":0}")]
    [InlineData("{\"v\":6,\"type\":\"respawn\",\"seq\":-1}")]
    [InlineData("{\"v\":6,\"type\":\"respawn\",\"seq\":2147483648}")]
    [InlineData("{\"v\":6,\"type\":\"respawn\",\"seq\":1.5}")]
    [InlineData("{\"v\":6,\"type\":\"respawn\",\"seq\":\"1\"}")]
    [InlineData("{\"v\":6,\"type\":\"choice\",\"seq\":1,\"choice\":30}")]
    [InlineData("{\"v\":6,\"type\":\"choice\",\"seq\":1,\"choice\":-1}")]
    public void RejectsMalformedOrOutOfRangeMessages(string json)
    {
        Assert.False(Parse(json, out var message));
        Assert.Null(message);
    }

    [Theory]
    [InlineData("position", "{\"x\":999,\"y\":999}")]
    [InlineData("damage", "100")]
    [InlineData("xp", "9999")]
    [InlineData("score", "100")]
    [InlineData("playerId", "12")]
    [InlineData("delta", "10")]
    [InlineData("v", "1")]
    [InlineData("seq", "2")]
    [InlineData("\\u0078", "0")]
    public void RejectsForgedAuthorityAndDuplicateFields(string key, string value)
    {
        Assert.False(Parse(Input[..^1] + $",\"{key}\":{value}}}", out _));
    }

    [Theory]
    [InlineData("\"x\":0", "\"x\":1.01")]
    [InlineData("\"y\":1", "\"y\":-1.01")]
    [InlineData("\"aim\":0", "\"aim\":4")]
    [InlineData("\"x\":0", "\"x\":1e100")]
    [InlineData("\"x\":0", "\"x\":\"NaN\"")]
    [InlineData("\"fire\":false", "\"fire\":1")]
    [InlineData("\"menu\":true", "\"menu\":null")]
    [InlineData("\"menu\":true", "\"Dash\":true")]
    public void RejectsInvalidInputFields(string before, string after) =>
        Assert.False(Parse(Input.Replace(before, after), out _));

    [Theory]
    [InlineData("<script>", "")]
    [InlineData("Pilot\n", "")]
    [InlineData("12345678901234567", "")]
    [InlineData("Pilot", "ABCDEFGH")]
    [InlineData("Pilot", "../room")]
    [InlineData("Pilot", "abc")]
    public void RejectsInvalidJoinValues(string name, string room)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                v = 6,
                type = "join",
                name,
                room,
            }
        );
        Assert.False(Parse(json, out _));
    }

    [Fact]
    public void BoundsPayloadAndDepthAndRejectsInvalidUtf8()
    {
        Assert.True(Parse(Input.PadRight(ClientProtocol.MaxMessageBytes), out _));
        Assert.False(Parse(Input.PadRight(ClientProtocol.MaxMessageBytes + 1), out _));
        Assert.False(Parse(new string('[', 9) + "0" + new string(']', 9), out _));
        var invalid = Encoding.UTF8.GetBytes(Join);
        invalid[Array.IndexOf(invalid, (byte)'P')] = 0xff;
        Assert.False(ClientProtocol.TryParse(invalid, out _));
    }

    [Fact]
    public void ConnectionRequiresJoinAndStrictSharedSequence()
    {
        var session = new ClientSession();
        Assert.False(session.TryAccept(new RespawnMessage(1)));
        Assert.True(session.TryAccept(new JoinMessage("Pilot", "")));
        Assert.False(session.TryAccept(new JoinMessage("Pilot", "")));
        Assert.True(session.TryAccept(new InputMessage(2, 0, 0, 0, false, false)));
        Assert.False(session.TryAccept(new ChoiceMessage(2, "shield.1")));
        Assert.False(session.TryAccept(new RespawnMessage(1)));
        Assert.True(session.TryAccept(new ChoiceMessage(3, "shield.1")));
        Assert.True(session.TryAccept(new RespawnMessage(int.MaxValue)));
        Assert.False(session.TryAccept(new RespawnMessage(int.MinValue)));
        Assert.Equal(int.MaxValue, session.LastReceivedSequence);
        Assert.Equal(0, new ClientSession().LastReceivedSequence);
    }
}
