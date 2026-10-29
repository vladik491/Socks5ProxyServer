using System.Net.Sockets;
using Socks5ProxyServer.Protocol;
using Socks5ProxyServer.Relay;
using Socks5ProxyServer.Stats;

namespace Socks5ProxyServer.Core;

public class ClientSessionHandler
{
    private readonly ServerConfiguration _configuration;
    private readonly TrafficTracker _tracker;
    private readonly Socks5Handshake _handshake = new();
    private readonly Socks5Authenticator _authenticator = new();
    private readonly StreamRelay _relay = new();

    public ClientSessionHandler(ServerConfiguration configuration, TrafficTracker tracker)
    {
        _configuration = configuration;
        _tracker = tracker;
    }

    /// <summary>
    /// Обрабатывает сессию клиента с информативным событийным логированием.
    /// </summary>
    public async Task HandleSessionAsync(TcpClient client, CancellationToken cancellationToken = default)
    {
        using (client)
        await using (var clientStream = client.GetStream())
        {
            if (!await _handshake.NegotiateAuthMethodAsync(clientStream, cancellationToken))
            {
                return;
            }

            string? username = await _authenticator.AuthenticateAsync(
                clientStream,
                _configuration.ValidateCredentials,
                cancellationToken);

            if (string.IsNullOrEmpty(username))
            {
                LogEvent(ConsoleColor.Yellow, "AUTH_FAIL", "Попытка входа с неверным паролем или неизвестным логином.");
                return;
            }

            LogEvent(ConsoleColor.Green, "AUTH", $"Пользователь '{username}' успешно авторизован.");

            if (!_tracker.TryAcquireConnection(username, _configuration.MaxConnectionsPerUser))
            {
                LogEvent(ConsoleColor.DarkYellow, "LIMIT", $"Превышен лимит подключений для '{username}' (максимум {_configuration.MaxConnectionsPerUser}).");
                await Socks5Request.SendReplyAsync(clientStream, Socks5Constants.ReplyConnectionNotAllowed, cancellationToken);
                return;
            }

            var userStats = _tracker.GetOrCreateUserStats(username);
            string targetDestination = "неизвестно";

            try
            {
                var request = await Socks5Request.ReadAsync(clientStream, cancellationToken);
                if (request == null || request.Command != Socks5Constants.CommandConnect)
                {
                    await Socks5Request.SendReplyAsync(clientStream, Socks5Constants.ReplyCommandNotSupported, cancellationToken);
                    return;
                }

                targetDestination = $"{request.TargetHost}:{request.TargetPort}";
                userStats.RecordDestination(targetDestination);

                using var targetClient = new TcpClient();
                try
                {
                    await targetClient.ConnectAsync(request.TargetHost, request.TargetPort, cancellationToken);
                }
                catch (SocketException)
                {
                    LogEvent(ConsoleColor.Red, "ERROR", $"Целевой узел {targetDestination} недоступен для '{username}'.");
                    await Socks5Request.SendReplyAsync(clientStream, Socks5Constants.ReplyHostUnreachable, cancellationToken);
                    return;
                }

                await using var targetStream = targetClient.GetStream();

                await Socks5Request.SendReplyAsync(clientStream, Socks5Constants.ReplySuccess, cancellationToken);

                LogEvent(ConsoleColor.Cyan, "CONNECT", $"{username} -> {targetDestination} (активных сессий: {userStats.ActiveConnections})");

                await _relay.RelayAsync(clientStream, targetStream, userStats, cancellationToken);
            }
            finally
            {
                _tracker.ReleaseConnection(username);
                LogEvent(ConsoleColor.DarkGray, "CLOSE", $"{username} завершил сессию к {targetDestination}. Всего передано: {userStats.TotalBytes / 1024.0:F2} КБ");
            }
        }
    }

    private static void LogEvent(ConsoleColor color, string tag, string message)
    {
        lock (Console.Out)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
            Console.ForegroundColor = color;
            Console.Write($"[{tag,-9}] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }
}