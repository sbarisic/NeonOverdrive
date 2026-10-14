using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class SurfaceExpansionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PlanetCoverStopsEachEmitterAndClearSpaceStillHits(int emitter)
    {
        var room = new Room(false, seed: 123);
        room.Join("Cover");
        ((IList)Get(room, "enemies")).Clear();
        var planet = room.Sector!.Locations.First(l => l.Solid);
        var start = planet.Position + new Vector2(-400, planet.Radius - 20);
        Set(Player(room), "Position", start);
        Set(Player(room), "Previous", start);
        Set(Player(room), "ProtectedUntil", 0L);
        Equip(room, Weapon(emitter));
        var target = Enemy(room, start.X + 800, start.Y, 999, 100);
        Fire(room, 120);
        Assert.Equal(100, Health(target));
        start.Y += 70;
        Set(Player(room), "Position", start);
        Set(Player(room), "Previous", start);
        Set(target, "Position", start + new Vector2(800, 0));
        Set(target, "Previous", start + new Vector2(800, 0));
        Fire(room, 120, 200);
        Assert.True(Health(target) < 100);
    }

    [Fact]
    public void SurfaceLootLaunchesOutwardAndOnlyRoversAreReplaced()
    {
        var room = new Room(seed: 123);
        room.Join("Observer");
        var list = (IList)Get(room, "enemies");
        var units = list.Cast<object>().Where(e => (int)Get(e, "Home") == 80).ToArray();
        var planet = room.Sector!.Locations.Single(l => l.Id == 1);
        var geometry = new SectorCollision(room.Sector);
        foreach (var unit in units)
        {
            for (var seed = 0; seed < 32; seed++)
            {
                var (position, velocity) = ((Vector2, Vector2))Call(room, "Eject", unit, seed)!;
                Assert.Null(geometry.TraceShips(position, position, 12));
                Assert.True(Vector2.Dot(velocity, position - planet.Position) > 0);
            }
            if (!(bool)Get(unit, "Installation"))
                Set(unit, "Hull", 0);
        }
        room.Step();
        Step(room, 600);
        Assert.DoesNotContain(room.Snapshot().Enemies!, e => e.Home == 80 && e.Kind == "turret");
        Assert.Equal(2, room.Snapshot().Enemies!.Count(e => e.Home == 80 && e.Kind == "rover"));
        foreach (var unit in list.Cast<object>().Where(e => (int)Get(e, "Home") == 80))
            Set(unit, "Hull", 0);
        room.Step();
        Step(room, 600);
        Assert.DoesNotContain(room.Snapshot().Enemies!, e => e.Home == 80);
        Assert.Equal("destroyed", room.SectorSnapshot(1)!.Locations.Single(l => l.Id == 80).State);
    }

    [Fact]
    public void FractionalFriendlyShieldsWaitEightSecondsAndRecoverFully()
    {
        var room = new Room(seed: 123);
        room.Join("Observer");
        var ally = ((IList)Get(room, "enemies"))
            .Cast<object>()
            .First(e => (bool)Get(e, "Allied") && (int)Get(e, "SurfacePlanet") > 0);
        Call(room, "Hit", Shot(-500), null, ally, .25f);
        Step(room, 239);
        Assert.Equal(.25f, Get(ally, "ShieldFraction"));
        room.Step();
        Assert.Equal(0f, Get(ally, "ShieldFraction"));
        Assert.Equal(Get(ally, "MaxShield"), Get(ally, "Shield"));
    }

    [Fact]
    public void NaturalTrafficProducesAnEmergencyWithoutPlayerProximity()
    {
        var room = new Room(seed: 123);
        room.Join("AtPort");
        var traffic = new List<string>();
        for (var tick = 0; tick < 20 * 60 * Room.TickRate; tick++)
        {
            room.Step();
            if (tick % 900 == 0)
            {
                var fleet = room.Snapshot().Enemies!;
                var relief = fleet.FirstOrDefault(e => e.Kind == "relief");
                traffic.Add(
                    $"{tick / 30}s NPCs={fleet.Length} relief={relief?.Hull}/{relief?.Shield} at={relief?.X},{relief?.Y} nearby={fleet.Count(e => e.Faction == "hostile" && relief is not null && Vector2.Distance(new(e.X, e.Y), new(relief.X, relief.Y)) < 1000)}"
                );
            }
            if (room.SectorSnapshot(1)!.Rescue is { State: "active" } rescue)
            {
                Assert.InRange(rescue.Remaining, 6, 8);
                Assert.InRange(room.Snapshot().Enemies!.Length, 1, Room.EnemyCapacity);
                return;
            }
        }
        var ships = room.Snapshot().Enemies!.Where(e => e.Kind == "relief");
        Assert.Fail(
            "No natural rescue: "
                + JsonSerializer.Serialize(ships)
                + "\n"
                + string.Join("\n", traffic)
        );
    }

    private static object? Call(Room room, string name, params object?[] args) =>
        typeof(Room)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(room, args);

    private static object Shot(int owner)
    {
        var shot = Activator.CreateInstance(
            typeof(Room).GetNestedType("Shot", BindingFlags.NonPublic)!,
            true
        )!;
        Set(shot, "Owner", owner);
        return shot;
    }

    [Fact]
    public void EveryBayAndSocketBoundaryIsEnforcedWithoutSkills()
    {
        for (var bay = 0; bay < 4; bay++)
        for (var slot = 0; slot <= 8; slot++)
        {
            var required = ModuleCatalog.UnlockLevels[bay] + slot;
            var room = Start(Math.Max(1, required - 1));
            var loadout = PilotProfile.Starter().Weapons;
            loadout[bay] = Weapon(1);
            if (slot > 0)
                loadout[bay].Attachments[slot - 1] = 6;
            Fund(room, loadout);
            room.Input(1, new(1, 0, 0, 0, false, false, true));
            room.Step();
            if (required > 1 && slot == 0)
                Assert.False(room.Equip(1, new(2, loadout)));
            Set(Player(room), "Experience", Room.ExperienceForLevel(required));
            Assert.True(room.Equip(1, new(3, loadout)));
            Assert.Equal(slot, ModuleCatalog.ModSlots(required, bay));
            Assert.Empty(room.Snapshot().Players[0].Skills);
        }
    }

    [Fact]
    public void LegacyMigrationKeepsDormantModulesAndRefundsOnlyBayPoints()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var profile = PilotProfile.Starter() with
            {
                RunId = "legacy",
                Credits = 987,
                Run = new(
                    Room.ExperienceForLevel(8),
                    ["bay.2", "bay.3", "shield.1"],
                    2,
                    1,
                    .25f,
                    .5f,
                    [4, 3, 2, 1],
                    new long[4],
                    new long[4],
                    12,
                    15
                ),
            };
            profile.Owned[6] = 2;
            profile.Weapons[0].Attachments[7] = 6;
            profile.Weapons[2] = Weapon(1, 6);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    new Dictionary<string, PilotProfile> { ["PILOT"] = profile }
                )
            );
            var loaded = new ProfileStore(path).Get("Pilot");
            Assert.Equal(987, loaded.Credits);
            Assert.Equal(2, loaded.Owned[6]);
            Assert.Equal(6, loaded.Weapons[0].Attachments[7]);
            Assert.Equal(6, loaded.Weapons[2].Attachments[0]);
            Assert.False(loaded.Weapons[2].Enabled);
            Assert.Equal(new[] { "shield.1" }, loaded.Run!.Skills);
            Assert.Equal(.25f, loaded.Run.HullFraction);
            Assert.Equal(2, loaded.Run.Hull);
            Assert.Equal(profile.Run.WeaponCooldown, loaded.Run.WeaponCooldown);
            Assert.Equal(
                JsonSerializer.Serialize(loaded),
                JsonSerializer.Serialize(new ProfileStore(path).Get("Pilot"))
            );
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void PlanetRimsBlockShipsAndWeaponsAndPathsRemainOutside()
    {
        var sector = SectorDefinition.Generate(42);
        Assert.Equal(24000, sector.Width);
        Assert.Equal(16000, sector.Height);
        Assert.Equal(3, sector.Locations.Count(l => l.Solid));
        var body = sector.Locations.First(l => l.Solid);
        Assert.Equal(1160, body.Radius);
        Assert.False(sector.Locations.Single(l => l.Biome == "gas").Solid);
        var geometry = new SectorCollision(sector);
        var a = body.Position - new Vector2(1500, 0);
        var b = body.Position + new Vector2(1500, 0);
        Assert.NotNull(geometry.Trace(a, b, 1.5f));
        var velocity = new Vector2(300, 0);
        var stopped = geometry.Move(a, b, 14, ref velocity);
        Assert.InRange(Vector2.Distance(stopped, body.Position), 1173.9f, 1174.5f);
        Assert.InRange(velocity.Length(), 0, .01f);
        var route = new SectorNavigation(sector, geometry).Route(a, b);
        Assert.NotEmpty(route);
        foreach (var point in route)
        {
            Assert.Null(geometry.TraceShips(a, point, 32));
            a = point;
        }
    }

    [Fact]
    public void FriendlySurfaceUnitsIgnoreAllPlayerPayloadsAndUseFractionalShields()
    {
        var room = new Room(seed: 123);
        room.Join("Pilot");
        var allied = ((IList)Get(room, "enemies"))
            .Cast<object>()
            .First(e => (bool)Get(e, "Allied") && (int)Get(e, "SurfacePlanet") > 0);
        var hull = (int)Get(allied, "Hull");
        var shield = (int)Get(allied, "Shield");
        Assert.Equal(hull, shield);
        Assert.Equal(0f, Call(room, "Hit", Shot(1), null, allied, 100f));
        Assert.Equal(hull, Get(allied, "Hull"));
        Assert.Equal(shield, Get(allied, "Shield"));
        Call(room, "Hit", Shot(-500), null, allied, .25f);
        Assert.Equal(.25f, Get(allied, "ShieldFraction"));
        Call(room, "StatusDamage", allied, 5f, Shot(1));
        Assert.Equal(hull, Get(allied, "Hull"));
    }

    private static (Room Room, object Ship, object[] Attackers) RescueBattle(
        ProfileStore? store = null
    )
    {
        var room = new Room(seed: 123, profiles: store);
        room.Join("Rescuer");
        room.Join("Other");
        ((IList)Get(room, "enemies")).Clear();
        Set(room, "<Tick>k__BackingField", 10000L);
        var ship = Enemy(room, 10000, 9000, 900, 240);
        ship.GetType().GetProperty("Type")!.SetValue(ship, "relief");
        Set(ship, "Allied", true);
        Set(ship, "LastHit", 9999L);
        Set(ship, "Level", 3);
        var attackers = Enumerable
            .Range(1, 6)
            .Select(i => Enemy(room, 10000 + i * 50, 9000, i, 10))
            .ToArray();
        foreach (var attacker in attackers)
            Set(attacker, "NpcTarget", 900);
        Call(room, "StepRescue");
        Assert.Equal("active", room.SectorSnapshot(1)!.Rescue!.State);
        return (room, ship, attackers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RescueRewardsArePersonalExactlyOnceAndShipDeathWins(bool shipDies)
    {
        var (room, ship, attackers) = RescueBattle();
        Call(room, "Hit", Shot(1), null, attackers[0], 1f);
        Call(room, "Hit", Shot(2), null, attackers[0], .5f);
        foreach (var attacker in attackers)
            Set(attacker, "Hull", 0);
        if (shipDies)
            Set(ship, "Hull", 0);
        Call(room, "StepRescue");
        Call(room, "StepRescue");
        Call(room, "AdmitPersonalDrops");
        Assert.Equal(shipDies ? "failed" : "rescued", room.SectorSnapshot(1)!.Rescue!.State);
        Assert.Equal(shipDies ? 0 : 1, room.Snapshot().Credits!.Length);
        Assert.Empty(room.Snapshot().ModuleDrops!);
        if (!shipDies)
        {
            var money = Assert.Single(room.Snapshot().Credits!);
            Assert.Equal(240, money.Value);
            Assert.Equal(1, money.Owner);
            Assert.Equal(180, Assert.Single(room.Snapshot().Energy!).Value);
            Assert.Empty(room.ForViewer(room.Snapshot(), 2).Credits!);
        }
    }

    [Fact]
    public void RepairTendersCannotCountAsRescueAttackers()
    {
        var (room, _, attackers) = RescueBattle();
        Set(room, "rescue", null!);
        attackers[0].GetType().GetProperty("Type")!.SetValue(attackers[0], "tender");
        Call(room, "StepRescue");
        Assert.Null(room.SectorSnapshot(1)!.Rescue);
        Call(room, "StepNpcCombat", attackers[0]);
        Assert.Equal(0, Get(attackers[0], "NpcTarget"));
    }

    [Fact]
    public void DesignatedRescueAttackersPursueTheReliefShipOutsideAcquisitionRange()
    {
        var (room, ship, attackers) = RescueBattle();
        Set(ship, "Position", new Vector2(12000, 9000));
        foreach (var attacker in attackers)
        {
            var before = (Vector2)Get(attacker, "Position");
            Assert.Equal(true, Call(room, "StepNpcCombat", attacker));
            Assert.Equal(900, Get(attacker, "NpcTarget"));
            for (var i = 0; i < 90; i++)
            {
                Call(room, "StepNpcCombat", attacker);
                Call(room, "StepNpcFlight");
            }
            Assert.True(
                Vector2.Distance((Vector2)Get(attacker, "Position"), (Vector2)Get(ship, "Position"))
                    < Vector2.Distance(before, (Vector2)Get(ship, "Position"))
            );
        }
    }

    [Fact]
    public void KillingTheDesignatedGroupResolvesDuringTheNormalSimulationTick()
    {
        var (room, ship, attackers) = RescueBattle();
        Set(ship, "Home", 22);
        // An unrelated surviving hostile must not hold the rescue open.
        Enemy(room, 20000, 9000, 901, 10);
        foreach (var attacker in attackers)
            Call(room, "Hit", Shot(1), null, attacker, 100f);
        room.Step();
        var result = room.SectorSnapshot(1)!.Rescue!;
        Assert.Equal("rescued", result.State);
        Assert.Equal(0, result.Remaining);
        Assert.Contains(room.Snapshot().Credits!, c => c.Owner == 1 && c.Value == 240);
        Assert.Contains(room.Snapshot().Energy!, e => e.Owner == 1 && e.Value == 180);
    }

    [Fact]
    public void DeathInvalidatesRescueContributionAndRadioDoesNotRevealTerrain()
    {
        var (room, _, attackers) = RescueBattle();
        var before = room.SectorSnapshot(1)!.Pilot!.Cells;
        Assert.NotNull(room.SectorSnapshot(2)!.Rescue);
        Assert.Equal(before, room.SectorSnapshot(1)!.Pilot!.Cells);
        Call(room, "Hit", Shot(1), null, attackers[0], 1f);
        Set(Player(room), "RunId", "another-life");
        foreach (var attacker in attackers)
            Set(attacker, "Hull", 0);
        Call(room, "StepRescue");
        Call(room, "AdmitPersonalDrops");
        Assert.Empty(room.Snapshot().Credits!);
    }

    [Fact]
    public void DisconnectedStatusContributionRetainsPersonalRescueRewards()
    {
        var (room, _, attackers) = RescueBattle();
        room.Disconnect(1);
        Step(room, 151);
        Assert.DoesNotContain(room.Snapshot().Players, p => p.Id == 1);
        Call(room, "StatusDamage", attackers[0], 1f, Shot(1));
        foreach (var attacker in attackers)
            Set(attacker, "Hull", 0);
        Call(room, "StepRescue");
        Call(room, "AdmitPersonalDrops");
        var reward = Assert.Single(room.Snapshot().Credits!);
        Assert.Equal(1, reward.Owner);
        Assert.Empty(room.ForViewer(room.Snapshot(), 2).Credits!);
        Assert.Equal(1, room.Join("Rescuer"));
        Assert.Equal(reward.Id, Assert.Single(room.Snapshot().Credits!).Id);
    }

    [Fact]
    public void FailedRescuePickupPersistenceRetriesTheSameRewardExactlyOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-rescue.json");
        try
        {
            var store = new ProfileStore(path);
            var (room, _, attackers) = RescueBattle(store);
            Call(room, "Hit", Shot(1), null, attackers[0], 1f);
            foreach (var attacker in attackers)
                Set(attacker, "Hull", 0);
            Call(room, "StepRescue");
            Call(room, "AdmitPersonalDrops");
            var reward = Assert.Single(room.Snapshot().Credits!);
            Set(Player(room), "Position", new Vector2(reward.X, reward.Y));
            Set(Player(room), "Previous", new Vector2(reward.X, reward.Y));
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Call(room, "StepCredits");
                Assert.Equal(100, store.Get("Rescuer").Credits);
                Assert.Equal(reward.Id, Assert.Single(room.Snapshot().Credits!).Id);
            }
            Call(room, "StepRescue");
            Call(room, "StepCredits");
            Call(room, "StepCredits");
            Assert.Equal(340, store.Get("Rescuer").Credits);
            Assert.Empty(room.Snapshot().Credits!);
            Assert.Equal(340, new ProfileStore(path).Get("Rescuer").Credits);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }
}
