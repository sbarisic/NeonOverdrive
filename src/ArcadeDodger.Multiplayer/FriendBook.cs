using System.Text.Json;

namespace ArcadeDodger.Multiplayer;

public sealed record FriendLink(string From, string To, bool Accepted);

public sealed record SocialSnapshot(
    string[] Friends,
    string[] Incoming,
    string[] Outgoing,
    int Sequence = 0,
    string Result = ""
);

/// <summary>Name-based social state shared by rooms. Mutated only by the server simulation owner.</summary>
public sealed class FriendBook
{
    private List<FriendLink> links = new();
    private readonly string? path;
    private HashSet<string> mutual = new(StringComparer.Ordinal);

    private static string Pair(string a, string b)
    {
        a = Key(a);
        b = Key(b);
        return string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    }

    private void Index() =>
        mutual = links
            .Where(l => l.Accepted)
            .Select(l => Pair(l.From, l.To))
            .ToHashSet(StringComparer.Ordinal);

    public static string Key(string name) => name.Trim().ToUpperInvariant();

    public static bool ValidName(string name) =>
        name.Trim().Length is > 0 and <= 16
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-');

    public FriendBook(string? path = null)
    {
        this.path = path is null ? null : Path.GetFullPath(path);
        if (this.path is not null && File.Exists(this.path))
        {
            links =
                JsonSerializer.Deserialize<List<FriendLink>>(File.ReadAllText(this.path)) ?? new();
            if (
                links.Count > 10000
                || links.Any(l =>
                    !ValidName(l.From) || !ValidName(l.To) || Key(l.From) == Key(l.To)
                )
            )
                throw new InvalidDataException(
                    "Invalid friend store. Preserve and repair the existing file before starting."
                );
        }
        Index();
    }

    public bool Mutual(string a, string b) => mutual.Contains(Pair(a, b));

    private static bool Matches(FriendLink l, string a, string b) =>
        (Key(l.From) == Key(a) && Key(l.To) == Key(b))
        || (Key(l.From) == Key(b) && Key(l.To) == Key(a));

    public SocialSnapshot Snapshot(string name, int sequence = 0, string result = "") =>
        new(
            links
                .Where(l => l.Accepted && (Key(l.From) == Key(name) || Key(l.To) == Key(name)))
                .Select(l => Key(l.From) == Key(name) ? l.To : l.From)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            links
                .Where(l => !l.Accepted && Key(l.To) == Key(name))
                .Select(l => l.From)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            links
                .Where(l => !l.Accepted && Key(l.From) == Key(name))
                .Select(l => l.To)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            sequence,
            result
        );

    public string Change(string actor, string target, string action)
    {
        actor = actor.Trim();
        target = target.Trim();
        if (!ValidName(actor) || !ValidName(target) || Key(actor) == Key(target))
            return "invalid_name";
        var existing = links.FirstOrDefault(l => Matches(l, actor, target));
        var next = new List<FriendLink>(links);
        switch (action)
        {
            case "request":
                if (existing is not null)
                    return existing.Accepted ? "already_friends" : "request_pending";
                if (
                    links.Count >= 10000
                    || new[] { actor, target }.Any(n =>
                        links.Count(l => Key(l.From) == Key(n) || Key(l.To) == Key(n)) >= 32
                    )
                )
                    return "list_full";
                next.Add(new(actor, target, false));
                break;
            case "accept":
                if (existing is null || existing.Accepted || Key(existing.To) != Key(actor))
                    return "no_incoming_request";
                next.Remove(existing);
                next.Add(existing with { Accepted = true });
                break;
            case "decline":
                if (existing is null || existing.Accepted || Key(existing.To) != Key(actor))
                    return "no_incoming_request";
                next.Remove(existing);
                break;
            case "cancel":
                if (existing is null || existing.Accepted || Key(existing.From) != Key(actor))
                    return "no_outgoing_request";
                next.Remove(existing);
                break;
            case "remove":
                if (existing is null || !existing.Accepted)
                    return "not_friends";
                next.Remove(existing);
                break;
            default:
                return "invalid_action";
        }
        // Persist before exposing success. A failed save cannot silently create a temporary friendship.
        try
        {
            if (path is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(next));
                File.Move(temporary, path, true);
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
        links = next;
        Index();
        return "ok";
    }
}
