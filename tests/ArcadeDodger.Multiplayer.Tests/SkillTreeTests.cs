using System.Collections;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ArcadeDodger.Server;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class SkillTreeTests
{
    public static IEnumerable<object[]> Nodes =>
        SkillCatalog.Items.Select(s => new object[] { s.Id });

    internal static void Learn(Room r, string id, ref int seq)
    {
        var d = SkillCatalog.Get(id)!;
        if (r.Snapshot().Players[0].Skills.Contains(id))
            return;
        if (d.Prerequisite is not null)
            Learn(r, d.Prerequisite, ref seq);
        Assert.True(r.Choice(1, new(++seq, id)));
    }

    [Fact]
    public void CatalogHas66UniqueNodesWithValidGuardsAndNoCycles()
    {
        Assert.Equal(66, SkillCatalog.Items.Length);
        Assert.Equal(66, SkillCatalog.Items.Select(s => s.Id).Distinct().Count());
        Assert.Equal(7, SkillCatalog.Items.Select(s => s.Branch).Distinct().Count());
        foreach (var d in SkillCatalog.Items)
        {
            Assert.Equal(1, d.Cost);
            Assert.InRange(d.MinLevel, 2, 15);
            var seen = new HashSet<string> { d.Id };
            var parent = d.Prerequisite;
            while (parent is not null)
            {
                Assert.True(seen.Add(parent));
                var p = SkillCatalog.Get(parent);
                Assert.NotNull(p);
                Assert.True(p.MinLevel < d.MinLevel);
                parent = p.Prerequisite;
            }
        }
        Assert.Equal(8, SkillCatalog.Get("luckyfind.1")!.MinLevel);
    }

    [Theory]
    [MemberData(nameof(Nodes))]
    public void EveryNodeChecksLevelPrerequisiteCostDuplicateAndReplay(string id)
    {
        var d = SkillCatalog.Get(id)!;
        var r = Start(d.MinLevel - 1);
        var seq = 0;
        Assert.False(r.Choice(1, new(++seq, id)));
        Assert.Equal("level_required", r.SkillAction(1)!.Result);
        Set(Player(r), "Experience", Room.ExperienceForLevel(100));
        if (d.Prerequisite is not null)
        {
            Assert.False(r.Choice(1, new(++seq, id)));
            Assert.Equal("prerequisite_required", r.SkillAction(1)!.Result);
            Learn(r, d.Prerequisite, ref seq);
        }
        var points = r.Snapshot().Players[0].SkillPoints;
        Assert.True(r.Choice(1, new(++seq, id)));
        Assert.Equal(points - 1, r.Snapshot().Players[0].SkillPoints);
        Assert.False(r.Choice(1, new(seq, id)));
        Assert.False(r.Choice(1, new(++seq, id)));
        Assert.Equal(points - 1, r.Snapshot().Players[0].SkillPoints);
    }

    [Fact]
    public void TwoMasteriesAreEnforcedInSequencedMailboxAndResetOnRespawn()
    {
        var r = Start(20);
        var peer = new Peer { PlayerId = 1 };
        peer.Submit(new ChoiceMessage(1, "kinetic.1"));
        peer.Submit(new ChoiceMessage(2, "laser.1"));
        peer.Submit(new ChoiceMessage(3, "rocket.1"));
        peer.ApplyPending(r);
        Assert.Equal(2, r.Snapshot().Players[0].Skills.Length);
        Assert.Equal("mastery_limit", r.SkillAction(1)!.Result);
        Assert.True(r.Choice(1, new(4, "kinetic.2")));
        Set(Player(r), "Hull", 0);
        Assert.False(r.Choice(1, new(5, "hull.1")));
        Assert.True(r.Respawn(1, new(6)));
        var p = r.Snapshot().Players[0];
        Assert.Empty(p.Skills);
        Assert.Equal(0, p.SkillPoints);
        Assert.Equal(2, p.MaxShield);
        Assert.Equal(3, p.MaxHull);
    }

    [Fact]
    public void PointsAreTwoPerLevelAndCannotOverspend()
    {
        var r = Start(2);
        Assert.Equal(2, r.Snapshot().Players[0].SkillPoints);
        Assert.True(r.Choice(1, new(1, "shield.1")));
        Assert.True(r.Choice(1, new(2, "rate.1")));
        Assert.False(r.Choice(1, new(3, "hull.1")));
        Assert.Equal("no_points", r.SkillAction(1)!.Result);
    }

    [Theory]
    [InlineData(8, 2)]
    [InlineData(16, 3)]
    [InlineData(24, 4)]
    public void BayMilestonesAreAutomaticAndResetOnDeath(int level, int weapons)
    {
        var r = Start(level);
        Assert.Equal(
            Enumerable.Range(0, 4).Select(i => i < weapons),
            r.Snapshot().Players[0].UnlockedWeapons!
        );
        Equip(r, Enumerable.Range(0, weapons).Select(_ => Weapon(1)).ToArray());
        Fire(r, 1, 3);
        Assert.Equal(weapons, r.Snapshot().Shots.Length);
        Set(Player(r), "Hull", 0);
        Assert.True(r.Respawn(1, new(4)));
        Assert.Equal(new[] { true, false, false, false }, r.Snapshot().Players[0].UnlockedWeapons);
    }

    [Fact]
    public void StringSkillProtocolRejectsLegacyNumericChoicesAndUnknownIds()
    {
        Assert.True(
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(
                    "{\"v\":10,\"type\":\"choice\",\"seq\":1,\"choice\":\"shield.1\"}"
                ),
                out var m
            )
        );
        Assert.Equal(new ChoiceMessage(1, "shield.1"), m);
        foreach (var choice in new[] { "0", "\"unknown\"", "null" })
            Assert.False(
                ClientProtocol.TryParse(
                    Encoding.UTF8.GetBytes(
                        $"{{\"v\":10,\"type\":\"choice\",\"seq\":1,\"choice\":{choice}}}"
                    ),
                    out _
                )
            );
    }

    [Fact]
    public void AllShieldAndHullTiersRestoreOnlyTheirAddedPoint()
    {
        var r = Start(20);
        var seq = 0;
        for (var i = 1; i <= 6; i++)
        {
            Set(Player(r), "Shield", 0);
            Assert.True(r.Choice(1, new(++seq, $"shield.{i}")));
            Assert.Equal(1, r.Snapshot().Players[0].Shield);
            Assert.Equal(2 + i, r.Snapshot().Players[0].MaxShield);
        }
        for (var i = 1; i <= 3; i++)
        {
            Set(Player(r), "Hull", 1);
            Assert.True(r.Choice(1, new(++seq, $"hull.{i}")));
            Assert.Equal(2, r.Snapshot().Players[0].Hull);
            Assert.Equal(3 + i, r.Snapshot().Players[0].MaxHull);
        }
    }
}
