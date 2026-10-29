using System.Net;
using System.Net.Sockets;
using Socks5ProxyServer.Stats;

namespace Socks5ProxyServer.Core;

public class Socks5Server
{
    private readonly ServerConfiguration _configuration;
    private readonly TrafficTracker _tracker;
    private readonly ClientSessionHandler _sessionHandler;
    private TcpListener? _listener;

    public Socks5Server(ServerConfiguration configuration, TrafficTracker tracker)
    {
        _configuration = configuration;
        _tracker = tracker;
        _sessionHandler = new ClientSessionHandler(configuration, tracker);
    }

    /// <summary>
    /// Запускает TCP-слушатель прокси-сервера и фоновый вывод статистики.
    /// Обрабатывает клиентов параллельно в асинхронном цикле.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener = new TcpListener(IPAddress.Any, _configuration.Port);
        _listener.Start();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SERVER] SOCKS5-прокси сервер запущен на порту {_configuration.Port}");
        Console.WriteLine($"[SERVER] Лимит подключений на пользователя: {_configuration.MaxConnectionsPerUser}");
        Console.ResetColor();

        _ = Task.Run(() => DisplayStatisticsLoopAsync(cancellationToken), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => _sessionHandler.HandleSessionAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Ошибка при приеме соединения: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    public void Stop()
    {
        _listener?.Stop();
    }

    private async Task DisplayStatisticsLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            var allStats = _tracker.GetAllStats();
            if (allStats.Count == 0)
            {
                continue;
            }

            Console.WriteLine(new string('-', 85));
            Console.WriteLine($"{"ПОЛЬЗОВАТЕЛЬ",-15} | {"СЕССИИ",-8} | {"ПРИНЯТО (КБ)",-14} | {"ОТПРАВЛЕНО (КБ)",-16} | {"ПОСЕЩЕННЫЕ ХОСТЫ"}");
            Console.WriteLine(new string('-', 85));

            foreach (var stat in allStats)
            {
                string hosts = string.Join(", ", stat.VisitedDestinations.Keys.Take(3));
                if (stat.VisitedDestinations.Count > 3)
                {
                    hosts += $" (+{stat.VisitedDestinations.Count - 3})";
                }

                Console.WriteLine(
                    $"{stat.Username,-15} | " +
                    $"{stat.ActiveConnections,-8} | " +
                    $"{stat.BytesReceived / 1024.0,-14:F2} | " +
                    $"{stat.BytesSent / 1024.0,-16:F2} | " +
                    $"{hosts}");
            }

            Console.WriteLine(new string('-', 85));
        }
    }
}