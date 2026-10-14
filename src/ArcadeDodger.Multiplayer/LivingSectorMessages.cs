namespace ArcadeDodger.Multiplayer;

public sealed record RadioMessage(
    int Id,
    string Channel,
    string Speaker,
    string Text,
    long Expires
);

public sealed record TradeStock(
    int Id,
    string Name,
    int Price,
    int Module,
    WeaponItem? Weapon,
    ShipItem? Gear
);

public sealed record CargoSnapshot(
    int Id,
    string Kind,
    float X,
    float Y,
    int Credits,
    int TowOwner
);

public sealed record TerritorySnapshot(int Id, string Owner, float Capture, bool Contested);

public sealed record WorldObjective(
    int Id,
    string Kind,
    string Name,
    float X,
    float Y,
    float Uncertainty
);

public sealed record RadioContact(
    int Id,
    string Name,
    string Role,
    string State,
    int Destination,
    long CargoRevision,
    bool Trade,
    bool Work
);

public sealed record LivingSnapshot(
    RadioMessage[] Radio,
    TradeStock[] Stock,
    CargoSnapshot[] Cargo,
    TerritorySnapshot[] Territory,
    int Conversation,
    string Job,
    string JobState,
    int[] Targets,
    string Police,
    float Scan,
    int Tow,
    string Event,
    int EventTarget,
    int EventSeconds,
    RaceSnapshot? Race = null,
    WorldObjective? Objective = null,
    RadioContact[]? Contacts = null,
    WorldObjective? EventObjective = null,
    ProtectionStatus? Protection = null
);

public sealed record ProtectionMark(int Id, float X, float Y, bool Visible, long SeenAt);

public sealed record ProtectionStatus(
    int Ship,
    int Hull,
    int Shield,
    int Remaining,
    bool Participated,
    string Reason,
    ProtectionMark[] Attackers
);
