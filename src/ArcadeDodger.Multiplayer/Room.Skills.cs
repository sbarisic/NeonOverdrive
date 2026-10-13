using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public const string EnergySiphon = "siphon.1";
    public static int SkillCount => SkillCatalog.Items.Length;

    private static bool HasSkill(Player p, string id) => p.Skills.Contains(id);

    private static int Rank(Player p, string family) => SkillCatalog.Rank(p.Skills, family);

    private static float Value(Player p, string family, float fallback = 0) =>
        SkillCatalog.Value(p.Skills, family, fallback)
        + (
            family == "collector" ? 20 * Gear(p, 6)
            : family == "fortune" ? .1f * Gear(p, 7)
            : 0
        );

    private static int SkillPointsFor(Player p) =>
        Math.Max(0, 2 * (LevelForExperience(p.Experience) - 1) - p.Skills.Count);

    private static int MaxShieldFor(Player p) =>
        ChassisCatalog.Get(p.Chassis).Shield + Rank(p, "shield") + Gear(p, 0);

    private static int MaxHullFor(Player p) =>
        ChassisCatalog.Get(p.Chassis).Hull + Rank(p, "hull") + Gear(p, 8);

    private static float MoveSpeedFor(Player p) =>
        ChassisCatalog.Get(p.Chassis).Speed
        * (1 + Value(p, "slipstream") + Value(p, "cruise") + .05f * Gear(p, 2));

    private static float AccelerationFor(Player p) =>
        ChassisCatalog.Get(p.Chassis).Acceleration * (1 + Value(p, "thruster") + .1f * Gear(p, 3));

    private static float SteeringFor(Player p) =>
        ChassisCatalog.Get(p.Chassis).Steering * (1 + Value(p, "vector") + .1f * Gear(p, 3));

    private static int ShieldDelay(Player p) =>
        (int)(MathF.Max(1, Value(p, "recharge", 5) - .5f * Gear(p, 1)) * TickRate);

    private static bool Unlocked(Player p, int i) =>
        i == 0 || LevelForExperience(p.Experience) >= ModuleCatalog.UnlockLevels[i];

    private static bool Active(Player p, int i) =>
        Unlocked(p, i) && p.Weapons[i].Enabled && p.Weapons[i].Emitter > 0;

    private static string WeaponFor(Player p) =>
        string.Join(
            " + ",
            p.Weapons.Where((w, i) => Active(p, i))
                .Select(w =>
                    w.Emitter switch
                    {
                        2 => "laser",
                        3 => "rocket",
                        4 => "plasma",
                        _ => "blaster",
                    }
                )
        );

    private static WeaponStats Stats(Player p, int emitter)
    {
        if (p.StatsSkills != p.Skills || p.StatsCount != p.Skills.Count)
        {
            p.StatsSkills = p.Skills;
            p.StatsCount = p.Skills.Count;
            p.StatsCache = Enumerable
                .Range(1, 4)
                .Select(e =>
                {
                    var s = CalculateWeaponStats(p.Skills, e);
                    var bonus =
                        (1 + Value(p, "strength") + .05f * Gear(p, 10))
                        / (1 + Value(p, "strength"));
                    return s with
                    {
                        Damage = s.Damage * bonus,
                        StatusPower = s.StatusPower * bonus,
                    };
                })
                .ToArray();
        }
        return p.StatsCache[emitter - 1];
    }

    public static WeaponStats CalculateWeaponStats(IEnumerable<string> learned, int emitter)
    {
        var skills = learned.ToHashSet();
        float V(string family, float fallback = 0) => SkillCatalog.Value(skills, family, fallback);
        var family = emitter switch
        {
            1 => "kinetic",
            2 => "laser",
            3 => "rocket",
            4 => "plasma",
            _ => "",
        };
        float M(string effect) =>
            skills
                .Select(SkillCatalog.Get)
                .Where(s => s?.Mastery == family && s.Effect == effect)
                .Sum(s => s!.Value);
        var strength = 1 + V("strength");
        var direct = 1 + M("direct");
        var rate = 1 + V("rate");
        var range =
            1300 * (1 + V("range")) * (1 + M("range")) * (emitter == 2 ? 1 + V("dynamics") : 1);
        return new(
            emitter,
            (
                emitter == 2 ? 4
                : emitter >= 3 ? 2
                : 1
            )
                * strength
                * direct
                * (emitter == 2 ? rate : 1),
            (emitter == 1 ? 1 : direct + M("splash")) / direct,
            1 + M("radius"),
            range,
            (
                emitter == 3 ? 240
                : emitter == 4 ? 300
                : emitter == 1 ? 650
                : 650
            )
                * (emitter == 2 ? 1 : 1 + V("dynamics"))
                * (1 + M("speed")),
            (
                emitter == 3 ? 18
                : emitter == 4 ? 12
                : emitter == 1 ? 5
                : 7
            ) / rate,
            1 + M("shield"),
            (int)M("pierce"),
            strength
        );
    }

    public bool Choice(int id, ChoiceMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        var d = SkillCatalog.Get(request.Choice);
        var result =
            p.Hull <= 0 ? "dead"
            : d is null ? "unknown_skill"
            : HasSkill(p, d.Id) ? "already_learned"
            : LevelForExperience(p.Experience) < d.MinLevel ? "level_required"
            : d.Prerequisite is not null && !HasSkill(p, d.Prerequisite) ? "prerequisite_required"
            : d.Mastery is not null
            && d.Tier == 1
            && p.Skills.Select(SkillCatalog.Get)
                .Where(s => s?.Mastery is not null)
                .Select(s => s!.Mastery)
                .Distinct()
                .Count() >= 2
                ? "mastery_limit"
            : SkillPointsFor(p) < 1 ? "no_points"
            : "ok";
        p.SkillReceipt = new(request.Sequence, request.Choice, result);
        if (result != "ok")
            return false;
        p.Skills.Add(d!.Id);
        if (d.Family == "shield")
            p.Shield = Math.Min(MaxShieldFor(p), p.Shield + 1);
        if (d.Family == "hull")
            p.Hull = Math.Min(MaxHullFor(p), p.Hull + 1);
        if (d.Family == "repair")
            p.HullAt = Tick + 10 * TickRate;
        return true;
    }

    public SkillReceipt? SkillAction(int id) => players[id].SkillReceipt;

    public CreditReward? LastCreditReward(int id) => players[id].CreditReward;

    private void Fire(Player p)
    {
        for (var step = 0; step < 4; step++)
        {
            var i = (step + (int)(Tick % 4)) % 4;
            var w = p.Weapons[i];
            if (
                !Active(p, i)
                || w.Emitter == 2
                || Tick < p.WeaponReady[i]
                || shots.Count >= ProjectileLimit
            )
                continue;
            var interval = Stats(p, w.Emitter).Interval;
            // Preserve fractional cadence while held; do not bank volleys during idle periods.
            p.WeaponReady[i] = (p.WeaponReady[i] <= Tick - 1 ? Tick : p.WeaponReady[i]) + interval;
            shots.Add(CreateModularShot(p, p.Aim, i));
        }
    }
}
