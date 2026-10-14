using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class LivingMissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProtectionUsesActualAttackersAndProtectedShipDeathWins(bool shipDies)
    {
        var (room, _) = World();
        Call(room, "SpawnCivilian", NpcRole.Miner, 3);
        var miner = ((IList)Get(room, "enemies"))
            .Cast<object>()
            .Single(e => (NpcRole)Get(e, "Role") == NpcRole.Miner);
        var at = (Vector2)Get(miner, "Position");
        var attacker = Enemy(room, at.X + 250, at.Y, 900, 40);
        Set(attacker, "NpcTarget", Get(miner, "Id"));
        Assert.Equal("ok", Act(room, "mission_protection"));
        ((IDictionary)Get(attacker, "Contributors"))[1] = ((string)Get(Player(room), "RunId"), 1f);
        Set(attacker, "Hull", 0);
        Call(room, "WorldEnemyDefeated", attacker);
        if (shipDies)
        {
            Set(miner, "Hull", 0);
            Call(room, "WorldEnemyDefeated", miner);
        }
        Call(room, "StepWorldJobs");
        Assert.Equal(shipDies ? "failed" : "ready", room.SectorSnapshot(1)!.World!.JobState);
    }

    [Fact]
    public void SmugglingIsIndependentOfStationContractsAndCannotUseRaceProtection()
    {
        var (room, _) = World();
        Assert.Equal("ok", Act(room, "accept", 1));
        Assert.Equal("ok", Act(room, "mission_smuggling"));
        Set(Player(room), "Docked", 0);
        Assert.Equal("race_not_ready", Act(room, "race_join", 0));
        Set(Player(room), "Docked", room.SectorSnapshot(1)!.World!.Targets[0]);
        Call(room, "StepWorldJobs");
        Assert.Equal("ready", room.SectorSnapshot(1)!.World!.JobState);
        Assert.Equal("ok", Act(room, "mission_claim"));
        Assert.Equal(1, room.SectorSnapshot(1)!.Pilot!.Contract);
    }

    [Fact]
    public void RaceProtectionEndsOutsideTheCourseAndCannotRestoreItByReconnecting()
    {
        var (room, _) = World();
        Set(Player(room), "Docked", 0);
        var start = room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(400, 0);
        Set(Player(room), "Position", start);
        Assert.Equal("ok", Act(room, "race_join", 0));
        PrepareRaces(room);
        Set(room, "<Tick>k__BackingField", 301L);
        Set(Player(room), "Position", new Vector2(22000, 15000));
        Call(room, "StepRaces");
        Assert.Equal("disqualified", room.SectorSnapshot(1)!.World!.Race!.State);
        Assert.False(room.Snapshot().Players[0].Protected);
        room.Disconnect(1);
        room.Join("Pilot");
        Assert.False(room.Snapshot().Players[0].Protected);
    }

    private static object? Call(Room room, string name, params object[] args) =>
        typeof(Room)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(room, args);

    private static string Act(Room room, string action, int target = 0)
    {
        room.SectorAction(1, new((int)Get(Player(room), "LastReceived") + 1, action, target));
        return room.SectorSnapshot(1)!.Pilot!.Receipt!.Result;
    }

    private static (Room Room, ProfileStore Store) World()
    {
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, seed: 123);
        room.Join("Pilot");
        Set(Player(room), "Docked", 10);
        return (room, store);
    }

    [Theory]
    [InlineData("bounty", true, "ready")]
    [InlineData("bounty", false, "failed")]
    [InlineData("interception", true, "ready")]
    [InlineData("interception", false, "failed")]
    public void CombatAssignmentsRequireRealRunContributionAndOnlyPayOnce(
        string kind,
        bool contributed,
        string outcome
    )
    {
        var (room, store) = World();
        var target = Enemy(room, 10000, 9000, 800, 50);
        Set(target, "Level", 5);
        Set(target, "Role", NpcRole.Courier);
        Assert.Equal("ok", Act(room, "mission_" + kind));
        var count = room.Snapshot().Enemies!.Length;
        if (contributed)
            ((IDictionary)Get(target, "Contributors"))[1] = (
                (string)Get(Player(room), "RunId"),
                1f
            );
        Set(target, "Hull", 0);
        Call(room, "WorldEnemyDefeated", target);
        Assert.Equal(outcome, room.SectorSnapshot(1)!.World!.JobState);
        Assert.Equal(count, room.Snapshot().Enemies!.Length); // Acceptance never spawns combatants.
        if (contributed)
        {
            Assert.Equal("ok", Act(room, "mission_claim"));
            var wallet = store.Get("Pilot").Credits;
            Assert.True(wallet > 100);
            Assert.Equal("assignment_incomplete", Act(room, "mission_claim"));
            Assert.Equal(wallet, store.Get("Pilot").Credits);
        }
    }

    [Fact]
    public void CourierArrivalAndCompetitorDeliveryFailExistingAssignments()
    {
        var (room, _) = World();
        var target = Enemy(room, 10000, 9000, 800);
        Set(target, "Role", NpcRole.Courier);
        Assert.Equal("ok", Act(room, "mission_interception"));
        Set(target, "State", "arrived");
        Call(room, "StepWorldJobs");
        Assert.Equal("failed", room.SectorSnapshot(1)!.World!.JobState);
        Act(room, "mission_abandon");
        Call(room, "AddCargo", "container", new Vector2(1000, 14000), 30, 900L * 30);
        Assert.Equal("ok", Act(room, "mission_recovery"));
        var list = (IList)Get(room, "worldCargo");
        list.Remove(list.Cast<object>().Single(c => (string)Get(c, "Kind") == "container"));
        Call(room, "StepWorldJobs");
        Assert.Equal("failed", room.SectorSnapshot(1)!.World!.JobState);
    }

    [Fact]
    public void CompletedAnomalyKeepsFrozenTechnologyAfterEventExpiresAndIssuerIsGone()
    {
        var (room, store) = World();
        var at = new Vector2(1000, 14000);
        Call(room, "AddCargo", "anomaly", at, 200, 900L * 30);
        Assert.Equal("ok", Act(room, "mission_exploration"));
        var first = room.SectorSnapshot(1)!.World!.Objective!;
        Assert.Equal(1500, first.Uncertainty);
        Set(Player(room), "Position", at);
        for (var i = 0; i < 3; i++)
            Assert.Equal("ok", Act(room, "mission_scan"));
        Assert.Equal("ready", room.SectorSnapshot(1)!.World!.JobState);
        ((IList)Get(room, "worldCargo")).Clear();
        Call(room, "StepWorldJobs");
        Assert.Equal("ready", room.SectorSnapshot(1)!.World!.JobState);
        Assert.Equal("ok", Act(room, "mission_claim"));
        var equipment = store.Get("Pilot").Equipment!;
        Assert.Equal(5, equipment.Weapons.Length);
        Assert.Equal(4, ModuleLevels.Level(equipment.Weapons[^1].Emitter));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryRaceRequiresOrderedGatesAndSavesAuthoritativeClassRecord(int category)
    {
        var (room, store) = World();
        Set(Player(room), "Docked", 0);
        var start = room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(400, 0);
        Set(Player(room), "Position", start);
        Set(Player(room), "Previous", start);
        Assert.Equal("ok", Act(room, "race_join", category));
        PrepareRaces(room);
        var race = ((IDictionary)Get(room, "races"))[1]!;
        var course = (Vector2[])Get(race, "Course");
        Set(room, "<Tick>k__BackingField", 315L);
        Set(Player(room), "Position", course[^1]);
        Set(Player(room), "Previous", course[^1]);
        Call(room, "StepRaces");
        Assert.Equal(1, Get(race, "Checkpoint"));
        for (var i = 1; i < course.Length; i++)
        {
            Set(room, "<Tick>k__BackingField", 315L + i * 30);
            Set(Player(room), "Position", course[i]);
            Set(Player(room), "Previous", course[i]);
            Call(room, "StepRaces");
        }
        Assert.Equal("finished", Get(race, "State"));
        Assert.Equal(100, store.Get("Pilot").Credits);
        var record = Assert.Single(store.Get("Pilot").RaceRecords!);
        Assert.True(record.Seconds > 0);
        Assert.True(record.Ghost.Length > 0);
        Assert.Contains(":0:0:", record.Key);
    }
}
