namespace ArcadeDodger.Server;

internal sealed class ConnectionAdmission
{
    private readonly Dictionary<string, int> counts = new();
    private readonly object gate = new();
    private int total;

    public bool Enter(string ip)
    {
        lock (gate)
        {
            counts.TryGetValue(ip, out var count);
            if (total >= 192 || count >= 8)
                return false;
            counts[ip] = count + 1;
            total++;
            return true;
        }
    }

    public void Leave(string ip)
    {
        lock (gate)
        {
            if (--counts[ip] == 0)
                counts.Remove(ip);
            total--;
        }
    }
}
