using System.Collections.Immutable;
using System.Numerics;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class WeaponLeadTests
{
    [Fact]
    public void BeamRangeIncludesActiveRangeModifiersButStopsBeforeConversion()
    {
        var pilot = Pilot(6, 7) with
        {
            Weapons = ImmutableArray.Create(new WeaponConfig(2, [6, 7])),
        };
        var initial = pilot.WeaponStats!.Single(s => s.Emitter == 2).Range;
        Assert.Equal(initial * 1.25f * 1.3f, WeaponLead.BeamRange(pilot, 0), 2);
        pilot = pilot with { Weapons = ImmutableArray.Create(new WeaponConfig(2, [25])) };
        Assert.Equal(initial * .05f, WeaponLead.BeamRange(pilot, 0), 2);
    }

    private static PlayerSnapshot Pilot(params int[] modules) =>
        Start().Snapshot().Players[0] with
        {
            Level = 20,
            Weapons = ImmutableArray.Create(new WeaponConfig(1, modules)),
        };

    [Fact]
    public void AcceleratorUsesTravelTimeBeforeAndAfterItsCheckpoint()
    {
        var pilot = Pilot(6);
        var muzzle = ChassisCatalog.Hardpoint(
            pilot.Chassis,
            0,
            new(pilot.X, pilot.Y),
            pilot.Heading
        );
        Assert.True(
            WeaponLead.TrySolve(
                pilot,
                0,
                muzzle + new Vector2(650, 0),
                Vector2.Zero,
                out var point,
                out var time,
                out _
            )
        );
        Assert.Equal(.1f + 585f / 812.5f, time, 4);
        Assert.Equal(muzzle.X + 650, point.X, 3);
    }

    [Fact]
    public void DormantAcceleratorDoesNotChangeTravelTime()
    {
        var pilot = Pilot(6) with { Level = 1 };
        var muzzle = ChassisCatalog.Hardpoint(
            pilot.Chassis,
            0,
            new(pilot.X, pilot.Y),
            pilot.Heading
        );
        Assert.True(
            WeaponLead.TrySolve(
                pilot,
                0,
                muzzle + new Vector2(650, 0),
                Vector2.Zero,
                out _,
                out var time,
                out _
            )
        );
        Assert.Equal(1, time, 4);
    }

    [Fact]
    public void SplitAndHomingStagesDoNotInventAnExactDescendantLead()
    {
        foreach (var module in new[] { 11, 14, 26 })
        {
            var pilot = Pilot(module);
            Assert.False(
                WeaponLead.TrySolve(
                    pilot,
                    0,
                    new(pilot.X + 700, pilot.Y),
                    Vector2.Zero,
                    out _,
                    out _,
                    out var limited
                )
            );
            Assert.True(limited);
        }
    }

    [Fact]
    public void MovingTargetMeetsCentreLineAtPredictedTime()
    {
        var pilot = Pilot(6, 7);
        var muzzle = ChassisCatalog.Hardpoint(
            pilot.Chassis,
            0,
            new(pilot.X, pilot.Y),
            pilot.Heading
        );
        var target = muzzle + new Vector2(800, 0);
        var velocity = new Vector2(0, 80);
        Assert.True(
            WeaponLead.TrySolve(pilot, 0, target, velocity, out var point, out var time, out _)
        );
        Assert.InRange(Vector2.Distance(point, target + velocity * time), 0, .01f);
        Assert.Equal(65 + 812.5f * (time - .1f), Vector2.Distance(muzzle, point), 2);
    }
}
