using System.Buffers.Binary;
using System.Text;
using Socks5ProxyServer.Core;
using Socks5ProxyServer.Protocol;
using Socks5ProxyServer.Stats;
using Xunit;

namespace Socks5ProxyServer.Tests;

public class ProxyServerTests
{
    // 1. ТЕСТЫ СТАТИСТИКИ И ЛИМИТОВ (Stats)
    [Fact]
    public void UserStats_AddBytes_IncrementsAccurately()
    {
        var stats = new UserStats("alice");
        stats.AddBytesReceived(1024);
        stats.AddBytesSent(512);

        Assert.Equal(1024, stats.BytesReceived);
        Assert.Equal(512, stats.BytesSent);
        Assert.Equal(1536, stats.TotalBytes);
    }

    [Fact]
    public void UserStats_TryIncrementConnections_EnforcesMaxLimit()
    {
        var stats = new UserStats("bob");

        Assert.True(stats.TryIncrementConnections(2));
        Assert.True(stats.TryIncrementConnections(2));
        Assert.False(stats.TryIncrementConnections(2));
        Assert.Equal(2, stats.ActiveConnections);
    }

    [Fact]
    public void UserStats_RecordDestination_ProtectsAgainstDosOverflow()
    {
        var stats = new UserStats("charlie");

        for (int i = 0; i < 1050; i++)
        {
            stats.RecordDestination($"host{i}.com:80");
        }

        Assert.Equal(1000, stats.VisitedDestinations.Count);
    }

    [Fact]
    public void TrafficTracker_AcquireAndRelease_ManagesSlotAccurately()
    {
        var tracker = new TrafficTracker();

        Assert.True(tracker.TryAcquireConnection("alice", 1));
        Assert.False(tracker.TryAcquireConnection("alice", 1));

        tracker.ReleaseConnection("alice");

        Assert.True(tracker.TryAcquireConnection("alice", 1));
    }


    // 2. ТЕСТЫ КОНФИГУРАЦИИ (Core)
    [Fact]
    public void ServerConfiguration_ValidateCredentials_AcceptsOnlyValidUser()
    {
        var config = new ServerConfiguration();
        config.Users["alice"] = "secret123";

        Assert.True(config.ValidateCredentials("alice", "secret123"));
        Assert.False(config.ValidateCredentials("alice", "wrongpass"));
        Assert.False(config.ValidateCredentials("unknown", "secret123"));
    }

    [Fact]
    public void ServerConfiguration_ValidateCredentials_IsCaseInsensitiveForUsername()
    {
        var config = new ServerConfiguration();
        config.Users["Alice"] = "secret123";

        Assert.True(config.ValidateCredentials("ALICE", "secret123"));
    }


    // 3. ТЕСТЫ РУКОПОЖАТИЯ (RFC 1928 Handshake)
    [Fact]
    public async Task Socks5Handshake_NegotiateAuthMethod_SelectsUsernamePasswordMethod()
    {
        var handshake = new Socks5Handshake();
        var clientStream = new MemoryStream();
        clientStream.Write([0x05, 0x02, 0x00, 0x02]);
        clientStream.Position = 0;

        bool result = await handshake.NegotiateAuthMethodAsync(clientStream);

        Assert.True(result);
        byte[] response = clientStream.ToArray();
        Assert.Equal(0x05, response[4]);
        Assert.Equal(0x02, response[5]);
    }

    [Fact]
    public async Task Socks5Handshake_NegotiateAuthMethod_RejectsIfUserPassMissing()
    {
        var handshake = new Socks5Handshake();
        var clientStream = new MemoryStream();
        clientStream.Write([0x05, 0x01, 0x00]);
        clientStream.Position = 0;

        bool result = await handshake.NegotiateAuthMethodAsync(clientStream);

        Assert.False(result);
        byte[] response = clientStream.ToArray();
        Assert.Equal(0x05, response[3]);
        Assert.Equal(0xFF, response[4]);
    }


    // 4. ТЕСТЫ АУТЕНТИФИКАЦИИ (RFC 1929 Auth)
    [Fact]
    public async Task Socks5Authenticator_AuthenticateAsync_SucceedsWithValidCredentials()
    {
        var authenticator = new Socks5Authenticator();
        var stream = new MemoryStream();

        stream.Write([0x01, 0x05]);
        stream.Write(Encoding.UTF8.GetBytes("admin"));
        stream.WriteByte(0x04);
        stream.Write(Encoding.UTF8.GetBytes("1234"));
        stream.Position = 0;

        string? username = await authenticator.AuthenticateAsync(stream, (u, p) => u == "admin" && p == "1234");

        Assert.Equal("admin", username);
        byte[] response = stream.ToArray();
        Assert.Equal(0x01, response[^2]); 
        Assert.Equal(0x00, response[^1]);
    }

    [Fact]
    public async Task Socks5Authenticator_AuthenticateAsync_RejectsInvalidCredentials()
    {
        var authenticator = new Socks5Authenticator();
        var stream = new MemoryStream();

        stream.Write([0x01, 0x04]);
        stream.Write(Encoding.UTF8.GetBytes("user"));
        stream.WriteByte(0x03);
        stream.Write(Encoding.UTF8.GetBytes("bad"));
        stream.Position = 0;

        string? username = await authenticator.AuthenticateAsync(stream, (u, p) => false);

        Assert.Null(username);
        byte[] response = stream.ToArray();
        Assert.Equal(0x01, response[^1]);
    }


    // 5. ТЕСТЫ РАЗБОРА ЗАПРОСОВ (RFC 1928 Requests)
    [Fact]
    public async Task Socks5Request_ReadAsync_ParsesValidIPv4()
    {
        var stream = new MemoryStream();
        stream.Write([0x05, 0x01, 0x00, 0x01, 127, 0, 0, 1, 0x1F, 0x90]);
        stream.Position = 0;

        var request = await Socks5Request.ReadAsync(stream);

        Assert.NotNull(request);
        Assert.True(request.IsValid);
        Assert.Equal("127.0.0.1", request.TargetHost);
        Assert.Equal(8080, request.TargetPort);
    }

    [Fact]
    public async Task Socks5Request_ReadAsync_ParsesValidFQDN()
    {
        var stream = new MemoryStream();
        byte[] domainBytes = Encoding.ASCII.GetBytes("example.com");
        stream.Write([0x05, 0x01, 0x00, 0x03, (byte)domainBytes.Length]);
        stream.Write(domainBytes);
        stream.Write([0x00, 0x50]);
        stream.Position = 0;

        var request = await Socks5Request.ReadAsync(stream);

        Assert.NotNull(request);
        Assert.True(request.IsValid);
        Assert.Equal("example.com", request.TargetHost);
        Assert.Equal(80, request.TargetPort);
    }

    [Fact]
    public async Task Socks5Request_ReadAsync_RejectsUnsupportedCommandWithCode0x07()
    {
        var stream = new MemoryStream();
        stream.Write([0x05, 0x02, 0x00, 0x01]);
        stream.Position = 0;

        var request = await Socks5Request.ReadAsync(stream);

        Assert.NotNull(request);
        Assert.False(request.IsValid);
        Assert.Equal(Socks5Constants.ReplyCommandNotSupported, request.ErrorCode);
    }

    [Fact]
    public async Task Socks5Request_ReadAsync_RejectsIPv6WithCode0x08()
    {
        var stream = new MemoryStream();
        stream.Write([0x05, 0x01, 0x00, 0x04]);
        stream.Position = 0;

        var request = await Socks5Request.ReadAsync(stream);

        Assert.NotNull(request);
        Assert.False(request.IsValid);
        Assert.Equal(Socks5Constants.ReplyAddressTypeNotSupported, request.ErrorCode);
    }

    [Fact]
    public async Task Socks5Request_SendReplyAsync_WritesCompliantBinaryHeader()
    {
        var stream = new MemoryStream();

        await Socks5Request.SendReplyAsync(stream, Socks5Constants.ReplySuccess);

        byte[] reply = stream.ToArray();
        Assert.Equal(10, reply.Length);
        Assert.Equal(0x05, reply[0]); 
        Assert.Equal(0x00, reply[1]); 
        Assert.Equal(0x00, reply[2]); 
        Assert.Equal(0x01, reply[3]); 
    }

    [Fact]
    public void BigEndianPort_BinaryPrimitives_DecodesCorrectly()
    {
        byte[] portBytes = [0x01, 0xBB];
        ushort port = BinaryPrimitives.ReadUInt16BigEndian(portBytes);

        Assert.Equal(443, port);
    }
}