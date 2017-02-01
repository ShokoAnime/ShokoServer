using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Commons.Utils
{
    public class Misc
    {
        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        private static extern long StrFormatByteSize(long fileSize,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        public static string FormatByteSize(long fileSize)
        {
            StringBuilder sbBuffer = new StringBuilder(20);
            StrFormatByteSize(fileSize, sbBuffer, 20);
            return sbBuffer.ToString();
        }

        public static string DownloadWebPage(string url)
        {
            return DownloadWebPage(url, null, false);
        }

        public static string DownloadWebPage(string url, string cookieHeader, bool setUserAgent)
        {
            try
            {
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
                webReq.Timeout = 30000; // 30 seconds
                webReq.Proxy = null;
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

                if (!String.IsNullOrEmpty(cookieHeader))
                    webReq.Headers.Add("Cookie", cookieHeader);
                if (setUserAgent)
                    webReq.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();

                Stream responseStream = WebResponse.GetResponseStream();
                String enco = WebResponse.CharacterSet;
                Encoding encoding = null;
                if (!String.IsNullOrEmpty(enco))
                    encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
                if (encoding == null)
                    encoding = Encoding.Default;
                StreamReader Reader = new StreamReader(responseStream, encoding);

                string output = Reader.ReadToEnd();

                WebResponse.Close();
                responseStream.Close();

                //logger.Trace("DownloadWebPage Response: {0}", output);

                return output;
            }
            catch (Exception ex)
            {
                string msg = "---------- ERROR IN DOWNLOAD WEB PAGE ---------" + Environment.NewLine +
                             url + Environment.NewLine +
                             ex.ToString() + Environment.NewLine + "------------------------------------";

                // if the error is a 404 error it may mean that there is a bad series association
                // so lets log it to the web cache so we can investigate
                if (ex.ToString().Contains("(404) Not Found"))
                {
                }

                return "";
            }
        }

        public static void DownloadFile(string url, string destFile, string cookieHeader, bool setUserAgent)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    if (!String.IsNullOrEmpty(cookieHeader))
                        client.Headers.Add("Cookie", cookieHeader);
                    if (setUserAgent)
                        client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");

                    client.DownloadFile(url, destFile);
                }

            }
            catch (Exception ex)
            {
                string msg = "---------- ERROR IN DOWNLOAD WEB PAGE ---------" + Environment.NewLine +
                             url + Environment.NewLine +
                             ex.ToString() + Environment.NewLine + "------------------------------------";

                // if the error is a 404 error it may mean that there is a bad series association
                // so lets log it to the web cache so we can investigate
                if (ex.ToString().Contains("(404) Not Found"))
                {
                }
            }
        }
    }
}
