using System.Collections;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ArcadeDodger.Server;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class ModularWeaponTests
{
    [Fact]
    public void FixedCheckpointsSplitAtTenPercentWithoutLosingTheRestOfTheTick()
    {
        var r = Start();
        Equip(r, Weapon(1, 0, 11, 17));
        Fire(r);
        r.Input(1, new(4, 0, 0, 0, false, false));
        Step(r, 6);
        var children = r.Snapshot().Shots;
        Assert.Equal(2, children.Length);
        Assert.All(
            children,
            c =>
            {
                Assert.Equal(MuzzleX + 130, c.BirthX, 2);
                Assert.True(c.X > MuzzleX + 130);
                Assert.True(c.Parent > 0);
            }
        );
        Step(r, 4);
        Assert.All(r.Snapshot().Shots, c => Assert.Equal(1, c.Effects));
        Step(r, 90);
        Assert.Empty(r.Snapshot().Shots);
    }

    [Theory]
    [InlineData(1, 650)]
    [InlineData(3, 240)]
    [InlineData(4, 300)]
    public void EmittersKeepTheirOwnVelocity(int emitter, float speed)
    {
        var r = Start();
        Equip(r, Weapon(emitter));
        Fire(r);
        Assert.Equal(speed / 30, Assert.Single(r.Snapshot().Shots).Dx, 3);
    }

    [Fact]
    public void PurchasesAndLoadoutsAreOwnedAtomicAndReplaySafe()
    {
        var r = Start(2);
        var w = Loadout(Weapon(1, 6));
        Assert.False(r.Equip(1, new(1, w)));
        r.Input(1, new(2, 0, 0, 0, false, false, true));
        r.Step();
        Assert.False(r.Equip(1, new(3, w)));
        Assert.True(r.Buy(1, new(4, 6)));
        Assert.Equal(40, r.Workshop(1).Credits);
        Assert.False(r.Buy(1, new(4, 6)));
        Assert.False(r.Buy(1, new(5, 6)));
        Assert.Equal(40, r.Workshop(1).Credits);
        w[1] = Weapon(1, 6);
        Assert.False(r.Equip(1, new(6, w)));
        w[1] = Weapon(1) with { Enabled = false };
        Assert.True(r.Equip(1, new(7, w)));
        w[0].Attachments[0] = 5;
        Assert.Equal(6, r.Snapshot().Players[0].Weapons![0].Attachments[0]);
        var clone = JsonSerializer.Deserialize<RoomSnapshot>(
            JsonSerializer.Serialize(r.Snapshot())
        )!;
        Assert.Equal(6, clone.Players[0].Weapons![0].Attachments[0]);
    }

    [Fact]
    public void ServerMailboxOrdersPurchaseAndEquipAfterEditingIntent()
    {
        var r = Start(2);
        var peer = new Peer { PlayerId = 1 };
        peer.Submit(new InputMessage(1, 0, 0, 0, false, false, true));
        peer.Submit(new BuyMessage(2, 6));
        peer.Submit(new EquipMessage(3, Loadout(Weapon(1, 6))));
        peer.ApplyPending(r);
        r.Step();
        Assert.Equal(1, r.Snapshot().Players[0].Ack);
        Assert.Equal(6, r.Snapshot().Players[0].Weapons![0].Attachments[0]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 2)]
    [InlineData(16, 3)]
    [InlineData(24, 4)]
    public void WeaponsUnlockPerRunAndResetConfigurationsOnRespawn(int level, int count)
    {
        var r = Start(level);
        Equip(r, Enumerable.Range(0, count).Select(_ => Weapon(1)).ToArray());
        Fire(r);
        Assert.Equal(count, r.Snapshot().Shots.Length);
        Set(Player(r), "Hull", 0);
        Assert.True(r.Respawn(1, new(4)));
        Assert.Equal(1, r.Snapshot().Players[0].Level);
        Assert.Equal(
            new[] { true, false, false, false },
            r.Snapshot().Players[0].Weapons.Select(w => w.Enabled)
        );
        ((IList)Get(r, "shots")).Clear();
        r.Input(1, new(5, 0, 0, 0, true, false));
        r.Step();
        Assert.Single(r.Snapshot().Shots);
    }

    [Fact]
    public void LaserDoublesAtFivePercentThenSharesAtFifteenPercent()
    {
        var r = Start();
        Equip(r, Weapon(2, 5, 0, 11));
        Fire(r);
        var b = r.Snapshot().Beams!;
        Assert.Contains(b, x => x.X == MuzzleX && x.EndX == MuzzleX + 65 && x.Power == 1);
        Assert.Contains(b, x => x.X == MuzzleX + 65 && x.Power == 2);
        Assert.Equal(2, b.Count(x => MathF.Abs(x.X - (MuzzleX + 195)) < .01f && x.Power == 1));
        var e = Enemy(r, MuzzleX + 195 + 200 * MathF.Cos(.22f), 800 + 200 * MathF.Sin(.22f));
        Fire(r, 30, 4);
        Assert.Equal(96, Health(e), 3);
    }

    [Theory]
    [InlineData(11, 2)]
    [InlineData(12, 3)]
    [InlineData(13, 6)]
    public void SplittersConserveProjectileDamage(int module, int branches)
    {
        var r = Start();
        Equip(r, Weapon(1, module));
        Fire(r);
        r.Input(1, new(4, 0, 0, 0, false, false));
        Step(r, 3);
        Assert.Equal(branches, r.Snapshot().Shots.Length);
        var sum = ((IList)Get(r, "shots")).Cast<object>().Sum(s => (float)Get(s, "Damage"));
        Assert.Equal(1, sum, 4);
    }

    [Fact]
    public void FastShotsExecuteMultipleCheckpointsAndDoNotMoveGatesWhenRangeChanges()
    {
        var r = Start();
        Equip(r, Weapon(1, 7, 5, 6));
        Fire(r);
        var shot = ((IList)Get(r, "shots"))[0]!;
        Set(shot, "Velocity", new Vector2(300, 0));
        r.Input(1, new(4, 0, 0, 0, false, false));
        r.Step();
        Assert.Equal(1690f, (float)Get(shot, "Range"), 2);
        Assert.Equal(2f, Get(shot, "Damage"));
        Assert.Equal(375f, ((Vector2)Get(shot, "Velocity")).X);
        Assert.True((float)Get(shot, "Distance") > 300);
    }

    [Theory]
    [InlineData(17, "BurnUntil")]
    [InlineData(18, "SlowUntil")]
    [InlineData(19, "RadiationUntil")]
    [InlineData(20, "CorrosionUntil")]
    [InlineData(28, "MarkUntil")]
    public void AlienEffectsActivateOnlyAfterCheckpointAndRefreshWithoutUnboundedStacking(
        int module,
        string field
    )
    {
        var r = Start();
        var e = Enemy(r, 1150);
        Equip(r, Weapon(2, module));
        Fire(r, 30);
        Assert.True((long)Get(e, field) > r.Tick);
        r.Input(1, new(33, 0, 0, 0, false, false));
        Step(r, 155);
        Assert.True((long)Get(e, field) < r.Tick);
    }

    [Fact]
    public void PierceHitsMultipleTargetsButNotTwiceAcrossAdjacentStages()
    {
        var r = Start();
        var a = Enemy(r, 1120);
        var b = Enemy(r, 1160, id: 2);
        Equip(r, Weapon(2, 8));
        Fire(r, 30);
        Assert.Equal(96, Health(a), 3);
        Assert.Equal(96, Health(b), 3);
    }

    [Fact]
    public void StabilizerNarrowsBranchesAndSeekerBendsTowardAliens()
    {
        var r = Start();
        Equip(r, Weapon(2, 9, 11));
        Fire(r);
        var branch = r.Snapshot().Beams!.First(b => MathF.Abs(b.X - (MuzzleX + 130)) < .01f);
        Assert.Equal(
            .11f,
            MathF.Abs(MathF.Atan2(branch.EndY - branch.Y, branch.EndX - branch.X)),
            3
        );
        var q = Start();
        Enemy(q, 1300, 900);
        Equip(q, Weapon(2, 14));
        Fire(q);
        Assert.Contains(q.Snapshot().Beams!, b => b.EndY > b.Y);
    }

    [Fact]
    public void OverchargerBoostsShieldWithoutBoostingHullAndLeechExcludesOverkill()
    {
        var r = Start();
        var id = r.Join("Rival")!.Value;
        Set(Player(r, id), "Position", new Vector2(1150, 800));
        Set(Player(r, id), "ProtectedUntil", 0L);
        Equip(r, Weapon(2, 10));
        Fire(r, 30);
        Assert.Equal(0, r.Snapshot().Players[1].Hull); // 2 shield costs 1 raw damage, leaving 3 for hull.
        var q = Start();
        Set(Player(q), "Shield", 0);
        Set(Player(q), "ShieldAt", 10000L);
        var e = Enemy(q, 1200, hull: 1);
        Equip(q, Weapon(2, 27, 5, 5, 5, 5));
        Fire(q, 30);
        Assert.InRange((float)Get(Player(q), "LeechFraction"), .09f, .11f);
        Assert.Equal(0, q.Snapshot().Players[0].Shield);
    }

    [Fact]
    public void TeslaStunsGravityPullsAndRelayDoesNotRecursivelyExplode()
    {
        var r = Start();
        var e = Enemy(r, 1150);
        Equip(r, Weapon(2, 21));
        Fire(r, 31);
        Assert.True((long)Get(e, "StunReady") > r.Tick);
        var q = Start();
        var target = Enemy(q, 1200);
        var pulled = Enemy(q, 1200, 850, 2);
        Equip(q, Weapon(2, 22));
        Fire(q, 15);
        Assert.True(((Vector2)Get(pulled, "Position")).Y < 850);
        var a = Start();
        var first = Enemy(a, 1200);
        var second = Enemy(a, 1250, 850, 2);
        var third = Enemy(a, 1250, 930, 3);
        Equip(a, Weapon(2, 16));
        Fire(a, 30);
        Assert.Equal(96, Health(first), 3);
        Assert.Equal(98, Health(second), 3);
        Assert.Equal(100, Health(third), 3);
    }

    [Fact]
    public void BlastsAndProximityApplyPayloadExactlyOnce()
    {
        var r = Start();
        var direct = Enemy(r, 1200);
        var splash = Enemy(r, 1200, 840, 2);
        Equip(r, Weapon(2, 23));
        Fire(r, 30);
        Assert.Equal(96, Health(direct), 3);
        Assert.Equal(98, Health(splash), 3);
        var q = Start();
        var target = Enemy(q, 1200);
        Equip(q, Weapon(2, 24));
        Fire(q, 30);
        Assert.Equal(96, Health(target), 3);
        Assert.Equal(1155, q.Snapshot().Beams![^1].EndX, 2);
    }

    [Fact]
    public void RicochetContinuesDistanceAndPlasmaConversionResumesStages()
    {
        var r = Start();
        Set(Player(r), "Position", new Vector2(Room.Width - 180, 800));
        Equip(r, Weapon(2, 15));
        Fire(r);
        Assert.Contains(r.Snapshot().Beams!, b => b.X > b.EndX);
        var q = Start();
        Equip(q, Weapon(2, 25, 5));
        Fire(q);
        Assert.Single(q.Snapshot().Shots);
        var converted = q.Snapshot().Shots[0];
        Assert.Equal(4, converted.Kind);
        Assert.True(converted.Parent > 0);
        Assert.Equal(MuzzleX + 65, converted.BirthX);
        q.Input(1, new(4, 0, 0, 0, false, false));
        Step(q, 6);
        Assert.Equal(4f, Get(((IList)Get(q, "shots"))[0]!, "Damage"));
    }

    [Fact]
    public void MinesArmExpireAndTerminateDownstreamStages()
    {
        var r = Start();
        Equip(r, Weapon(2, 26, 5));
        Fire(r);
        Assert.Single(r.Snapshot().Mines!);
        Assert.False(r.Snapshot().Mines![0].Armed);
        var e = Enemy(r, 1080);
        r.Input(1, new(4, 0, 0, 0, false, false));
        Step(r, 14);
        Assert.Equal(100, Health(e));
        r.Step();
        Assert.Empty(r.Snapshot().Mines!);
        Assert.Equal(96, Health(e));
    }

    [Fact]
    public void FourExtremeChainsRemainBoundedAndEachWeaponGetsBeamBudget()
    {
        var r = Start(15);
        Set(Player(r), "Skills", new HashSet<string> { "bay.2", "bay.3", "bay.4" });
        Equip(
            r,
            Weapon(2, 13, 13, 13, 13, 13, 13, 13, 13),
            Weapon(2, 12, 12, 12, 12),
            Weapon(2, 25),
            Weapon(2, 26)
        );
        for (var i = 0; i < 400; i++)
        {
            Fire(r, 1, i + 3);
            var f = r.Snapshot();
            Assert.InRange(f.Shots.Length, 0, Room.ProjectileCapacity);
            Assert.InRange(f.Beams!.Length, 0, 64);
            Assert.InRange(f.Mines!.Length, 0, 32);
            Assert.Equal(4, f.Beams.Select(b => b.Weapon).Distinct().Count());
        }
    }

    [Fact]
    public void EquipmentCloakBlocksMovementFireAndAlienTargetingThenExpires()
    {
        var r = Start(ai: true);
        var e = Enemy(r, 1010);
        r.Input(1, new(1, 1, 1, 0, true, true, true));
        Step(r, 70);
        var p = r.Snapshot().Players[0];
        Assert.True(p.Editing);
        Assert.Equal(1000, p.X);
        Assert.Equal(2, p.Shield);
        Assert.Empty(r.Snapshot().Shots);
        Assert.NotEqual(new Vector2(1010, 800), Get(e, "Position"));
        Assert.NotEqual("engage", Get(e, "State"));
        Step(r, 21);
        Assert.False(r.Snapshot().Players[0].Editing);
        r.Input(1, new(2, 0, 0, 0, false, false, true));
        r.Step();
        r.Disconnect(1);
        Assert.False(r.Snapshot().Players[0].Editing);
    }

    [Fact]
    public void EightAmplifiersAreAllowedAndDamageIsNotLimitedByVisualThickness()
    {
        var r = Start();
        Equip(r, Weapon(2, 5, 5, 5, 5, 5, 5, 5, 5));
        var enemy = Enemy(r, 1800, hull: 2000);
        Fire(r);
        Assert.Equal(256, r.Snapshot().Beams![^1].Power);
        Assert.Equal(2000 - 1024f / 30, Health(enemy), 2);
    }

    [Fact]
    public void BurnDeliversItsFullDurationAtAnyTickPhaseAndLeechTracksActualDotDamage()
    {
        var r = Start();
        Set(Player(r), "Shield", 0);
        Set(Player(r), "ShieldAt", 10000L);
        Equip(r, Weapon(2, 27, 17));
        var enemy = Enemy(r, 1200);
        Step(r, 28);
        Fire(r);
        r.Input(1, new(4, 0, 0, 0, false, false));
        Step(r, 90);
        Assert.Equal(100 - 4f / 30 - 3, Health(enemy), 2);
        Assert.Equal((4f / 30 + 3) * .1f, (float)Get(Player(r), "LeechFraction"), 3);
    }

    [Fact]
    public void FireRateConversionConservesIncreasedLaserDps()
    {
        var r = Start();
        Equip(r, Weapon(2, 25));
        Set(
            Player(r),
            "Skills",
            new HashSet<string> { "rate.1", "rate.2", "rate.3", "rate.4", "rate.5" }
        );
        Fire(r);
        Assert.Single(r.Snapshot().Shots);
        Assert.Equal(
            3f,
            ((IList)Get(r, "shots")).Cast<object>().Sum(s => (float)Get(s, "Damage")),
            3
        );
    }

    [Fact]
    public void SaturatedMineDeploymentRetainsProjectileUntilCapacityReturns()
    {
        var r = Start();
        Equip(r, Weapon(2, 26));
        Fire(r);
        var mines = (IList)Get(r, "mines");
        var mine = mines[0];
        while (mines.Count < 32)
            mines.Add(mine);
        r.Input(1, new(4, 0, 0, 0, false, false, true));
        r.Step();
        Assert.True(r.Equip(1, new(5, Loadout(Weapon(1, 26)))));
        Fire(r, 1, 7);
        r.Input(1, new(8, 0, 0, 0, false, false));
        Step(r, 5);
        Assert.Single(r.Snapshot().Shots);
        Assert.Equal(MuzzleX + 65, r.Snapshot().Shots[0].X, 2);
        mines.Clear();
        r.Step();
        Assert.Empty(r.Snapshot().Shots);
        Assert.Single(r.Snapshot().Mines!);
    }

    [Fact]
    public void FullRoomMixedWeaponsStayWithinNetworkAndEntityBounds()
    {
        var friends = new FriendBook();
        var r = new Room(false, friends, sectorEnabled: false);
        for (var i = 0; i < 24; i++)
        {
            var id = r.Join("Load" + i)!.Value;
            Set(Player(r, id), "Experience", Room.ExperienceForLevel(15));
            Set(
                Player(r, id),
                "Skills",
                new HashSet<string>
                {
                    "bay.2",
                    "bay.3",
                    "bay.4",
                    "rate.1",
                    "rate.2",
                    "rate.3",
                    "rate.4",
                    "rate.5",
                }
            );
            Set(Player(r, id), "Position", new Vector2(1000, 800));
            Set(Player(r, id), "Previous", new Vector2(1000, 800));
            Set(
                Player(r, id),
                "Weapons",
                System.Collections.Immutable.ImmutableArray.Create(
                    Weapon(2, 12, 12, 12, 12),
                    Weapon(2, 11, 25),
                    Weapon(2, 11, 26),
                    Weapon(3, 11, 11, 17)
                )
            );
            for (var j = 0; j < i; j++)
            {
                friends.Change("Load" + j, "Load" + i, "request");
                friends.Change("Load" + i, "Load" + j, "accept");
            }
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var tick = 1; tick <= 90; tick++)
        {
            for (var id = 1; id <= 24; id++)
                r.Input(id, new(tick, 0, 0, 0, true, false));
            r.Step();
            var f = r.Snapshot();
            Assert.InRange(f.Shots.Length, 0, Room.ProjectileCapacity);
            Assert.InRange(f.Beams!.Length, 0, 1536);
            Assert.InRange(f.Mines!.Length, 0, 128);
            Assert.All(f.Beams.GroupBy(b => b.Owner), g => Assert.InRange(g.Count(), 1, 64));
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(f, ArenaService.Json).Length < 524288);
        }
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(9),
            "A loaded three-second simulation must not stall for seconds per tick."
        );
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(29, false)]
    [InlineData(5, true)]
    public void PurchaseProtocolIsStrict(int module, bool valid)
    {
        Assert.Equal(
            valid,
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(
                    $"{{\"v\":12,\"type\":\"buy\",\"seq\":1,\"module\":{module}}}"
                ),
                out _
            )
        );
    }
}
