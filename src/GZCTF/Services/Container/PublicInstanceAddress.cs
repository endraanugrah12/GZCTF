using System.Net;
using System.Net.Sockets;

namespace GZCTF.Services.Container;

internal static class PublicInstanceAddress
{
    internal static async Task<string> ResolveAsync(string host, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Set ContainerProvider.PublicIP to the challenge host's public IP.");
        if (IPAddress.TryParse(host.Trim().Trim('[', ']'), out var literal)) return literal.ToString();
        // Compatibility for existing installations. Operators behind a CDN must set PublicIP
        // explicitly: DNS for the website can point to the CDN rather than the challenge host.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
        return (addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Could not resolve challenge host; set ContainerProvider.PublicIP.")).ToString();
    }
}
