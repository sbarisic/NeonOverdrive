using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    // True means the parent ended; descendants continue with the unused tick budget.
    private bool ApplyStage(Shot s, int id, float budget, int firstSegment, int limit)
    {
        if (id == 0)
            return false;
        var d = ModuleCatalog.Get(id);
        switch (id)
        {
            case 5:
                s.Damage *= d.Value;
                s.VisualPower *= d.Value;
                break;
            case 6:
                if (s.IsBeam)
                    s.Range *= d.Value;
                else
                {
                    s.Velocity *= d.Value;
                    s.SpeedMultiplier *= d.Value;
                }
                ;
                break;
            case 7:
                s.Range *= d.Value;
                break;
            case 8:
                s.Pierce += (int)d.Value;
                break;
            case 9:
                s.Spread *= d.Value;
                break;
            case 10:
                s.ShieldMultiplier *= d.Value;
                break;
            case 11:
            case 12:
            case 13:
                var count = (int)d.Value;
                if (
                    s.IsBeam
                        ? beams.Count - firstSegment + count > limit
                        : shots.Count + count > ProjectileLimit
                )
                    break;
                for (var i = 0; i < count; i++)
                {
                    var angle = (-d.Radius + 2 * d.Radius * i / (count - 1)) * s.Spread;
                    var child = s.Copy();
                    child.Id = ++nextShot;
                    child.Parent = s.Id;
                    child.Ancestors = [.. s.Ancestors, s.Id];
                    child.Born = Tick;
                    child.BirthPosition = s.Position;
                    child.Damage /= count;
                    child.VisualPower /= count;
                    child.Velocity = Vector2.Transform(s.Velocity, Matrix3x2.CreateRotation(angle));
                    if (id == 13)
                        child.Range = child.Distance + (child.Range - child.Distance) * .5f;
                    if (!s.IsBeam)
                        shots.Add(child);
                    Advance(child, budget, firstSegment, limit);
                }
                s.Expires = Tick;
                return true;
            case 14:
                s.Homing = true;
                Guide(s);
                break;
            case 15:
                s.Bounces += (int)d.Value;
                break;
            case 16:
                s.Relays++;
                break;
            case 17:
                s.Burns++;
                s.Burning = true;
                break;
            case 18:
                s.Freezes++;
                s.Cryo = true;
                break;
            case 19:
                s.Rads++;
                s.Radiation = true;
                break;
            case 20:
                s.Corrodes++;
                break;
            case 21:
                s.Teslas++;
                break;
            case 22:
                s.Gravity++;
                break;
            case 23:
                s.Blasts++;
                break;
            case 24:
                s.Proximity = true;
                break;
            case 25:
                if (s.Kind == 4)
                    break;
                if (!s.IsBeam)
                {
                    s.Kind = 4;
                    s.Blast = MathF.Max(s.Blast, 45 * s.RadiusMultiplier);
                    s.Velocity = Vector2.Normalize(s.Velocity) * 520 * s.SpeedMultiplier / TickRate;
                    break;
                }
                if (s.EmitPlasma && shots.Count < ProjectileLimit)
                {
                    var child = s.Copy();
                    child.Id = ++nextShot;
                    child.Parent = s.Id;
                    child.Ancestors = [.. s.Ancestors, s.Id];
                    child.BirthPosition = s.Position;
                    child.Born = Tick;
                    child.IsBeam = false;
                    child.Kind = 4;
                    child.Damage /= 2;
                    child.Blast = 45 * child.RadiusMultiplier;
                    child.Velocity =
                        Vector2.Normalize(s.Velocity) * 520 * s.SpeedMultiplier / TickRate;
                    child.Expires = Tick + 180;
                    shots.Add(child);
                    if (players.TryGetValue(s.Owner, out var p))
                        p.PlasmaReady[s.Weapon] = Tick + 15;
                }
                return true;
            case 26:
                if (!s.IsBeam || s.EmitMine)
                {
                    if (AddMine(s))
                    {
                        if (s.IsBeam && players.TryGetValue(s.Owner, out var p))
                            p.MineReady[s.Weapon] = Tick + 30;
                    }
                    else if (!s.IsBeam)
                    {
                        // Keep the payload at its checkpoint and retry when deployment capacity returns.
                        s.Cursor--;
                        s.Expires++;
                        return true;
                    }
                }
                s.Expires = Tick;
                return true;
            case 27:
                s.Leeches++;
                break;
            case 28:
                s.SalvageMark = true;
                break;
        }
        return false;
    }
}
