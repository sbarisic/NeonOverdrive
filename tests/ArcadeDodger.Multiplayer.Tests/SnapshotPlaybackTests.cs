using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class SnapshotPlaybackTests
{
    private static RoomSnapshot Frame(long tick, float x, float aim = 0) =>
        new(
            1,
            "snapshot",
            tick,
            [new(1, "Pilot", x, 0, aim, 3, 2, 0, false, true)],
            [new(1, 1, x, 0)]
        );

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
        Assert.NotEmpty(playback.Sample(.3)!.Shots); // Still inside the buffered interval.
        Assert.Empty(playback.Sample(1)!.Shots);
    }

    [Fact]
    public void CollisionEndpointPlaysThroughBeforeImpactEvenWhenNewestFrameRemovedShot()
    {
        var playback = new SnapshotPlayback();
        playback.Receive(Frame(0, 0), 0);
        playback.Receive(Frame(2, 86), 2d / 30);
        var hit = new CombatEvent(1, 1, 1, 120, 0, 3, 1);
        playback.Receive(Frame(4, 0) with { Shots = [], CombatEvents = [hit] }, 4d / 30);
        var approaching = playback.Sample(4d / 30)!;
        Assert.Equal(43, Assert.Single(approaching.Shots).X, 3);
        Assert.Empty(approaching.CombatEvents!);
        var near = playback.Sample(5.5 / 30)!;
        Assert.Equal(103, Assert.Single(near.Shots).X, 3);
        Assert.Empty(near.CombatEvents!);
        var contact = playback.Sample(6d / 30)!;
        Assert.Empty(contact.Shots);
        Assert.Single(contact.CombatEvents!);
        Assert.Empty(playback.Sample(100)!.Shots);
    }

    [Fact]
    public void PiercingContactDoesNotBecomeAProjectileEndpoint()
    {
        var playback = new SnapshotPlayback();
        playback.Receive(Frame(0, 0), 0);
        playback.Receive(
            Frame(2, 86) with
            {
                CombatEvents = [new(1, 1, 3, 50, 0, 1, 1, 1, 0)],
            },
            2d / 30
        );
        playback.Receive(Frame(4, 0) with { Shots = [] }, 4d / 30);
        Assert.Equal(86, Assert.Single(playback.Sample(5.5 / 30)!.Shots).X);
    }

    [Fact]
    public void ServerRecordsActualCollisionAndDestructionWithoutAddingDamage()
    {
        var room = WorkshopRig.Start();
        WorkshopRig.Enemy(room, 1100, hull: 1);
        WorkshopRig.Fire(room, 5);
        var frame = room.Snapshot();
        var hit = Assert.Single(frame.CombatEvents!, e => e.Kind == 1);
        Assert.InRange(hit.X, 1070, 1100);
        Assert.Single(frame.CombatEvents!, e => e.Kind == 2);
        Assert.Empty(frame.Enemies!);
        WorkshopRig.Step(room, 12);
        Assert.DoesNotContain(room.Snapshot().CombatEvents!, e => e.Id == hit.Id);
    }
}
