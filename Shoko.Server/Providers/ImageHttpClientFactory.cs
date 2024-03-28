using System.Net.Http;
using System.Net.Security;

namespace Shoko.Server.Providers;

public class ImageHttpClientFactory : IHttpClientFactory
{
    private HttpClient _client;
    public HttpClient CreateClient(string name)
    {
        if (_client != null) return _client;

        var httpClient = new HttpClient(new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = delegate { return true; }
            }
        });
        httpClient.DefaultRequestHeaders.Add("user-agent", "JMM");

        return _client = httpClient;
    }
}
