using System.Collections.Concurrent;

namespace Socks5ProxyServer.Stats;

public class TrafficTracker
{
    private readonly ConcurrentDictionary<string, UserStats> _userStatistics =
        new(StringComparer.OrdinalIgnoreCase);

    public UserStats GetOrCreateUserStats(string username)
    {
        return _userStatistics.GetOrAdd(username, name => new UserStats(name));
    }

    public bool TryAcquireConnection(string username, int maxAllowedConnections)
    {
        var stats = GetOrCreateUserStats(username);
        return stats.TryIncrementConnections(maxAllowedConnections);
    }

    public void ReleaseConnection(string username)
    {
        if (_userStatistics.TryGetValue(username, out var stats))
        {
            stats.DecrementConnections();
        }
    }

    public IReadOnlyCollection<UserStats> GetAllStats()
    {
        return _userStatistics.Values.ToArray();
    }
}