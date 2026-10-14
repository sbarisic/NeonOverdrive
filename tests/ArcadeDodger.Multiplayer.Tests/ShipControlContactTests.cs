using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class ShipControlContactTests
{
    [Fact]
    public void ExportAuthoritativePreviewFixtures()
    {
        var fixtures = new List<object>();
        for (var level = 1; level <= 4; level++)
            foreach (var module in ModuleCatalog.Items.Where(d => d.Id >= 5))
            {
                var room = Start(9);
                var pilot = Player(room);
                var key = ModuleLevels.Key(module.Id, level);
                var weapon = Weapon(1, key) with { EmitterLevel = level };
                Set(
                    pilot,
                    "Weapons",
                    System.Collections.Immutable.ImmutableArray.Create(
                        weapon,
                        Weapon(1),
                        Weapon(1),
                        Weapon(1)
                    )
                );
                var shot = typeof(Room)
                    .GetMethod("CreateModularShot", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(room, [pilot, 0f, 0])!;
                Set(shot, "Distance", 65f);
                Set(shot, "Cursor", 1);
                typeof(Room)
                    .GetMethod("ApplyStage", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(room, [shot, key, 0f, 0, 64]);
                var descendants = ((IList)Get(room, "shots")).Cast<object>().ToArray();
                var result = descendants.FirstOrDefault() ?? shot;
                var blast = (float)Get(result, "Blast");
                if (module.Id == 26)
                {
                    var mine = ((IList)Get(room, "mines"))[0]!;
                    var payload = mine.GetType().GetProperty("Payload")!.GetValue(mine)!;
                    blast = (float)Get(payload, "Blast");
                }
                fixtures.Add(
                    new
                    {
                        weapon = new
                        {
                            emitter = ModuleLevels.Key(1, level),
                            attachments = weapon.Attachments,
                        },
                        damage = Get(result, "Damage"),
                        range = Get(result, "Range"),
                        speed = module.Id == 26
                            ? 0
                            : ((Vector2)Get(result, "Velocity")).Length() * Room.TickRate,
                        radius = blast,
                        branches = Math.Max(1, descendants.Length),
                        kind = module.Id == 26 ? "Mine"
                        : (int)Get(result, "Kind") == 4 ? "Plasma"
                        : "Kinetic",
                    }
                );
            }
        if (Environment.GetEnvironmentVariable("NEON_PREVIEW_FIXTURES") is { } path)
            System.IO.File.WriteAllText(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    new
                    {
                        definitions = ModuleLevels.Catalog,
                        stats = Enumerable
                            .Range(1, 4)
                            .Select(e => Room.CalculateWeaponStats([], e)),
                        fixtures,
                    },
                    new System.Text.Json.JsonSerializerOptions(
                        System.Text.Json.JsonSerializerDefaults.Web
                    )
                )
            );
        Assert.Equal(96, fixtures.Count);
    }

    [Fact]
    public void CursorWrapOverrideAndReleasePreserveMomentum()
    {
        var pos = new Vector2(5000);
        var velocity = new Vector2(70, 90);
        var heading = 3.1f;
        var throttle = 0f;
        Movement.Steer(
            ref pos,
            ref velocity,
            ref heading,
            ref throttle,
            Vector2.Zero,
            .1f,
            cursorHeading: -3.1f
        );
        Assert.Equal(-3.1f, heading, 4);
        Assert.Equal(new Vector2(70, 90), velocity);
        Movement.Steer(
            ref pos,
            ref velocity,
            ref heading,
            ref throttle,
            new(-1, 0),
            .1f,
            cursorHeading: -2
        );
        Assert.InRange(heading, 2.9f, 3.1f);
        var released = heading;
        Movement.Steer(ref pos, ref velocity, ref heading, ref throttle, Vector2.Zero, 1);
        Assert.Equal(released, heading);
        Assert.Equal(new Vector2(70, 90), velocity);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void CursorIntegrationIndependentOfFrequency(int hz)
    {
        Vector2 Run(int frequency)
        {
            var p = new Vector2(5000);
            var v = new Vector2(100, 20);
            float h = 0,
                t = .7f;
            for (var i = 0; i < frequency; i++)
                Movement.Steer(
                    ref p,
                    ref v,
                    ref h,
                    ref t,
                    Vector2.Zero,
                    1f / frequency,
                    cursorHeading: 2
                );
            Assert.Equal(2, h, 3);
            return v;
        }
        Assert.InRange(Vector2.Distance(Run(30), Run(hz)), 0, .2f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void VectorThrustersPublishInertiaAndTurningWithoutHealing(int tier)
    {
        var room = Start();
        var p = Player(room);
        ((int[])Get(p, "GearTiers"))[3] = tier;
        Set(p, "Hull", 2);
        Set(p, "Shield", 1);
        Set(p, "HullFraction", .4f);
        var state = room.Snapshot().Players[0];
        Assert.Equal(1 / (1 + .1f * tier), state.Inertia, 5);
        Assert.Equal(ChassisCatalog.Get(0).TurnRadians * (1 + .1f * tier), state.TurnRate, 5);
        Assert.Equal(110 * (1 + .1f * tier), state.Acceleration, 4);
        Assert.Equal(220, state.MoveSpeed);
        Assert.Equal(2, state.Hull);
        Assert.Equal(.4f, Get(p, "HullFraction"));
    }

    [Fact]
    public void CursorProtocolRejectsNonBooleanAndOldClients()
    {
        foreach (var value in new[] { "true", "false", "1", "null", "\"yes\"" })
        {
            var json =
                $$"""{"v":15,"type":"input","seq":1,"x":0,"y":0,"aim":0,"fire":false,"menu":false,"cursorSteering":{{value}}}""";
            Assert.Equal(
                value is "true" or "false",
                ClientProtocol.TryParse(Encoding.UTF8.GetBytes(json), out _)
            );
            Assert.False(
                ClientProtocol.TryParse(
                    Encoding.UTF8.GetBytes(json.Replace("\"v\":15", "\"v\":12")),
                    out _
                )
            );
        }
    }

    [Fact]
    public void FastCrossingIsSweptAndSlidesWithoutBounce()
    {
        Vector2[] hull = [new(-14, -7), new(14, -7), new(14, 7), new(-14, 7)];
        var a = new Vector2(200, 10);
        var b = Vector2.Zero;
        var va = new Vector2(12000, 90);
        var vb = Vector2.Zero;
        Assert.True(
            ShipContact.Resolve(hull, new(-200, 10), ref a, ref va, 1, hull, b, ref b, ref vb, 0)
        );
        Assert.True(a.X < 0);
        Assert.Equal(0, va.X, 3);
        Assert.Equal(90, va.Y);
        Assert.Equal(Vector2.Zero, b);
    }

    [Fact]
    public void BoundingCirclesDoNotHitParallelNarrowHulls()
    {
        var hull = ShipContact.Hull(0, 0);
        Assert.Null(
            ShipContact.Sweep(hull, new(-100, 22), new(100, 22), hull, Vector2.Zero, Vector2.Zero)
        );
    }

    [Fact]
    public void UnequalMassAndFrozenBodiesSeparateWithoutDamage()
    {
        var a = new Vector2(0);
        var b = new Vector2(12, 0);
        var va = new Vector2(100, 0);
        var vb = Vector2.Zero;
        Assert.True(
            ShipContact.Resolve(
                ShipContact.Hull(0, 0),
                a,
                ref a,
                ref va,
                1f / (28 * 14),
                ShipContact.Hull(6, 0),
                b,
                ref b,
                ref vb,
                1f / (88 * 48)
            )
        );
        Assert.True(a.Length() > Vector2.Distance(b, new(12, 0)) * 5);
        Assert.InRange(
            ShipContact.Sweep(ShipContact.Hull(0, 0), a, a, ShipContact.Hull(6, 0), b, b)?.Depth
                ?? 0,
            0,
            .002f
        );
    }

    [Fact]
    public void RoomSeparatesPlayersAndExposedBodiesWithoutOrdinaryDamage()
    {
        var room = Start();
        var second = room.Join("Second")!.Value;
        var a = Player(room);
        var b = Player(room, second);
        Set(a, "Position", new Vector2(1000, 800));
        Set(b, "Position", new Vector2(1010, 800));
        room.Disconnect(second);
        room.Step();
        var states = room.Snapshot().Players;
        Assert.Equal(2, states.Length);
        Assert.All(states, p => Assert.Equal(3, p.Hull));
        Assert.Null(
            ShipContact.Sweep(
                ShipContact.Hull(0, 0),
                new(states[0].X, states[0].Y),
                new(states[0].X, states[0].Y),
                ShipContact.Hull(0, 0),
                new(states[1].X, states[1].Y),
                new(states[1].X, states[1].Y)
            )
        );
    }

    [Fact]
    public void OccupiedArrivalSpawnsRemainClear()
    {
        var room = new Room(false);
        for (var i = 0; i < 12; i++)
            room.Join("Pilot" + i);
        var pilots = room.Snapshot().Players;
        foreach (var a in pilots)
        foreach (var b in pilots)
            if (a.Id < b.Id)
                Assert.True(Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y)) > 30);
    }

    [Fact]
    public void MenuPilotIsFixedWhileOtherFactionsSlideAroundIt()
    {
        var room = Start();
        var p = Player(room);
        room.Input(1, new(1, 0, 0, 0, false, true));
        var friendly = Enemy(room, 1010, 800, 81, 100);
        Set(friendly, "Allied", true);
        var hostile = Enemy(room, 990, 800, 82, 100);
        room.Step();
        Assert.Equal(new Vector2(1000, 800), Get(p, "Position"));
        Assert.Equal(3, Get(p, "Hull"));
        Assert.Equal(2, Get(p, "Shield"));
        foreach (var e in new[] { friendly, hostile })
            Assert.Null(
                ShipContact.Sweep(
                    ShipContact.Hull(0, 0),
                    (Vector2)Get(p, "Position"),
                    (Vector2)Get(p, "Position"),
                    ShipContact.Hull(
                        (int)e.GetType().GetProperty("Chassis")!.GetValue(e)!,
                        0,
                        EnemyCatalog.Size(1)
                    ),
                    (Vector2)Get(e, "Position"),
                    (Vector2)Get(e, "Position")
                )
            );
    }

    [Fact]
    public void RamDamageRequiresHullContactAndRespectsCooldown()
    {
        var room = Start();
        var pilot = Player(room);
        var ram = Enemy(room, 1040, 800, 85, 100);
        room.Step();
        Assert.Equal(2, Get(pilot, "Shield"));
        Set(ram, "Position", (Vector2)Get(pilot, "Position"));
        room.Step();
        var shield = (int)Get(pilot, "Shield") - (float)Get(pilot, "ShieldFraction");
        Assert.True(shield < 2);
        Set(ram, "Position", (Vector2)Get(pilot, "Position"));
        room.Step();
        Assert.Equal(shield, (int)Get(pilot, "Shield") - (float)Get(pilot, "ShieldFraction"));
        Step(room, 30);
        Set(ram, "Position", (Vector2)Get(pilot, "Position"));
        room.Step();
        Assert.True((int)Get(pilot, "Shield") - (float)Get(pilot, "ShieldFraction") < shield);
    }

    [Fact]
    public void PredictionUsesTheSameVisibleHullContact()
    {
        var p = new PlayerSnapshot(
            1,
            "Pilot",
            1000,
            800,
            0,
            3,
            2,
            1,
            false,
            true,
            VelocityX: 200,
            VelocityY: 0
        );
        var other = p with { Id = 2, X = 1040, VelocityX = 0, Menu = true };
        var frame = new RoomSnapshot(13, "snapshot", 1, [p, other], []);
        var prediction = new LocalPrediction();
        prediction.Receive(p, 1, 0);
        prediction.SetContacts(frame, 1);
        prediction.Input(0, 0, 0, false, 0);
        var result = prediction.Sample(.2)!;
        Assert.True(result.X < 1020);
        Assert.Equal(0, result.VelocityX, 3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryModuleTierRunsAndDormantCopiesAreMasked(int level)
    {
        var room = Start(9);
        var p = Player(room);
        var create = typeof(Room).GetMethod(
            "CreateModularShot",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        var apply = typeof(Room).GetMethod(
            "ApplyStage",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        foreach (var definition in ModuleCatalog.Items.Where(d => d.Id >= 5))
        {
            var key = ModuleLevels.Key(definition.Id, level);
            var shot = create.Invoke(room, [p, 0f, 0])!;
            Set(shot, "Distance", 65f);
            Set(shot, "Cursor", 1);
            Set(shot, "Program", new int[8]);
            apply.Invoke(room, [shot, key, 0f, 0, 64]);
            Assert.True(float.IsFinite((float)Get(shot, "Damage")));
            Assert.True(float.IsFinite((float)Get(shot, "Range")));
            if (definition.Id == 25)
            {
                Assert.Equal(4, Get(shot, "Kind"));
                Assert.Equal(10, ((Vector2)Get(shot, "Velocity")).Length(), 3);
                Assert.Equal(45 * ModuleLevels.Bonus(level), Get(shot, "Blast"));
            }
            if (definition.Id == 14)
                Assert.Equal(ModuleCatalog.Get(key).Value, Get(shot, "HomingRate"));
            if (definition.Id == 8)
                Assert.Equal((int)ModuleCatalog.Get(key).Value, Get(shot, "Pierce"));
            ((IList)Get(room, "shots")).Clear();
        }
        Set(p, "Experience", 0);
        var weapons = System.Collections.Immutable.ImmutableArray.Create(
            Weapon(1, ModuleLevels.Key(25, level)),
            Weapon(1),
            Weapon(1),
            Weapon(1)
        );
        Set(p, "Weapons", weapons);
        var dormant = create.Invoke(room, [p, 0f, 0])!;
        Assert.All((int[])Get(dormant, "Program"), id => Assert.Equal(0, id));
    }
}
