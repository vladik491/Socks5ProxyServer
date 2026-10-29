using Socks5ProxyServer.Core;
using Socks5ProxyServer.Stats;

namespace Socks5ProxyServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.Title = "SOCKS5 Proxy Server with Accounting";

        var configuration = new ServerConfiguration
        {
            Port = 1080,
            MaxConnectionsPerUser = 3
        };

        // Регистрация разрешенных пользователей (RFC 1929)
        configuration.Users["alice"] = "secret123";
        configuration.Users["bob"] = "pass456";

        var tracker = new TrafficTracker();
        var server = new Socks5Server(configuration, tracker);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Console.WriteLine("\n[SERVER] Завершение работы по сигналу Ctrl+C...");
            cts.Cancel();
        };

        try
        {
            await server.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Ожидаемое завершение работы сервера
        }
        finally
        {
            server.Stop();
            Console.WriteLine("[SERVER] Сервер успешно остановлен.");
        }
    }
}