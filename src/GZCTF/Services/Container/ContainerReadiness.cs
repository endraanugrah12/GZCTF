using System.Net.Sockets;

namespace GZCTF.Services.Container;

/// <summary>Checks only the endpoint assigned to an authorized challenge instance.</summary>
public static class ContainerReadiness
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    });

    public static async Task<bool> CheckAsync(Models.Data.Container container, bool https,
        CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            if (https)
            {
                if (string.IsNullOrWhiteSpace(container.PublicIP)) return false;
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    new UriBuilder("https", container.PublicIP, 443).Uri);
                using var response = await Client.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                var status = (int)response.StatusCode;
                // Redirects and authentication gates mean the application is reachable.
                return status is >= 200 and < 400 or 401 or 403;
            }

            using var client = new TcpClient();
            await client.ConnectAsync(container.IP, container.Port, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or OperationCanceledException
                                   or UriFormatException or ArgumentException)
        {
            return false;
        }
    }
}
