using System.Numerics;
using Xunit;
namespace ArcadeDodger.Tests;

public class ArenaTests
{
    private static readonly Box Player = new(1426, 796, 28, 28);
    [Fact] public void CameraClampsAndCoordinatesRoundTripWithShake()
    {
        Assert.Equal(Vector2.Zero, ArenaView.CameraOrigin(new(0,0,28,28)));
        Assert.Equal(new Vector2(1920,1080), ArenaView.CameraOrigin(new(2852,1592,28,28)));
        var origin = ArenaView.CameraOrigin(Player); var shake = new Vector2(3,-2); var world = new Vector2(1600,900);
        Assert.Equal(world, ArenaView.ScreenToWorld(ArenaView.WorldToScreen(world,origin,shake),origin,shake));
        Assert.Equal(new Vector2(960,540), ArenaView.WorldToScreen(new(1440,810),origin,Vector2.Zero));
    }
    [Fact] public void RadarIsNorthUpAndMapsArenaCorners()
    {
        var radar = new Box(770,55,170,96);
        Assert.Equal(new Vector2(770,55), ArenaView.Map(Vector2.Zero,radar));
        Assert.Equal(new Vector2(940,151), ArenaView.Map(new(2880,1620),radar));
        Assert.Equal(new Vector2(855,103), ArenaView.Map(new(1440,810),radar));
    }
    [Fact] public void AlienDamageIsNearestFirstAndDoesNotRepeat()
    {
        var c = new CombatModel();
        var near = new Enemy { Position = new(100,100), Radius = 20, Health = 3 };
        var far = new Enemy { Position = new(200,100), Radius = 20, Health = 1 };
        var enemies = new List<Enemy> { far, near };
        c.Projectiles.Add(new Projectile { Position = new(0,100), Velocity = new(5000,0) });
        c.Update(.05f,Player,Vector2.Zero,false,new(),enemies);
        Assert.Equal(2,near.Health); Assert.Equal(1,far.Health); Assert.Empty(c.Projectiles);
        var p = new Projectile { Position = new(100,100), Velocity = Vector2.Zero, RemainingHits = 4 };
        c.Projectiles.Add(p);
        c.Update(.01f,Player,Vector2.Zero,false,new(),enemies);
        c.Update(.01f,Player,Vector2.Zero,false,new(),enemies);
        Assert.Equal(1,near.Health); Assert.Equal(3,p.RemainingHits);
    }
    [Fact] public void HostileSweepKillsEvenWhenEndpointsMiss()
    {
        var arena = new ArenaModel(new Random(1));
        arena.HostileProjectiles.Add(new HostileProjectile { Position = new(1300,810), Velocity = new(6000,0), Radius = 5, Life = 3 });
        Assert.True(arena.Update(.05f,Player,Player,0));
    }
    [Fact] public void HostileRelativeSweepDetectsCrossingPlayer()
    {
        var arena = new ArenaModel(new Random(1));
        var previous = new Box(1426,690,28,28); var next = new Box(1426,890,28,28);
        arena.HostileProjectiles.Add(new HostileProjectile { Position = new(1300,810), Velocity = new(5600,0), Radius = 5, Life = 3 });
        Assert.True(arena.Update(.05f,next,previous,0));
    }
    [Fact] public void PauseAndRestartFreezeAndClearAlienState()
    {
        var m = new GameModel(new Random(2)); m.Start();
        var e = new Enemy { Kind = EnemyKind.Gunner, Position = new(1800,810), Health = 3, Telegraph = .8f };
        m.Arena.Enemies.Add(e);
        m.Arena.HostileProjectiles.Add(new HostileProjectile { Position = new(0,0), Velocity = new(100,0), Life = 3 });
        m.TogglePause(); m.Update(.05f,1,1);
        Assert.Equal(new Vector2(1800,810),e.Position); Assert.Equal(.8f,e.Telegraph);
        Assert.Equal(Vector2.Zero,m.Arena.HostileProjectiles[0].Position);
        m.Start(); Assert.Empty(m.Arena.Enemies); Assert.Empty(m.Arena.HostileProjectiles);
        Assert.Equal(Player,m.Player);
    }
    [Fact] public void PickupsAndShotsUseWorldRatherThanViewportBounds()
    {
        var c = new CombatModel();
        c.Projectiles.Add(new Projectile { Position = new(1500,810), Velocity = new(100,0) });
        c.Update(.05f,Player,Vector2.Zero,false,new()); Assert.Single(c.Projectiles);
        for(int i=0;i<121;i++) c.Update(.05f,Player,Vector2.Zero,false,new());
        Assert.Contains(c.Pickups,p => p.Position.X > GameModel.Width);
    }
}
