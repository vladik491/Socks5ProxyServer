using System.Net.Sockets;
using System.Text;

namespace Socks5ProxyServer.Protocol;

// Модуль протокола SOCKS5 (RFC 1929).
// Выполняет бинарный разбор подпереговоров аутентификации (логин и пароль),
// сверяет данные с зарегистрированными учетными записями и возвращает статус доступа (0x00 / 0x01).

public class Socks5Authenticator
{
    public async Task<string?> AuthenticateAsync(Stream stream, Func<string, string, bool> validateCredentials, CancellationToken cancellationToken = default)
    {
        byte[] versionBuffer = new byte[1];
        await stream.ReadExactlyAsync(versionBuffer, cancellationToken);

        if (versionBuffer[0] != Socks5Constants.AuthVersion)
        {
            return null;
        }

        byte[] ulenBuffer = new byte[1];
        await stream.ReadExactlyAsync(ulenBuffer, cancellationToken);
        int usernameLength = ulenBuffer[0];

        byte[] usernameBytes = new byte[usernameLength];
        await stream.ReadExactlyAsync(usernameBytes, cancellationToken);
        string username = Encoding.UTF8.GetString(usernameBytes);

        byte[] plenBuffer = new byte[1];
        await stream.ReadExactlyAsync(plenBuffer, cancellationToken);
        int passwordLength = plenBuffer[0];

        byte[] passwordBytes = new byte[passwordLength];
        await stream.ReadExactlyAsync(passwordBytes, cancellationToken);
        string password = Encoding.UTF8.GetString(passwordBytes);

        bool isValid = validateCredentials(username, password);
        byte status = isValid ? Socks5Constants.AuthSuccess : Socks5Constants.AuthFailure;

        byte[] authResponse = [Socks5Constants.AuthVersion, status];
        await stream.WriteAsync(authResponse, cancellationToken);

        return isValid ? username : null;
    }
}