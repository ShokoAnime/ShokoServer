using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using NLog;

namespace Shoko.Server.WebCache
{
    public class XMLService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static AppVersionsResult GetAppVersions()
        {
            try
            {
                AppVersionsResult appVersions = null;

                string uri = string.Format("http://{0}/GetAppVersions.aspx", ServerSettings.WebCache_Address);
                string xml = GetData(uri);

                if (xml.Trim().Length == 0) return null;

                XmlSerializer serializer = new XmlSerializer(typeof(AppVersionsResult));
                XmlDocument docSearchResult = new XmlDocument();
                docSearchResult.LoadXml(xml);

                XmlNodeReader reader = new XmlNodeReader(docSearchResult.DocumentElement);
                object obj = serializer.Deserialize(reader);
                appVersions = (AppVersionsResult) obj;

                return appVersions;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in XMLService.GetAppVersions:: {0}");
                return null;
            }
        }

        private static string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
        {
            try
            {
                string prop = doc[keyName][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                logger.Info("---------------------------------------------------------------");
                logger.Info("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
                logger.Info("keyName: {0}, propertyName: {1}", keyName, propertyName);
                logger.Info("---------------------------------------------------------------");
            }

            return "";
        }

        private static string GetData(string uri)
        {
            try
            {
                DateTime start = DateTime.Now;

                logger.Trace("GetData for: {0}", uri.ToString());
                string xml = Utils.DownloadWebPage(uri);
                TimeSpan ts = DateTime.Now - start;
                logger.Trace("GetData returned in {0}: {1} (in {2} ms)", Utils.GetParentMethodName(), xml,
                    ts.TotalMilliseconds);
                if (xml.Contains(Constants.WebCacheError)) return "";

                return xml;
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in XMLService.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error(2) in XMLService.GetData: {0}");
            }

            return "";
        }

        private static void SendData(string uri, string xml)
        {
            WebRequest req = null;
            WebResponse rsp = null;
            try
            {
                DateTime start = DateTime.Now;


                req = WebRequest.Create(uri);
                req.Method = "POST"; // Post method
                req.ContentType = "text/xml"; // content type
                req.Proxy = null;

                // Wrap the request stream with a text-based writer
                StreamWriter writer = new StreamWriter(req.GetRequestStream());
                // Write the XML text into the stream
                writer.WriteLine(xml);
                writer.Close();
                // Send the data to the webserver
                rsp = req.GetResponse();

                TimeSpan ts = DateTime.Now - start;
                logger.Trace("Sent Web Cache Update in {0} ms: {1} --- {2}", ts.TotalMilliseconds, uri, xml);
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in XMLServiceQueue.SendData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error(2) in XMLServiceQueue.SendData: {0}");
            }
            finally
            {
                if (req != null) req.GetRequestStream().Close();
                if (rsp != null) rsp.GetResponseStream().Close();
            }
        }
    }
}