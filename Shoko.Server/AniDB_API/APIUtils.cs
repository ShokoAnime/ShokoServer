using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using NLog;
using Shoko.Server;
using Shoko.Server.AniDB_API;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace AniDBAPI
{
    public static class APIUtils
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public const int LastYear = 2050;

        public static string DownloadWebPage(string url)
        {
            try
            {
                AniDBRateLimiter.UDP.EnsureRate();

                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Timeout = 20000; // 20 seconds
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (HttpWebResponse webResponse = (HttpWebResponse) webReq.GetResponse())
                {
                    if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.ContentLength == 0)
                        throw new Exception("Response Body was expected, but none returned");
                    using (Stream responseStream = webResponse.GetResponseStream())
                    {
                        if (responseStream == null)
                            throw new Exception("Response Body was expected, but none returned");
                        string charset = webResponse.CharacterSet;
                        Encoding encoding = null;
                        if (!string.IsNullOrEmpty(charset))
                            encoding = Encoding.GetEncoding(charset);
                        if (encoding == null)
                            encoding = Encoding.UTF8;
                        StreamReader reader = new StreamReader(responseStream, encoding);

                        string output = reader.ReadToEnd();
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in APIUtils.DownloadWebPage: {0}", ex);
                return string.Empty;
            }
        }

        public static Stream DownloadWebBinary(string url)
        {
            try
            {
                AniDBRateLimiter.UDP.EnsureRate();

                HttpWebResponse response = null;
                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(url);
                // Note: some network proxies require the useragent string to be set or they will deny the http request
                // this is true for instance for EVERY thailand internet connection (also needs to be set for banners/episodethumbs and any other http request we send)
                webReq.UserAgent = "Anime2MP";
                webReq.Timeout = 20000; // 20 seconds
                response = (HttpWebResponse) webReq.GetResponse();

                return response != null 
                    ? response.GetResponseStream() 
                    : null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in APIUtils.DownloadWebBinary: {0}");
                return null;
            }
        }

        public static XmlDocument LoadAnimeHTTPFromFile(int animeID)
        {
            string filePath = ServerSettings.Instance.AnimeXmlDirectory;
            string fileName = $"AnimeDoc_{animeID}.xml";
            string fileNameWithPath = Path.Combine(filePath, fileName);

            logger.Trace($"Trying to load anime XML from cache: {fileNameWithPath}");
            if (!Directory.Exists(filePath))
            {
                logger.Trace($"XML cache diretory does not exist. Trying to create it: {filePath}");
                Directory.CreateDirectory(filePath);
            }

            if (!File.Exists(fileNameWithPath))
            {
                logger.Trace($"XML file {fileNameWithPath} does not exist. exiting");
                return null;
            }
            using (StreamReader re = File.OpenText(fileNameWithPath))
            {
                logger.Trace($"File exists. Loading anime XML from cache: {fileNameWithPath}");
                string rawXML = re.ReadToEnd();

                var docAnime = new XmlDocument();
                docAnime.LoadXml(rawXML);
                return docAnime;
            }
        }

        public static void WriteAnimeHTTPToFile(int animeID, string xml)
        {
            try
            {
                string filePath = ServerSettings.Instance.AnimeXmlDirectory;
                string fileName = $"AnimeDoc_{animeID}.xml";
                string fileNameWithPath = Path.Combine(filePath, fileName);

                logger.Trace($"Writing anime XML to cache: {fileNameWithPath}");
                if (!Directory.Exists(filePath))
                {
                    logger.Trace($"XML cache diretory does not exist. Trying to create it: {filePath}");
                    Directory.CreateDirectory(filePath);
                }

                // First check to make sure we not rights issue
                if (!Utils.IsDirectoryWritable(filePath))
                {
                    logger.Trace($"Unable to access {fileNameWithPath}. Insufficient permissions. Attemping to grant.");
                    return;
                }

                // Check again and only if write-able we create it
                logger.Trace($"Can write to {filePath}. Writing xml file {fileNameWithPath}");
                using (var sw = File.CreateText(fileNameWithPath))
                {
                    sw.Write(xml);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error occurred during WriteAnimeHTTPToFile(): {ex}");
            }
        }
    }
}