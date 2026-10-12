using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using ArcadeDodger.Server;
using Xunit;
using Xunit.Abstractions;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class CombatPerformanceTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(256, 96)]
    [InlineData(512, 192)]
    [InlineData(1024, 256)]
    public async Task MeasureCombatTransportAndPlayback(int projectileLimit, int enemyLimit)
    {
        var folder = Environment.GetEnvironmentVariable("NEON_PERF_OUTPUT");
        var path = folder is null ? null : Path.Combine(folder, $"profiles-{projectileLimit}.json");
        if (folder is not null)
            Directory.CreateDirectory(folder);
        var store = new ProfileStore(path);
        var r = new Room(
            true,
            profiles: store,
            seed: 234,
            projectileLimit: projectileLimit,
            enemyLimit: enemyLimit
        );
        var all = SkillCatalog
            .Items.Where(s => s.Family is "strength" or "rate" or "bay")
            .Select(s => s.Id)
            .ToHashSet();
        for (var i = 0; i < 24; i++)
        {
            var id = r.Join("Perf" + i)!.Value;
            var p = Player(r, id);
            var pos = new Vector2(2000 + i % 6 * 1000, 3200 + i / 6 * 1000);
            Set(p, "Position", pos);
            Set(p, "Previous", pos);
            Set(p, "Hull", 10000);
            Set(p, "Experience", Room.ExperienceForLevel(15));
            Set(p, "Skills", new HashSet<string>(all));
            Set(
                p,
                "Weapons",
                new[]
                {
                    Weapon(1, 13, 13, 13, 13),
                    Weapon(2, 11, 11, 11, 25),
                    Weapon(2, 26),
                    Weapon(3, 11, 11, 5),
                }.ToImmutableArray()
            );
        }
        for (var i = r.Snapshot().Enemies!.Length; i < enemyLimit; i++)
            Enemy(r, 2600 + i % 16 * 300, 2800 + i / 16 * 240, 1000 + i, 10000);
        store.BackgroundWrites = true;
        var encoders = Enumerable.Range(0, 24).Select(_ => new SnapshotEncoder()).ToArray();
        var playback = new SnapshotPlayback();
        var endToEnd = new List<double>();
        var samples = new List<double>();
        var serialization = new List<double>();
        var interpolation = new List<double>();
        var frames = new List<RoomSnapshot>();
        long bytes = 0,
            allocated = GC.GetTotalAllocatedBytes();
        int peakShots = 0;
        var total = Stopwatch.StartNew();
        for (var tick = 1; tick <= 900; tick++)
        {
            var wholeTick = Stopwatch.StartNew();
            for (var id = 1; id <= 24; id++)
                r.Input(id, new(tick, 0, 0, -.4f, true, false));
            var timer = Stopwatch.StartNew();
            r.Step();
            if (tick > 300)
                samples.Add(timer.Elapsed.TotalMilliseconds);
            var frame = r.Snapshot();
            peakShots = Math.Max(peakShots, frame.Shots.Length);
            Assert.InRange(frame.Shots.Length, 0, projectileLimit);
            Assert.InRange(frame.Enemies!.Length, 0, enemyLimit);
            Assert.InRange(frame.Beams!.Length, 0, 1536);
            Assert.InRange(frame.Mines!.Length, 0, 128);
            if (tick % 2 == 0)
            {
                timer.Restart();
                for (var id = 1; id <= 24; id++)
                    bytes += encoders[id - 1]
                        .Encode(
                            r.ForViewer(frame, id) with
                            {
                                Workshop = r.Workshop(id),
                                Sector = r.SectorSnapshot(id),
                            }
                        )
                        .Length;
                if (tick > 300)
                    serialization.Add(timer.Elapsed.TotalMilliseconds);
                playback.Receive(frame, tick / 30d);
                if (tick > 300 && tick <= 480)
                    frames.Add(frame);
            }
            timer.Restart();
            playback.Sample(tick / 30d);
            playback.Sample(tick / 30d + 1d / 60);
            if (tick > 300)
                interpolation.Add(timer.Elapsed.TotalMilliseconds / 2);
            await store.FlushAsync();
            if (tick > 300)
                endToEnd.Add(wholeTick.Elapsed.TotalMilliseconds);
        }
        double P95(List<double> values) => values.Order().ElementAt((int)(values.Count * .95));
        var report = new
        {
            projectileLimit,
            enemyLimit,
            peakShots,
            simulationP95 = P95(samples),
            endToEndP95 = P95(endToEnd),
            serialization24PeersP95 = P95(serialization),
            playbackP95 = P95(interpolation),
            simulationMax = samples.Max(),
            allocatedBytes = GC.GetTotalAllocatedBytes() - allocated,
            bytesPerSecond = bytes / 30,
            elapsedMs = total.Elapsed.TotalMilliseconds,
            warmupTicks = 300,
            measuredTicks = 600,
            machine = Environment.MachineName,
            runtime = Environment.Version.ToString(),
        };
        output.WriteLine(JsonSerializer.Serialize(report));
        if (folder is not null)
        {
            File.WriteAllText(
                Path.Combine(folder, $"combat-{projectileLimit}.json"),
                JsonSerializer.Serialize(report)
            );
            File.WriteAllText(
                Path.Combine(folder, $"frames-{projectileLimit}.json"),
                JsonSerializer.Serialize(new { sector = r.Sector, frames }, ArenaService.Json)
            );
        }
    }
}
