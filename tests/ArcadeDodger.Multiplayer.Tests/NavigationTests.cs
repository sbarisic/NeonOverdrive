using System.Numerics;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class NavigationTests
{
    [Fact]
    public void MovingTargetDoesNotStarvePendingSearch()
    {
        var sector = SectorDefinition.Generate(123);
        var navigation = new SectorNavigation(sector, new(sector));
        using var waypoint = new WaypointNavigation(navigation);
        var from = sector.Locations.First(l => l.Kind == "station").Position + new Vector2(180, 0);
        var target = sector.Locations.Last(l => l.Kind == "station").Position + new Vector2(250, 0);
        waypoint.Request(from, target, 51);
        waypoint.Advance(1);
        for (var i = 0; i < 100000 && waypoint.Pending; i++)
        {
            waypoint.Request(from, target + new Vector2(100 + i % 100, 0), 51);
            waypoint.Advance(7, double.PositiveInfinity);
        }
        Assert.False(waypoint.Pending);
        Assert.NotEmpty(waypoint.Path);
        Assert.Equal(1, waypoint.SearchesStarted);
        foreach (var point in waypoint.Path)
        {
            Assert.True(navigation.CanTravel(from, point, 51));
            from = point;
        }
    }

    [Fact]
    public void IncrementalSearchMatchesSynchronousRouteAndHonorsWorkBudget()
    {
        var sector = SectorDefinition.Generate(123);
        var geometry = new SectorCollision(sector);
        var navigation = new SectorNavigation(sector, geometry);
        var start = sector.Locations.First(l => l.Kind == "station").Position + new Vector2(180, 0);
        var target = sector.Locations.Last(l => l.Kind == "station").Position;
        using var job = navigation.Begin(start, target, 51);
        Assert.Equal(0, job.WorkItems);
        var calls = 0;
        while (!job.Complete)
        {
            var before = job.WorkItems;
            job.Advance(7);
            Assert.InRange(job.WorkItems - before, 0, 7);
            Assert.True(++calls < 100000);
        }
        Assert.True(calls > 1);
        Assert.NotEmpty(job.Path);
        Assert.Equal(navigation.Route(start, target, 51), job.Path);
        foreach (var point in job.Path)
        {
            Assert.True(navigation.CanTravel(start, point, 51));
            start = point;
        }
    }

    [Fact]
    public void RadiusCachesDoNotAllowLargeShipsThroughSmallGaps()
    {
        var sector = new SectorDefinition(
            1,
            1280,
            960,
            [],
            [new(1, 640, 330, 130), new(2, 640, 630, 130)]
        );
        var navigation = new SectorNavigation(sector, new(sector));
        var start = new Vector2(160, 480);
        var end = new Vector2(1120, 480);
        Assert.Single(navigation.Route(start, end, 14));
        var large = navigation.Route(start, end, 51);
        Assert.True(large.Length > 1);
        foreach (var point in large)
        {
            Assert.True(navigation.CanTravel(start, point, 51));
            start = point;
        }
    }

    [Fact]
    public void BlockedAndOutsideTargetsResolveToReachableInBoundsApproaches()
    {
        var sector = new SectorDefinition(1, 1280, 960, [], [new(1, 1100, 480, 150)]);
        var navigation = new SectorNavigation(sector, new(sector));
        foreach (var target in new[] { new Vector2(1100, 480), new Vector2(1500, 480) })
        {
            var start = new Vector2(160, 480);
            var path = navigation.Route(start, target, 51);
            Assert.NotEmpty(path);
            foreach (var p in path)
            {
                Assert.True(navigation.CanTravel(start, p, 51));
                start = p;
            }
        }
    }

    [Fact]
    public void FollowingAValidPathDoesNotRestartSearch()
    {
        var sector = SectorDefinition.Generate(5);
        var navigation = new SectorNavigation(sector, new(sector));
        using var waypoint = new WaypointNavigation(navigation);
        var from = sector.Locations.First(l => l.Kind == "station").Position + new Vector2(180, 0);
        var to = sector.Locations.Last(l => l.Kind == "station").Position;
        waypoint.Request(from, to, 51);
        Assert.Empty(waypoint.Path);
        Finish(waypoint);
        var path = waypoint.Path.ToArray();
        Assert.NotEmpty(path);
        foreach (var next in path)
        {
            var distance = Vector2.Distance(from, next);
            for (var d = 60; d < distance; d += 60)
            {
                waypoint.Request(Vector2.Lerp(from, next, d / distance), to, 51);
                waypoint.Advance();
                Assert.False(waypoint.Pending);
            }
            from = next;
        }
        Assert.Equal(1, waypoint.SearchesStarted);
    }

    [Fact]
    public void RetargetingDiscardsPendingWorkAndRadiusChangesInvalidatePath()
    {
        var sector = SectorDefinition.Generate(1);
        using var waypoint = new WaypointNavigation(new(sector, new(sector)));
        var from = sector.Locations.First(l => l.Kind == "station").Position + new Vector2(180, 0);
        waypoint.Request(from, sector.Locations.Last(l => l.Kind == "station").Position, 14);
        waypoint.Advance(1);
        Assert.True(waypoint.Pending);
        waypoint.Request(from, from + new Vector2(50, 0), 51);
        Finish(waypoint);
        Assert.Equal(from + new Vector2(50, 0), Assert.Single(waypoint.Path));
        Assert.Equal(2, waypoint.SearchesStarted);
        waypoint.Request(from, from + new Vector2(50, 0), 14);
        Assert.True(waypoint.Pending);
        Finish(waypoint);
        Assert.Equal(3, waypoint.SearchesStarted);
    }

    [Fact]
    public void UnreachableRouteIsCachedUntilPilotMoves()
    {
        var sector = new SectorDefinition(1, 1280, 960, [], [new(1, 160, 480, 200)]);
        using var waypoint = new WaypointNavigation(new(sector, new(sector)));
        for (var i = 0; i < 100; i++)
        {
            waypoint.Request(new(160, 480), new(1120, 480), 14);
            Finish(waypoint);
        }
        Assert.Empty(waypoint.Path);
        Assert.Equal(1, waypoint.SearchesStarted);
    }

    private static void Finish(WaypointNavigation waypoint)
    {
        for (var i = 0; waypoint.Pending && i < 10000; i++)
            waypoint.Advance(256, double.PositiveInfinity);
        Assert.False(waypoint.Pending);
    }
}
