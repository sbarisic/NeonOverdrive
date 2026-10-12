using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public sealed class ReplayLifecycleTests
{
    private sealed class Clock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(double seconds) => timestamp += (long)(seconds * TimestampFrequency);
    }

    [Theory]
    [InlineData(59.999, true)]
    [InlineData(60, false)]
    [InlineData(60.001, false)]
    public void RecoveryBoundaryPreservesOrReplacesTheEntireRun(double elapsed, bool recovered)
    {
        var clock = new Clock();
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, clock: clock);
        room.Join("Modules");
        Equip(room, Weapon(2, 5, 11));
        var old = Player(room);
        Set(old, "Experience", Room.ExperienceForLevel(10));
        Set(old, "Skills", new HashSet<string> { "strength.1", "bay.2" });
        Set(old, "Hull", 2);
        Set(old, "Shield", 0);
        Set(old, "ShieldAt", 999999L);
        Set(old, "Position", new Vector2(4000, 4000));
        Set(old, "Previous", new Vector2(4000, 4000));
        Set(old, "WeaponReady", new double[] { 999999, 0, 0, 0 });
        var run = store.Get("Modules").RunId;
        var credits = store.Get("Modules").Credits;
        room.Disconnect(1);
        Step(room, 151); // The body has left the world, but recovery remains available.
        clock.Advance(elapsed);
        room.Disconnect(1); // Repeated cleanup must not renew the deadline.
        Assert.Equal(1, room.Join("MODULES"));
        var pilot = room.Snapshot().Players.Single();
        Assert.Equal(recovered ? "recovered" : "fresh", room.JoinKind(1));
        Assert.Equal(recovered ? run : store.Get("Modules").RunId, pilot.RunId);
        Assert.Equal(recovered ? 10 : 1, pilot.Level);
        Assert.Equal(recovered ? 2 : 3, pilot.Hull);
        Assert.Equal(recovered ? 0 : 2, pilot.Shield);
        Assert.Equal(recovered ? credits : 100, store.Get("Modules").Credits);
        Assert.Equal(0, store.Get("Modules").ReplayCount);
        if (recovered)
        {
            Assert.Same(old, Player(room));
            Assert.Equal(new[] { "bay.2", "strength.1" }, pilot.Skills);
            Assert.Equal(999999, ((double[])Get(Player(room), "WeaponReady"))[0]);
            Assert.Equal(2, store.Get("Modules").Weapons[0].Emitter);
            Assert.Equal(new[] { 5, 11 }, store.Get("Modules").Weapons[0].Attachments.Take(2));
        }
        if (!recovered)
        {
            Assert.NotEqual(run, pilot.RunId);
            Assert.Empty(pilot.Skills);
            Assert.Equal(0, pilot.SkillPoints);
            Assert.Equal(4, store.Get("Modules").Owned.Sum());
            Assert.All(
                store.Get("Modules").Weapons,
                w => Assert.All(w.Attachments, a => Assert.Equal(0, a))
            );
            Assert.Equal(new[] { true, false, false, false }, pilot.UnlockedWeapons);
            Assert.All((double[])Get(Player(room), "WeaponReady"), t => Assert.Equal(0, t));
            var port = room.Sector!.Locations.Single(l => l.Id == 10);
            Assert.Equal(port.X + 140, pilot.X);
            Assert.Equal(port.Y, pilot.Y);
        }
    }

    [Fact]
    public void LeaveAlwaysResetsButOnlyDeathsIncreaseStartingCredits()
    {
        var store = new ProfileStore();
        var room = Start(10, store);
        for (var deaths = 0; deaths < 3; deaths++)
        {
            var expected = 100 + deaths * 5;
            Assert.Equal(expected, store.Get("Modules").Credits);
            store.Credit("Modules", 200);
            store.Buy("Modules", 6);
            var previous = store.Get("Modules").RunId;
            Assert.True(room.Leave(1, new(10)));
            Assert.False(room.Leave(1, new(10)));
            Assert.Equal(10, room.LeaveSequence(1));
            Assert.Equal(1, room.Join("modules"));
            Assert.Equal(expected, store.Get("Modules").Credits);
            Assert.Equal(deaths, store.Get("Modules").ReplayCount);
            Assert.Equal(0, store.Get("Modules").Owned[6]);
            Assert.NotEqual(previous, store.Get("Modules").RunId);
            var run = store.Get("Modules").RunId;
            Set(Player(room), "Hull", 0);
            Assert.True(room.Respawn(1, new(1)));
            Assert.False(room.Respawn(1, new(2)));
            Assert.Equal("ok", store.ResetRun("Modules", run));
            Assert.Equal(deaths + 1, store.Get("Modules").ReplayCount);
            Assert.Equal(expected + 5, store.Get("Modules").Credits);
            // Reset session sequences without adding a death or carrying over purchases.
            room.Disconnect(1);
            room.Join("Modules");
        }
    }

    [Fact]
    public void DeathDuringExposureCountsOnceAndPreparedStarterIsReused()
    {
        var clock = new Clock();
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, clock: clock);
        room.Join("Pilot");
        room.Disconnect(1);
        Set(Player(room), "Hull", 0);
        Step(room, 30);
        var prepared = store.Get("Pilot");
        Assert.Equal(1, prepared.ReplayCount);
        clock.Advance(60);
        Assert.Equal(1, room.Join("Pilot"));
        Assert.Equal(prepared.RunId, room.Snapshot().Players.Single().RunId);
        Assert.Equal(105, room.Workshop(1).Credits);
        Assert.Equal(1, room.Workshop(1).ReplayCount);
    }

    [Fact]
    public async Task AdmissionWaitsForOrderedWritesAndOldOperationsCannotChangeNewWallet()
    {
        var store = new ProfileStore();
        var room = Start(profiles: store);
        room.Input(1, new(1, 0, 0, 0, false, false, true));
        room.Step();
        var oldRun = store.Get("Modules").RunId;
        store.BackgroundWrites = true;
        var hold = new TaskCompletionSource();
        _ = store.Schedule(() =>
        {
            hold.Task.GetAwaiter().GetResult();
            return "ok";
        });
        Assert.True(room.Buy(1, new(2, 6)));
        room.Leave(1, new(3));
        Assert.Null(room.TryJoin("Modules", out var error));
        Assert.Equal("pending", error);
        Assert.Empty(room.Snapshot().Players);
        hold.SetResult();
        await store.FlushAsync();
        room.Step();
        Assert.Equal(1, room.Join("Modules"));
        Assert.Equal(100, room.Workshop(1).Credits);
        Assert.Equal(0, room.Workshop(1).Owned[6]);
        Assert.Equal(
            "stale_run",
            store.ForRun("Modules", oldRun, () => store.Credit("Modules", 999))
        );
        Assert.Equal("ok", store.ResetRun("Modules", oldRun));
        Assert.Equal(0, room.Workshop(1).ReplayCount);
        Assert.False((bool)Get(Player(room), "EconomyPending"));
    }

    [Fact]
    public async Task CancelledAdmissionDoesNotResetWalletAndStaleRewardsReleaseTheirReservation()
    {
        var store = new ProfileStore();
        var room = Start(profiles: store);
        store.Credit("Modules", 77);
        room.Leave(1, new(1));
        var oldRun = store.Get("Modules").RunId;
        store.BackgroundWrites = true;
        var hold = new TaskCompletionSource();
        _ = store.Schedule(() =>
        {
            hold.Task.GetAwaiter().GetResult();
            return "ok";
        });
        Assert.Null(room.Join("Modules"));
        room.CancelPendingJoin("Modules");
        hold.SetResult();
        await store.FlushAsync();
        room.Step();
        Assert.Equal(oldRun, store.Get("Modules").RunId);
        Assert.Equal(177, store.Get("Modules").Credits);

        store.BackgroundWrites = false;
        Assert.Equal(1, room.Join("Modules"));
        Set(Player(room), "Position", new Vector2(1000, 800));
        Set(Player(room), "Previous", new Vector2(1000, 800));
        store.BackgroundWrites = true;
        hold = new TaskCompletionSource();
        _ = store.Schedule(() =>
        {
            hold.Task.GetAwaiter().GetResult();
            return "ok";
        });
        Enemy(room, 1000, hull: 0);
        room.Step();
        var drop = ((IList)Get(room, "credits"))[0]!;
        Assert.True((bool)Get(drop, "Pending"));
        Set(Player(room), "Hull", 0);
        Assert.False(room.Respawn(1, new(1)));
        hold.SetResult();
        await store.FlushAsync();
        room.Step();
        Assert.False((bool)Get(drop, "Pending"));
        Assert.Equal(105, store.Get("Modules").Credits);
        Assert.Equal(1, store.Get("Modules").ReplayCount);
    }

    [Fact]
    public void FailedCheckpointCannotReuseAnAlreadyPlayedStarterOnLeave()
    {
        var path = Path.Combine(Path.GetTempPath(), "neon-checkpoint-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new ProfileStore(path);
            var room = Start(profiles: store);
            Set(Player(room), "Hull", 0);
            Step(room, 30);
            Assert.True(store.Get("Modules").RunPrepared);
            Directory.CreateDirectory(path + ".tmp");
            Assert.True(room.Respawn(1, new(1)));
            var playedRun = store.Get("Modules").RunId;
            Directory.Delete(path + ".tmp");
            store.Credit("Modules", 500);
            store.Buy("Modules", 6);
            Directory.CreateDirectory(path + ".tmp");
            room.Leave(1, new(2));
            Directory.Delete(path + ".tmp");
            Assert.Equal(1, room.Join("Modules"));
            Assert.NotEqual(playedRun, store.Get("Modules").RunId);
            Assert.Equal(105, store.Get("Modules").Credits);
            Assert.Equal(0, store.Get("Modules").Owned[6]);
            Assert.Equal(1, store.Get("Modules").ReplayCount);
        }
        finally
        {
            if (Directory.Exists(path + ".tmp"))
                Directory.Delete(path + ".tmp");
            File.Delete(path + ".tmp");
            File.Delete(path);
        }
    }

    [Fact]
    public void FailedFreshSaveIsAtomicAndLegacyProfilesDefaultToZeroReplays()
    {
        var path = Path.Combine(Path.GetTempPath(), "neon-replay-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new ProfileStore(path);
            var room = Start(5, store);
            store.Credit("Modules", 70);
            var before = File.ReadAllText(path);
            room.Leave(1, new(1));
            before = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            Assert.Null(room.TryJoin("Modules", out var error));
            Assert.Equal("save_failed", error);
            Assert.Equal(before, File.ReadAllText(path));
            Assert.Equal(170, store.Get("Modules").Credits);
            Assert.Equal(0, store.Get("Modules").ReplayCount);
            Directory.Delete(path + ".tmp");
            Assert.Equal(1, room.Join("Modules"));
            Assert.Equal(100, store.Get("Modules").Credits);
            Set(Player(room), "Hull", 0);
            Assert.True(room.Respawn(1, new(1)));
            var loaded = new ProfileStore(path);
            Assert.Equal(1, loaded.Get("Modules").ReplayCount);
            Assert.Equal(105, loaded.Get("Modules").StartingCredits);
            var restarted = new Room(false, profiles: loaded);
            Assert.NotNull(restarted.Join("Modules"));
            Assert.Equal(105, restarted.Workshop(1).Credits);
            Assert.Equal(1, restarted.Workshop(1).ReplayCount);
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            foreach (var profile in json)
            {
                profile.Value!.AsObject().Remove("ReplayCount");
                profile.Value.AsObject().Remove("RunPrepared");
            }
            File.WriteAllText(path, json.ToJsonString());
            Assert.Equal(0, new ProfileStore(path).Get("Modules").ReplayCount);
        }
        finally
        {
            if (Directory.Exists(path + ".tmp"))
                Directory.Delete(path + ".tmp");
            File.Delete(path + ".tmp");
            File.Delete(path);
        }
    }
}
