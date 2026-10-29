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
    public byte ErrorCode { get; }

    public bool IsValid => ErrorCode == Socks5Constants.ReplySuccess;

    private Socks5Request(byte command, byte addressType, string targetHost, int targetPort, byte errorCode)
    {
        Command = command;
        AddressType = addressType;
        TargetHost = targetHost;
        TargetPort = targetPort;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Считывает запрос SOCKS5 и выполняет валидацию команды и типа адреса по RFC 1928.
    /// </summary>
    public static async Task<Socks5Request?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
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

        if (command != Socks5Constants.CommandConnect)
        {
            return new Socks5Request(command, addressType, string.Empty, 0, Socks5Constants.ReplyCommandNotSupported);
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
                byte[] domainLenBuffer = new byte[1];
                await stream.ReadExactlyAsync(domainLenBuffer, cancellationToken);
                byte[] domainBuffer = new byte[domainLenBuffer[0]];
                await stream.ReadExactlyAsync(domainBuffer, cancellationToken);
                targetHost = Encoding.ASCII.GetString(domainBuffer);
                break;

            case Socks5Constants.AddressTypeIPv6:
            default:
                return new Socks5Request(command, addressType, string.Empty, 0, Socks5Constants.ReplyAddressTypeNotSupported);
        }

        byte[] portBuffer = new byte[2];
        await stream.ReadExactlyAsync(portBuffer, cancellationToken);
        int targetPort = BinaryPrimitives.ReadUInt16BigEndian(portBuffer);

        return new Socks5Request(command, addressType, targetHost, targetPort, Socks5Constants.ReplySuccess);
    }

    public static async Task SendReplyAsync(Stream stream, byte replyCode, CancellationToken cancellationToken = default)
    {
        byte[] response = [
            Socks5Constants.Version5,
            replyCode,
            0x00,
            Socks5Constants.AddressTypeIPv4,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        ];

        await stream.WriteAsync(response, cancellationToken);
    }
}