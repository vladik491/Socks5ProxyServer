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
    /// Обрабатывает полный жизненный цикл сессии клиента: согласование, авторизация,
    /// проверка квот соединений, подключение к целевому хосту и пересылка трафика.
    /// </summary>
    public async Task HandleSessionAsync(TcpClient client, CancellationToken cancellationToken = default)
    {
        using (client)
        await using (var clientStream = client.GetStream())
        {
            // 1. Рукопожатие SOCKS5
            if (!await _handshake.NegotiateAuthMethodAsync(clientStream, cancellationToken))
            {
                return;
            }

            // 2. Аутентификация RFC 1929
            string? username = await _authenticator.AuthenticateAsync(
                clientStream,
                _configuration.ValidateCredentials,
                cancellationToken);

            if (string.IsNullOrEmpty(username))
            {
                return;
            }

            // 3. Проверка лимита одновременных подключений пользователя
            if (!_tracker.TryAcquireConnection(username, _configuration.MaxConnectionsPerUser))
            {
                await Socks5Request.SendReplyAsync(
                    clientStream,
                    Socks5Constants.ReplyConnectionNotAllowed,
                    cancellationToken);
                return;
            }

            var userStats = _tracker.GetOrCreateUserStats(username);

            try
            {
                // 4. Разбор запроса CONNECT
                var request = await Socks5Request.ReadAsync(clientStream, cancellationToken);
                if (request == null || request.Command != Socks5Constants.CommandConnect)
                {
                    await Socks5Request.SendReplyAsync(
                        clientStream,
                        Socks5Constants.ReplyCommandNotSupported,
                        cancellationToken);
                    return;
                }

                userStats.RecordDestination($"{request.TargetHost}:{request.TargetPort}");

                // 5. Установка исходящего TCP-соединения с целевым сайтом/сервером
                using var targetClient = new TcpClient();
                try
                {
                    await targetClient.ConnectAsync(request.TargetHost, request.TargetPort, cancellationToken);
                }
                catch (SocketException)
                {
                    await Socks5Request.SendReplyAsync(
                        clientStream,
                        Socks5Constants.ReplyHostUnreachable,
                        cancellationToken);
                    return;
                }

                await using var targetStream = targetClient.GetStream();

                // 6. Успешный ответ клиенту о готовности туннеля
                await Socks5Request.SendReplyAsync(
                    clientStream,
                    Socks5Constants.ReplySuccess,
                    cancellationToken);

                // 7. Запуск двунаправленной пересылки данных
                await _relay.RelayAsync(clientStream, targetStream, userStats, cancellationToken);
            }
            finally
            {
                _tracker.ReleaseConnection(username);
            }
        }
    }
}