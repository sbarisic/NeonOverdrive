using System.Text.Json;

namespace ArcadeDodger.Multiplayer;

public abstract record ClientMessage;

public sealed record JoinMessage(string Name, string Room) : ClientMessage;

public abstract record SequencedMessage(int Sequence) : ClientMessage;

public sealed record InputMessage(
    int Sequence,
    float X,
    float Y,
    float Aim,
    bool Fire,
    bool Menu,
    bool Editing = false,
    bool Brake = false,
    bool CursorSteering = false,
    bool FullThrottle = false
) : SequencedMessage(Sequence);

public sealed record ChoiceMessage(int Sequence, string Choice) : SequencedMessage(Sequence);

public sealed record FriendMessage(int Sequence, string Name, string Action)
    : SequencedMessage(Sequence);

public sealed record EquipMessage(int Sequence, WeaponConfig[] Weapons)
    : SequencedMessage(Sequence);

public sealed record BuyMessage(int Sequence, int Module) : SequencedMessage(Sequence);

public sealed record IncompatibleMessage : ClientMessage;

public sealed record RespawnMessage(int Sequence) : SequencedMessage(Sequence);

public sealed record LeaveMessage(int Sequence) : SequencedMessage(Sequence);

/// <summary>Strict, bounded intent-only wire parser. Transport and gameplay validation are separate.</summary>
public static class ClientProtocol
{
    public const int Version = 16;
    public const int MaxMessageBytes = 16384;
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 8 };

    public static bool TryParse(ReadOnlyMemory<byte> utf8, out ClientMessage? message)
    {
        message = null;
        if (utf8.Length is 0 or > MaxMessageBytes)
            return false;
        try
        {
            using var document = JsonDocument.Parse(utf8, Options);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            // JsonDocument permits duplicate properties; explicitly forbid ambiguity.
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name))
                    return false;
            if (!Integer(root, "v", out var version) || !Text(root, "type", out var type))
                return false;

            if (version != Version)
                return false;
            switch (type)
            {
                case "leave":
                    if (
                        !Fields(names, "v", "type", "seq") || !Sequence(root, out var leaveSequence)
                    )
                        return false;
                    message = new LeaveMessage(leaveSequence);
                    return true;
                case "join":
                    if (
                        !Fields(names, "v", "type", "name", "room")
                        || !Text(root, "name", out var name)
                        || !Text(root, "room", out var room)
                        || name.Length > 16
                        || !name.All(IsNameCharacter)
                        || (room.Length != 0 && (room.Length != 8 || !room.All(IsHex)))
                    )
                        return false;
                    name = name.Trim();
                    message = new JoinMessage(name.Length == 0 ? "Pilot" : name, room);
                    return true;
                case "input":
                    if (
                        names.Any(n =>
                            n
                                is not (
                                    "v"
                                    or "type"
                                    or "seq"
                                    or "x"
                                    or "y"
                                    or "aim"
                                    or "fire"
                                    or "menu"
                                    or "editing"
                                    or "brake"
                                    or "cursorSteering"
                                    or "fullThrottle"
                                )
                        )
                        || !Sequence(root, out var sequence)
                        || !Number(root, "x", -1, 1, out var x)
                        || !Number(root, "y", -1, 1, out var y)
                        || !Number(root, "aim", -MathF.PI, MathF.PI, out var aim)
                        || !Boolean(root, "fire", out var fire)
                        || !Boolean(root, "menu", out var menu)
                    )
                        return false;
                    var editing = false;
                    if (names.Contains("editing") && !Boolean(root, "editing", out editing))
                        return false;
                    var brake = false;
                    if (names.Contains("brake") && !Boolean(root, "brake", out brake))
                        return false;
                    var cursorSteering = false;
                    if (
                        names.Contains("cursorSteering")
                        && !Boolean(root, "cursorSteering", out cursorSteering)
                    )
                        return false;
                    var fullThrottle = false;
                    if (
                        names.Contains("fullThrottle")
                        && !Boolean(root, "fullThrottle", out fullThrottle)
                    )
                        return false;
                    message = new InputMessage(
                        sequence,
                        x,
                        y,
                        aim,
                        fire,
                        menu,
                        editing,
                        brake,
                        cursorSteering,
                        fullThrottle
                    );
                    return true;
                case "choice":
                    if (
                        !Fields(names, "v", "type", "seq", "choice")
                        || !Sequence(root, out sequence)
                        || !Text(root, "choice", out var choice)
                        || SkillCatalog.Get(choice) is null
                    )
                        return false;
                    message = new ChoiceMessage(sequence, choice);
                    return true;
                case "friend":
                    if (
                        !Fields(names, "v", "type", "seq", "name", "action")
                        || !Sequence(root, out sequence)
                        || !Text(root, "name", out var friendName)
                        || !FriendBook.ValidName(friendName)
                        || !Text(root, "action", out var action)
                        || action
                            is not ("request" or "accept" or "decline" or "cancel" or "remove")
                    )
                        return false;
                    message = new FriendMessage(sequence, friendName.Trim(), action);
                    return true;
                case "buy":
                    if (
                        !Fields(names, "v", "type", "seq", "module")
                        || !Sequence(root, out sequence)
                        || !Integer(root, "module", out var item)
                        || !ModuleCatalog.Valid(item)
                    )
                        return false;
                    message = new BuyMessage(sequence, item);
                    return true;
                case "equipment":
                    if (
                        !Sequence(root, out sequence)
                        || !Text(root, "action", out var equipmentAction)
                        || equipmentAction is not ("apply" or "sell" or "page")
                        || names.Any(n =>
                            n
                                is not (
                                    "v"
                                    or "type"
                                    or "seq"
                                    or "action"
                                    or "revision"
                                    or "edits"
                                    or "bays"
                                    or "ship"
                                    or "item"
                                    or "module"
                                    or "quantity"
                                    or "page"
                                )
                        )
                    )
                        return false;
                    if (!StrictEquipment(root, equipmentAction))
                        return false;
                    var equipment = root.Deserialize<EquipmentMessage>(
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    );
                    if (
                        equipment is null
                        || equipment.Revision < 0
                        || equipment.Page < 0
                        || equipment.Page > 1000000
                        || equipment.Quantity < 1
                        || equipment.Edits?.Length > 32
                    )
                        return false;
                    message = equipment with { Sequence = sequence };
                    return true;
                case "sector":
                    if (
                        !Fields(names, "v", "type", "seq", "action", "target")
                        || !Sequence(root, out sequence)
                        || !Text(root, "action", out var sectorAction)
                        || sectorAction
                            is not (
                                "dock"
                                or "undock"
                                or "repair"
                                or "buy_chassis"
                                or "cruise"
                                or "radio"
                                or "radio_trade"
                                or "radio_close"
                                or "convoy_buy"
                                or "tow"
                                or "receipts_ack"
                                or "race_join"
                                or "race_stop"
                                or "race_retry"
                                or "mission_bounty"
                                or "mission_interception"
                                or "mission_smuggling"
                                or "mission_recovery"
                                or "mission_exploration"
                                or "mission_protection"
                                or "mission_claim"
                                or "mission_abandon"
                                or "mission_scan"
                                or "police_surrender"
                                or "police_pay"
                                or "police_refuse"
                                or "wreck_reactor"
                                or "wreck_cargo"
                                or "wreck_navigation"
                                or "wreck_abandon"
                                or "accept"
                                or "claim"
                                or "interact"
                                or "cancel"
                                or "ping"
                                or "broadcast"
                                or "defend"
                                or "hail"
                                or "goodbye"
                                or "job_accept"
                                or "job_claim"
                                or "activate"
                                or "turret_one"
                                or "turret_two"
                                or "repair_bot"
                            )
                        || !Integer(root, "target", out var target)
                        || target < 0
                    )
                        return false;
                    message = new SectorActionMessage(sequence, sectorAction, target);
                    return true;
                case "respawn":
                    if (!Fields(names, "v", "type", "seq") || !Sequence(root, out sequence))
                        return false;
                    message = new RespawnMessage(sequence);
                    return true;
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        } // Includes invalid UTF-8 decoded strings.
    }

    private static bool StrictEquipment(JsonElement root, string action)
    {
        if (action == "page")
            return root.TryGetProperty("page", out var page) && page.TryGetInt32(out _);
        if (!root.TryGetProperty("revision", out var revision) || !revision.TryGetInt64(out _))
            return false;
        if (action == "sell")
            return true;
        if (
            !root.TryGetProperty("edits", out var edits)
            || edits.ValueKind != JsonValueKind.Array
            || edits.GetArrayLength() > 32
            || !root.TryGetProperty("bays", out var bays)
            || bays.ValueKind != JsonValueKind.Array
            || bays.GetArrayLength() != 4
            || !root.TryGetProperty("ship", out var ship)
            || ship.ValueKind != JsonValueKind.Array
            || ship.GetArrayLength() != 6
        )
            return false;
        foreach (var edit in edits.EnumerateArray())
        {
            if (
                edit.ValueKind != JsonValueKind.Object
                || edit.EnumerateObject().Count() != 2
                || !Fields(
                    edit.EnumerateObject().Select(p => p.Name).ToHashSet(),
                    "id",
                    "attachments"
                )
                || !Text(edit, "id", out var id)
                || !Guid.TryParseExact(id, "N", out _)
                || !edit.TryGetProperty("attachments", out var parts)
                || parts.ValueKind != JsonValueKind.Array
                || parts.GetArrayLength() != 8
            )
                return false;
            foreach (var part in parts.EnumerateArray())
                if (!part.TryGetInt32(out var n) || n != 0 && n is < 5 or > 28)
                    return false;
        }
        foreach (var bay in bays.EnumerateArray())
            if (
                bay.ValueKind != JsonValueKind.Object
                || bay.EnumerateObject().Count() != 2
                || !Fields(
                    bay.EnumerateObject().Select(p => p.Name).ToHashSet(),
                    "weapon",
                    "enabled"
                )
                || !Boolean(bay, "enabled", out _)
                || !ItemId(bay.GetProperty("weapon"))
            )
                return false;
        return ship.EnumerateArray().All(ItemId);
    }

    private static bool ItemId(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null
        || value.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(value.GetString(), "N", out _);

    private static bool Fields(HashSet<string> actual, params string[] expected) =>
        actual.SetEquals(expected);

    private static bool IsNameCharacter(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '_' or '-';

    private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static bool Sequence(JsonElement root, out int value) =>
        Integer(root, "seq", out value) && value > 0;

    private static bool Integer(JsonElement root, string key, out int value)
    {
        value = 0;
        return root.TryGetProperty(key, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }

    private static bool Number(JsonElement root, string key, float min, float max, out float value)
    {
        value = 0;
        return root.TryGetProperty(key, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetSingle(out value)
            && float.IsFinite(value)
            && value >= min
            && value <= max;
    }

    private static bool Text(JsonElement root, string key, out string value)
    {
        value = "";
        if (!root.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString()!;
        return true;
    }

    private static bool Boolean(JsonElement root, string key, out bool value)
    {
        value = false;
        if (
            !root.TryGetProperty(key, out var element)
            || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
        )
            return false;
        value = element.GetBoolean();
        return true;
    }
}

/// <summary>Per-connection admission order, not a simulation acknowledgment. Use on one owner thread.</summary>
public sealed class ClientSession
{
    public bool Joined { get; private set; }
    public int LastReceivedSequence { get; private set; }

    public bool TryAccept(ClientMessage message)
    {
        if (message is JoinMessage)
        {
            if (Joined)
                return false;
            Joined = true;
            return true;
        }
        if (
            !Joined
            || message is not SequencedMessage sequenced
            || sequenced.Sequence <= LastReceivedSequence
        )
            return false;
        LastReceivedSequence = sequenced.Sequence;
        return true;
    }
}
