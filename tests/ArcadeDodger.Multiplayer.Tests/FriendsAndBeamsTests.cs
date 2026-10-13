using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class FriendsAndBeamsTests
{
    private static object Get(object o, string key) =>
        o.GetType()
            .GetField(key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(o)!;

    private static void Set(object o, string key, object value) =>
        o.GetType()
            .GetField(key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(o, value);

    private static object Player(Room r, int id = 1) => ((IDictionary)Get(r, "players"))[id]!;

    private static Room Start(FriendBook? book = null)
    {
        var r = new Room(false, book, sectorEnabled: false);
        r.Join("Player1");
        Set(Player(r), "Experience", Room.ExperienceForLevel(10));
        Set(Player(r), "Position", new Vector2(1000, 800));
        Set(Player(r), "Previous", new Vector2(1000, 800));
        return r;
    }

    private static void Equip(Room r, params int[] modules)
    {
        var attachments = new int[8];
        if (modules.Length > 1)
        {
            attachments[1] = ModuleCatalog.Twin;
        }
        WorkshopRig.Equip(r, new WeaponConfig(modules[0], attachments));
    }

    private static object Enemy(Room r, float x, float y, int id = 1)
    {
        var e = Activator.CreateInstance(
            typeof(Room).GetNestedType("Enemy", BindingFlags.NonPublic)!,
            true
        )!;
        Set(e, "Id", id);
        Set(e, "Hull", 100);
        Set(e, "Position", new Vector2(x, y));
        Set(e, "Previous", new Vector2(x, y));
        ((IList)Get(r, "enemies")).Add(e);
        return e;
    }

    private static int Target(Room r, string name, float x)
    {
        var id = r.Join(name)!.Value;
        Set(Player(r, id), "Position", new Vector2(x, 800));
        Set(Player(r, id), "ProtectedUntil", 0L);
        return id;
    }

    [Fact]
    public void RequestsRequireRecipientAcceptanceAndPersistAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), "neon-friends-" + Guid.NewGuid() + ".json");
        try
        {
            var book = new FriendBook(path);
            Assert.Equal("ok", book.Change(" Player1 ", "Player2", "request"));
            Assert.False(book.Mutual("Player1", "Player2"));
            Assert.Equal("request_pending", book.Change("Player2", "Player1", "request"));
            Assert.Equal("no_incoming_request", book.Change("Player1", "Player2", "accept"));
            Assert.Equal(new[] { "Player1" }, new FriendBook(path).Snapshot("player2").Incoming);
            Assert.Equal("ok", book.Change("player2", "PLAYER1", "accept"));
            var restored = new FriendBook(path);
            Assert.True(restored.Mutual("player1", " Player2 "));
            Assert.True(restored.Mutual("Player2", "Player1"));
            Assert.Equal("ok", restored.Change("Player1", "Player2", "remove"));
            Assert.False(new FriendBook(path).Mutual("Player1", "Player2"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DeclineCancelSelfRequestsAndReplayCannotCreateFriendships()
    {
        var b = new FriendBook();
        Assert.Equal("invalid_name", b.Change("A", "a", "request"));
        b.Change("A", "B", "request");
        Assert.Equal("no_incoming_request", b.Change("A", "B", "decline"));
        Assert.Equal("ok", b.Change("B", "A", "decline"));
        Assert.Empty(b.Snapshot("A").Outgoing);
        b.Change("A", "B", "request");
        Assert.Equal("ok", b.Change("A", "B", "cancel"));
        var r = Start(b);
        var target = Target(r, "Player2", 1100);
        Assert.True(r.Friend(1, new(1, "Player2", "request")));
        Assert.False(r.Friend(1, new(1, "Player2", "accept")));
        Assert.False(r.Friend(1, new(2, "Player2", "accept")));
        Assert.True(r.Friend(target, new(1, "Player1", "accept")));
        Assert.True(b.Mutual("Player1", "Player2"));
        Assert.Equal(0, r.Snapshot().Players[0].Ack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyAcceptedFriendsAreTransparentToShots(bool accepted)
    {
        var b = new FriendBook();
        b.Change("Player1", "Player2", "request");
        if (accepted)
            b.Change("Player2", "Player1", "accept");
        var r = Start(b);
        Target(r, "Player2", 1040);
        var enemy = Enemy(r, 1070, 800);
        r.Input(1, new(1, 0, 0, 0, true, false));
        r.Step();
        r.Input(1, new(2, 0, 0, 0, false, false));
        for (var i = 0; i < 4; i++)
            r.Step();
        Assert.Equal(accepted ? 2 : 1, r.Snapshot().Players[1].Shield);
        Assert.Equal(accepted ? 99 : 100, Get(enemy, "Hull"));
    }

    [Fact]
    public void RocketSplashDoesNotHurtMutualFriends()
    {
        var b = new FriendBook();
        b.Change("Player1", "Player2", "request");
        b.Change("Player2", "Player1", "accept");
        var r = Start(b);
        Target(r, "Player2", 1060);
        Enemy(r, 1070, 800);
        Equip(r, 3);
        r.Input(1, new(3, 0, 0, 0, true, false));
        for (var i = 0; i < 6; i++)
            r.Step();
        Assert.Equal(2, r.Snapshot().Players[1].Shield);
        Assert.NotEmpty(r.Snapshot().Explosions!);
    }

    [Fact]
    public void LaserIsShipAttachedAndDealsFourDamagePerSecondRatherThanOneHitPerTick()
    {
        var r = Start();
        var e = Enemy(r, 1200, 800);
        Equip(r, 2);
        for (var i = 0; i < 30; i++)
        {
            r.Input(1, new(i + 3, 0, 0, 0, true, false));
            r.Step();
        }
        Assert.Equal(96, Get(e, "Hull"));
        Assert.Empty(r.Snapshot().Shots);
        var beam = r.Snapshot().Beams![0];
        Assert.Equal(1000, beam.X);
        Assert.Equal(1184, r.Snapshot().Beams![^1].EndX, 3);
        r.Input(1, new(33, 0, 1, MathF.PI / 2, true, false));
        r.Step();
        beam = r.Snapshot().Beams![0];
        Assert.Equal(r.Snapshot().Players[0].Y, beam.Y);
        Assert.True(beam.EndY > beam.Y);
        r.Input(1, new(34, 0, 0, 0, false, false));
        r.Step();
        Assert.Empty(r.Snapshot().Beams!);
    }

    [Fact]
    public void LaserTravelGateCreatesContinuousTreeAtTenPercentAndSplitsPower()
    {
        var r = Start();
        Equip(r, 2, 5, 7);
        r.Input(1, new(3, 0, 0, 0, true, false));
        r.Step();
        var beam = r.Snapshot().Beams!;
        Assert.Contains(beam, b => MathF.Abs(b.EndX - 1130) < .01f);
        Assert.Equal(
            2,
            beam.Count(b => MathF.Abs(b.X - 1130) < .01f && MathF.Abs(b.Y - 800) < .01f)
        );
        var e = Enemy(r, 1130 + 200 * MathF.Cos(.22f), 800 + 200 * MathF.Sin(.22f));
        for (var i = 0; i < 30; i++)
        {
            r.Input(1, new(i + 4, 0, 0, 0, true, false));
            r.Step();
        }
        Assert.Equal(98, Get(e, "Hull"));
    }

    [Fact]
    public void LaserIgnoresFriendsAndStopsInMenusAndOnStaleInput()
    {
        var b = new FriendBook();
        b.Change("Player1", "Player2", "request");
        b.Change("Player2", "Player1", "accept");
        var r = Start(b);
        Target(r, "Player2", 1100);
        var e = Enemy(r, 1200, 800);
        Equip(r, 2);
        for (var i = 0; i < 30; i++)
        {
            r.Input(1, new(i + 3, 0, 0, 0, true, false));
            r.Step();
        }
        Assert.Equal(96, Get(e, "Hull"));
        Assert.Equal(2, r.Snapshot().Players[1].Shield);
        r.Input(1, new(33, 0, 0, 0, true, false, true));
        r.Step();
        Assert.Empty(r.Snapshot().Beams!);
        r.Input(1, new(34, 0, 0, 0, true, false));
        r.Step();
        for (var i = 0; i < 8; i++)
            r.Step();
        Assert.Empty(r.Snapshot().Beams!);
    }

    [Fact]
    public void SplitPlaybackNeverHasAParentChildGapAndChildrenBeginAtFork()
    {
        var playback = new SnapshotPlayback();
        playback.Receive(
            new(1, "snapshot", 10, [], [new(1, 1, 100, 100, 10, 0, 1, Modular: true)]),
            0
        );
        playback.Receive(
            new(
                1,
                "snapshot",
                12,
                [],
                [
                    new(
                        2,
                        1,
                        130,
                        110,
                        10,
                        10,
                        11,
                        Modular: true,
                        Parent: 1,
                        BirthX: 120,
                        BirthY: 100
                    ),
                    new(
                        3,
                        1,
                        130,
                        90,
                        10,
                        -10,
                        11,
                        Modular: true,
                        Parent: 1,
                        BirthX: 120,
                        BirthY: 100
                    ),
                ]
            ),
            2d / 30
        );
        for (var i = 0; i <= 12; i++)
        {
            var shots = playback.Sample((2 + i / 4d) / 30)!.Shots;
            Assert.NotEmpty(shots);
            Assert.DoesNotContain(shots, s => s.Id == 1 && shots.Any(c => c.Parent == 1));
            Assert.All(shots, s => Assert.InRange(s.X, 100, 130));
        }
    }

    [Theory]
    [InlineData("accept", true)]
    [InlineData("request", true)]
    [InlineData("force", false)]
    public void FriendWireCommandsAreStrict(string action, bool valid)
    {
        Assert.Equal(
            valid,
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(
                    $"{{\"v\":10,\"type\":\"friend\",\"seq\":1,\"name\":\"Player2\",\"action\":\"{action}\"}}"
                ),
                out _
            )
        );
    }
}
