using System.Text.Json;

namespace Socks5ProxyServer.Core;

// Модуль ядра сервера.
// Управляет параметрами сервера (порт, лимит подключений) и хранилищем учетных записей.
// Обеспечивает персистентное сохранение и загрузку настроек из конфигурационного файла users.json.

public class ServerConfiguration
{
    private const string DefaultConfigPath = "users.json";

    public int Port { get; set; } = 1080;
    public int MaxConnectionsPerUser { get; set; } = 3;
    public Dictionary<string, string> Users { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ValidateCredentials(string username, string password)
    {
        return Users.TryGetValue(username, out var expectedPassword) && expectedPassword == password;
    }

    public static ServerConfiguration LoadOrCreateDefault(string filePath = DefaultConfigPath)
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<ServerConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config != null)
            {
                return config;
            }
        }

        var defaultConfig = new ServerConfiguration();
        defaultConfig.Users["alice"] = "secret123";
        defaultConfig.Users["bob"] = "pass456";
        defaultConfig.Users["charlie"] = "qwerty789";
        defaultConfig.Users["admin"] = "admin2026";

        string defaultJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(filePath, defaultJson);
        return defaultConfig;
    }
}