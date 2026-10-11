using System.Text.Json;

namespace ArcadeDodger.Multiplayer;

public abstract record ClientMessage;
public sealed record JoinMessage(string Name, string Room) : ClientMessage;
public abstract record SequencedMessage(int Sequence) : ClientMessage;
public sealed record InputMessage(int Sequence, float X, float Y, float Aim, bool Fire, bool Dash)
    : SequencedMessage(Sequence);
public sealed record ChoiceMessage(int Sequence, int Choice) : SequencedMessage(Sequence);
public sealed record RespawnMessage(int Sequence) : SequencedMessage(Sequence);

/// <summary>Strict, bounded intent-only wire parser. Transport and gameplay validation are separate.</summary>
public static class ClientProtocol
{
    public const int Version = 1;
    public const int MaxMessageBytes = 1024;
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 8 };

    public static bool TryParse(ReadOnlyMemory<byte> utf8, out ClientMessage? message)
    {
        message = null;
        if (utf8.Length is 0 or > MaxMessageBytes) return false;
        try
        {
            using var document = JsonDocument.Parse(utf8, Options);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            // JsonDocument permits duplicate properties; explicitly forbid ambiguity.
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name)) return false;
            if (!Integer(root, "v", out var version) || version != Version ||
                !Text(root, "type", out var type)) return false;

            switch (type)
            {
                case "join":
                    if (!Fields(names, "v", "type", "name", "room") ||
                        !Text(root, "name", out var name) || !Text(root, "room", out var room) ||
                        name.Length > 16 || !name.All(IsNameCharacter) ||
                        (room.Length != 0 && (room.Length != 8 || !room.All(IsHex)))) return false;
                    name = name.Trim();
                    message = new JoinMessage(name.Length == 0 ? "Pilot" : name, room);
                    return true;
                case "input":
                    if (!Fields(names, "v", "type", "seq", "x", "y", "aim", "fire", "dash") ||
                        !Sequence(root, out var sequence) ||
                        !Number(root, "x", -1, 1, out var x) || !Number(root, "y", -1, 1, out var y) ||
                        !Number(root, "aim", -MathF.PI, MathF.PI, out var aim) ||
                        !Boolean(root, "fire", out var fire) || !Boolean(root, "dash", out var dash)) return false;
                    message = new InputMessage(sequence, x, y, aim, fire, dash);
                    return true;
                case "choice":
                    if (!Fields(names, "v", "type", "seq", "choice") || !Sequence(root, out sequence) ||
                        !Integer(root, "choice", out var choice) || choice is < 0 or > 2) return false;
                    message = new ChoiceMessage(sequence, choice);
                    return true;
                case "respawn":
                    if (!Fields(names, "v", "type", "seq") || !Sequence(root, out sequence)) return false;
                    message = new RespawnMessage(sequence);
                    return true;
                default:
                    return false;
            }
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; } // Includes invalid UTF-8 decoded strings.
    }

    private static bool Fields(HashSet<string> actual, params string[] expected) => actual.SetEquals(expected);
    private static bool IsNameCharacter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
        or >= '0' and <= '9' or ' ' or '_' or '-';
    private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f';
    private static bool Sequence(JsonElement root, out int value) => Integer(root, "seq", out value) && value > 0;
    private static bool Integer(JsonElement root, string key, out int value)
    {
        value = 0;
        return root.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value);
    }
    private static bool Number(JsonElement root, string key, float min, float max, out float value)
    {
        value = 0;
        return root.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Number &&
            element.TryGetSingle(out value) && float.IsFinite(value) && value >= min && value <= max;
    }
    private static bool Text(JsonElement root, string key, out string value)
    {
        value = "";
        if (!root.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString()!;
        return true;
    }
    private static bool Boolean(JsonElement root, string key, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(key, out var element) ||
            element.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
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
            if (Joined) return false;
            Joined = true;
            return true;
        }
        if (!Joined || message is not SequencedMessage sequenced ||
            sequenced.Sequence <= LastReceivedSequence) return false;
        LastReceivedSequence = sequenced.Sequence;
        return true;
    }
}
