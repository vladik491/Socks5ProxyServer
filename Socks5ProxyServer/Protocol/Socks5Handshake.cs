using System.Net.Sockets;

namespace Socks5ProxyServer.Protocol;

// Модуль протокола SOCKS5 (RFC 1928).
// Реализует процедуру начального рукопожатия: считывает поддерживаемые клиентом методы
// и подтверждает обязательное требование аутентификации по логину и паролю (метод 0x02).

public class Socks5Handshake
{
    public async Task<bool> NegotiateAuthMethodAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[2];
        await stream.ReadExactlyAsync(header, cancellationToken);

        byte version = header[0];
        byte methodCount = header[1];

        if (version != Socks5Constants.Version5 || methodCount == 0)
        {
            return false;
        }

        byte[] methods = new byte[methodCount];
        await stream.ReadExactlyAsync(methods, cancellationToken);

        if (methods.Contains(Socks5Constants.MethodUsernamePassword))
        {
            byte[] successResponse = [Socks5Constants.Version5, Socks5Constants.MethodUsernamePassword];
            await stream.WriteAsync(successResponse, cancellationToken);
            return true;
        }

        byte[] failureResponse = [Socks5Constants.Version5, Socks5Constants.MethodNoAcceptable];
        await stream.WriteAsync(failureResponse, cancellationToken);
        return false;
    }
}