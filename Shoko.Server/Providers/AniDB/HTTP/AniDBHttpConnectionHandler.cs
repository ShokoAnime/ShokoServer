using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AniDBHttpConnectionHandler : ConnectionHandler, IHttpConnectionHandler
{
    public override double BanTimerResetLength => 12;
    private readonly HttpRateLimiter _rateLimiter;
    private readonly IHttpClientFactory _httpClientFactory;

    public override string Type => "HTTP";
    protected override UpdateType BanEnum => UpdateType.HTTPBan;
    public bool IsAlive => true;

    public AniDBHttpConnectionHandler(ILoggerFactory loggerFactory, HttpRateLimiter rateLimiter, IHttpClientFactory httpClientFactory) : base(loggerFactory)
    {
        _rateLimiter = rateLimiter;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HttpResponse<string>> GetHttp(string url, bool force = false)
    {
        if (!force && IsBanned)
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.HTTPBan,
                BanExpires = BanTime?.AddHours(BanTimerResetLength),
            };
        }

        var response = await _rateLimiter.EnsureRate(async () =>
        {
            using var httpClient = _httpClientFactory.CreateClient("AniDB");
            var baseAddress = new Uri(Utils.SettingsProvider.GetSettings().AniDb.HTTPServerUrl);
            if (httpClient.BaseAddress == null || !httpClient.BaseAddress.Equals(baseAddress))
                httpClient.BaseAddress = baseAddress;

            using var response = await httpClient.GetAsync(url);
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
