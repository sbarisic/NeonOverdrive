namespace ArcadeDodger.Multiplayer;

public sealed record SectorActionMessage(int Sequence, string Action, int Target = 0)
    : SequencedMessage(Sequence);

public sealed record SectorReceipt(
    int Sequence,
    string Result,
    int Cursor = 0,
    string Run = "",
    int ThroughSequence = 0
);

public sealed record LocationState(int Id, int Hull, string State, int LaunchTicks = 0);

public sealed record ContractDefinition(
    int Id,
    int Station,
    string Kind,
    int Target,
    int Credits,
    int Experience
);

public sealed record SectorSessionSnapshot(
    int Docked,
    int LastStation,
    int[] Discovered,
    int[] Cells,
    int Contract,
    int[] Progress,
    bool Carrying,
    bool Ready,
    int[] Completed,
    float Interaction,
    SectorReceipt? Receipt,
    int RepairCost = 0,
    MapPing[]? Pings = null,
    string LootNotice = "",
    int[]? CoolingContracts = null,
    int[]? Contributions = null,
    int InteractionTarget = 0,
    SectorReceipt[]? Receipts = null
);

public sealed record MapPing(
    int Id,
    int Sender,
    string Name,
    int Location,
    int Pilot,
    float X,
    float Y,
    long Expires,
    bool Broadcast,
    int Patrol = 0
);

public sealed record SectorState(
    LocationState[] Locations,
    SectorSessionSnapshot? Pilot = null,
    RescueSnapshot? Rescue = null,
    ServiceSnapshot? Services = null,
    LivingSnapshot? World = null
);
