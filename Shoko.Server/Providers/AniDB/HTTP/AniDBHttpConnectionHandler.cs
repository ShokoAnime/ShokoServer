using System;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Shoko.Server.Commands;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class AniDBHttpConnectionHandler : ConnectionHandler, IHttpConnectionHandler
    {
        IServiceProvider IHttpConnectionHandler.ServiceProvider => ServiceProvider;
        public override int BanTimerResetLength => 12;

        public override string Type => "HTTP";
        public override UpdateType BanEnum => UpdateType.HTTPBan;

        public AniDBHttpConnectionHandler(IServiceProvider provider, CommandProcessor queue, HttpRateLimiter rateLimiter) : base(provider, queue, rateLimiter) { }

        public HttpBaseResponse<string> GetHttp(string url)
        {
            var response = GetHttpDirectly(url);

            return response;
        }

        public HttpBaseResponse<string> GetHttpDirectly(string url)
        {
            try
            {
                if (IsBanned) throw new AniDBBannedException { BanType = UpdateType.HTTPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength) };
                RateLimiter.EnsureRate();

                var webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Timeout = 20000; // 20 seconds
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using var webResponse = (HttpWebResponse) webReq.GetResponse();
                if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.ContentLength == 0)
                    throw new EndOfStreamException("Response Body was expected, but none returned");

                using var responseStream = webResponse.GetResponseStream();
                if (responseStream == null)
                    throw new EndOfStreamException("Response Body was expected, but none returned");

                var charset = webResponse.CharacterSet;
                Encoding encoding = null;
                if (!string.IsNullOrEmpty(charset))
                    encoding = Encoding.GetEncoding(charset);
                encoding ??= Encoding.UTF8;
                var reader = new StreamReader(responseStream, encoding);

                var output = reader.ReadToEnd();

                if (CheckForBan(output)) throw new AniDBBannedException { BanType = UpdateType.HTTPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength) };
                return new HttpBaseResponse<string> {Response = output, Code = webResponse.StatusCode};
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return null;
            }
        }

        private bool CheckForBan(string xmlResult)
        {
            if (string.IsNullOrEmpty(xmlResult)) return false;
            var index = xmlResult.IndexOf(@">banned<", StringComparison.InvariantCultureIgnoreCase);
            if (index <= -1) return false;
            IsBanned = true;
            return true;
        }
    }
}
