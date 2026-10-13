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
        Assert.InRange(
            frozen.X,
            100,
            100 + Room.Speed * (float)LocalPrediction.MaxPredictionSeconds
        );
        Assert.Equal(frozen.X, prediction.Sample(1000)!.X);
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
    public void OutOfOrderSnapshotsCannotRewindAndGameplayFieldsStayAuthoritative()
    {
        var prediction = Start();
        prediction.Input(1, 0, 0, true, 0);
        var frame = Pilot(130) with
        {
            Hull = 2,
            Shield = 0,
            Experience = 50,
            Level = 3,
            Acceleration = 1500,
        };
        prediction.Receive(frame, 3, .1);
        prediction.Receive(Pilot(900), 2, .2);
        var sample = prediction.Sample(.2)!;
        Assert.Equal(frame.Hull, sample.Hull);
        Assert.Equal(frame.Shield, sample.Shield);
        Assert.Equal(frame.Experience, sample.Experience);
        Assert.Equal(frame.Level, sample.Level);
        Assert.Equal(frame.Acceleration, sample.Acceleration);
        Assert.True(sample.X < 900);
    }

    [Fact]
    public void AccelerationAndCoastingMatchSharedPhysicsAcrossInputBoundaries()
    {
        var prediction = Start();
        prediction.Input(0, -1, 0, false, 0);
        Vector2 position = new(100),
            velocity = Vector2.Zero;
        float heading = 0,
            throttle = 0;
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, new(0, -1), .1f);
        Assert.Equal(position.X, prediction.Sample(.1)!.X, 3);
        prediction.Input(0, 0, 0, false, .1);
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, Vector2.Zero, .1f);
        Assert.Equal(position.X, prediction.Sample(.2)!.X, 3);
        Assert.Equal(.05f, prediction.Sample(.2)!.Throttle, 4);
        prediction.Input(0, 0, 0, true, .2);
        Assert.Equal(position.X, prediction.Sample(.3)!.X, 3);
        Assert.Equal(0, prediction.Sample(.3)!.VelocityX);
    }

    [Fact]
    public void PredictionMatchesServerAndIgnoresExtraPackets()
    {
        var room = new Room(false, sectorEnabled: false);
        var id = room.Join("Pilot")!.Value;
        var p = Start(room.Snapshot().Players[0]);
        for (int seq = 1; seq <= 100; seq++)
        {
            p.Input(-1, -1, 0, false, 0);
            p.SentInput(seq, 0);
            room.Input(id, new(seq, -1, -1, 0, false, false));
        }
        room.Step();
        Assert.Equal(room.Snapshot().Players[0].X, p.Sample(1d / 30)!.X, 3);
        Assert.Equal(room.Snapshot().Players[0].VelocityX, p.Sample(1d / 30)!.VelocityX, 3);
    }

    [Fact]
    public void CurrentVelocityCoastsWithoutInputAndRejectsInvalidSnapshots()
    {
        var p = Start(Pilot() with { VelocityX = 200 });
        Assert.Equal(120f, p.Sample(.1)!.X, 3);
        p.Receive(Pilot() with { VelocityX = float.NaN }, 1, .1);
        Assert.Equal(120f, p.Sample(.1)!.X, 3);
    }

    [Fact]
    public void SteeringPredictionMatchesSharedIntegratorWithEngineBonuses()
    {
        var pilot = Pilot(1000, 1000) with
        {
            MoveSpeed = 360,
            Acceleration = 1800,
            Steering = 2700,
        };
        var prediction = Start(pilot);
        for (var i = 0; i <= 30; i++)
        {
            prediction.Input(.5f, -1, -2, false, i / 60d);
            prediction.SentInput(i + 1, i / 60d);
        }
        Vector2 position = new(1000),
            velocity = Vector2.Zero;
        float heading = 0,
            throttle = 0;
        for (var i = 0; i < 15; i++)
            Movement.Steer(
                ref position,
                ref velocity,
                ref heading,
                ref throttle,
                new(.5f, -1),
                1f / 30,
                360,
                1800,
                2700
            );
        var sampled = prediction.Sample(.5)!;
        Assert.InRange(Vector2.Distance(position, new(sampled.X, sampled.Y)), 0, .02f);
        Assert.Equal(heading, sampled.Heading, 4);
        Assert.Equal(throttle, sampled.Throttle, 4);
        Assert.Equal(-2, sampled.Aim);
    }

    [Fact]
    public void MouseRefreshDoesNotDiscardAccelerationHistory()
    {
        var p = Start();
        for (int i = 0; i <= 500; i++)
            p.Input(0, -1, i / 1000f, false, i / 1000d);
        Assert.InRange(p.Sample(.5)!.X, 101.1f, 101.25f);
        Assert.InRange(p.HistoryCount, 1, 4);
    }
}
