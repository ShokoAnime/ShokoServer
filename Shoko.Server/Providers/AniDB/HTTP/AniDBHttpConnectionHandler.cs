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
        public override int BanTimerResetLength => 12;

        public override string Type => "HTTP";
        public override UpdateType BanEnum => UpdateType.HTTPBan;

        public AniDBHttpConnectionHandler(ILogger<AniDBHttpConnectionHandler> logger, CommandProcessor queue) : base(logger, queue) { }

        public HttpBaseResponse<string> GetHttp(string url)
        {
            try
            {
                AniDBRateLimiter.UDP.EnsureRate();

                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Timeout = 20000; // 20 seconds
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using HttpWebResponse webResponse = (HttpWebResponse) webReq.GetResponse();
                if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.ContentLength == 0)
                    throw new EndOfStreamException("Response Body was expected, but none returned");
                
                using Stream responseStream = webResponse.GetResponseStream();
                if (responseStream == null)
                    throw new EndOfStreamException("Response Body was expected, but none returned");

                string charset = webResponse.CharacterSet;
                Encoding encoding = null;
                if (!string.IsNullOrEmpty(charset))
                    encoding = Encoding.GetEncoding(charset);
                if (encoding == null)
                    encoding = Encoding.UTF8;
                StreamReader reader = new StreamReader(responseStream, encoding);

                string output = reader.ReadToEnd();

                if (CheckForBan(output)) return null;
                return new HttpBaseResponse<string> {Response = output, Code = webResponse.StatusCode};
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return null;
            }
        }

        private bool CheckForBan(string xmlresult)
        {
            if (string.IsNullOrEmpty(xmlresult)) return false;
            var index = xmlresult.IndexOf(@">banned<", StringComparison.InvariantCultureIgnoreCase);
            if (-1 >= index) return false;
            IsBanned = true;
            return true;
        }
    }
}
