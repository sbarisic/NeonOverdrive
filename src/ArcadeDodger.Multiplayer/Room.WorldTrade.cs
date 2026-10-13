using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private void SpawnCivilian(NpcRole role, int index)
    {
        if (enemies.Count >= EnemyLimit)
            return;
        var chassis = role switch
        {
            NpcRole.Police => 2,
            NpcRole.Patrol => 4,
            NpcRole.Mercenary => 5,
            _ => 3,
        };
        var at = Location(10)!.Position + new Vector2(-220 + index * 75, 210);
        var ship = new Enemy
        {
            Id = ++nextEnemy,
            Type = "gunner",
            Faction = role == NpcRole.Trader ? NpcFaction.Neutral : NpcFaction.Friendly,
            Role = role,
            ChassisOverride = chassis,
            Position = at,
            Previous = at,
            Hull = 60,
            MaxHull = 60,
            Shield = 30,
            MaxShield = 30,
            Level = 1,
            Home = 10,
        };
        enemies.Add(ship);
        var agent = new Civilian
        {
            Ship = ship,
            Name = $"{role} {index + 1}",
            Identity = index,
            DepartAt = Tick + 60 * TickRate,
        };
        civilians.Add(agent);
        if (role == NpcRole.Trader)
            LoadManifest(agent);
    }

    private void LoadManifest(Civilian agent)
    {
        agent.Stock.RemoveAll(s => !agent.Reserved.Contains(s.Id));
        if (agent.Reserved.Count > 0)
            return;
        agent.CargoRevision++;
        var tier =
            agent.Ship.Home == 12 ? 4
            : agent.Ship.Home == 11 ? 2
            : 1;
        for (var i = 0; i < 5; i++)
        {
            var level = Math.Clamp(tier + (worldRandom!.NextSingle() < .2f ? 1 : 0), 1, 4);
            var module = i < 3 ? ModuleLevels.Key(worldRandom!.Next(5, 29), level) : 0;
            var weapon =
                i == 3 ? EquipmentCatalog.Build(worldRandom!.Next(1, 5), tier, level) : null;
            var gear =
                i == 4
                    ? new ShipItem(
                        Guid.NewGuid().ToString("N"),
                        EquipmentCatalog.GearForRoll(
                            worldRandom!.NextSingle(),
                            worldRandom.NextSingle()
                        ),
                        level
                    )
                    : null;
            var resale =
                module > 0 ? EquipmentCatalog.Resale(module)
                : weapon is not null ? EquipmentCatalog.Resale(weapon)
                : 25 * level;
            var standard =
                module > 0 ? ModuleCatalog.Get(module).Price
                : weapon is not null
                    ? ModuleCatalog.Get(weapon.Emitter).Price
                        + weapon.Attachments.Where(v => v > 0).Sum(v => ModuleCatalog.Get(v).Price)
                : 100 * level;
            var name =
                module > 0 ? ModuleCatalog.Get(module).Name
                : weapon is not null ? ModuleCatalog.Get(weapon.Emitter).Name + " build"
                : EquipmentCatalog.Items[gear!.Kind].Name + $" T{level}";
            agent.Stock.Add(
                new(
                    ++nextWorldId,
                    name,
                    Math.Max(resale + 1, (int)(standard * .6f)),
                    module,
                    weapon,
                    gear
                )
            );
        }
    }

    private bool MoveCivilian(Enemy npc)
    {
        var agent = civilians.FirstOrDefault(c => c.Ship == npc);
        if (agent is null)
            return false;
        if (
            players.Values.Any(p =>
                p.Connected && p.Hull > 0 && ExplorationFor(p).Hailing == npc.Id
            )
        )
        {
            npc.State = "docking";
            return true;
        }
        if (Tick - npc.LastHit >= 6 * TickRate && WorkCivilian(agent))
            return true;
        if (
            npc.Role is NpcRole.Police or NpcRole.Patrol or NpcRole.Mercenary
            && enemyGrid.Query(npc.Position, npc.Position, 900).Any(e => !e.Allied && e.Hull > 0)
        )
            return false;
        var fleeing = Tick - npc.LastHit < 6 * TickRate && npc.LastHit > 0;
        if (fleeing)
        {
            agent.Destination = Sector!
                .Locations.Where(l => l.Kind == "station" && FriendlyStation(l.Id))
                .MinBy(l => Vector2.DistanceSquared(l.Position, npc.Position))!
                .Id;
            agent.DepartAt = Tick;
            if (npc.State != "fleeing")
                Broadcast(
                    "distress",
                    agent.Name,
                    "Taking fire. Heading for a friendly station; assistance requested."
                );
            npc.State = "fleeing";
        }
        else
            npc.State = Tick < agent.DepartAt ? "docking" : "traveling";
        if (!FriendlyStation(agent.Destination))
            agent.Destination = NextFriendlyStation(npc.Home);
        var goal = Location(agent.Destination)!.Position;
        if (Tick < agent.DepartAt)
            return true;
        if (Vector2.Distance(npc.Position, goal) < 180)
        {
            npc.Home = agent.Destination;
            agent.Destination = NextFriendlyStation(npc.Home);
            agent.DepartAt = Tick + 60 * TickRate;
            if (npc.Role == NpcRole.Trader)
            {
                LoadManifest(agent);
                Broadcast(
                    "trade",
                    agent.Name,
                    "Fresh cargo loaded. Radio for today's limited offers."
                );
            }
            return true;
        }
        var delta = Navigate(npc, goal) - npc.Position;
        if (delta.LengthSquared() > 1)
        {
            npc.Aim = MathF.Atan2(delta.Y, delta.X);
            npc.Position = MoveShip(
                npc.Position,
                npc.Position + Vector2.Normalize(delta) * 100 / TickRate,
                EnemyRadius(npc)
            );
        }
        return true;
    }

    private int NextFriendlyStation(int current)
    {
        for (var offset = 1; offset <= 3; offset++)
        {
            var candidate = 10 + (current - 10 + offset) % 3;
            if (FriendlyStation(candidate))
                return candidate;
        }
        return 10;
    }

    private string RadioAction(Player p, string action, int target)
    {
        var state = Living(p);
        if (action == "radio_close")
        {
            state.Conversation = 0;
            if (ExplorationFor(p).Hailing != 0)
                EndHail(p, ExplorationFor(p));
            return "ok";
        }
        var ship = enemies.FirstOrDefault(e => e.Id == target && e.Allied && e.Hull > 0);
        if (ship is null || Vector2.Distance(p.Position, ship.Position) > Detection(p))
            return "out_of_range";
        // Switching back to sensor-range radio must release an earlier trade cloak.
        if (action == "radio" && ExplorationFor(p).Hailing != 0)
            EndHail(p, ExplorationFor(p));
        state.Conversation = target;
        if (action == "radio_trade")
        {
            if (
                !civilians.Any(c => c.Ship == ship && c.Ship.Role == NpcRole.Trader)
                && !contacts.Any(c => c.Ship == ship && c.Role == "trader")
            )
                return "trade_unavailable";
            if (
                p.Docked != 0
                || Vector2.Distance(p.Position, ship.Position) > 180
                || !ServiceLine(p.Position, ship.Position)
            )
                return "out_of_range";
            ExplorationFor(p).Hailing = target;
            p.Pending = p.Held = null;
            p.Editing = p.Menu = true;
            p.Throttle = 0;
            p.Velocity = Vector2.Zero;
            CancelCruise(p);
            return "ok";
        }
        var agent = civilians.FirstOrDefault(c => c.Ship == ship);
        var contact = contacts.FirstOrDefault(c => c.Ship == ship);
        var response =
            ship.State == "fleeing"
                ? "Can't stay. They're on my tail!"
                : ship.Role switch
                {
                    NpcRole.Trader =>
                        "Manifest is real, prices are firm. Pull alongside if something catches your eye.",
                    NpcRole.Miner =>
                        "Good rock here. Bad company nearby. Keep those guns warm for me.",
                    NpcRole.Scavenger =>
                        "First beam on the wreck gets the salvage. Watch the old navigation uplinks.",
                    NpcRole.Police =>
                        "Patrol channel. Keep your cargo legal and your approach lanes clear.",
                    NpcRole.Mercenary =>
                        "I follow the bounties. You want work, there are always bigger targets.",
                    _ => "Holding the line. Radio a distress call if the corridor gets rough.",
                };
        Broadcast("conversation", agent?.Name ?? contact?.Name ?? "Independent pilot", response);
        return "ok";
    }

    private string BuyConvoy(Player p, int stockId)
    {
        if (p.EconomyPending)
            return "busy";
        var agent = civilians.FirstOrDefault(c => c.Stock.Any(s => s.Id == stockId));
        if (agent is null || agent.Ship.Hull <= 0 || !agent.Ship.Allied)
            return "stock_unavailable";
        if (ExplorationFor(p).Hailing != agent.Ship.Id)
            return "open_close_range_trade";
        if (
            Vector2.Distance(p.Position, agent.Ship.Position) > 180
            || !ServiceLine(p.Position, agent.Ship.Position)
        )
            return "out_of_range";
        if (!agent.Reserved.Add(stockId))
            return "stock_reserved";
        var item = agent.Stock.First(s => s.Id == stockId);
        p.EconomyPending = true;
        var sequence = p.LastReceived;
        var committed = false;
        PersistForRun(
            p,
            () =>
            {
                var result = profiles.TradeItem(
                    p.Name,
                    $"convoy:{Sector!.Seed}:{item.Id}",
                    item.Price,
                    item.Module,
                    item.Weapon,
                    item.Gear
                );
                committed = result == "ok";
                return result;
            },
            result =>
            {
                p.EconomyPending = false;
                agent.Reserved.Remove(stockId);
                if (committed)
                {
                    agent.Stock.RemoveAll(s => s.Id == stockId);
                    agent.CargoRevision++;
                }
                ExplorationFor(p).Receipt = new(sequence, result);
            }
        );
        return profiles.BackgroundWrites ? "pending" : ExplorationFor(p).Receipt!.Result;
    }
}
