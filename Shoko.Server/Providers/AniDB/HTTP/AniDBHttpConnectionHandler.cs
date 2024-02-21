using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AniDBHttpConnectionHandler : ConnectionHandler, IHttpConnectionHandler
{
    public override double BanTimerResetLength => 12;

    public override string Type => "HTTP";
    public override UpdateType BanEnum => UpdateType.HTTPBan;
    public bool IsAlive => true;

    public AniDBHttpConnectionHandler(ILoggerFactory loggerFactory, HttpRateLimiter rateLimiter) : base(loggerFactory, rateLimiter)
    {
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
                BanType = UpdateType.HTTPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength)
            };
        }

        RateLimiter.EnsureRate();

        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        });
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
        client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1");

        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync();
        if (responseStream == null)
        {
            throw new EndOfStreamException("Response Body was expected, but none returned");
        }

        var charset = response.Content.Headers.ContentType?.CharSet;
        Encoding encoding = null;
        if (!string.IsNullOrEmpty(charset))
        {
            encoding = Encoding.GetEncoding(charset);
        }

        encoding ??= Encoding.UTF8;
        using var reader = new StreamReader(responseStream, encoding);
        var output = await reader.ReadToEndAsync();

        if (CheckForBan(output))
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.HTTPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength)
            };
        }

        return new HttpResponse<string> { Response = output, Code = response.StatusCode };
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
