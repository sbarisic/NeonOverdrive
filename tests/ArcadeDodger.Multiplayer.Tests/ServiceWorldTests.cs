using System.Collections;
using System.Numerics;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class ServiceWorldTests
{
    private static object? Call(object target, string method, params object?[] args) =>
        target
            .GetType()
            .GetMethod(
                method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .Invoke(target, args);

    private static Room World(bool ai = false, ProfileStore? store = null)
    {
        var room = new Room(ai, profiles: store, seed: 123);
        room.Join("Builder");
        ((ProfileStore)Get(room, "profiles")).Credit("Builder", 2000);
        return room;
    }

    private static void At(Room room, Vector2 at, int id = 1)
    {
        Set(Player(room, id), "Position", at);
        Set(Player(room, id), "Previous", at);
        Set(Player(room, id), "Velocity", Vector2.Zero);
    }

    private static SectorReceipt Act(Room room, string action, int target = 0, int id = 1)
    {
        var seq = (int)Get(Player(room, id), "LastReceived") + 1;
        room.SectorAction(id, new(seq, action, target));
        return room.SectorSnapshot(id)!.Pilot!.Receipt!;
    }

    private static object Unit(Room room, string type) =>
        ((IList)Get(room, "enemies"))
            .Cast<object>()
            .First(e =>
                (bool)Get(e, "Allied")
                && (string)e.GetType().GetProperty("Type")!.GetValue(e)! == type
            );

    [Fact]
    public void ThreeDistinctContactsHailTradeAndReleaseCloakWithoutDocking()
    {
        var r = World(true);
        var contacts = r.SectorSnapshot(1)!.Services!.Contacts;
        Assert.Equal(3, contacts.Length);
        Assert.Equal(3, contacts.Select(c => c.Role).Distinct().Count());
        var trader = contacts.Single(c => c.Role == "trader");
        At(r, new(trader.X, trader.Y));
        Assert.Equal("ok", Act(r, "hail", trader.Id).Result);
        Assert.True(r.Snapshot().Players[0].Editing);
        Assert.Equal(0, r.Snapshot().Players[0].Docked);
        Assert.True(r.Buy(1, new(2, 6)));
        Assert.Equal("ok", r.Workshop(1).Result);
        Assert.Equal("ok", Act(r, "goodbye").Result);
        Assert.False(r.Snapshot().Players[0].Editing);
        Assert.False(r.Buy(1, new(4, 6)));
    }

    [Fact]
    public void HailIsSharedAndNpcDeathClosesAllConversations()
    {
        var r = World(true);
        r.Join("Second");
        var contact = r.SectorSnapshot(1)!.Services!.Contacts[0];
        for (var id = 1; id <= 2; id++)
        {
            At(r, new(contact.X, contact.Y), id);
            Act(r, "hail", contact.Id, id);
        }
        var npc = ((IList)Get(r, "enemies"))
            .Cast<object>()
            .Single(e => (int)Get(e, "Id") == contact.Id);
        Set(npc, "Hull", 0);
        Call(r, "StepContacts");
        Assert.All(r.Snapshot().Players, p => Assert.False(p.Editing));
        Assert.Equal(2, r.SectorSnapshot(1)!.Services!.Contacts.Length);
        Set(r, "<Tick>k__BackingField", 9000L);
        Call(r, "StepContacts");
        Assert.Equal(3, r.SectorSnapshot(1)!.Services!.Contacts.Length);
    }

    [Fact]
    public void ContactRoutesBeginAfterSixtySecondsAndHailingStopsThem()
    {
        var r = World(true);
        var ship = Unit(r, "contact");
        var before = (Vector2)Get(ship, "Position");
        Call(r, "MoveContact", ship);
        Assert.Equal(before, Get(ship, "Position"));
        Set(r, "<Tick>k__BackingField", 1801L);
        Call(r, "MoveContact", ship);
        for (var i = 0; i < 90; i++)
        {
            Call(r, "MoveContact", ship);
            Call(r, "StepNpcFlight");
        }
        Assert.NotEqual(before, Get(ship, "Position"));
        At(r, (Vector2)Get(ship, "Position"));
        Act(r, "hail", (int)Get(ship, "Id"));
        before = (Vector2)Get(ship, "Position");
        Call(r, "MoveContact", ship);
        Assert.Equal(before, Get(ship, "Position"));
    }

    [Fact]
    public void CacheSearchIsPersonalAndSurvivesDeathAndRejoin()
    {
        var r = World();
        var cache = r.Sector!.Locations.First(l => l.Kind == "cache");
        At(r, cache.Position);
        Assert.Equal("ok", Act(r, "interact", cache.Id).Result);
        Step(r, 90);
        Assert.Contains(cache.Id, r.SectorSnapshot(1)!.Services!.ClaimedCaches);
        Assert.Equal("cache_already_searched", Act(r, "interact", cache.Id).Result);
        Set(Player(r), "Hull", 0);
        r.Step();
        r.Respawn(1, new(3));
        Assert.Contains(cache.Id, r.SectorSnapshot(1)!.Services!.ClaimedCaches);
        r.Leave(1, new(4));
        r.Join("Builder");
        Assert.Contains(cache.Id, r.SectorSnapshot(1)!.Services!.ClaimedCaches);
        r.Join("Second");
        Assert.Empty(r.SectorSnapshot(2)!.Services!.ClaimedCaches);
    }

    [Fact]
    public void ExplorationAssignmentTargetsUnsearchedCachesAndCanTurnInAtStation()
    {
        var r = World(true);
        var contact = r.SectorSnapshot(1)!.Services!.Contacts.Single(c => c.Role == "explorer");
        At(r, new(contact.X, contact.Y));
        Act(r, "hail", contact.Id);
        Assert.Equal("ok", Act(r, "job_accept", contact.Id).Result);
        Act(r, "goodbye");
        var targets = r.SectorSnapshot(1)!.Services!.Assignment.Targets;
        foreach (var target in targets)
        {
            At(r, r.Sector!.Locations.Single(l => l.Id == target).Position);
            Assert.Equal("ok", Act(r, "interact", target).Result);
            Step(r, 90);
        }
        Assert.True(r.SectorSnapshot(1)!.Services!.Assignment.Ready);
        At(r, r.Sector!.Locations.Single(l => l.Id == 10).Position);
        Act(r, "dock", 10);
        var wallet = r.Workshop(1).Credits;
        Assert.Equal("ok", Act(r, "job_claim").Result);
        Assert.Equal(wallet + 40, r.Workshop(1).Credits);
        Assert.Equal("incomplete", Act(r, "job_claim").Result);
        Assert.True(r.SectorSnapshot(1)!.Services!.Assignment.Cooldown > 0);
    }

    [Fact]
    public void ConstructionChargesOnceAndRepairPreservesFractionsAndDoesNotResurrect()
    {
        var r = World();
        var site = r.Sector!.Locations.Single(l => l.Id == 40);
        At(r, site.Position);
        Assert.Equal("ok", Act(r, "activate", 40).Result);
        Assert.Equal("socket_unavailable", Act(r, "activate", 40).Result);
        Assert.Equal("ok", Act(r, "turret_one", 40).Result);
        Assert.Equal("ok", Act(r, "repair_bot", 40).Result);
        Assert.Equal(1750, r.Workshop(1).Credits);
        var repair = Unit(r, "repair-bot");
        At(r, (Vector2)Get(repair, "Position") + new Vector2(0, 60));
        Set(Player(r), "Hull", 2);
        Set(Player(r), "HullFraction", .5f);
        var turret = Unit(r, "salvage-turret");
        Set(turret, "Hull", 0);
        Step(r, 30);
        Assert.Equal(2, r.Snapshot().Players[0].Hull);
        Assert.InRange((float)Get(Player(r), "HullFraction"), 0, .0001f);
        Assert.Equal(0, Get(turret, "Hull"));
        Set(Unit(r, "salvage-core"), "Hull", 0);
        r.Step();
        Assert.False(r.SectorSnapshot(1)!.Services!.Outposts.Single(s => s.Id == 40).Active);
        Assert.DoesNotContain(
            r.Snapshot().Enemies!,
            e => e.Kind.StartsWith("salvage-") || e.Kind == "repair-bot"
        );
        Assert.Empty(r.Snapshot().Energy!);
    }

    [Fact]
    public void FailedConstructionWriteLeavesWalletAndSocketUnchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new ProfileStore(path);
            var r = World(store: store);
            At(r, r.Sector!.Locations.Single(l => l.Id == 40).Position);
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Equal("save_failed", Act(r, "activate", 40).Result);
            Assert.Equal(2100, store.Get("Builder").Credits);
            Assert.False(r.SectorSnapshot(1)!.Services!.Outposts.Single(s => s.Id == 40).Active);
            Assert.Equal("ok", Act(r, "activate", 40).Result);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void FactoryDispatchesExistingShipsAndStopsNewRaidsAfterDestruction()
    {
        var r = World(true);
        var site = r.Sector!.Locations.Single(l => l.Id == 40);
        At(r, site.Position);
        Act(r, "activate", 40);
        for (var i = 0; i < 6; i++)
        {
            var e = Enemy(r, 18000 + i * 20, 14000, 900 + i, 100);
            Set(e, "Home", 23);
        }
        var before = r.Snapshot().Enemies!.Length;
        Set(r, "<Tick>k__BackingField", 7200L);
        Call(r, "StepRaid");
        Assert.Equal(8, r.SectorSnapshot(1)!.Services!.Raid!.Remaining);
        Assert.Equal(before, r.Snapshot().Enemies!.Length);
        var attackers = (HashSet<int>)Get(r, "raiders");
        foreach (
            var e in ((IList)Get(r, "enemies"))
                .Cast<object>()
                .Where(e => attackers.Contains((int)Get(e, "Id")))
        )
            Set(e, "Hull", 0);
        Call(r, "StepRaid");
        Assert.Null(r.SectorSnapshot(1)!.Services!.Raid);
        Set(((IDictionary)Get(r, "sources"))[23]!, "Destroyed", true);
        Set(r, "<Tick>k__BackingField", 20000L);
        Call(r, "StepRaid");
        Assert.Null(r.SectorSnapshot(1)!.Services!.Raid);
    }

    [Fact]
    public async Task ConcurrentConstructionReservesOneSocketAndRefundsInvalidatedBuild()
    {
        var store = new ProfileStore();
        var r = World(store: store);
        r.Join("Second");
        store.Credit("Second", 1000);
        var point = r.Sector!.Locations.Single(l => l.Id == 40).Position;
        At(r, point);
        At(r, point, 2);
        Assert.Equal("ok", Act(r, "activate", 40).Result);
        var before = store.Get("Builder").Credits;
        store.BackgroundWrites = true;
        var release = new TaskCompletionSource();
        _ = store.Schedule(() =>
        {
            release.Task.GetAwaiter().GetResult();
            return "ok";
        });
        try
        {
            Assert.Equal("pending", Act(r, "turret_one", 40).Result);
            Assert.Equal("busy", Act(r, "turret_one", 40, 2).Result);
            Set(Unit(r, "salvage-core"), "Hull", 0);
            Call(r, "StepOutposts");
        }
        finally
        {
            release.SetResult();
        }
        await store.FlushAsync();
        Call(r, "CompleteTransactions");
        Assert.Equal(before - 100, store.Get("Builder").Credits);
        Call(r, "StepOutposts");
        await store.FlushAsync();
        Call(r, "CompleteTransactions");
        Assert.Equal(before, store.Get("Builder").Credits);
        Call(r, "StepOutposts");
        await store.FlushAsync();
        Call(r, "CompleteTransactions");
        Assert.Equal(before, store.Get("Builder").Credits);
        Assert.False(
            r.SectorSnapshot(1)!.Services!.Outposts.Single(s => s.Id == 40).Sockets[1].Pending
        );
        Assert.DoesNotContain(r.Snapshot().Enemies!, e => e.Kind == "salvage-turret");
    }

    [Fact]
    public void ConstructionRejectsCapacityAndRepairsDoNotRefillShieldsOrHealSelf()
    {
        var r = World();
        At(r, r.Sector!.Locations.Single(l => l.Id == 40).Position);
        Act(r, "activate", 40);
        Act(r, "repair_bot", 40);
        var repair = Unit(r, "repair-bot");
        Set(repair, "Hull", 30);
        At(r, (Vector2)Get(repair, "Position") + new Vector2(0, 50));
        Set(Player(r), "Shield", 0);
        Set(Player(r), "ShieldAt", 10000L);
        Step(r, 30);
        Assert.Equal(30, Get(repair, "Hull"));
        Assert.Equal(0, r.Snapshot().Players[0].Shield);
        At(r, r.Sector.Locations.Single(l => l.Id == 40).Position);
        for (var i = r.Snapshot().Enemies!.Length; i < 192; i++)
            Enemy(r, 20000, 15000, 1000 + i);
        var wallet = r.Workshop(1).Credits;
        Assert.Equal("npc_capacity_reached", Act(r, "turret_one", 40).Result);
        Assert.Equal(wallet, r.Workshop(1).Credits);
    }

    [Fact]
    public void BountyRequiresActualDamageAndRejectsOldRunContributions()
    {
        var r = World(true);
        var contact = r.SectorSnapshot(1)!.Services!.Contacts.Single(c => c.Role == "bounty");
        At(r, new(contact.X, contact.Y));
        Act(r, "hail", contact.Id);
        Act(r, "job_accept", contact.Id);
        Act(r, "goodbye");
        var shot = Activator.CreateInstance(
            typeof(Room).GetNestedType("Shot", System.Reflection.BindingFlags.NonPublic)!,
            true
        )!;
        Set(shot, "Owner", 1);
        for (var i = 0; i < 4; i++)
        {
            var e = Enemy(r, 20000, 15000, 800 + i);
            Call(r, "Hit", shot, null, e, i == 0 ? .5f : 1f);
            Call(r, "RewardEncounter", e);
        }
        Assert.Equal(3, r.SectorSnapshot(1)!.Services!.Assignment.Progress);
        var stale = Enemy(r, 20000, 15000, 900);
        Call(r, "Hit", shot, null, stale, 1f);
        Set(Player(r), "RunId", "changed-run");
        Call(r, "RewardEncounter", stale);
        Assert.Equal(3, r.SectorSnapshot(1)!.Services!.Assignment.Progress);
    }

    [Fact]
    public void PiercingContactEffectsOccurOncePerTargetAndNeverForFriendlyPassThrough()
    {
        var r = Start();
        var first = Enemy(r, 1200, id: 1);
        var friendly = Enemy(r, 1250, id: 2);
        Set(friendly, "Allied", true);
        var second = Enemy(r, 1300, id: 3);
        Equip(r, Weapon(1, 8));
        Fire(r);
        r.Input(1, new(4, 0, 0, 0, false, false));
        var effects = new Dictionary<int, CombatEvent>();
        for (var i = 0; i < 12; i++)
        {
            r.Step();
            foreach (var e in r.Snapshot().CombatEvents ?? [])
                if (e.Kind == 3)
                    effects[e.Id] = e;
        }
        Assert.Equal(2, effects.Count);
        Assert.All(effects.Values, e => Assert.True(e.Dx > 0));
        Assert.Equal(100, Get(friendly, "Hull"));
        Assert.Equal(99, Get(first, "Hull"));
        Assert.Equal(99, Get(second, "Hull"));
    }

    [Fact]
    public async Task CancelledConstructionRetriesFailedRefundWithoutDoubleCredit()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new ProfileStore(path);
            var r = World(store: store);
            At(r, r.Sector!.Locations.Single(l => l.Id == 40).Position);
            store.BackgroundWrites = true;
            Act(r, "activate", 40);
            await store.FlushAsync();
            r.Disconnect(1);
            Call(r, "CompleteTransactions");
            Assert.Equal(2000, store.Get("Builder").Credits);
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Call(r, "StepOutposts");
                await store.FlushAsync();
                Call(r, "CompleteTransactions");
                Assert.Equal(2000, store.Get("Builder").Credits);
                Assert.True(
                    r.SectorSnapshot(1)!
                        .Services!.Outposts.Single(s => s.Id == 40)
                        .Sockets[0]
                        .Pending
                );
            }
            Call(r, "StepOutposts");
            await store.FlushAsync();
            Call(r, "CompleteTransactions");
            Assert.Equal(2100, store.Get("Builder").Credits);
            Call(r, "StepOutposts");
            await store.FlushAsync();
            Call(r, "CompleteTransactions");
            Assert.Equal(2100, store.Get("Builder").Credits);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void RepairLinksCannotCrossAsteroidCover()
    {
        var r = World();
        At(r, r.Sector!.Locations.Single(l => l.Id == 40).Position);
        Act(r, "activate", 40);
        Act(r, "repair_bot", 40);
        var rock = r.Sector.Obstacles.First();
        var repair = Unit(r, "repair-bot");
        var delta = new Vector2(rock.Radius + 25, 0);
        Set(repair, "Position", rock.Position - delta);
        At(r, rock.Position + delta);
        Set(Player(r), "Hull", 1);
        Step(r, 30);
        Assert.Equal(1, r.Snapshot().Players[0].Hull);
        Assert.Equal(0, (float)Get(Player(r), "HullFraction"));
    }

    [Fact]
    public void CapturedSiteSendsSurvivingRaidersHome()
    {
        var r = World(true);
        At(r, r.Sector!.Locations.Single(l => l.Id == 40).Position);
        Act(r, "activate", 40);
        for (var i = 0; i < 6; i++)
            Set(Enemy(r, 18000 + i * 20, 14000, 950 + i, 100), "Home", 23);
        Set(r, "<Tick>k__BackingField", 7200L);
        Call(r, "StepRaid");
        var attackers = ((HashSet<int>)Get(r, "raiders")).ToArray();
        var target = (int)Get(r, "raidSite");
        Set(((IDictionary)Get(r, "territory"))[target]!, "Friendly", false);
        Set(Unit(r, "salvage-core"), "Hull", 0);
        Call(r, "StepOutposts");
        Assert.Null(r.SectorSnapshot(1)!.Services!.Raid);
        var returns = (HashSet<int>)Get(r, "returningRaiders");
        Assert.Equal(8, returns.Count);
        var npc = ((IList)Get(r, "enemies"))
            .Cast<object>()
            .Single(e => (int)Get(e, "Id") == attackers[0]);
        Set(npc, "Position", new Vector2(15000, 11000));
        var before = (Vector2)Get(npc, "Position");
        Assert.Equal(true, Call(r, "ReturnFromRaid", npc));
        for (var i = 0; i < 90; i++)
        {
            Call(r, "ReturnFromRaid", npc);
            Call(r, "StepNpcFlight");
        }
        Assert.NotEqual(before, Get(npc, "Position"));
        Assert.Equal("return", Get(npc, "State"));
    }

    [Fact]
    public void GeneratedCacheAndConstructionPositionsAreClearAcrossSeeds()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var r = new Room(false, seed: seed);
            r.Join("Surveyor");
            var geometry = new SectorCollision(r.Sector!);
            var caches = r.Sector!.Locations.Where(l => l.Kind == "cache").ToArray();
            Assert.Equal(24, caches.Length);
            var navigation = new SectorNavigation(r.Sector, geometry);
            var arrival = r.Sector.Locations.Single(l => l.Id == 10).Position + new Vector2(140, 0);
            foreach (var cache in caches)
            {
                Assert.Null(geometry.TraceShips(cache.Position, cache.Position, 30));
                var route = navigation.Route(arrival, cache.Position, Room.Radius);
                Assert.NotEmpty(route);
                Assert.Equal(cache.Position, route[^1]);
                var previous = arrival;
                foreach (var point in route)
                {
                    Assert.Null(geometry.TraceShips(previous, point, Room.Radius));
                    previous = point;
                }
            }
            foreach (var site in r.SectorSnapshot(1)!.Services!.Outposts)
            foreach (var socket in site.Sockets)
                Assert.Null(
                    geometry.TraceShips(new(socket.X, socket.Y), new(socket.X, socket.Y), 35)
                );
        }
    }
}
