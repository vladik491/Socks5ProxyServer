using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Socks5ProxyServer.Protocol;

public class Socks5Request
{
    public byte Command { get; }
    public byte AddressType { get; }
    public string TargetHost { get; }
    public int TargetPort { get; }

    public Socks5Request(byte command, byte addressType, string targetHost, int targetPort)
    {
        Command = command;
        AddressType = addressType;
        TargetHost = targetHost;
        TargetPort = targetPort;
    }

    /// <summary>
    /// Считывает и парсит бинарный пакет запроса SOCKS5 (RFC 1928, раздел 4).
    /// Поддерживает адресацию IPv4 и FQDN (доменные имена).
    /// </summary>
    public static async Task<Socks5Request?> ReadAsync(NetworkStream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);

        byte version = header[0];
        byte command = header[1];
        byte addressType = header[3];

        if (version != Socks5Constants.Version5)
        {
            return null;
        }

        string targetHost;

        switch (addressType)
        {
            case Socks5Constants.AddressTypeIPv4:
                byte[] ipv4Buffer = new byte[4];
                await stream.ReadExactlyAsync(ipv4Buffer, cancellationToken);
                targetHost = new IPAddress(ipv4Buffer).ToString();
                break;

            case Socks5Constants.AddressTypeDomain:
                byte[] domainLengthBuffer = new byte[1];
                await stream.ReadExactlyAsync(domainLengthBuffer, cancellationToken);
                int domainLength = domainLengthBuffer[0];

                byte[] domainBuffer = new byte[domainLength];
                await stream.ReadExactlyAsync(domainBuffer, cancellationToken);
                targetHost = Encoding.ASCII.GetString(domainBuffer);
                break;

            default:
                return null;
        }

        // Порт назначения передается 2 байтами в сетевом порядке (Big-Endian)
        byte[] portBuffer = new byte[2];
        await stream.ReadExactlyAsync(portBuffer, cancellationToken);
        int targetPort = BinaryPrimitives.ReadUInt16BigEndian(portBuffer);

        return new Socks5Request(command, addressType, targetHost, targetPort);
    }

    /// <summary>
    /// Отправляет клиенту бинарный ответ о статусе подключения к целевому узлу.
    /// </summary>
    public static async Task SendReplyAsync(
        NetworkStream stream,
        byte replyCode,
        CancellationToken cancellationToken = default)
    {
        // 0x01 означает фиктивный IPv4 0.0.0.0:0 в качестве BND.ADDR по стандарту RFC
        byte[] response = [
            Socks5Constants.Version5,
            replyCode,
            0x00, // Зарезервированный байт (RSV)
            Socks5Constants.AddressTypeIPv4,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        ];

        await stream.WriteAsync(response, cancellationToken);
    }
}