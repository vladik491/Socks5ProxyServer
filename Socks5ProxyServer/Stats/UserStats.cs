using System.Collections.Concurrent;

namespace Socks5ProxyServer.Stats;

public class UserStats
{
    private long _bytesReceived;
    private long _bytesSent;
    private int _activeConnections;

    public string Username { get; }
    public ConcurrentDictionary<string, byte> VisitedDestinations { get; }

    public long BytesReceived => Interlocked.Read(ref _bytesReceived);
    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long TotalBytes => BytesReceived + BytesSent;
    public int ActiveConnections => Volatile.Read(ref _activeConnections);

    public UserStats(string username)
    {
        Username = username;
        VisitedDestinations = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
    }

    public void AddBytesReceived(long count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _bytesReceived, count);
        }
    }

    public void AddBytesSent(long count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _bytesSent, count);
        }
    }

    public bool TryIncrementConnections(int maxLimit)
    {
        while (true)
        {
            int current = Volatile.Read(ref _activeConnections);
            if (current >= maxLimit)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _activeConnections, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    public void DecrementConnections()
    {
        Interlocked.Decrement(ref _activeConnections);
    }

    public void RecordDestination(string destination)
    {
        if (!string.IsNullOrWhiteSpace(destination) && VisitedDestinations.Count < 1000)
        {
            VisitedDestinations.TryAdd(destination, 0);
        }
    }
}