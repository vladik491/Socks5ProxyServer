namespace Socks5ProxyServer.Core;

public class ServerConfiguration
{
    public int Port { get; init; } = 1080;
    public int MaxConnectionsPerUser { get; init; } = 5;
    public Dictionary<string, string> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Проверяет соответствие логина и пароля пользователя зарегистрированным учетным записям.
    /// </summary>
    public bool ValidateCredentials(string username, string password)
    {
        return Users.TryGetValue(username, out var expectedPassword) && expectedPassword == password;
    }
}