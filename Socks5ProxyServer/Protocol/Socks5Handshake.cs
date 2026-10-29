using System.Net.Sockets;

namespace Socks5ProxyServer.Protocol;

public class Socks5Handshake
{
    /// <summary>
    /// Выполняет первичное согласование по RFC 1928: проверяет версию SOCKS5 
    /// и подтверждает использование обязательной аутентификации 0x02 (Username/Password).
    /// </summary>
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