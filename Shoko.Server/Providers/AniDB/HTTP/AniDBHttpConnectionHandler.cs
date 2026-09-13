using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AniDBHttpConnectionHandler : ConnectionHandler, IHttpConnectionHandler
{
    private readonly HttpRateLimiter _rateLimiter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsProvider _settingsProvider;

    public override string Type => "HTTP";

    protected override UpdateType BanEnum => UpdateType.HTTPBan;

    public bool IsAlive => true;

    public AniDBHttpConnectionHandler(AniDbBanStateService banStateService, ILoggerFactory loggerFactory, HttpRateLimiter rateLimiter, IHttpClientFactory httpClientFactory, ISettingsProvider settingsProvider) : base(loggerFactory, banStateService.Http)
    {
        _rateLimiter = rateLimiter;
        _httpClientFactory = httpClientFactory;
        _settingsProvider = settingsProvider;
    }

    /// <summary>
    /// Sends a GET request to the AniDB HTTP API.
    /// </summary>
    /// <param name="url">The URL or path to request.</param>
    /// <param name="force">Bypass the ban check. A ban response still registers the ban but does not extend an existing one.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="AniDBBannedException">Thrown when the server responds 403 or the response body indicates a ban.</exception>
    /// <exception cref="HttpRequestException">Thrown for other non-success status codes.</exception>
    public async Task<HttpResponse<string>> GetHttp(string url, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && IsBanned)
            throw CreateBanException();

        return await _rateLimiter.EnsureRate(async () =>
        {
            var httpClient = _httpClientFactory.CreateClient("AniDB");
            httpClient.BaseAddress = new Uri(_settingsProvider.GetSettings().AniDb.HTTPServerUrl);

            Logger.LogTrace("AniDB HTTP request: {Url}", url);

            using var message = await httpClient.GetAsync(url, cancellationToken);
            Logger.LogTrace("AniDB HTTP response: {StatusCode} for {Url}", (int)message.StatusCode, url);

            // AniDB signals a ban with 403; detect it before EnsureSuccessStatusCode
            // throws a raw HttpRequestException with no ban state.
            if (message.StatusCode is HttpStatusCode.Forbidden)
                throw CreateBanException();

            message.EnsureSuccessStatusCode();

            var output = await message.Content.ReadAsStringAsync(cancellationToken);

            if (ContainsBan(output))
                throw CreateBanException();

            return new HttpResponse<string> { Response = output, Code = message.StatusCode };
        });
    }

    /// <summary>
    /// Registers the ban unless one is already active (re-stamping would extend it),
    /// then builds the exception with the expiry.
    /// </summary>
    private AniDBBannedException CreateBanException() => AniDBBannedException.For(BanState);

    private static bool ContainsBan(string xmlResult)
    {
        if (string.IsNullOrEmpty(xmlResult))
            return false;

        return xmlResult.IndexOf(">banned<", StringComparison.InvariantCultureIgnoreCase) != -1;
    }
}
