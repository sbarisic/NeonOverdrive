using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ArcadeDodger.Server;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class LivingWorldTests
{
    [Fact]
    public void SharedRaceCountdownHoldsShipsAndRetiresShotsBeforeTheyCanHitLater()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        room.Join("B");
        var start = room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(400, 0);
        At(room, 1, start);
        At(room, 2, start + new Vector2(0, 30));
        Assert.Equal("ok", Action(room, 1, "race_join", 0));
        Assert.Equal("ok", Action(room, 2, "race_join", 0));
        PrepareRaces(room);
        start = new(room.Snapshot().Players[0].X, room.Snapshot().Players[0].Y);
        Assert.Equal(
            room.SectorSnapshot(1)!.World!.Race!.Countdown,
            room.SectorSnapshot(2)!.World!.Race!.Countdown
        );
        for (var i = 0; i < 60; i++)
        {
            room.Input(1, new(100 + i, 1, -1, 1, true, false));
            room.Step();
        }
        Assert.Equal(start.X, room.Snapshot().Players[0].X);
        Assert.Equal(start.Y, room.Snapshot().Players[0].Y);
        Assert.Equal(0, room.Snapshot().Players[0].Throttle);
        Assert.Empty(room.Snapshot().Shots);
        Assert.Equal("ok", Action(room, 1, "race_stop"));
        Assert.False(room.Snapshot().Players[0].Protected);
    }

    [Fact]
    public void PoliceWarnThenConfiscateWithCappedFineAndFlightExpiresOutOfDetection()
    {
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, seed: 12);
        room.Join("A");
        Call(room, "SpawnCivilian", NpcRole.Police, 6);
        var police = room.Snapshot().Enemies!.Single(e => e.Role == "police");
        At(room, 1, new(police.X, police.Y));
        var state = Call(room, "Living", Player(room))!;
        Set(state, "IllegalCargo", 1);
        Set(state, "Job", "smuggling");
        Set(state, "JobState", "active");
        Set(room, "<Tick>k__BackingField", 10L);
        Call(room, "StepPolice");
        Assert.Equal("scanning", Get(state, "Police"));
        Set(room, "<Tick>k__BackingField", 100L);
        Call(room, "StepPolice");
        Assert.Equal("decision", Get(state, "Police"));
        Assert.Equal("fine_required", Action(room, 1, "police_surrender"));
        store.Debit("A", 93);
        Action(room, 1, "police_pay");
        Assert.Equal(0, store.Get("A").Credits);
        Assert.Equal(0, Get(state, "IllegalCargo"));
        Assert.Equal("failed", Get(state, "JobState"));
        Set(state, "IllegalCargo", 1);
        Set(state, "Police", "decision");
        Assert.Equal("ok", Action(room, 1, "police_refuse"));
        At(room, 1, new(1000, 14000));
        Set(room, "<Tick>k__BackingField", 1900L);
        Call(room, "StepPolice");
        Assert.Equal("", Get(state, "Police"));
    }

    [Fact]
    public void CargoDeliveryRetriesFailedWriteWithoutDuplicateMoneyOrConsumption()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new ProfileStore(path);
            var room = new Room(false, profiles: store, seed: 12);
            room.Join("A");
            var at = room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(200, 0);
            Call(room, "AddCargo", "container", at, 30, 900L * 30);
            var cargo = ((IList)Get(room, "worldCargo"))
                .Cast<object>()
                .Single(c => (string)Get(c, "Kind") == "container");
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Call(room, "DeliverCargo", Player(room), cargo);
            Assert.Equal(100, store.Get("A").Credits);
            Assert.Contains(cargo, ((IList)Get(room, "worldCargo")).Cast<object>());
            Call(room, "DeliverCargo", Player(room), cargo);
            Call(room, "DeliverCargo", Player(room), cargo);
            Assert.Equal(130, store.Get("A").Credits);
            Assert.DoesNotContain(cargo, ((IList)Get(room, "worldCargo")).Cast<object>());
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void DerelictResolutionCannotBeRepeatedAfterDeath()
    {
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, seed: 12);
        room.Join("A");
        var wreck = ((IList)Get(room, "worldCargo"))[0]!;
        var id = (int)Get(wreck, "Id");
        At(room, 1, (Vector2)Get(wreck, "Position"));
        var choice = new[] { "reactor", "cargo", "navigation" }[id % 3];
        Assert.Equal("ok", Action(room, 1, "wreck_" + choice, id));
        var wallet = store.Get("A").Credits;
        Assert.Equal("already_searched", Action(room, 1, "wreck_" + choice, id));
        Assert.Equal(wallet, store.Get("A").Credits);
        store.ResetRun("A", store.Get("A").RunId);
        Set(Player(room), "RunId", store.Get("A").RunId);
        Assert.Equal("already_searched", Action(room, 1, "wreck_" + choice, id));
        Assert.Equal(105, store.Get("A").Credits);
    }

    [Fact]
    public void TerritoryRepairPreservesFractionalDamageAndWaitsAfterAHit()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        At(room, 1, room.Sector!.Locations.Single(l => l.Id == 11).Position);
        Set(Player(room), "Hull", 3);
        Set(Player(room), "HullFraction", .5f);
        Set(Player(room), "LastDamage", 100L);
        Set(room, "<Tick>k__BackingField", 399L);
        Call(room, "StepTerritory");
        Assert.Equal(.5f, Get(Player(room), "HullFraction"));
        Set(room, "<Tick>k__BackingField", 400L);
        for (var i = 0; i < 30; i++)
            Call(room, "StepTerritory");
        Assert.Equal(.47f, (float)Get(Player(room), "HullFraction"), 3);
        Assert.Equal(3, Get(Player(room), "Hull"));
    }

    private static object? Call(Room room, string method, params object[] args) =>
        typeof(Room)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(room, args);

    private static string Action(Room room, int player, string action, int target = 0)
    {
        room.SectorAction(
            player,
            new((int)Get(Player(room, player), "LastReceived") + 1, action, target)
        );
        return room.SectorSnapshot(player)!.Pilot!.Receipt!.Result;
    }

    private static void At(Room room, int id, Vector2 point)
    {
        Set(Player(room, id), "Position", point);
        Set(Player(room, id), "Previous", point);
    }

    [Fact]
    public async Task ConcurrentConvoyBuyersReserveOneActualItemUntilDurableCompletion()
    {
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, seed: 12);
        room.Join("A");
        room.Join("B");
        store.Credit("A", 10000);
        store.Credit("B", 10000);
        Call(room, "SpawnCivilian", NpcRole.Trader, 0);
        var trader = room.Snapshot().Enemies!.Single(e => e.Role == "trader");
        foreach (var id in new[] { 1, 2 })
        {
            At(room, id, new(trader.X, trader.Y));
            Assert.Equal("ok", Action(room, id, "radio_trade", trader.Id));
        }
        var item = room.SectorSnapshot(1)!.World!.Stock[0];
        store.BackgroundWrites = true;
        var release = new TaskCompletionSource();
        _ = store.Schedule(() =>
        {
            release.Task.GetAwaiter().GetResult();
            return "ok";
        });
        try
        {
            Assert.Equal("pending", Action(room, 1, "convoy_buy", item.Id));
            Assert.Equal("stock_reserved", Action(room, 2, "convoy_buy", item.Id));
        }
        finally
        {
            release.SetResult();
        }
        await store.FlushAsync();
        Call(room, "CompleteTransactions");
        Assert.Equal(10100 - item.Price, store.Get("A").Credits);
        Assert.Equal(10100, store.Get("B").Credits);
        Assert.Equal(1, store.Get("A").Equipment!.Loose[item.Module]);
        Assert.DoesNotContain(room.SectorSnapshot(1)!.World!.Stock, s => s.Id == item.Id);
        Assert.Equal("stock_unavailable", Action(room, 2, "convoy_buy", item.Id));
    }

    [Fact]
    public void RemoteRadioNeverCloaksAndCloseTradingEndsWhenTargetDies()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        Call(room, "SpawnCivilian", NpcRole.Trader, 0);
        var trader = room.Snapshot().Enemies!.Single(e => e.Role == "trader");
        At(room, 1, new(trader.X + 500, trader.Y));
        Assert.Equal("ok", Action(room, 1, "radio", trader.Id));
        Assert.False(room.Snapshot().Players[0].Editing);
        Assert.Equal("out_of_range", Action(room, 1, "radio_trade", trader.Id));
        At(room, 1, new(trader.X, trader.Y));
        Assert.Equal("ok", Action(room, 1, "radio_trade", trader.Id));
        Assert.True(room.Snapshot().Players[0].Editing);
        var entity = ((IList)Get(room, "enemies"))
            .Cast<object>()
            .Single(e => (int)Get(e, "Id") == trader.Id);
        Set(entity, "Hull", 0);
        Call(room, "StepContacts");
        Assert.False(room.Snapshot().Players[0].Editing);
    }

    [Fact]
    public void TowHasOneOwnerAndDisconnectReleasesIt()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        room.Join("B");
        var at = room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(500, 0);
        Call(room, "AddCargo", "container", at, 30, 900L * 30);
        At(room, 1, at);
        At(room, 2, at);
        var cargo = room.SectorSnapshot(1)!.World!.Cargo.Single(c => c.Kind == "container");
        Assert.Equal("ok", Action(room, 1, "tow", cargo.Id));
        Assert.Equal("tow_unavailable", Action(room, 2, "tow", cargo.Id));
        Assert.Equal(132, room.Snapshot().Players[0].MoveSpeed, 3);
        room.Disconnect(1);
        Assert.Equal("ok", Action(room, 2, "tow", cargo.Id));
    }

    [Fact]
    public void DockedOccupantsBlockCaptureIndefinitelyAndHostileStationDeniesNewDocks()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        var station = room.Sector!.Locations.Single(l => l.Id == 11);
        var attacker = Enemy(room, station.X + 100, station.Y, id: 900, hull: 100);
        Set(Player(room), "Docked", 11);
        At(room, 1, station.Position);
        for (var i = 0; i < 1000; i++)
            Call(room, "StepTerritory");
        Assert.Equal(
            "friendly",
            room.SectorSnapshot(1)!.World!.Territory.Single(t => t.Id == 11).Owner
        );
        Set(Player(room), "Docked", 0);
        At(room, 1, new(1000, 1000));
        for (var i = 0; i < 900; i++)
            Call(room, "StepTerritory");
        Assert.Equal(
            "hostile",
            room.SectorSnapshot(1)!.World!.Territory.Single(t => t.Id == 11).Owner
        );
        At(room, 1, station.Position);
        Assert.Equal("station_contested", Action(room, 1, "dock", 11));
    }

    [Fact]
    public void TerritoryCellsCoverSectorAndMatchNearestSite()
    {
        var sector = SectorDefinition.Generate(12);
        var cells = TerritoryGeometry.Cells(sector);
        Assert.Equal(8, cells.Count);
        var area = 0d;
        foreach (var (id, polygon) in cells)
        {
            double signed = 0;
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Length];
                signed += (double)a.X * b.Y - (double)b.X * a.Y;
                Assert.InRange(a.X, -.01f, sector.Width + .01f);
                Assert.InRange(a.Y, -.01f, sector.Height + .01f);
            }
            area += Math.Abs(signed) / 2;
        }
        Assert.InRange(
            area,
            sector.Width * sector.Height - 100,
            sector.Width * sector.Height + 100
        );
    }

    [Fact]
    public void RaceEntryRetiresOffenseAndBlocksOtherInteractions()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        At(room, 1, room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(400, 0));
        room.Input(1, new(1, 0, 0, 0, true, false));
        room.Step();
        Assert.NotEmpty(room.Snapshot().Shots);
        Assert.Equal("ok", Action(room, 1, "race_join", 0));
        PrepareRaces(room);
        Assert.True(room.Snapshot().Players[0].Protected);
        Assert.Equal("finish_race_first", Action(room, 1, "mission_smuggling"));
        Assert.All(
            ((IList)Get(room, "shots")).Cast<object>(),
            s => Assert.True((bool)Get(s, "Cancelled"))
        );
        Assert.Equal("ok", Action(room, 1, "race_stop"));
        Assert.False(room.Snapshot().Players[0].Protected);
    }

    [Fact]
    public void SocketEncoderSendsUnchangedStockAndGhostOnlyOnce()
    {
        var room = new Room(false, seed: 12);
        room.Join("A");
        At(room, 1, room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(400, 0));
        Action(room, 1, "race_join", 0);
        var encoder = new SnapshotEncoder();
        var frame = room.Snapshot() with { Sector = room.SectorSnapshot(1) };
        using var first = JsonDocument.Parse(encoder.Encode(frame));
        using var second = JsonDocument.Parse(encoder.Encode(frame));
        Assert.Equal(
            JsonValueKind.Array,
            first
                .RootElement.GetProperty("sector")
                .GetProperty("world")
                .GetProperty("race")
                .GetProperty("course")
                .ValueKind
        );
        Assert.Equal(
            JsonValueKind.Null,
            second
                .RootElement.GetProperty("sector")
                .GetProperty("world")
                .GetProperty("race")
                .GetProperty("course")
                .ValueKind
        );
    }
}
