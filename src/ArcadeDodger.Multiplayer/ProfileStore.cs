using System.Text.Json;

namespace ArcadeDodger.Multiplayer;

/// <summary>Ordered persistence worker; publish each complete transaction after durable replacement.</summary>
public sealed partial class ProfileStore
{
    private readonly string? path;
    public bool BackgroundWrites { get; set; }
    private readonly object writer = new();
    private Task tail = Task.CompletedTask;

    public Task<string> Schedule(Func<string> action)
    {
        lock (writer)
        {
            var next = tail.ContinueWith(
                _ => action(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default
            );
            tail = next;
            return next;
        }
    }

    public Task FlushAsync()
    {
        lock (writer)
            return tail;
    }

    public string SaveRun(string name, string runId, RunCheckpoint checkpoint)
    {
        var p = Get(name);
        return p.RunId != runId
            ? "stale_run"
            : Commit(name, p with { Run = checkpoint, RunPrepared = false });
    }

    public string ResetRun(string name, string previous)
    {
        var p = Get(name);
        if (p.RunId != previous)
            return "ok";
        if (p.ReplayCount == PilotProfile.MaxReplayCount)
            return "replay_limit";
        return Commit(
            name,
            PilotProfile.Starter(p.ReplayCount + 1) with
            {
                RunId = Guid.NewGuid().ToString("N"),
                RunPrepared = true,
            }
        );
    }

    // Admission consumes a prepared death reset, or replaces an abandoned run.
    public string StartRun(string name, string previous, bool reusePrepared = true)
    {
        var p = Get(name);
        if (p.RunId != previous)
            return "stale_run";
        if (reusePrepared && p.RunPrepared)
            return Commit(name, p with { RunPrepared = false });
        return Commit(
            name,
            PilotProfile.Starter(p.ReplayCount) with
            {
                RunId = Guid.NewGuid().ToString("N"),
            }
        );
    }

    public string ForRun(string name, string runId, Func<string> operation) =>
        Get(name).RunId == runId ? operation() : "stale_run";

    public string GrantModule(string name, int module)
    {
        if (!ModuleCatalog.Valid(module))
            return "invalid_item";
        var p = Get(name);
        if (p.Owned[module] == int.MaxValue)
            return "inventory_full";
        return Commit(name, AddModule(p, module));
    }

    private volatile Dictionary<string, PilotProfile> profiles = new(StringComparer.Ordinal);

    public ProfileStore(string? path = null)
    {
        this.path = path is null ? null : Path.GetFullPath(path);
        if (this.path is not null && File.Exists(this.path))
        {
            profiles =
                JsonSerializer.Deserialize<Dictionary<string, PilotProfile>>(
                    File.ReadAllText(this.path)
                ) ?? throw new InvalidDataException("Empty profile store.");
            profiles = profiles.ToDictionary(
                p => p.Key,
                p =>
                    p.Value with
                    {
                        Run = p.Value.Run is { } run
                            ? run with
                            {
                                Skills = run
                                    .Skills.Where(s => s is not ("bay.2" or "bay.3" or "bay.4"))
                                    .Select(SkillCatalog.Migrate)
                                    .Distinct()
                                    .ToArray(),
                            }
                            : null,
                    }
            );
            if (profiles.Any(p => p.Key != FriendBook.Key(p.Key) || !Valid(p.Value)))
                throw new InvalidDataException(
                    "Invalid profile store; preserve and repair it before starting."
                );
            profiles = profiles.ToDictionary(
                p => p.Key,
                p =>
                {
                    var migrated = MigrateLoadout(p.Value);
                    if (p.Value.Equipment is null)
                    {
                        var level = Room.LevelForExperience(p.Value.Run?.Experience ?? 0);
                        migrated = (
                            migrated.Equipment! with
                            {
                                Bays = migrated
                                    .Equipment.Bays.Select(
                                        (b, i) =>
                                            b with
                                            {
                                                Enabled =
                                                    b.Enabled
                                                    && level >= ModuleCatalog.UnlockLevels[i],
                                            }
                                    )
                                    .ToArray(),
                            }
                        ).Project(migrated);
                    }
                    return migrated;
                }
            );
            File.WriteAllText(this.path + ".tmp", JsonSerializer.Serialize(profiles));
            File.Move(this.path + ".tmp", this.path, true);
        }
    }

    private static PilotProfile MigrateLoadout(PilotProfile profile)
    {
        return EquipmentState.Migrate(profile).Project(profile);
    }

    // Snapshot metadata must not copy and migrate an unbounded equipment inventory.
    public long ReplayCountFor(string name) =>
        profiles.GetValueOrDefault(FriendBook.Key(name))?.ReplayCount ?? 0;

    public PilotProfile Get(string name) =>
        MigrateLoadout(
            (profiles.GetValueOrDefault(FriendBook.Key(name)) ?? PilotProfile.Starter()).Copy()
        );

    public string Ensure(string name)
    {
        var p = Get(name);
        return profiles.ContainsKey(FriendBook.Key(name)) && p.RunId.Length > 0
            ? "ok"
            : Commit(name, p with { RunId = Guid.NewGuid().ToString("N") });
    }

    public static bool ValidLoadout(WeaponConfig[]? weapons, int[] owned)
    {
        if (weapons is null || weapons.Length != 4)
            return false;
        var used = new int[29];
        foreach (var w in weapons)
        {
            if (
                w is null
                || w.Emitter is < 0 or > 4
                || w.Attachments is null
                || w.Attachments.Length != 8
            )
                return false;
            if (w.Emitter > 0)
                used[w.Emitter]++;
            foreach (var id in w.Attachments)
            {
                if (id != 0 && id is < 5 or > 28)
                    return false;
                if (id > 0)
                    used[id]++;
            }
        }
        return Enumerable.Range(1, 28).All(i => used[i] <= owned[i]);
    }

    private static bool Valid(PilotProfile p) =>
        p is not null
        && p.Credits >= 0
        && p.ReplayCount is >= 0 and <= PilotProfile.MaxReplayCount
        && p.Owned is { Length: 29 }
        && p.Owned.All(n => n >= 0)
        && ValidLoadout(p.Weapons, p.Owned)
        && ValidRun(p.Run)
        && (p.Equipment is null || ValidEquipment(p.Equipment));

    private static bool ValidRun(RunCheckpoint? r) =>
        r is null
        || r.Experience is >= 0 and <= Room.MaxExperience
            && r.Skills is not null
            && r.Skills.All(s => SkillCatalog.Get(s) != null)
            && r.Hull >= 0
            && r.Shield >= 0
            && float.IsFinite(r.HullFraction)
            && r.HullFraction is >= 0 and < 1
            && float.IsFinite(r.ShieldFraction)
            && r.ShieldFraction is >= 0 and < 1
            && float.IsFinite(r.VelocityX)
            && float.IsFinite(r.VelocityY)
            && float.IsFinite(r.Heading)
            && float.IsFinite(r.Throttle)
            && r.Throttle is >= 0 and <= 1
            && r.ShieldDelay >= 0
            && r.HullDelay >= 0
            && r.WeaponCooldown is { Length: 4 }
            && r.WeaponCooldown.All(t => double.IsFinite(t) && t >= 0)
            && r.PlasmaCooldown is { Length: 4 }
            && r.PlasmaCooldown.All(t => t >= 0)
            && r.MineCooldown is { Length: 4 }
            && r.MineCooldown.All(t => t >= 0);

    public string Buy(string name, int module)
    {
        if (!ModuleCatalog.Valid(module) || module == 1)
            return "invalid_item";
        var p = Get(name);
        var price = ModuleCatalog.Get(module).Price;
        if (p.Credits < price)
            return "insufficient_credits";
        if (p.Owned[module] == int.MaxValue)
            return "inventory_full";
        return Commit(name, AddModule(p with { Credits = p.Credits - price }, module));
    }

    public string Equip(string name, WeaponConfig[] weapons, int? level = null)
    {
        var p = Get(name);
        return !ValidLoadout(weapons, p.Owned) ? "invalid_inventory"
            : !ModuleCatalog.LevelAllows(
                weapons,
                level ?? Room.LevelForExperience(p.Run?.Experience ?? 0)
            )
                ? "level_required"
            : Commit(
                name,
                p with
                {
                    Weapons = weapons.Select(w => w.Copy()).ToArray(),
                    Equipment = null,
                }
            );
    }

    public string Credit(string name, int amount)
    {
        var p = Get(name);
        if (amount <= 0 || p.Credits > long.MaxValue - amount)
            return "invalid_credit";
        return Commit(name, p with { Credits = p.Credits + amount });
    }

    public string Debit(string name, int amount)
    {
        var p = Get(name);
        if (amount <= 0)
            return "invalid_debit";
        return p.Credits < amount
            ? "insufficient_credits"
            : Commit(name, p with { Credits = p.Credits - amount });
    }

    private string Commit(string name, PilotProfile profile)
    {
        profile = MigrateLoadout(profile);
        var next = new Dictionary<string, PilotProfile>(profiles, StringComparer.Ordinal)
        {
            [FriendBook.Key(name)] = profile.Copy(),
        };
        try
        {
            if (path is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(next));
                File.Move(path + ".tmp", path, true);
            }
        }
        catch (IOException)
        {
            return "save_failed";
        }
        catch (UnauthorizedAccessException)
        {
            return "save_failed";
        }
        profiles = next;
        return "ok";
    }
}
