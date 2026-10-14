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
            var pos = new Vector2(2000 + i % 6 * 3000, 14500 + i / 6 * 200);
            Set(p, "Position", pos);
            Set(p, "Previous", pos);
            Set(p, "Hull", 10000);
            Set(p, "Experience", Room.ExperienceForLevel(32));
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
        // Include shared structures and the three roaming contacts in the same NPC budget.
        var builder = Player(r, 1);
        var originalPosition = (Vector2)Get(builder, "Position");
        Set(builder, "Position", r.Sector!.Locations.Single(l => l.Id == 40).Position);
        store.Credit("Perf0", 1000);
        var buildSequence = 0;
        foreach (var action in new[] { "activate", "turret_one", "turret_two", "repair_bot" })
            r.SectorAction(1, new(++buildSequence, action, 40));
        Assert.True(r.SectorSnapshot(1)!.Services!.Outposts.Single(s => s.Id == 40).Active);
        Set(builder, "Position", originalPosition);
        Set(builder, "Previous", originalPosition);
        for (var i = r.Snapshot().Enemies!.Length; i < enemyLimit; i++)
            Enemy(r, 2200 + i % 16 * 700, 14000 + i / 16 * 100, 1000 + i, 10000);
        store.BackgroundWrites = true;
        var encoders = Enumerable.Range(0, 24).Select(_ => new SnapshotEncoder()).ToArray();
        var playback = new SnapshotPlayback();
        var endToEnd = new List<double>();
        var samples = new List<double>();
        var serialization = new List<double>();
        var interpolation = new List<double>();
        var frames = new List<RoomSnapshot>();
        var viewerFrames = new List<RoomSnapshot>();
        long bytes = 0,
            allocated = GC.GetTotalAllocatedBytes();
        int peakShots = 0;
        var rootsByPilot = new int[24];
        var quietWindow = new int[24];
        var maximumQuietTicks = 0;
        var total = Stopwatch.StartNew();
        for (var tick = 1; tick <= 900; tick++)
        {
            var wholeTick = Stopwatch.StartNew();
            for (var id = 1; id <= 24; id++)
            {
                // Keep the synthetic durability above normal chassis capacity; territorial
                // repair otherwise clamps it and turns this saturation test into a death test.
                Set(Player(r, id), "LastDamage", r.Tick);
                r.Input(id, new(tick + 4, 0, 0, -.4f, true, false));
            }
            var readyBefore = Enumerable
                .Range(1, 24)
                .Select(id => ((double[])Get(Player(r, id), "WeaponReady")).ToArray())
                .ToArray();
            var timer = Stopwatch.StartNew();
            r.Step();
            if (tick > 300)
                samples.Add(timer.Elapsed.TotalMilliseconds);
            var frame = r.Snapshot();
            if (tick > 300)
            {
                for (var pilot = 0; pilot < 24; pilot++)
                    quietWindow[pilot]++;
                // A contact-range round can hit and disappear in its birth tick.
                // Advancing a projectile bay's cooldown records actual admitted fire,
                // independent of whether that round survives into the snapshot.
                for (var pilot = 0; pilot < 24; pilot++)
                {
                    var ready = (double[])Get(Player(r, pilot + 1), "WeaponReady");
                    foreach (var bay in new[] { 0, 3 })
                        if (ready[bay] > readyBefore[pilot][bay])
                        {
                            rootsByPilot[pilot]++;
                            quietWindow[pilot] = 0;
                        }
                }
                maximumQuietTicks = Math.Max(maximumQuietTicks, quietWindow.Max());
            }
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
                {
                    frames.Add(frame);
                    viewerFrames.Add(
                        r.ForViewer(frame, 1) with
                        {
                            Workshop = r.Workshop(1),
                            Sector = r.SectorSnapshot(1),
                        }
                    );
                }
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
            minimumRootShotsPerPilot = rootsByPilot.Min(),
            maximumQuietTicks,
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
            var recordingEncoder = new SnapshotEncoder();
            var wireFrames = viewerFrames
                .Select(f => System.Text.Encoding.UTF8.GetString(recordingEncoder.Encode(f)))
                .ToArray();
            File.WriteAllText(
                Path.Combine(folder, $"combat-{projectileLimit}.json"),
                JsonSerializer.Serialize(report)
            );
            File.WriteAllText(
                Path.Combine(folder, $"frames-{projectileLimit}.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        sector = r.Sector,
                        frames,
                        wireFrames,
                    },
                    ArenaService.Json
                )
            );
        }
        if (projectileLimit == Room.ProjectileCapacity)
        {
            Assert.True(
                rootsByPilot.Min() > 0 && maximumQuietTicks < 300,
                $"Saturated combat starved a pilot: minimum roots {rootsByPilot.Min()}, longest gap {maximumQuietTicks} ticks."
            );
            if (Environment.GetEnvironmentVariable("NEON_PERF_ENFORCE") == "1")
                Assert.True(
                    report.endToEndP95 < 25,
                    $"Production server p95 {report.endToEndP95:F2} ms exceeds 25 ms."
                );
        }
    }
}
