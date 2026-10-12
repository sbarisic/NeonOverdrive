namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private readonly FriendBook friends;

    public SocialSnapshot Social(int id) =>
        players.TryGetValue(id, out var p)
            ? friends.Snapshot(p.Name, p.SocialSequence, p.SocialResult)
            : new([], [], []);

    public bool Friend(int id, FriendMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        p.SocialSequence = request.Sequence;
        p.SocialResult = friends.Change(p.Name, request.Name, request.Action);
        return p.SocialResult == "ok";
    }

    private bool Friendly(string ownerName, Player target) =>
        ownerName.Length > 0
        && (
            FriendBook.Key(ownerName) == FriendBook.Key(target.Name)
            || friends.Mutual(ownerName, target.Name)
        );

    private bool Friendly(Shot s, Player target) =>
        Friendly(
            s.OwnerName.Length > 0 ? s.OwnerName
                : players.TryGetValue(s.Owner, out var owner) ? owner.Name
                : "",
            target
        );
}
