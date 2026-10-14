using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class LivingAuditTests
{
    [Fact]
    public void SmugglingCannotDiscardInspectedCargoByAbandoningOrClaiming()
    {
        var room = World();
        Assert.Equal("ok", Act(room, "mission_smuggling"));
        var state = Call(room, "Living", Player(room))!;
        Set(state, "Police", "scanning");
        Assert.Equal("resolve_inspection_first", Act(room, "mission_abandon"));
        Set(state, "JobState", "ready");
        Assert.Equal("resolve_inspection_first", Act(room, "mission_claim"));
        Assert.Equal("ok", Act(room, "police_surrender"));
        Assert.Equal("ok", Act(room, "mission_abandon"));
        Assert.Equal("", Get(state, "Police"));
    }

    [Fact]
    public void ResourceRushTracksRemainingCargoAndResolvesWhenItIsGone()
    {
        var room = new Room(true, seed: 123);
        room.Join("Pilot");
        Call(room, "AddCargo", "container", new Vector2(1000, 14000), 30, 27000L);
        Call(room, "AddCargo", "container", new Vector2(1100, 14000), 30, 27000L);
        var all = (IList)Get(room, "worldCargo");
        var cargo = all.Cast<object>().Where(c => (string)Get(c, "Kind") == "container").ToArray();
        var ids = (HashSet<int>)Get(room, "eventCargo");
        foreach (var item in cargo)
            ids.Add((int)Get(item, "Id"));
        Set(room, "worldEvent", "resource-rush");
        Set(room, "worldEventTarget", (int)Get(cargo[0], "Id"));
        Set(room, "worldEventEnds", 9000L);
        all.Remove(cargo[0]);
        Call(room, "StepWorldEvents");
        Assert.Equal((int)Get(cargo[1], "Id"), room.SectorSnapshot(1)!.World!.EventObjective!.Id);
        all.Remove(cargo[1]);
        Call(room, "StepWorldEvents");
        Assert.Equal("", room.SectorSnapshot(1)!.World!.Event);
        Assert.InRange((long)Get(room, "worldEventAt"), 360 * 30, 600 * 30);
    }

    [Fact]
    public void ContactRewardPersistsXpWithWalletAndSharesAssignmentCooldown()
    {
        var store = new ProfileStore();
        var room = new Room(false, profiles: store, seed: 123);
        room.Join("Pilot");
        Set(Player(room), "Docked", 10);
        var legacy = Call(room, "ExplorationFor", Player(room))!;
        Set(legacy, "Job", "bounty");
        ((HashSet<int>)Get(legacy, "JobProgress")).UnionWith([1, 2, 3]);
        Assert.Equal("ok", Act(room, "job_claim"));
        Assert.Equal(160, store.Get("Pilot").Credits);
        Assert.Equal(45, store.Get("Pilot").Run!.Experience);
        Assert.Equal("assignment_cooldown", Act(room, "mission_smuggling"));
        Set(legacy, "Job", "explorer");
        Assert.Equal("ok", Act(room, "mission_abandon"));
        Assert.Equal("", Get(legacy, "Job"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealTickResolvesLastProtectionAttackerBeforeMissionOutcome(bool shipDies)
    {
        var room = World();
        Call(room, "SpawnCivilian", NpcRole.Miner, 3);
        var miner = Enemies(room).Single(e => (NpcRole)Get(e, "Role") == NpcRole.Miner);
        var at = (Vector2)Get(miner, "Position");
        var attacker = Enemy(room, at.X + 250, at.Y, 900, 40);
        Set(attacker, "NpcTarget", Get(miner, "Id"));
        Assert.Equal("ok", Act(room, "mission_protection"));
        ((IDictionary)Get(attacker, "Contributors"))[1] = ((string)Get(Player(room), "RunId"), 1f);
        Set(attacker, "Hull", 0);
        if (shipDies)
            Set(miner, "Hull", 0);
        room.Step();
        Assert.Equal(shipDies ? "failed" : "ready", room.SectorSnapshot(1)!.World!.JobState);
    }

    [Fact]
    public void TargetDeathDuringRaceFailsAssignmentInsteadOfLeavingItStuck()
    {
        var room = World();
        var target = Enemy(room, 10000, 9000, 800, 50);
        Set(target, "Level", 5);
        Assert.Equal("ok", Act(room, "mission_bounty"));
        Set(Player(room), "Docked", 0);
        Set(
            Player(room),
            "Position",
            room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(400, 0)
        );
        Assert.Equal("ok", Act(room, "race_join"));
        Set(target, "Hull", 0);
        room.Step();
        Assert.Equal("failed", room.SectorSnapshot(1)!.World!.JobState);
        Act(room, "race_stop");
        Assert.Equal("ok", Act(room, "mission_abandon"));
    }

    [Fact]
    public void RecoveryCannotReserveCargoAlreadyBeingDeliveredByAnNpc()
    {
        var room = World();
        Call(room, "AddCargo", "container", new Vector2(1000, 14000), 30, 27000L);
        var cargo = ((IList)Get(room, "worldCargo"))
            .Cast<object>()
            .Single(c => (string)Get(c, "Kind") == "container");
        Set(cargo, "Owner", -100);
        Assert.Equal("no_eligible_target", Act(room, "mission_recovery"));
        Set(cargo, "Owner", 0);
        Assert.Equal("ok", Act(room, "mission_recovery"));
    }

    [Fact]
    public void CompletedAnomalyCannotOfferTheSameUniqueWeaponAgain()
    {
        var room = World();
        var at = new Vector2(1000, 14000);
        Call(room, "AddCargo", "anomaly", at, 200, 27000L);
        Assert.Equal("ok", Act(room, "mission_exploration"));
        Set(Player(room), "Position", at);
        for (var i = 0; i < 3; i++)
            Assert.Equal("ok", Act(room, "mission_scan"));
        Assert.Equal("ok", Act(room, "mission_claim"));
        Set(room, "<Tick>k__BackingField", 10000L);
        Assert.Equal("no_eligible_target", Act(room, "mission_exploration"));
    }

    [Fact]
    public void FleeingTraderLeavesItsPauseAndRecordsActualRefuge()
    {
        var room = World();
        Call(room, "SpawnCivilian", NpcRole.Trader, 0);
        var agent = ((IList)Get(room, "civilians"))[0]!;
        var npc = Get(agent, "Ship");
        var station = room.Sector!.Locations.Single(l => l.Id == 10).Position;
        Set(npc, "Position", station + new Vector2(400, 0));
        Set(room, "<Tick>k__BackingField", 20L);
        Set(npc, "LastHit", 19L);
        Call(room, "MoveCivilian", npc);
        Assert.Equal(10, Get(agent, "Destination"));
        for (var i = 0; i < 90; i++)
        {
            Call(room, "MoveCivilian", npc);
            Call(room, "StepNpcFlight");
        }
        Assert.True(Vector2.Distance((Vector2)Get(npc, "Position"), station) < 400);
        Set(npc, "Position", station + new Vector2(160, 0));
        Set(npc, "Velocity", Vector2.Zero);
        Call(room, "MoveCivilian", npc);
        Assert.Equal(10, Get(npc, "Home"));
    }

    [Fact]
    public void SwitchingFromCloseTradeToRadioReleasesCloak()
    {
        var room = World();
        Call(room, "SpawnCivilian", NpcRole.Trader, 0);
        var ship = Enemies(room).Single(e => (NpcRole)Get(e, "Role") == NpcRole.Trader);
        Set(Player(room), "Docked", 0);
        Set(Player(room), "Position", (Vector2)Get(ship, "Position") + new Vector2(60, 0));
        var id = (int)Get(ship, "Id");
        room.Input(1, new(1, 0, 0, 0, false, false));
        Assert.Equal("ok", Act(room, "radio_trade", id));
        room.Step();
        Assert.True((bool)Get(Player(room), "Editing"));
        Assert.Equal("ok", Act(room, "radio", id));
        Assert.False((bool)Get(Player(room), "Editing"));
        Assert.Equal(0, room.SectorSnapshot(1)!.Services!.Hailing);
    }

    private static Room World()
    {
        var room = new Room(false, seed: 123);
        room.Join("Pilot");
        Set(Player(room), "Docked", 10);
        return room;
    }

    private static IEnumerable<object> Enemies(Room room) =>
        ((IList)Get(room, "enemies")).Cast<object>();

    private static object? Call(Room room, string name, params object[] args) =>
        typeof(Room)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(room, args);

    private static string Act(Room room, string action, int target = 0)
    {
        room.SectorAction(1, new((int)Get(Player(room), "LastReceived") + 1, action, target));
        return room.SectorSnapshot(1)!.Pilot!.Receipt!.Result;
    }
}
