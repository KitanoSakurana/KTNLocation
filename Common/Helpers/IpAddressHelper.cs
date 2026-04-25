using System.Net;
using System.Net.Sockets;

namespace KTNLocation.Helpers;

public static class IpAddressHelper
{
    public static bool TryToIPv4Number(string ipText, out long value, out string normalizedIp)
    {
        value = 0;
        normalizedIp = string.Empty;

        if (string.IsNullOrWhiteSpace(ipText))
        {
            return false;
        }

        if (!IPAddress.TryParse(ipText.Trim(), out var address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        value = ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
        normalizedIp = address.ToString();
        return true;
    }
}
