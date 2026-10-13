using System.Text.Json;
using System.Text.Json.Serialization;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Server;

// Runs on the socket sender, after the bounded channel has selected the next frame.
// Dropped queued frames therefore cannot drop an essential private-state update.
public sealed class SnapshotEncoder
{
    private static readonly JsonSerializerOptions WireOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(ArenaService.Json);
        options.Converters.Add(new ShotWireConverter());
        options.Converters.Add(new BeamWireConverter());
        return options;
    }

    private readonly Dictionary<string, byte[]> previous = [];
    private int revision;

    private T? Changed<T>(string key, T? value)
        where T : class
    {
        var encoded = JsonSerializer.SerializeToUtf8Bytes(value, ArenaService.Json);
        if (previous.TryGetValue(key, out var old) && old.AsSpan().SequenceEqual(encoded))
            return null;
        previous[key] = encoded;
        revision++;
        return value;
    }

    public byte[] Encode(RoomSnapshot frame)
    {
        var reduced = frame with
        {
            Workshop = Changed("workshop", frame.Workshop),
            Social = Changed("social", frame.Social),
            SkillReceipt = Changed("skill", frame.SkillReceipt),
            CreditReward = Changed("credit", frame.CreditReward),
            Sector = frame.Sector is null
                ? null
                : frame.Sector with
                {
                    Pilot = Changed("pilot", frame.Sector.Pilot),
                    World = frame.Sector.World is not { } world
                        ? null
                        : world with
                        {
                            Stock = Changed("stock", world.Stock)!,
                            Race = world.Race is not { } race
                                ? null
                                : race with
                                {
                                    Course = Changed("course", race.Course)!,
                                    Ghost = Changed("ghost", race.Ghost)!,
                                },
                        },
                },
        };
        return JsonSerializer.SerializeToUtf8Bytes(
            reduced with
            {
                PrivateRevision = revision,
            },
            WireOptions
        );
    }
}

internal sealed class ShotWireConverter : JsonConverter<ShotSnapshot>
{
    public override ShotSnapshot Read(
        ref Utf8JsonReader reader,
        Type type,
        JsonSerializerOptions options
    ) => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter w, ShotSnapshot s, JsonSerializerOptions options)
    {
        w.WriteStartArray();
        w.WriteNumberValue(s.Id);
        w.WriteNumberValue(s.Owner);
        w.WriteNumberValue(s.X);
        w.WriteNumberValue(s.Y);
        w.WriteNumberValue(s.Dx);
        w.WriteNumberValue(s.Dy);
        w.WriteNumberValue(s.Born);
        w.WriteNumberValue(s.Kind);
        w.WriteNumberValue(s.Effects);
        w.WriteNumberValue(s.Modular ? 1 : 0);
        w.WriteNumberValue(s.Parent);
        w.WriteNumberValue(s.BirthX);
        w.WriteNumberValue(s.BirthY);
        w.WriteNumberValue(s.Weapon);
        w.WriteNumberValue(s.Stage);
        w.WriteNumberValue(s.Power);
        w.WriteStartArray();
        foreach (var ancestor in s.Ancestors ?? [])
            w.WriteNumberValue(ancestor);
        w.WriteEndArray();
        w.WriteEndArray();
    }
}

internal sealed class BeamWireConverter : JsonConverter<BeamSnapshot>
{
    public override BeamSnapshot Read(
        ref Utf8JsonReader reader,
        Type type,
        JsonSerializerOptions options
    ) => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter w, BeamSnapshot b, JsonSerializerOptions options)
    {
        w.WriteStartArray();
        w.WriteNumberValue(b.Owner);
        w.WriteNumberValue(b.Segment);
        w.WriteNumberValue(b.X);
        w.WriteNumberValue(b.Y);
        w.WriteNumberValue(b.EndX);
        w.WriteNumberValue(b.EndY);
        w.WriteNumberValue(b.Effects);
        w.WriteNumberValue(b.Weapon);
        w.WriteNumberValue(b.Stage);
        w.WriteNumberValue(b.Power);
        w.WriteEndArray();
    }
}
