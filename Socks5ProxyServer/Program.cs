using Socks5ProxyServer.Core;
using Socks5ProxyServer.Stats;

namespace Socks5ProxyServer;

// Точка входа в приложение (Main).
// Выполняет инициализацию конфигурации, служб учета трафика, запуск SOCKS5-прокси сервера
// и перехват сигнала прерывания работы Console.CancelKeyPress.

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.Title = "SOCKS5 Proxy Server";

        // Читаем пользователей и лимиты из users.json (или создаем файл автоматически)
        var configuration = ServerConfiguration.LoadOrCreateDefault();
        var tracker = new TrafficTracker();
        var server = new Socks5Server(configuration, tracker);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Console.WriteLine("\n[SERVER] Завершение работы сервера...");
            cts.Cancel();
        };

        try
        {
            await server.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Корректный выход
        }
        finally
        {
            server.Stop();
            Console.WriteLine("[SERVER] Сервер успешно остановлен.");
        }
    }
}