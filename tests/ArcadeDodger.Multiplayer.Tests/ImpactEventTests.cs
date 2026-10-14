using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class ImpactEventTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ContactsReportFractionalDamageAndNeverPreannounceCover(bool shield, bool piercing)
    {
        var room = Start();
        Set(
            room,
            "geometry",
            new SectorCollision(new(1, 24000, 16000, [], [new(1, 1150, 800, 15)]))
        );
        var enemy = Enemy(room, 1100);
        if (shield)
        {
            Set(enemy, "Shield", 1);
            Set(enemy, "MaxShield", 1);
        }
        var shot = Activator.CreateInstance(
            typeof(Room).GetNestedType("Shot", BindingFlags.NonPublic)!,
            true
        )!;
        Set(shot, "Id", 100);
        Set(shot, "Owner", 1);
        Set(shot, "Position", new Vector2(1020, 800));
        Set(shot, "Velocity", new Vector2(200, 0));
        Set(shot, "Expires", 1000L);
        Set(shot, "Range", 1000f);
        Set(shot, "Damage", .25f);
        Set(shot, "Pierce", piercing ? 1 : 0);
        ((IList)Get(room, "shots")).Add(shot);
        room.Step();
        var events = room.Snapshot().CombatEvents!;
        var hit = Assert.Single(events, e => e.Material == (shield ? "shield" : "hull"));
        Assert.Equal(piercing ? 3 : 1, hit.Kind);
        Assert.Equal(1, hit.WeaponKind);
        if (piercing)
        {
            var rock = Assert.Single(events, e => e.Material == "rock");
            Assert.InRange(rock.X, 1130, 1136);
            Assert.True(rock.X > hit.X);
        }
        else
            Assert.DoesNotContain(events, e => e.Material == "rock");
    }
}
