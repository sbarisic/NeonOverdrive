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
    bool Editing = false
) : SequencedMessage(Sequence);

public sealed record ChoiceMessage(int Sequence, string Choice) : SequencedMessage(Sequence);

public sealed record FriendMessage(int Sequence, string Name, string Action)
    : SequencedMessage(Sequence);

public sealed record EquipMessage(int Sequence, WeaponConfig[] Weapons)
    : SequencedMessage(Sequence);

public sealed record BuyMessage(int Sequence, int Module) : SequencedMessage(Sequence);

public sealed record IncompatibleMessage : ClientMessage;

public sealed record RespawnMessage(int Sequence) : SequencedMessage(Sequence);

/// <summary>Strict, bounded intent-only wire parser. Transport and gameplay validation are separate.</summary>
public static class ClientProtocol
{
    public const int Version = 6;
    public const int MaxMessageBytes = 4096;
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
                        !(
                            Fields(names, "v", "type", "seq", "x", "y", "aim", "fire", "menu")
                            || Fields(
                                names,
                                "v",
                                "type",
                                "seq",
                                "x",
                                "y",
                                "aim",
                                "fire",
                                "menu",
                                "editing"
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
                    message = new InputMessage(sequence, x, y, aim, fire, menu, editing);
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
                case "equip":
                    if (
                        !Fields(names, "v", "type", "seq", "weapons")
                        || !Sequence(root, out sequence)
                        || !root.TryGetProperty("weapons", out var weapons)
                        || weapons.ValueKind != JsonValueKind.Array
                        || weapons.GetArrayLength() != 4
                    )
                        return false;
                    var configs = new List<WeaponConfig>();
                    foreach (var w in weapons.EnumerateArray())
                    {
                        if (w.ValueKind != JsonValueKind.Object)
                            return false;
                        var props = w.EnumerateObject().Select(p => p.Name).ToArray();
                        if (
                            props.Length != 3
                            || !Fields(props.ToHashSet(), "emitter", "attachments", "enabled")
                            || !Integer(w, "emitter", out var emitter)
                            || emitter is < 0 or > 4
                            || !Boolean(w, "enabled", out var enabled)
                            || !w.TryGetProperty("attachments", out var attachments)
                            || attachments.ValueKind != JsonValueKind.Array
                            || attachments.GetArrayLength() != 8
                        )
                            return false;
                        var parts = new List<int>();
                        foreach (var part in attachments.EnumerateArray())
                        {
                            if (!part.TryGetInt32(out var id) || id != 0 && id is < 5 or > 28)
                                return false;
                            parts.Add(id);
                        }
                        configs.Add(new(emitter, parts.ToArray(), enabled));
                    }
                    message = new EquipMessage(sequence, configs.ToArray());
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
                                or "accept"
                                or "claim"
                                or "interact"
                                or "cancel"
                                or "ping"
                                or "broadcast"
                                or "defend"
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
