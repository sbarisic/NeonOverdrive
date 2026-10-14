using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class ReliabilityRepairTests
{
    [Theory]
    [InlineData(44)]
    [InlineData(123)]
    public void FullRoomConvoysCompleteTripsWithoutLongTravelStalls(int seed)
    {
        var room = new Room(true, seed: seed);
        var observations = new Dictionary<int, (Vector2 At, long Tick)>();
        var delivered = false;
        for (var tick = 0; tick < 18000; tick++)
        {
            room.Step();
            if (tick % 30 != 0)
                continue;
            foreach (var agent in ((IList)Get(room, "civilians")).Cast<object>())
            {
                var ship = Get(agent, "Ship");
                if ((NpcRole)Get(ship, "Role") != NpcRole.Trader || (int)Get(ship, "Hull") <= 0)
                    continue;
                delivered |= (long)Get(agent, "CargoRevision") > 1;
                var id = (int)Get(ship, "Id");
                var at = (Vector2)Get(ship, "Position");
                if (
                    (string)Get(ship, "State") != "traveling"
                    || !observations.TryGetValue(id, out var prior)
                    || Vector2.Distance(at, prior.At) > 30
                )
                    observations[id] = (at, room.Tick);
                else
                {
                    Assert.True(
                        room.Tick - prior.Tick < 45 * Room.TickRate,
                        $"Seed {seed}: trader {id} stalled at {at}, route {Get(ship, "RouteIndex")}/{((Vector2[])Get(ship, "Route")).Length}, heading {Get(ship, "Heading")}, throttle {Get(ship, "Throttle")}"
                    );
                }
            }
        }
        Assert.True(delivered, $"No convoy delivered in seed {seed}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CoursesCanBeStagedAndFlownWithoutTeleporting(int category)
    {
        var room = new Room(false, seed: 44);
        room.Join("Racer");
        var pilot = Player(room);
        var start = room.Sector!.Locations.Single(l => l.Id == 10).Position + new Vector2(280, 0);
        Set(pilot, "Position", start);
        Set(pilot, "Previous", start);
        Assert.True(room.SectorAction(1, new(1, "race_join", category)));
        var race = ((IDictionary)Get(room, "races"))[1]!;
        var staged = false;
        var seq = 2;
        for (var tick = 0; tick < 28000; tick++)
        {
            var state = (string)Get(race, "State");
            if (state == "finished")
                break;
            Assert.Contains(
                state,
                new[] { "preparing", "staging", "countdown", "racing", "saving" }
            );
            var at = (Vector2)Get(pilot, "Position");
            var velocity = (Vector2)Get(pilot, "Velocity");
            var heading = (float)Get(pilot, "Heading");
            var throttle = (float)Get(pilot, "Throttle");
            var target =
                state == "racing"
                    ? ((Vector2[])Get(race, "Course"))[(int)Get(race, "Checkpoint")]
                    : (Vector2)Get(race, "Stage");
            var delta = target - at;
            var distance = delta.Length();
            var braking = state != "racing" && (state != "staging" || distance < 15);
            var desired =
                distance > .01f
                    ? Vector2.Normalize(delta)
                        * MathF.Min(180, MathF.Sqrt(120 * MathF.Max(0, distance - 5)))
                    : Vector2.Zero;
            var correction = (desired - velocity) * 2;
            var angle =
                braking && state == "staging"
                    ? (float)Get(race, "StageHeading")
                    : MathF.Atan2(correction.Y, correction.X);
            var want =
                !braking && MathF.Abs(MathF.IEEERemainder(angle - heading, MathF.Tau)) < .45f
                    ? Math.Clamp(correction.Length() / 110, 0, 1)
                    : 0;
            var throttleInput = Math.Clamp((throttle - want) * 60, -1, 1);
            room.Input(
                1,
                new(
                    seq++,
                    0,
                    throttleInput,
                    angle,
                    false,
                    false,
                    Brake: braking,
                    CursorSteering: true
                )
            );
            room.Step();
            staged |= state == "countdown";
        }
        Assert.True(staged);
        Assert.Equal("finished", Get(race, "State"));
        Assert.True((float)Get(race, "Result") > 0);
    }

    [Fact]
    public void ReceiptBackpressureRetainsEveryOutcomeAndRejectsExcessCommands()
    {
        var room = new Room(false, seed: 44);
        room.Join("Pilot");
        for (var seq = 1; seq <= 70; seq++)
            room.SectorAction(1, new(seq, "radio_close"));
        var outcomes = room.SectorSnapshot(1)!.Pilot!.Receipts!;
        Assert.Equal(65, outcomes.Length);
        var rejected = Assert.Single(outcomes, r => r.Result == "command_queue_full");
        Assert.Equal(65, rejected.Sequence);
        Assert.Equal(70, rejected.ThroughSequence);
        room.SectorAction(1, new(71, "receipts_ack", rejected.Cursor));
        Assert.Empty(room.SectorSnapshot(1)!.Pilot!.Receipts!);
        Assert.True(room.SectorAction(1, new(72, "radio_close")));
    }

    private static object? Call(Room room, string method, params object?[] args) =>
        typeof(Room)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(room, args);

    public static IEnumerable<object[]> ChassisCases() =>
        Enumerable
            .Range(0, 7)
            .SelectMany(c =>
                new[] { false, true }.SelectMany(tow =>
                    Enumerable.Range(0, 5).Select(tier => new object[] { c, tow, tier })
                )
            );

    [Theory]
    [MemberData(nameof(ChassisCases))]
    public void EveryStockChassisEscapesPlanetWithoutCruise(int chassis, bool towing, int thrusters)
    {
        var design = ChassisCatalog.Get(chassis);
        var geometry = new SectorCollision(
            new(1, 24000, 16000, [new(1, "planet", "Test", 5000, 5000, 1160, Solid: true)], [])
        );
        var at = new Vector2(6160 + design.Radius - .01f, 5000);
        var velocity = Vector2.Zero;
        float heading = 0,
            throttle = 0;
        var seconds = towing ? 15 : 10;
        for (var i = 0; i < seconds * 120; i++)
            Movement.Steer(
                ref at,
                ref velocity,
                ref heading,
                ref throttle,
                new(0, -1),
                1f / 120,
                design.Speed * (towing ? .6f : 1),
                design.Acceleration * (1 + .1f * thrusters),
                design.Steering * (1 + .1f * thrusters),
                geometry,
                radius: design.Radius
            );
        Assert.True(at.X >= 6560, $"Chassis {chassis}, tow {towing}: altitude {at.X - 6160}");
        Assert.True(velocity.X > 0);
    }

    [Fact]
    public void RepeatedRefugeUpdatesDoNotRerollManifest()
    {
        var room = new Room(false, seed: 44);
        Call(room, "SpawnCivilian", NpcRole.Trader, 0);
        var agent = ((IList)Get(room, "civilians"))[0]!;
        var npc = Get(agent, "Ship");
        var at = room.Sector!.Locations.Single(l => l.Id == 10).Position;
        Set(npc, "Position", at + new Vector2(160, 0));
        Set(npc, "LastHit", 19L);
        Set(room, "<Tick>k__BackingField", 20L);
        var revision = Get(agent, "CargoRevision");
        var stock = ((IList)Get(agent, "Stock")).Cast<TradeStock>().Select(s => s.Id).ToArray();
        for (var i = 0; i < 120; i++)
            Call(room, "MoveCivilian", npc);
        Assert.Equal(revision, Get(agent, "CargoRevision"));
        Assert.Equal(stock, ((IList)Get(agent, "Stock")).Cast<TradeStock>().Select(s => s.Id));
    }

    [Fact]
    public void ShieldDamageChargesTeslaAndLeech()
    {
        var room = Start();
        var target = Enemy(room, 1100);
        Set(target, "Shield", 10);
        Set(target, "MaxShield", 10);
        var pilot = Player(room);
        Set(pilot, "Shield", 0);
        var shot = Activator.CreateInstance(
            typeof(Room).GetNestedType("Shot", BindingFlags.NonPublic)!,
            true
        )!;
        Set(shot, "Owner", 1);
        Set(shot, "Leeches", 1f);
        Set(shot, "Teslas", 1f);
        Assert.Equal(2f, Call(room, "Hit", shot, null, target, 2f));
        Assert.Equal(2f, Get(target, "Charge"));
        Assert.Equal(.2f, (float)Get(pilot, "LeechFraction"), 4);
        Assert.Equal(100, Get(target, "Hull"));
    }

    [Fact]
    public void GravityWeaponsNeverMoveFixedOutpostBodies()
    {
        var room = Start();
        var target = Enemy(room, 1100);
        target.GetType().GetProperty("Type")!.SetValue(target, "salvage-turret");
        room.Step();
        var before = Get(target, "Position");
        var shot = Activator.CreateInstance(
            typeof(Room).GetNestedType("Shot", BindingFlags.NonPublic)!,
            true
        )!;
        Set(shot, "Owner", 1);
        Set(shot, "Gravity", 1f);
        Set(shot, "GravityRadius", 400f);
        Call(room, "Pull", shot, new Vector2(1200, 800), 1f);
        Assert.Equal(before, Get(target, "Position"));
    }

    [Fact]
    public void SmugglingReroutesWithoutChangingFrozenRewardRegion()
    {
        var room = new Room(false, seed: 44);
        room.Join("Pilot");
        var p = Player(room);
        Set(p, "Docked", 10);
        Assert.True(room.SectorAction(1, new(1, "mission_smuggling")));
        var state = Call(room, "Living", p)!;
        var region = Get(state, "JobRegion");
        var destination = ((int[])Get(state, "Targets"))[0];
        var control = ((IDictionary)Get(room, "territory"))[destination]!;
        Set(control, "Friendly", false);
        Call(room, "StepWorldJobs");
        Assert.NotEqual(destination, ((int[])Get(state, "Targets"))[0]);
        Assert.Equal(region, Get(state, "JobRegion"));
        Assert.Equal(1, Get(state, "IllegalCargo"));
    }

    [Fact]
    public void ConvoyManifestWaitsForReservedGoodsBeforeLoadingAtNewStation()
    {
        var room = new Room(false, seed: 44);
        Call(room, "SpawnCivilian", NpcRole.Trader, 0);
        var agent = ((IList)Get(room, "civilians"))[0]!;
        var stock = ((IList)Get(agent, "Stock")).Cast<TradeStock>().Select(s => s.Id).ToArray();
        var reserved = (HashSet<int>)Get(agent, "Reserved");
        reserved.Add(stock[0]);
        Set(Get(agent, "Ship"), "Home", 11);
        Call(room, "LoadManifest", agent);
        Assert.Equal(stock, ((IList)Get(agent, "Stock")).Cast<TradeStock>().Select(s => s.Id));
        Assert.True((bool)Get(agent, "ManifestPending"));
        reserved.Clear();
        Call(room, "MoveCivilian", Get(agent, "Ship"));
        Assert.Empty(
            ((IList)Get(agent, "Stock")).Cast<TradeStock>().Select(s => s.Id).Intersect(stock)
        );
        Assert.False((bool)Get(agent, "ManifestPending"));
    }

    [Fact]
    public void InterestMarginDoesNotExposeSelectableContacts()
    {
        var room = new Room(false, seed: 44);
        room.Join("Viewer");
        room.Join("Hidden");
        var at = (Vector2)Get(Player(room), "Position");
        Set(Player(room, 2), "Position", at + new Vector2(1250, 0));
        Enemy(room, at.X + 1250, at.Y, 77);
        var frame = room.ForViewer(room.Snapshot(), 1);
        Assert.Single(frame.Players);
        Assert.DoesNotContain(frame.Enemies!, e => e.Id == 77);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void AssistedThrustAgreesAcrossPredictionFrameRates(int hz)
    {
        (Vector2 At, Vector2 Velocity) Fly(int frequency)
        {
            var geometry = new SectorCollision(
                new(1, 24000, 16000, [new(1, "planet", "Test", 5000, 5000, 1160, Solid: true)], [])
            );
            var at = new Vector2(6180, 5000);
            var velocity = Vector2.Zero;
            float heading = .3f,
                throttle = 0;
            for (var i = 0; i < frequency * 8; i++)
                Movement.Steer(
                    ref at,
                    ref velocity,
                    ref heading,
                    ref throttle,
                    new(0, -1),
                    1f / frequency,
                    geometry: geometry,
                    radius: 20
                );
            return (at, velocity);
        }
        var expected = Fly(120);
        var actual = Fly(hz);
        Assert.InRange(Vector2.Distance(expected.At, actual.At), 0, .7f);
        Assert.InRange(Vector2.Distance(expected.Velocity, actual.Velocity), 0, .3f);
    }

    [Fact]
    public void DelayedCommandCompletionSurvivesNewerAcknowledgements()
    {
        var room = new Room(false, seed: 44);
        room.Join("Pilot");
        room.SectorAction(1, new(1, "radio_close"));
        var state = Call(room, "ExplorationFor", Player(room))!;
        var receipts = (IDictionary)Get(state, "Receipts");
        receipts.Clear();
        var property = state.GetType().GetProperty("Receipt")!;
        property.SetValue(state, new SectorReceipt(1, "pending"));
        room.SectorAction(1, new(2, "radio_close"));
        var newer = room.SectorSnapshot(1)!.Pilot!.Receipts!.Max(r => r.Cursor);
        room.SectorAction(1, new(3, "receipts_ack", newer));
        Assert.Equal("pending", Assert.Single(room.SectorSnapshot(1)!.Pilot!.Receipts!).Result);
        property.SetValue(state, new SectorReceipt(1, "persistence_failed"));
        var completion = Assert.Single(room.SectorSnapshot(1)!.Pilot!.Receipts!);
        Assert.Equal(1, completion.Sequence);
        Assert.True(completion.Cursor > newer);
        Assert.Equal("persistence_failed", completion.Result);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(44)]
    [InlineData(123)]
    [InlineData(777)]
    public void NewCiviliansUseClearDistributedBerthsAndScaledDurability(int seed)
    {
        var room = new Room(false, seed: seed);
        for (var i = 0; i < 3; i++)
            Call(room, "SpawnCivilian", NpcRole.Trader, i);
        var ships = room.Snapshot().Enemies!.Where(e => e.Role == "trader").ToArray();
        Assert.Equal(3, ships.Length);
        Assert.Equal(3, ships.Select(e => e.Home).Distinct().Count());
        var geometry = (SectorCollision)Get(room, "geometry");
        foreach (var ship in ships)
        {
            Assert.Null(geometry.TraceShips(new(ship.X, ship.Y), new(ship.X, ship.Y), ship.Radius));
            var design = ChassisCatalog.Get(ship.Chassis);
            var scale = EnemyCatalog.Health(ship.Level) / EnemyCatalog.Health(1);
            Assert.Equal((int)MathF.Ceiling(60 * design.Hull / 8f * scale), ship.MaxHull);
            Assert.Equal((int)MathF.Ceiling(30 * design.Shield / 4f * scale), ship.MaxShield);
            foreach (var other in ships.Where(e => e.Id != ship.Id))
                Assert.True(
                    Vector2.Distance(new(ship.X, ship.Y), new(other.X, other.Y))
                        > ship.Radius + other.Radius
                );
        }
    }

    [Fact]
    public void ResultsAreCorrelatedAndRetainedUntilAcknowledged()
    {
        var room = new Room(false, seed: 44);
        room.Join("Pilot");
        room.SectorAction(1, new(1, "radio_close"));
        room.SectorAction(1, new(2, "radio_close"));
        var receipts = room.SectorSnapshot(1)!.Pilot!.Receipts!;
        Assert.Equal(new[] { 1, 2 }, receipts.Select(r => r.Sequence));
        Assert.All(receipts, r => Assert.False(string.IsNullOrEmpty(r.Run)));
        room.SectorAction(1, new(3, "receipts_ack", receipts[0].Cursor));
        Assert.Equal(2, Assert.Single(room.SectorSnapshot(1)!.Pilot!.Receipts!).Sequence);
    }
}
