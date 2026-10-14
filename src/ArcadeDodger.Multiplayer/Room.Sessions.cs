using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private sealed class Admission
    {
        public string Result = "pending";
        public Player? Previous;
        public volatile bool Cancelled;
    }

    private readonly Dictionary<string, Admission> pendingJoins = [];
    public int AdmissionCount => players.Count + pendingJoins.Count;

    public void CancelPendingJoin(string name)
    {
        if (pendingJoins.Remove(FriendBook.Key(name), out var admission))
            admission.Cancelled = true;
    }

    public int? Join(string name) => TryJoin(name, out _);

    public int? TryJoin(string name, out string error)
    {
        error = "";
        var key = FriendBook.Key(name);
        if (players.Values.Any(p => p.Connected && FriendBook.Key(p.Name) == key))
        {
            error = "name_in_use";
            return null;
        }
        if (pendingJoins.TryGetValue(key, out var admission))
        {
            if (admission.Result == "pending")
            {
                error = "pending";
                return null;
            }
            pendingJoins.Remove(key);
            if (admission.Result != "ok")
            {
                error = admission.Result;
                return null;
            }
            return AddFreshPilot(name, admission.Previous);
        }
        var returning =
            players.Values.FirstOrDefault(p => FriendBook.Key(p.Name) == key)
            ?? suspended.GetValueOrDefault(key);
        if (!players.ContainsKey(returning?.Id ?? -1) && AdmissionCount >= Capacity)
        {
            error = "room_full";
            return null;
        }
        if (
            returning is not null
            && !returning.RecoveryDisabled
            && clock.GetElapsedTime(returning.DisconnectedAt) < TimeSpan.FromSeconds(60)
        )
        {
            suspended.Remove(key);
            players[returning.Id] = returning;
            returning.Connected = true;
            ResetSession(returning);
            returning.Recovered = true;
            return returning.Id;
        }
        if (returning is { Hull: <= 0, ResetComplete: false })
        {
            if (returning.ResetResult.Length > 0 && returning.ResetResult != "ok")
            {
                error = returning.ResetResult;
                returning.ResetResult = "";
                return null;
            }
            PilotDied(returning);
            if (!returning.ResetComplete)
            {
                error = "pending";
                return null;
            }
        }
        error = profiles.Ensure(name);
        if (error != "ok")
            return null;
        var previousRun = profiles.Get(name).RunId;
        admission = new() { Previous = returning };
        pendingJoins[key] = admission;
        if (returning is not null)
        {
            // Retire the abandoned body while admission waits; it cannot create new rewards.
            returning.RecoveryDisabled = true;
            RetireRun(returning);
            players.Remove(returning.Id);
            suspended[key] = returning;
        }
        Persist(
            () =>
                admission.Cancelled
                    ? "cancelled"
                    : profiles.StartRun(
                        name,
                        previousRun,
                        returning is null || returning.Hull <= 0
                    ),
            result => admission.Result = result
        );
        return TryJoin(name, out error);
    }

    private int AddFreshPilot(string name, Player? previous)
    {
        var spawn = SelectSpawn();
        var id = previous?.Id ?? ++nextPlayer;
        var profile = profiles.Get(name);
        suspended.Remove(FriendBook.Key(name));
        players[id] = new Player
        {
            Id = id,
            Name = name,
            Weapons = profile.Weapons.ToImmutableArray(),
            Position = spawn,
            Previous = spawn,
            ProtectedUntil = Tick + 60,
            LastInputTick = Tick,
            RunId = profile.RunId,
            Life = (previous?.Life ?? -1) + 1,
        };
        LoadEquipment(players[id], profile);
        ResetSession(players[id]);
        if (Sector is not null)
            ExplorationFor(players[id]).InteractionTarget = 0;
        return id;
    }

    private void ResetSession(Player p)
    {
        p.LastReceived = 0;
        p.Ack = 0;
        p.LastInputTick = Tick;
        p.Held = null;
        p.Pending = null;
        p.SkillReceipt = null;
        p.WorkshopSequence = 0;
        p.WorkshopResult = "";
        p.EquipSequence = 0;
        p.SocialSequence = 0;
        p.SocialResult = "";
        p.LeaveSequence = 0;
        if (Sector is not null)
        {
            var state = ExplorationFor(p);
            state.Receipt = null;
            state.Hailing = 0;
            state.SessionPlayer = p.Id;
        }
    }

    public bool Leave(int id, LeaveMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || (!p.Connected && p.LeaveSequence == 0)
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        p.LeaveSequence = request.Sequence;
        p.RecoveryDisabled = true;
        Disconnect(id);
        return true;
    }

    public int LeaveSequence(int id) => players.GetValueOrDefault(id)?.LeaveSequence ?? 0;

    public string JoinKind(int id) => players[id].Recovered ? "recovered" : "fresh";

    public string ResetResult(int id) =>
        players.TryGetValue(id, out var p) && p.Hull <= 0
            ? p.ResetPending
                ? "pending"
                : p.ResetComplete
                    ? "ok"
                    : p.ResetResult
            : "";

    private Vector2 SelectSpawn(int station = 10, int ignorePilot = 0, int chassis = 0)
    {
        if (Location(station) is { } port)
        {
            var radius = ChassisCatalog.Get(chassis).Radius;
            for (var i = 0; i < 24; i++)
            {
                var angle = i * MathF.Tau / 24;
                var point =
                    port.Position
                    + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Math.Max(140, 92 + radius);
                if (ClearSpawn(point, radius, ignorePilot: ignorePilot) == point)
                    return point;
            }
            return ClearSpawn(
                port.Position + new Vector2(140, 0),
                radius,
                ignorePilot: ignorePilot
            );
        }
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
        SaveCheckpoint(players[id]);
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
        CancelCruise(p);
        ReleaseTow(p);
        p.Throttle = 0;
        p.DisconnectedAt = clock.GetTimestamp();
        p.RemoveAt = Tick + 150;
        p.Pending = null;
        p.Held = null;
        p.Editing = false;
        p.Menu = false;
        SaveCheckpoint(p);
    }
}
