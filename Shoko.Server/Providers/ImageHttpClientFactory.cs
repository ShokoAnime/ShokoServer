using System.Net.Http;

namespace Shoko.Server.Providers;

public class ImageHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var httpClientHandler = new HttpClientHandler
        {
            // ignore all certificate failures
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        var httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Add("user-agent", "JMM");

        return httpClient;
    }
}
