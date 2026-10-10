using ArcadeDodger;
using Xunit;

namespace ArcadeDodger.Tests;

public sealed class GameModelTests
{
    private static GameModel Started(int seed = 42)
    {
        var model = new GameModel(new Random(seed));
        model.Start();
        return model;
    }

    [Fact]
    public void TitleDoesNotSimulate()
    {
        var m = new GameModel();
        var player = m.Player;
        m.Update(1, 1, 1);
        m.TogglePause();
        Assert.Equal(GameState.Title, m.State);
        Assert.Equal(player, m.Player);
        Assert.Equal(0f, m.Score);
        Assert.Empty(m.Obstacles);
    }

    [Fact]
    public void DiagonalHasSameSpeedAsStraight()
    {
        var a = Started(); var b = Started();
        var initial = a.Player;
        // Start at arena center; both moves are unobstructed.
        a.Update(.05f, 1, 0); b.Update(.05f, 1, -1);
        float dx = b.Player.X - initial.X, dy = b.Player.Y - initial.Y;
        Assert.Equal(a.Player.X - initial.X, MathF.Sqrt(dx * dx + dy * dy), 3);
    }

    [Theory]
    [InlineData(-1, -1, 0, 0)]
    [InlineData(1, 1, 2852, 1592)]
    public void PlayerIsClamped(float x, float y, float expectedX, float expectedY)
    {
        var m = Started();
        for (int i = 0; i < 200; i++) { m.Arena.Reset(); m.Update(.05f, x, y); }
        Assert.Equal(expectedX, m.Player.X);
        Assert.Equal(expectedY, m.Player.Y);
    }

    [Fact]
    public void FrameDeltaIsBounded()
    {
        var m = Started(); float x = m.Player.X;
        m.Update(10, 1, 0);
        Assert.Equal(GameModel.MaxDelta, m.Score, 5);
        Assert.Equal(GameModel.PlayerSpeed * GameModel.MaxDelta, m.Player.X - x, 3);
    }

    [Fact]
    public void ObstaclesFallAndAreRemoved()
    {
        var m = Started();
        var obstacle = new Obstacle { Bounds = new Box(0, 0, 30, 30), Speed = 100 };
        m.Obstacles.Add(obstacle);
        m.Update(.05f, 0, 0);
        Assert.Equal(5f, obstacle.Bounds.Y, 3);
        obstacle.Bounds = new Box(0, GameModel.ArenaHeight + 1, 30, 30);
        m.Update(.05f, 0, 0);
        Assert.DoesNotContain(obstacle, m.Obstacles);
    }

    [Fact]
    public void CollisionEndsRunAndRestartPreservesBest()
    {
        var m = Started();
        m.Update(.05f, 0, 0);
        m.Obstacles.Add(new Obstacle { Bounds = m.Player, Speed = 0 });
        m.Update(.05f, 0, 0);
        Assert.Equal(GameState.GameOver, m.State);
        Assert.True(m.BestScore > 0);
        float best = m.BestScore, score = m.Score;
        m.Update(.05f, 1, 1);
        Assert.Equal(score, m.Score);
        m.Start();
        Assert.Equal(GameState.Playing, m.State);
        Assert.Equal(0f, m.Score);
        Assert.Equal(best, m.BestScore);
        Assert.Empty(m.Obstacles);
    }

    [Fact]
    public void PauseFreezesAndResumeContinues()
    {
        var m = Started();
        m.Obstacles.Add(new Obstacle { Bounds = new Box(0, 0, 20, 20), Speed = 100 });
        m.TogglePause(); var player = m.Player; var obstacle = m.Obstacles[0].Bounds;
        m.Update(.05f, 1, 1);
        Assert.Equal(GameState.Paused, m.State);
        Assert.Equal(player, m.Player);
        Assert.Equal(obstacle, m.Obstacles[0].Bounds);
        Assert.Equal(0f, m.Score);
        m.TogglePause(); m.Update(.05f, 1, 0);
        Assert.Equal(GameState.Playing, m.State);
        Assert.True(m.Score > 0);
    }

    [Fact]
    public void SpawningIsDeterministicAndWithinArena()
    {
        var a = Started(); var b = Started();
        for (int i = 0; i < 60; i++) { a.Update(.05f, 0, 0); b.Update(.05f, 0, 0); }
        Assert.Empty(a.Obstacles);
        Assert.NotEmpty(a.Arena.Enemies);
        Assert.Equal(a.Arena.Enemies.Select(o => o.Position), b.Arena.Enemies.Select(o => o.Position));
        foreach (var o in a.Arena.Enemies) Assert.InRange(o.Position.X, o.Radius, GameModel.ArenaWidth - o.Radius);
    }

    [Fact]
    public void DifficultyIncreasesThenStopsAtBounds()
    {
        var m = Started(); float interval = m.SpawnInterval, speed = m.FallSpeed;
        for (int i = 0; i < 4000; i++) { m.Arena.Reset(); m.Update(.05f, 0, 0); }
        Assert.Equal(GameState.Playing, m.State);
        Assert.True(m.SpawnInterval < interval);
        Assert.True(m.FallSpeed > speed);
        Assert.True(m.SpawnInterval > 0);
        float cappedInterval = m.SpawnInterval, cappedSpeed = m.FallSpeed;
        for (int i = 0; i < 2000; i++) { m.Arena.Reset(); m.Update(.05f, 0, 0); }
        Assert.Equal(cappedInterval, m.SpawnInterval);
        Assert.Equal(cappedSpeed, m.FallSpeed);
    }

    [Fact]
    public void TouchingEdgesDoNotCountAsOverlap()
    {
        var a = new Box(0, 0, 10, 10);
        Assert.False(a.Intersects(new Box(10, 0, 10, 10)));
        Assert.True(a.Intersects(new Box(9, 9, 10, 10)));
    }
}
