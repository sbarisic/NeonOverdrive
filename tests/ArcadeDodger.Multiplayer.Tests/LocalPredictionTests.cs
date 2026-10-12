using System.Numerics;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class LocalPredictionTests
{
    private static PlayerSnapshot Pilot(float x = 100, float y = 100, int ack = 0) =>
        new(1, "Pilot", x, y, 0, 3, 2, ack, false, true);

    private static LocalPrediction Start(PlayerSnapshot? player = null)
    {
        var prediction = new LocalPrediction();
        prediction.Receive(player ?? Pilot(), 0, 0);
        return prediction;
    }

    [Fact]
    public void MovesAtRenderTimeNotPacketFrequencyAndStaleIntentStops()
    {
        var a = Start();
        var b = Start();
        a.Input(1, 0, 0, false, 0);
        b.Input(1, 0, 0, false, 0);
        for (var i = 1; i <= 100; i++) b.SentInput(i, 0);
        Assert.Equal(130, a.Sample(.1)!.X, 3);
        Assert.Equal(a.Sample(.1)!.X, b.Sample(.1)!.X);
        Assert.Equal(160, a.Sample(.2)!.X, 3);
        Assert.Equal(180, a.Sample(100)!.X, 3); // 8/30 second held-intent timeout
        Assert.Equal(a.Sample(100)!.X, b.Sample(100)!.X);
    }

    [Fact]
    public void SameAckAcrossSeveralTicksAdvancesTimeInsteadOfReplayingPacketAsStep()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, false, 0);
        prediction.SentInput(1, 0);
        prediction.Receive(Pilot(110, ack: 1), 1, .1);
        Assert.Equal(130, prediction.Sample(.1)!.X, 3);
        prediction.Receive(Pilot(140, ack: 1), 4, .2);
        Assert.Equal(160, prediction.Sample(.2)!.X, 3);
        Assert.Equal(175, prediction.Sample(.25)!.X, 3);
        prediction.Receive(Pilot(170, ack: 1), 7, .3);
        Assert.Equal(180, prediction.Sample(.3)!.X, 3);
    }

    [Fact]
    public void LatestIntentBurstMatchesRoomAndDoesNotMultiplyMovement()
    {
        var room = new Room(false,sectorEnabled:false);
        var id = room.Join("Pilot")!.Value;
        var initial = room.Snapshot().Players.Single();
        var prediction = Start(initial);
        for (var seq = 1; seq <= 100; seq++)
        {
            Assert.True(room.Input(id, new(seq, 1, 0, 0, false, false)));
            prediction.Input(1, 0, 0, false, 0);
            prediction.SentInput(seq, 0);
        }
        room.Step();
        var authoritative = room.Snapshot().Players.Single();
        Assert.Equal(100, authoritative.Ack);
        Assert.Equal(initial.X + Room.Speed / Room.TickRate, authoritative.X, 3);
        prediction.Receive(authoritative, room.Tick, .1);
        Assert.Equal(initial.X + 30, prediction.Sample(.1)!.X, 3);
    }

    [Fact]
    public void ReplayUsesIntentDurationsAndKeyReleaseImmediatelyStops()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, false, 0);
        prediction.SentInput(1, 0);
        prediction.Input(0, 1, MathF.PI / 2, false, .1);
        prediction.Input(0, 0, MathF.PI / 2, false, .2);
        prediction.Receive(Pilot(110, ack: 1), 1, .3);
        // Reconstruct .033 -> .1 right, .1 -> .2 down, then neutral.
        var sample = prediction.Sample(.9)!; // smoothing has settled
        Assert.Equal(130, sample.X, 2);
        Assert.Equal(130, sample.Y, 2);
        Assert.Equal(MathF.PI / 2, sample.Aim, 3);
        Assert.Equal(sample.X, prediction.Sample(10)!.X, 2);
        Assert.Equal(sample.Y, prediction.Sample(10)!.Y, 2);
    }

    [Fact]
    public void NormalizesClampsAndRejectsNonFiniteOrBackwardsInput()
    {
        var prediction = Start(Pilot(Room.Width - 1));
        prediction.Input(20, 20, MathF.Tau * 2, false, 0);
        var sample = prediction.Sample(.1)!;
        Assert.Equal(Room.Width - Room.Radius, sample.X);
        Assert.Equal(100 + 30 / MathF.Sqrt(2), sample.Y, 3);
        prediction.Input(float.NaN, 0, 0, false, .1);
        prediction.Input(0, 0, float.PositiveInfinity, false, .1);
        prediction.Input(0, 0, 0, false, -1);
        prediction.Receive(Pilot(float.NaN), 1, .1);
        Assert.Equal(sample.Y, prediction.Sample(.1)!.Y, 3);
        Assert.True(float.IsFinite(prediction.Sample(double.NaN)!.X));
    }

    [Fact]
    public void SmallCorrectionsSmoothButLargeCorrectionsAndTeleportsSnap()
    {
        var prediction = Start();
        prediction.Receive(Pilot(110), 1, .1);
        Assert.Equal(100, prediction.Sample(.1)!.X, 3);
        Assert.InRange(prediction.Sample(.2)!.X, 108, 110);
        Assert.InRange(prediction.Sample(.7)!.X, 109.99f, 110);
        prediction.Receive(Pilot(300), 2, .7);
        Assert.Equal(300, prediction.Sample(.7)!.X);
        prediction.Input(1, 0, 0, false, .7);
        prediction.Receive(Pilot(800), 3, .8);
        Assert.Equal(800, prediction.Sample(.8)!.X);
        Assert.Equal(800, prediction.Sample(1)!.X); // teleport discards held intent
    }

    [Fact]
    public void BoundsTimeAndHistoryEvenWithLiveIntentWithoutSnapshots()
    {
        var prediction = Start();
        for (var i = 0; i < 1000; i++)
        {
            var time = i / 2000d;
            prediction.Input(1, 0, 0, false, time);
            prediction.SentInput(i + 1, time);
        }
        Assert.InRange(prediction.HistoryCount, 1, LocalPrediction.MaxHistory);
        Assert.InRange(prediction.SentCount, 1, LocalPrediction.MaxHistory);
        var frozen = prediction.Sample(1)!;
        Assert.InRange(frozen.X, 100, 100 + Room.Speed * (float)LocalPrediction.MaxPredictionSeconds);
        Assert.Equal(frozen.X, prediction.Sample(1000)!.X);
    }

    [Fact]
    public void HighFrequencyRefreshAndMouseAimDoNotDiscardHeldMovement()
    {
        var prediction = Start();
        for (var i = 0; i <= 500; i++)
            prediction.Input(1, 0, i / 1000f, false, i / 1000d);
        // A half-second of holding right is still a full 150 units, regardless of event rate.
        Assert.Equal(250, prediction.Sample(.5)!.X, 3);
        Assert.Equal(.5f, prediction.Sample(.5)!.Aim, 3);
        Assert.InRange(prediction.HistoryCount, 1, 4);
    }

    [Fact]
    public void RefreshCompressionPreservesARealInputTimeoutAndDirectionChange()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, false, 0);
        prediction.Input(1, 0, 0, false, .4); // input actually stopped for longer than the timeout
        prediction.Input(0, 1, 0, false, .45);
        Assert.Equal(195, prediction.Sample(.5)!.X, 3);
        Assert.Equal(115, prediction.Sample(.5)!.Y, 3);
    }

    [Fact]
    public void DeathRespawnDisconnectRemovalAndResetDiscardOldIntent()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, true, 0);
        prediction.Receive(Pilot(120) with { Hull = 0, Experience = 42 }, 1, .1);
        Assert.Equal(120, prediction.Sample(.1)!.X);
        Assert.Equal(0, prediction.Sample(.3)!.Hull);
        Assert.Equal(42, prediction.Sample(.3)!.Experience);
        prediction.Input(1, 0, 0, true, .3);
        prediction.Receive(Pilot(140) with { Life = 1 }, 2, .4);
        Assert.Equal(140, prediction.Sample(.6)!.X);
        prediction.Input(1, 0, 0, false, .6);
        prediction.Receive(Pilot(145) with { Life = 1, Connected = false }, 3, .7);
        Assert.Equal(145, prediction.Sample(.9)!.X);
        Assert.Equal(0, prediction.HistoryCount);
        Assert.Equal(0, prediction.SentCount);
        prediction.Reset();
        Assert.Null(prediction.Sample(1));
        prediction.Receive(Pilot(200), 0, 0); // a fresh room can start at tick zero
        Assert.Equal(200, prediction.Sample(.2)!.X);
    }

    [Fact]
    public void LifeGenerationSnapsEvenWhenDeathSnapshotWasMissed()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, false, 0);
        prediction.Receive(Pilot(105) with { Life = 1 }, 2, .1);
        Assert.Equal(105, prediction.Sample(.1)!.X);
        Assert.Equal(105, prediction.Sample(.2)!.X);
    }

    [Fact]
    public void DashUsesLockedDirectionCooldownAndSnapshotRemainingTicks()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, true, 0);
        Assert.Equal(190, prediction.Sample(.1)!.X, 3);
        prediction.Input(0, 1, MathF.PI / 2, true, .1);
        Assert.Equal(280, prediction.Sample(.2)!.X, 3); // direction remains locked
        Assert.Equal(100, prediction.Sample(.2)!.Y, 3);
        Assert.Equal(130, prediction.Sample(.3)!.Y, 3); // normal movement after dash

        var resumed = Start(Pilot() with { DashTicks = 3, DashX = 1, DashCooldown = 80 });
        Assert.Equal(160, resumed.Sample(.1)!.X, 3); // authoritative dash needs no new key event
        resumed.Input(0, 1, MathF.PI / 2, true, 0);
        Assert.Equal(160, resumed.Sample(.1)!.X, 3); // two remaining server steps
        Assert.Equal(110, resumed.Sample(.1)!.Y, 3);
        var cooling = Start(Pilot() with { DashCooldown = 10 });
        cooling.Input(1, 0, 0, true, 0);
        Assert.Equal(130, cooling.Sample(.1)!.X, 3);
    }

    [Fact]
    public void StationaryDashUsesAimAndExplicitNeutralStopsLocalPresentation()
    {
        var prediction = Start();
        prediction.Input(0, 0, MathF.PI / 2, true, 0);
        Assert.Equal(100, prediction.Sample(.1)!.X, 3);
        Assert.Equal(190, prediction.Sample(.1)!.Y, 3);
        // Local menu/focus-neutral intent stops presentation immediately, then reconciles.
        prediction.Input(0, 0, MathF.PI / 2, false, .1);
        Assert.Equal(190, prediction.Sample(.2)!.Y, 3);
    }

    [Fact]
    public void OutOfOrderSnapshotsCannotRewindAndGameplayFieldsStayAuthoritative()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, true, 0);
        var frame = Pilot(130) with { Hull = 2, Shield = 0, Experience = 50, Level = 3, DashCooldown = 60 };
        prediction.Receive(frame, 3, .1);
        prediction.Receive(Pilot(900), 2, .2);
        var sample = prediction.Sample(.2)!;
        Assert.Equal(frame.Hull, sample.Hull);
        Assert.Equal(frame.Shield, sample.Shield);
        Assert.Equal(frame.Experience, sample.Experience);
        Assert.Equal(frame.Level, sample.Level);
        Assert.Equal(frame.DashCooldown, sample.DashCooldown);
        Assert.True(sample.X < 900);
    }
    [Fact]
    public void RoomExportsRemainingDashStateForPrediction()
    {
        var room = new Room(false,sectorEnabled:false);
        var id = room.Join("Pilot")!.Value;
        room.Input(id, new(1, 0, -1, 0, false, true));
        room.Step();
        var frame = room.Snapshot().Players.Single();
        Assert.Equal(Room.DashDuration, frame.DashTicks);
        Assert.Equal(Room.DashCooldownTicks, frame.DashCooldown);
        Assert.Equal(0, frame.DashX);
        Assert.Equal(-1, frame.DashY);
        room.Step();
        Assert.Equal(Room.DashDuration - 1, room.Snapshot().Players.Single().DashTicks);
    }
}
