using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public int? Join(string name)
    {
        if (
            players.Values.Any(p => p.Connected && FriendBook.Key(p.Name) == FriendBook.Key(name))
            || this.profiles.Ensure(name) != "ok"
        )
            return null;
        var key = FriendBook.Key(name);
        var returning = players.Values.FirstOrDefault(p =>
            !p.Connected && FriendBook.Key(p.Name) == key
        );
        if (returning is null)
            suspended.Remove(key, out returning);
        if (returning is not null)
        {
            if (!players.ContainsKey(returning.Id))
                players.Add(returning.Id, returning);
            returning.Connected = true;
            returning.LastReceived = 0;
            returning.Ack = 0;
            returning.LastInputTick = Tick;
            returning.Held = null;
            returning.Pending = null;
            returning.SkillReceipt = null;
            returning.WorkshopSequence = 0;
            returning.WorkshopResult = "";
            returning.EquipSequence = 0;
            returning.SocialSequence = 0;
            returning.SocialResult = "";
            if (Sector is not null)
            {
                var state = ExplorationFor(returning);
                state.Receipt = null;
                state.SessionPlayer = returning.Id;
            }
            return returning.Id;
        }
        if (players.Count >= Capacity)
            return null;
        var spawn = SelectSpawn();
        var id = ++nextPlayer;
        players.Add(
            id,
            new Player
            {
                Id = id,
                Name = name,
                Weapons = profiles.Get(name).Weapons.ToImmutableArray(),
                Position = spawn,
                Previous = spawn,
                ProtectedUntil = Tick + 60,
                LastInputTick = Tick,
                RunId = profiles.Get(name).RunId,
            }
        );
        RestoreCheckpoint(players[id], profiles.Get(name).Run);
        if (Sector is not null)
        {
            var s = ExplorationFor(players[id]);
            s.SessionPlayer = id;
            s.Receipt = null;
            s.InteractionTarget = 0;
        }
        return id;
    }

    private Vector2 SelectSpawn(int station = 10)
    {
        if (Location(station) is { } port)
            return port.Position + new Vector2(140, 0);
        var candidates = Enumerable
            .Range(0, 16)
            .Select(i => new Vector2(
                Width / 2 + MathF.Cos(i * MathF.Tau / 16) * (Width / 2 - 100),
                Height / 2 + MathF.Sin(i * MathF.Tau / 16) * (Height / 2 - 100)
            ));
        return candidates
            .OrderByDescending(c =>
                players
                    .Values.Where(p => p.Hull > 0)
                    .Select(p => p.Position)
                    .Concat(enemies.Where(e => e.Hull > 0).Select(e => e.Position))
                    .Concat(shots.Select(s => s.Position))
                    .Select(p => Vector2.DistanceSquared(c, p))
                    .DefaultIfEmpty(float.MaxValue)
                    .Min()
            )
            .First();
    }

    public bool Respawn(int id, RespawnMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        if (p.Hull > 0)
            return false;
        PilotDied(p);
        if (!p.ResetComplete)
            return false;
        var spawn = SelectSpawn(Sector is null ? 10 : ExplorationFor(p).LastStation);
        players[id] = new Player
        {
            Id = id,
            Name = p.Name,
            Weapons = profiles.Get(p.Name).Weapons.ToImmutableArray(),
            Position = spawn,
            Previous = spawn,
            ProtectedUntil = Tick + 60,
            LastInputTick = Tick,
            LastReceived = request.Sequence,
            Ack = request.Sequence,
            RunId = profiles.Get(p.Name).RunId,
            Life = p.Life + 1,
        };
        return true;
    }

    public bool Input(int id, InputMessage input)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || input.Sequence <= p.LastReceived
            || !float.IsFinite(input.X)
            || !float.IsFinite(input.Y)
            || !float.IsFinite(input.Aim)
            || MathF.Abs(input.X) > 1
            || MathF.Abs(input.Y) > 1
            || MathF.Abs(input.Aim) > MathF.PI
        )
            return false;
        p.LastReceived = input.Sequence;
        p.Pending = input; // Latest intent only: packet rate cannot increase movement rate.
        p.LastInputTick = Tick;
        return true;
    }

    public void Disconnect(int id)
    {
        if (!players.TryGetValue(id, out var p) || !p.Connected)
            return;
        p.Connected = false;
        p.RemoveAt = Tick + 150;
        p.Pending = null;
        p.Held = null;
        p.Editing = false;
        p.Menu = false;
        SaveCheckpoint(p);
    }
}
