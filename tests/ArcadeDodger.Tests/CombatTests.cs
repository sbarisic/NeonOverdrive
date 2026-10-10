using System.Numerics;
using Xunit;
namespace ArcadeDodger.Tests;

public class CombatTests
{
    private static readonly Box Player = new(100, 100, 28, 28);
    private static readonly Vector2 Center = new(114, 114);
    private static void Collect(CombatModel c, PickupKind kind)
    {
        c.Pickups.Add(new Pickup { Kind = kind, Position = Center });
        c.Update(0.01f, Player, Center, false, new());
    }
    [Theory]
    [InlineData(PickupKind.Blaster)]
    [InlineData(PickupKind.Spread)]
    [InlineData(PickupKind.Rapid)]
    [InlineData(PickupKind.Multishot)]
    [InlineData(PickupKind.Pierce)]
    [InlineData(PickupKind.Overdrive)]
    public void CountsOnlyWalkOverCollectionsOnce(PickupKind kind)
    {
        var c = new CombatModel();
        Assert.All(c.CollectedPickups.Values, count => Assert.Equal(0, count));
        var pickup = new Pickup { Kind = kind, Position = new(800, 400) };
        c.Pickups.Add(pickup);
        c.Update(0.01f, Player, Center, false, new());
        Assert.Single(c.Pickups); Assert.Equal(0, c.CollectedPickups[kind]);
        pickup.Position = Center;
        c.Update(0.01f, Player, Center, false, new());
        Assert.Empty(c.Pickups); Assert.Equal(1, c.CollectedPickups[kind]);
        c.Update(0.01f, Player, Center, false, new());
        Assert.Equal(1, c.CollectedPickups[kind]);
        Collect(c, kind);
        Assert.Equal(2, c.CollectedPickups[kind]);
        foreach (PickupKind other in Enum.GetValues<PickupKind>().Where(other => other != kind))
            Assert.Equal(0, c.CollectedPickups[other]);
        c.Pickups.Add(new Pickup { Kind = kind, Position = new(800, 400), Life = 0.001f });
        c.Update(0.01f, Player, Center, false, new());
        Assert.Empty(c.Pickups); Assert.Equal(2, c.CollectedPickups[kind]);
    }
    [Fact] public void ResetClearsEveryCollectedCountAndNewRunStartsAtZero()
    {
        var c = new CombatModel();
        foreach (PickupKind kind in Enum.GetValues<PickupKind>()) Collect(c, kind);
        Assert.All(c.CollectedPickups.Values, count => Assert.Equal(1, count));
        c.Reset();
        Assert.Equal(Enum.GetValues<PickupKind>().Length, c.CollectedPickups.Count);
        Assert.All(c.CollectedPickups.Values, count => Assert.Equal(0, count));
        Assert.Empty(c.Pickups); Assert.Equal(WeaponKind.Blaster, c.Weapon);
        Collect(c, PickupKind.Rapid);
        Assert.Equal(1, c.CollectedPickups[PickupKind.Rapid]);
        Assert.Equal(0, c.CollectedPickups[PickupKind.Blaster]);
    }
    [Fact] public void AimsAndZeroAimIsSafe()
    {
        var c = new CombatModel(); c.Update(0.01f, Player, new(500,114), true, new());
        Assert.True(c.Projectiles[0].Velocity.X > 0); Assert.Equal(0, c.Projectiles[0].Velocity.Y);
        c.Reset(); c.Update(0.01f, Player, Center, true, new());
        Assert.True(c.Projectiles[0].Velocity.Y < 0);
    }
    [Fact] public void SweptHitStopsAtNearestAndPierceHitsMultiple()
    {
        var c = new CombatModel();
        var obstacles = new List<Obstacle> { new() { Bounds = new(70,0,2,10) }, new() { Bounds = new(30,0,2,10) } };
        c.Projectiles.Add(new Projectile { Position = new(0,5), Velocity = new(2000,0) });
        c.Update(0.05f, Player, Center, false, obstacles);
        Assert.Single(obstacles); Assert.Equal(70, obstacles[0].Bounds.X); Assert.Equal(1,c.Kills);
        c.Projectiles.Add(new Projectile { Position = new(0,5), Velocity = new(2000,0), RemainingHits = 2 });
        obstacles.Add(new() { Bounds = new(30,0,2,10) });
        c.Update(0.05f, Player, Center, false, obstacles);
        Assert.Empty(obstacles); Assert.Equal(3,c.Kills);
    }
    [Fact] public void ModifiersCapAndPersistAcrossWeapons()
    {
        var c = new CombatModel();
        for (int i=0;i<5;i++) { Collect(c,PickupKind.Multishot); Collect(c,PickupKind.Pierce); Collect(c,PickupKind.Overdrive); }
        Collect(c,PickupKind.Spread);
        Assert.Equal(3,c.Multishot); Assert.Equal(3,c.Pierce); Assert.Equal(3,c.Overdrive);
        Assert.Equal(5,c.CollectedPickups[PickupKind.Multishot]);
        Assert.Equal(5,c.CollectedPickups[PickupKind.Pierce]);
        Assert.Equal(5,c.CollectedPickups[PickupKind.Overdrive]);
        c.Update(0.01f, Player, new(500,114), true, new());
        Assert.Equal(9,c.Projectiles.Count); Assert.All(c.Projectiles,p=>Assert.Equal(4,p.RemainingHits));
        c.Reset(); Assert.Equal(WeaponKind.Blaster,c.Weapon); Assert.Empty(c.Projectiles); Assert.Equal(0,c.Multishot);
    }
    [Fact] public void PauseTitleAndGameOverDoNotFireAndRestartClears()
    {
        var m = new GameModel(new Random(1));
        m.Update(0.05f,0,0,true,Vector2.Zero); Assert.Empty(m.Combat.Projectiles);
        m.Start(); m.Update(0.05f,0,0,true,new(480,0));
        var p=m.Combat.Projectiles[0]; var pos=p.Position;
        m.TogglePause(); m.Update(0.05f,0,0,true,Vector2.Zero); Assert.Equal(pos,p.Position);
        m.TogglePause(); m.Obstacles.Add(new() { Bounds=m.Player, Speed=0 });
        m.Update(0.01f,0,0); Assert.Equal(GameState.GameOver,m.State);
        int shots=m.Combat.ShotsFired; m.Update(0.05f,0,0,true,Vector2.Zero); Assert.Equal(shots,m.Combat.ShotsFired);
        m.Start(); Assert.Empty(m.Combat.Projectiles); Assert.Equal(0,m.Combat.ShotsFired);
    }
    [Fact] public void TimedPickupsExpireAndCooldownLimitsFiring()
    {
        var c = new CombatModel();
        for(int i=0;i<121;i++) c.Update(0.05f,new Box(100,300,28,28),new(500,314),true,new());
        Assert.NotEmpty(c.Pickups); Assert.InRange(c.ShotsFired,20,31);
        c.Pickups.Clear(); c.Pickups.Add(new Pickup { Position=new(800,400),Life=0.01f });
        c.Update(0.05f,Player,Center,false,new()); Assert.Empty(c.Pickups);
    }
    [Fact] public void RapidFiresFasterThanBlaster()
    {
        var b=new CombatModel(); var r=new CombatModel(); Collect(r,PickupKind.Rapid);
        for(int i=0;i<20;i++) { b.Update(0.05f,Player,new(500,114),true,new()); r.Update(0.05f,Player,new(500,114),true,new()); }
        Assert.True(r.ShotsFired>b.ShotsFired);
    }
}
