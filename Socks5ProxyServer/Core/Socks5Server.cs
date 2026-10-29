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
    /// Запускает TCP-слушатель сервера и принимает подключения в неблокирующем асинхронном режиме.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener = new TcpListener(IPAddress.Any, _configuration.Port);
        _listener.Start();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SERVER] SOCKS5-прокси запущен на порту {_configuration.Port}");
        Console.ResetColor();

        Console.WriteLine($"[CONFIG] Загружено пользователей: {_configuration.Users.Count} ({string.Join(", ", _configuration.Users.Keys)})");
        Console.WriteLine($"[CONFIG] Лимит одновременных подключений: {_configuration.MaxConnectionsPerUser}");
        Console.WriteLine($"[STATUS] Сервер готов к приему трафика. Нажмите Ctrl+C для выхода.\n");

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
                Console.WriteLine($"[ERROR] Ошибка входящего сокета: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    public void Stop()
    {
        _listener?.Stop();
        PrintFinalSummary();
    }

    private void PrintFinalSummary()
    {
        var stats = _tracker.GetAllStats();
        if (stats.Count == 0)
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\n[ИТОГОВАЯ СТАТИСТИКА СЕССИИ]");
        Console.ResetColor();

        foreach (var user in stats)
        {
            Console.WriteLine($"* Пользователь '{user.Username}':");
            Console.WriteLine($"    Принято: {user.BytesReceived / 1024.0:F2} КБ | Отправлено: {user.BytesSent / 1024.0:F2} КБ | Всего: {user.TotalBytes / 1024.0:F2} КБ");
            Console.WriteLine($"    Посещенные адреса: {(user.VisitedDestinations.IsEmpty ? "нет" : string.Join(", ", user.VisitedDestinations.Keys))}");
        }
    }
}