using System.Numerics;
using System.Text.Json;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class LivingSectorTests
{
    [Fact]
    public void StationBodiesStopCruiseAndLeaveReachableDockingApproaches()
    {
        var sector = SectorDefinition.Generate(123);
        var geometry = new SectorCollision(sector);
        foreach (var station in sector.Locations.Where(l => l.Kind == "station"))
        foreach (var chassis in ChassisCatalog.Items)
        {
            var from = station.Position + new Vector2(600, 0);
            var velocity = new Vector2(-2000, 0);
            var result = geometry.Move(
                from,
                station.Position - new Vector2(600, 0),
                chassis.Radius,
                ref velocity
            );
            Assert.InRange(Vector2.Distance(result, station.Position), 90 + chassis.Radius, 180);
            Assert.Equal(0, velocity.X);
            Assert.NotEmpty(
                new SectorNavigation(sector, geometry).Route(from, station.Position, chassis.Radius)
            );
        }
    }

    private static RunCheckpoint Run() =>
        new(60, [], 2, 1, .25f, .5f, [4, 3, 2, 1], new long[4], new long[4], 100, 200);

    [Fact]
    public void ChassisPurchasePreservesEquipmentDamageAndCooldownsAndIsRunBound()
    {
        var store = new ProfileStore();
        store.Ensure("P");
        store.Credit("P", 2000);
        var before = store.Get("P");
        Assert.Equal("wrong_station", store.BuyChassis("P", 6, 10, Run()));
        Assert.Equal("ok", store.BuyChassis("P", 6, 12, Run()));
        var after = store.Get("P");
        Assert.Equal(6, after.Chassis);
        Assert.Equal(before.Credits - 1200, after.Credits);
        Assert.Equal(
            before.Equipment!.Weapons.Select(w => w.Id),
            after.Equipment!.Weapons.Select(w => w.Id)
        );
        Assert.Equal(2, after.Run!.Hull);
        Assert.Equal(.25f, after.Run.HullFraction);
        Assert.Equal(100, after.Run.ShieldDelay);
        Assert.Equal(Run().WeaponCooldown, after.Run.WeaponCooldown);
        Assert.Equal("already_equipped", store.BuyChassis("P", 6, 12, Run()));
        Assert.Equal("ok", store.StartRun("P", after.RunId));
        Assert.Equal(0, store.Get("P").Chassis);
    }

    [Fact]
    public void FailedChassisPurchasePublishesNeitherWalletNorHull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new ProfileStore(path);
            store.Ensure("P");
            store.Credit("P", 2000);
            var before = JsonSerializer.Serialize(store.Get("P"));
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Equal("save_failed", store.BuyChassis("P", 6, 12, Run()));
            Assert.Equal(before, JsonSerializer.Serialize(store.Get("P")));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void TieredCopiesCannotBeSubstitutedWithCheaperComponents()
    {
        var store = new ProfileStore();
        store.Ensure("P");
        var seeker = ModuleLevels.Key(14, 3);
        store.GrantModule("P", 14);
        var equipment = store.Get("P").Equipment!;
        var weapon = equipment.Weapons[0];
        var parts = new int[8];
        parts[7] = seeker;
        EquipmentMessage Draft() =>
            new(
                1,
                "apply",
                store.Get("P").Equipment!.Revision,
                [new(weapon.Id, parts)],
                equipment.Bays,
                equipment.Ship
            );
        Assert.Equal("invalid_inventory", store.ApplyEquipment("P", Draft(), 1));
        store.GrantModule("P", seeker);
        Assert.Equal("ok", store.ApplyEquipment("P", Draft(), 1));
        var after = store.Get("P");
        Assert.Equal(1, after.Equipment!.Loose[14]);
        Assert.Equal(0, after.Equipment.Loose[seeker]);
        Assert.Equal(seeker, after.Weapons[0].Attachments[7]);
        Assert.Equal(2, after.Owned[14]);
        Assert.Equal("invalid_item", store.Buy("P", seeker));
    }

    [Fact]
    public void LevelsImproveSeekWithoutIncreasingSplitCount()
    {
        Assert.Equal(112, ModuleLevels.Catalog.Length);
        Assert.Equal(.18f, ModuleCatalog.Get(ModuleLevels.Key(14, 3)).Value, 4);
        Assert.Equal(800, ModuleCatalog.Get(ModuleLevels.Key(14, 3)).Radius);
        Assert.Equal(6, ModuleCatalog.Get(ModuleLevels.Key(13, 4)).Value);
        Assert.Equal(5, ModuleCatalog.Get(ModuleLevels.Key(8, 4)).Value);
        Assert.Equal(4, ModuleLevels.Roll(8, .99f));
        Assert.False(ModuleLevels.Valid(32));
        Assert.False(ModuleLevels.Valid(128));
    }

    [Fact]
    public void TradeRetriesDoNotDuplicateItemsOrCharges()
    {
        var store = new ProfileStore();
        store.Ensure("P");
        store.Credit("P", 1000);
        var key = ModuleLevels.Key(14, 3);
        Assert.Equal("ok", store.TradeItem("P", "convoy-1", 270, key));
        Assert.Equal("ok", store.TradeItem("P", "convoy-1", 270, key));
        Assert.Equal(830, store.Get("P").Credits);
        Assert.Equal(1, store.Get("P").Equipment!.Loose[key]);
    }

    [Fact]
    public void PreciseHullContactsIgnoreTheEnclosingCircleCorners()
    {
        Assert.Null(ChassisCollision.Trace(0, new(16, -40), new(16, 40), 0, 1));
        Assert.NotNull(ChassisCollision.Trace(0, new(-40, 0), new(40, 0), 0, 1));
        Assert.NotNull(ChassisCollision.Trace(6, new(-80, 0), new(80, 0), 0, 1));
        Assert.Equal(7, ChassisCatalog.Items.Length);
        Assert.All(ChassisCatalog.Items, c => Assert.Equal(4, c.Hardpoints.Length));
    }

    [Fact]
    public void RoomPublishesNewBaseSpeedAndSplitTerritoryWithoutGrantingStructures()
    {
        var room = new Room(false, seed: 12);
        room.Join("P");
        Assert.Equal(220, room.Snapshot().Players[0].MoveSpeed);
        var world = room.SectorSnapshot(1)!.World!;
        Assert.Equal(8, world.Territory.Length);
        Assert.Equal(4, world.Territory.Count(t => t.Owner == "friendly"));
        Assert.DoesNotContain(world.Territory, t => t.Id == 10);
    }

    [Fact]
    public void RaceRecordsSurviveFreshRunButEquipmentDoesNot()
    {
        var store = new ProfileStore();
        store.Ensure("P");
        Assert.Equal("ok", store.SaveRace("P", new("course", "sprint", 40, [0, 1, 2])));
        Assert.Equal("ok", store.SaveRace("P", new("course", "sprint", 50, [0, 9, 9])));
        store.GrantModule("P", 14);
        store.ResetRun("P", store.Get("P").RunId);
        var profile = store.Get("P");
        Assert.Equal(40, Assert.Single(profile.RaceRecords!).Seconds);
        Assert.Equal(0, profile.Owned[14]);
    }

    [Fact]
    public void CruiseUsesInstalledDriveAndCancelsOnMenu()
    {
        var room = new Room(false, seed: 12);
        room.Join("P");
        var p = Player(room);
        Set(p, "GearTiers", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 });
        Assert.True(room.SectorAction(1, new(1, "cruise")));
        for (var i = 0; i < 62; i++)
        {
            room.Input(1, new(i + 2, 0, -1, 0, false, false));
            room.Step();
        }
        Assert.Equal("active", room.Snapshot().Players[0].CruiseState);
        room.Input(1, new(100, 0, 0, 0, false, true));
        room.Step();
        Assert.Equal("cooldown", room.Snapshot().Players[0].CruiseState);
        Assert.Equal(0, room.Snapshot().Players[0].Throttle);
    }
}
