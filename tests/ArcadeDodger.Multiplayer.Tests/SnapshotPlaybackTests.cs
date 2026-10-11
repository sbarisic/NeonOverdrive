using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class SnapshotPlaybackTests
{
    private static RoomSnapshot Frame(long tick, float x, float aim = 0) => new(1, "snapshot", tick,
        [new(1, "Pilot", x, 0, aim, 3, 2, 0, false, true)], [new(1, 1, x, 0)]);

    [Fact]
    public void SamplesBetweenPacketsAndFreezesWithoutExtrapolation()
    {
        var playback = new SnapshotPlayback();
        Assert.Null(playback.Sample(0));
        playback.Receive(Frame(0, 0), 0);
        playback.Receive(Frame(2, 20), 2d / 30);
        playback.Receive(Frame(4, 40), 4d / 30);
        Assert.Equal(10, playback.Sample(4d / 30)!.Players[0].X, 3);
        var mid = playback.Sample(4.5 / 30)!;
        Assert.Equal(15, mid.Players[0].X, 3);
        Assert.Equal(15, mid.Shots[0].X, 3);
        Assert.Equal(40, playback.Sample(100)!.Players[0].X);
        playback.Receive(Frame(2, -100), 100);
        Assert.Equal(40, playback.Sample(100)!.Players[0].X);
    }

    [Fact]
    public void AimUsesShortestArcAndDiscreteStateRemainsAuthoritative()
    {
        var playback = new SnapshotPlayback();
        playback.Receive(Frame(0, 0, 3), 0);
        playback.Receive(Frame(6, 60, -3), .2);
        var sample = playback.Sample(.2)!;
        Assert.Equal(MathF.PI, sample.Players[0].Aim, 3);
        playback.Receive(Frame(8, 80) with { Players = [], Shots = [] }, .3);
        Assert.Empty(playback.Sample(.3)!.Players);
        Assert.Empty(playback.Sample(.3)!.Shots);
    }
}
