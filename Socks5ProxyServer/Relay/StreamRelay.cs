using System.Buffers;
using Socks5ProxyServer.Stats;

namespace Socks5ProxyServer.Relay;

// Модуль ретрансляции трафика.
// Организует асинхронную двунаправленную пересылку сетевых потоков между клиентом и целевым узлом.
// Использует пул буферов ArrayPool<byte> для Zero-Allocation передачи и параллельно ведет подсчет байт.

public class StreamRelay
{
    private const int BufferSize = 16384;

    public async Task RelayAsync(
        Stream clientStream,
        Stream targetStream,
        UserStats userStats,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var clientToTargetTask = ForwardStreamAsync(
            clientStream,
            targetStream,
            userStats.AddBytesSent,
            linkedCts);

        var targetToClientTask = ForwardStreamAsync(
            targetStream,
            clientStream,
            userStats.AddBytesReceived,
            linkedCts);

        // Ждем завершения любого из направлений (например, закрытия соединения одной из сторон)
        await Task.WhenAny(clientToTargetTask, targetToClientTask);

        // Отменяем оставшееся второе направление передачи
        await linkedCts.CancelAsync();

        // Дожидаемся корректного завершения обоих потоков
        await Task.WhenAll(
            clientToTargetTask.ContinueWith(t => { }, TaskScheduler.Default),
            targetToClientTask.ContinueWith(t => { }, TaskScheduler.Default));
    }

    private async Task ForwardStreamAsync(
        Stream source,
        Stream destination,
        Action<long> onBytesRead,
        CancellationTokenSource cts)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            while (!cts.IsCancellationRequested)
            {
                int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);

                if (bytesRead == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                await destination.FlushAsync(cts.Token);

                onBytesRead(bytesRead);
            }
        }
        catch (OperationCanceledException)
        {
            // Корректное завершение при отмене токена
        }
        catch (IOException)
        {
            // Обрыв соединения клиентом или сервером
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await cts.CancelAsync();
        }
    }
}