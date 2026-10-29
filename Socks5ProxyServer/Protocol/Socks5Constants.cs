namespace Socks5ProxyServer.Protocol;

public static class Socks5Constants
{
    // Версия протокола SOCKS (RFC 1928)
    public const byte Version5 = 0x05;

    // Методы аутентификации (RFC 1928)
    public const byte MethodNoAuth = 0x00;
    public const byte MethodUsernamePassword = 0x02;
    public const byte MethodNoAcceptable = 0xFF;

    // Версия и статусы для подпереговоров логин/пароль (RFC 1929)
    public const byte AuthVersion = 0x01;
    public const byte AuthSuccess = 0x00;
    public const byte AuthFailure = 0x01;

    // Поддерживаемые команды SOCKS5
    public const byte CommandConnect = 0x01;

    // Типы целевых адресов
    public const byte AddressTypeIPv4 = 0x01;
    public const byte AddressTypeDomain = 0x03;
    public const byte AddressTypeIPv6 = 0x04;

    // Коды ответов сервера клиенту (RFC 1928, Раздел 6)
    public const byte ReplySuccess = 0x00;
    public const byte ReplyGeneralFailure = 0x01;
    public const byte ReplyConnectionNotAllowed = 0x02;
    public const byte ReplyNetworkUnreachable = 0x03;
    public const byte ReplyHostUnreachable = 0x04;
    public const byte ReplyConnectionRefused = 0x05;
    public const byte ReplyCommandNotSupported = 0x07;
    public const byte ReplyAddressTypeNotSupported = 0x08;
}