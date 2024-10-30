using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AniDBHttpConnectionHandler : ConnectionHandler, IHttpConnectionHandler
{
    private readonly HttpClient _httpClient;
    public override double BanTimerResetLength => 12;

    public override string Type => "HTTP";
    protected override UpdateType BanEnum => UpdateType.HTTPBan;
    public bool IsAlive => true;

    public AniDBHttpConnectionHandler(ILoggerFactory loggerFactory, HttpRateLimiter rateLimiter) : base(loggerFactory, rateLimiter)
    {
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = delegate { return true; }
            }
        });
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1");
        _httpClient.BaseAddress = new Uri(Utils.SettingsProvider.GetSettings().AniDb.HTTPServerUrl);
    }

    public async Task<HttpResponse<string>> GetHttp(string url)
    {
        var response = await GetHttpDirectly(url);

        return response;
    }

    public async Task<HttpResponse<string>> GetHttpDirectly(string url)
    {
        if (IsBanned)
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.HTTPBan,
                BanExpires = BanTime?.AddHours(BanTimerResetLength),
            };
        }

        var response = await RateLimiter.EnsureRateAsync(async () =>
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var output = await response.Content.ReadAsStringAsync();

            if (CheckForBan(output))
            {
                throw new AniDBBannedException
                {
                    BanType = UpdateType.HTTPBan,
                    BanExpires = BanTime?.AddHours(BanTimerResetLength),
                };
            }

            return new HttpResponse<string> { Response = output, Code = response.StatusCode };
        });

        return response;
    }

    private bool CheckForBan(string xmlResult)
    {
        if (string.IsNullOrEmpty(xmlResult))
        {
            return false;
        }

        var index = xmlResult.IndexOf(@">banned<", StringComparison.InvariantCultureIgnoreCase);
        if (index <= -1)
        {
            return false;
        }

        IsBanned = true;
        return true;
    }
}
