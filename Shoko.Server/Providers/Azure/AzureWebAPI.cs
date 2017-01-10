using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using NLog;
using Shoko.Commons.Utils;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Entities;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.Azure
{
    public class AzureWebAPI
    {
        private static readonly string azureHostBaseAddress = "jmm.azurewebsites.net";
        //private static readonly string azureHostBaseAddress = "localhost:50994";

        private static Logger logger = LogManager.GetCurrentClassLogger();

        #region TvDB

        public static void Delete_CrossRefAniDBTvDB(int animeID, int aniDBStartEpisodeType, int aniDBStartEpisodeNumber,
            int tvDBID,
            int tvDBSeasonNumber, int tvDBStartEpisodeNumber)
        {
            // id = animeid
            // p = username
            // p2 = AniDBStartEpisodeType
            // p3 = AniDBStartEpisodeNumber
            // p4 = TvDBID
            // p5 = TvDBSeasonNumber
            // p6 = TvDBStartEpisodeNumber
            // p7 = auth key

            //localhost:50994
            //jmm.azurewebsites.net
            string uri = string.Format(
                @"http://{0}/api/CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}&p3={4}&p4={5}&p5={6}&p6={7}&p7={8}",
                azureHostBaseAddress,
                animeID, ServerSettings.AniDB_Username, aniDBStartEpisodeType, aniDBStartEpisodeNumber, tvDBID,
                tvDBSeasonNumber,
                tvDBStartEpisodeNumber, ServerSettings.WebCacheAuthKey);


            string json = DeleteDataJson(uri);
        }

        public static void Send_CrossRefAniDBTvDB(SVR_CrossRef_AniDB_TvDBV2 data, string animeName)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_TvDB", azureHostBaseAddress);

            Azure_CrossRef_AniDB_TvDB_Request input = data.ToRequest(animeName);
            string json = JSONHelper.Serialize<Azure_CrossRef_AniDB_TvDB_Request>(input);
            SendData(uri, json, "POST");
        }

        public static List<Azure_CrossRef_AniDB_TvDB> Get_CrossRefAniDBTvDB(int animeID)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;


            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_TvDB/{1}?p={2}", azureHostBaseAddress, animeID,
                username);
            string msg = string.Format("Getting AniDB/TvDB Cross Ref From Cache: {0}", animeID);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/TvDB Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<Azure_CrossRef_AniDB_TvDB> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_AniDB_TvDB>>(json);

            return xrefs;
        }

        #endregion

        #region Trakt

        public static List<Azure_CrossRef_AniDB_Trakt> Get_CrossRefAniDBTrakt(int animeID)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Trakt/{1}?p={2}", azureHostBaseAddress, animeID,
                username);
            string msg = string.Format("Getting AniDB/Trakt Cross Ref From Cache: {0}", animeID);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/Trakt Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<Azure_CrossRef_AniDB_Trakt> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_AniDB_Trakt>>(json);

            return xrefs;
        }

        public static void Send_CrossRefAniDBTrakt(SVR_CrossRef_AniDB_TraktV2 data, string animeName)
        {
            if (!ServerSettings.WebCache_Trakt_Send) return;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Trakt", azureHostBaseAddress);

            Azure_CrossRef_AniDB_Trakt_Request input = data.ToRequest(animeName);
            string json = JSONHelper.Serialize<Azure_CrossRef_AniDB_Trakt_Request>(input);
            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefAniDBTrakt(int animeID, int aniDBStartEpisodeType, int aniDBStartEpisodeNumber,
            string traktID,
            int traktSeasonNumber, int traktStartEpisodeNumber)
        {
            // id = animeid
            // p = username
            // p2 = AniDBStartEpisodeType
            // p3 = AniDBStartEpisodeNumber
            // p4 = traktID
            // p5 = traktSeasonNumber
            // p6 = traktStartEpisodeNumber
            // p7 = auth key

            if (!ServerSettings.WebCache_Trakt_Send) return;

            //localhost:50994
            //jmm.azurewebsites.net
            string uri = string.Format(
                @"http://{0}/api/CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}&p3={4}&p4={5}&p5={6}&p6={7}&p7={8}",
                azureHostBaseAddress,
                animeID, ServerSettings.AniDB_Username, aniDBStartEpisodeType, aniDBStartEpisodeNumber, traktID,
                traktSeasonNumber,
                traktStartEpisodeNumber, ServerSettings.WebCacheAuthKey);


            string json = DeleteDataJson(uri);
        }

        #endregion

        #region MAL

        public static void Send_CrossRefAniDBMAL(CrossRef_AniDB_MAL data)
        {
            if (!ServerSettings.WebCache_MAL_Send) return;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_MAL", azureHostBaseAddress);

            Azure_CrossRef_AniDB_MAL_Request input = data.CloneToRequest();
            input.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                input.Username = Constants.AnonWebCacheUsername;
            string json = JSONHelper.Serialize<Azure_CrossRef_AniDB_MAL_Request>(input);

            SendData(uri, json, "POST");
        }

        public static Azure_CrossRef_AniDB_MAL Get_CrossRefAniDBMAL(int animeID)
        {
            if (!ServerSettings.WebCache_MAL_Get) return null;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_MAL/{1}?p={2}", azureHostBaseAddress, animeID,
                username);
            string msg = string.Format("Getting AniDB/MAL Cross Ref From Cache: {0}", animeID);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/MAL Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            Azure_CrossRef_AniDB_MAL xref = JSONHelper.Deserialize<Azure_CrossRef_AniDB_MAL>(json);
            xref.Self = string.Format(CultureInfo.CurrentCulture, "api/crossRef_anidb_mal/{0}", xref.CrossRef_AniDB_MALID);
            return xref;
        }

        public static void Delete_CrossRefAniDBMAL(int animeID, int epType, int epNumber)
        {
            // id = animeid
            // p = username
            // p2 = AniDBStartEpisodeType
            // p3 = AniDBStartEpisodeNumber

            if (!ServerSettings.WebCache_MAL_Send) return;

            //localhost:50994
            //jmm.azurewebsites.net
            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_MAL/{1}?p={2}&p2={3}&p3={4}",
                azureHostBaseAddress,
                animeID, ServerSettings.AniDB_Username, epType, epNumber);


            string json = DeleteDataJson(uri);
        }

        #endregion

        #region Cross Ref Other

        public static Azure_CrossRef_AniDB_Other Get_CrossRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            if (!ServerSettings.WebCache_TvDB_Get) return null;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Other/{1}?p={2}&p2={3}", azureHostBaseAddress,
                animeID,
                username, (int) xrefType);
            string msg = string.Format("Getting AniDB/Other Cross Ref From Cache: {0}", animeID);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/MAL Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            Azure_CrossRef_AniDB_Other xref = JSONHelper.Deserialize<Azure_CrossRef_AniDB_Other>(json);

            return xref;
        }

        public static void Send_CrossRefAniDBOther(SVR_CrossRef_AniDB_Other data)
        {
            if (!ServerSettings.WebCache_TvDB_Send) return;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Other", azureHostBaseAddress);

            Azure_CrossRef_AniDB_Other_Request input = data.CloneToRequest();
            string json = JSONHelper.Serialize<Azure_CrossRef_AniDB_Other_Request>(input);

            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            // id = animeid
            // p = username
            // p2 = AniDBStartEpisodeType
            // p3 = AniDBStartEpisodeNumber

            if (!ServerSettings.WebCache_TvDB_Send) return;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Other/{1}?p={2}&p2={3}", azureHostBaseAddress,
                animeID,
                username, (int) xrefType);


            string json = DeleteDataJson(uri);
        }

        #endregion

        #region Cross Ref File Episode

        /*public static List<CrossRef_File_Episode> Get_CrossRefFileEpisode()
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Get) return null;

            //string username = ServerSettings.AniDB_Username;
            //if (ServerSettings.WebCache_Anonymous)
            //    username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_File_Episode/{1}?p={2}", azureHostBaseAddress, "88D29145F18DCEA4D4C41EF94B950378", "Ilast");
            string msg = string.Format("Getting File/Episode Cross Ref From Cache: {0}", "88D29145F18DCEA4D4C41EF94B950378");

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got File/Episode Cross Ref From Cache: {0} - {1}", "88D29145F18DCEA4D4C41EF94B950378", ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<CrossRef_File_Episode> xrefs = JSONHelper.Deserialize<List<CrossRef_File_Episode>>(json);

            return xrefs;
        }*/

        public static List<Azure_CrossRef_File_Episode> Get_CrossRefFileEpisode(SVR_VideoLocal vid)
        {
            if (!ServerSettings.WebCache_XRefFileEpisode_Get) return null;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_File_Episode/{1}?p={2}", azureHostBaseAddress, vid.Hash,
                username);
            string msg = string.Format("Getting File/Episode Cross Ref From Cache: {0}", vid.Hash);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got File/Episode Cross Ref From Cache: {0} - {1}", vid.Hash, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<Azure_CrossRef_File_Episode> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_File_Episode>>(json);

            return xrefs;
        }

        public static void Send_CrossRefFileEpisode(SVR_CrossRef_File_Episode data)
        {
            if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/CrossRef_File_Episode", azureHostBaseAddress);

            Azure_CrossRef_File_Episode_Request input = data.ToRequest();
            string json = JSONHelper.Serialize<Azure_CrossRef_File_Episode_Request>(input);

            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefFileEpisode(string hash)
        {
            if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/CrossRef_File_Episode/{1}?p={2}", azureHostBaseAddress, hash,
                username);


            string json = DeleteDataJson(uri);
        }

        #endregion

        #region Anime

        public static string Get_AnimeXML(int animeID)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/animexml/{1}", azureHostBaseAddress, animeID);

            DateTime start = DateTime.Now;
            string msg = string.Format("Getting Anime XML Data From Cache: {0}", animeID);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string xml = GetDataXML(uri);

            // remove the string container
            int iStart = xml.IndexOf("<?xml");
            if (iStart > 0)
            {
                string end = "</string>";
                int iEnd = xml.IndexOf(end);
                if (iEnd > 0)
                {
                    xml = xml.Substring(iStart, iEnd - iStart - 1);
                }
            }

            TimeSpan ts = DateTime.Now - start;
            string content = xml;
            if (content.Length > 100) content = content.Substring(0, 100);
            msg = string.Format("Got Anime XML Data From Cache: {0} - {1} - {2}", animeID, ts.TotalMilliseconds, content);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            return xml;
        }

        public static void Send_AnimeFull(SVR_AniDB_Anime data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/animefull", azureHostBaseAddress);
            Azure_AnimeFull obj = data.ToContractAzure();
            string json = JSONHelper.Serialize<Azure_AnimeFull>(obj);
            SendData(uri, json, "POST");
        }

        public static void Send_AnimeXML(Azure_AnimeXML data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/animexml", azureHostBaseAddress);
            string json = JSONHelper.Serialize<Azure_AnimeXML>(data);
            SendData(uri, json, "POST");
        }

        #endregion

        #region Anime Titles

        public static void Send_AnimeTitle(Azure_AnimeIDTitle data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/animeidtitle", azureHostBaseAddress);
            string json = JSONHelper.Serialize<Azure_AnimeIDTitle>(data);
            SendData(uri, json, "POST");
        }

        public static List<Azure_AnimeIDTitle> Get_AnimeTitle(string query)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
            string uri = string.Format(@"http://{0}/api/animeidtitle/{1}", azureHostBaseAddress, query);
            string msg = string.Format("Getting Anime Title Data From Cache: {0}", query);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got Anime Title Data From Cache: {0} - {1}", query, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<Azure_AnimeIDTitle> titles = JSONHelper.Deserialize<List<Azure_AnimeIDTitle>>(json);

            return titles;
        }

        #endregion

        #region Admin Messages

        public static List<Azure_AdminMessage> Get_AdminMessages()
        {
            try
            {
                string uri = string.Format(@"http://{0}/api/AdminMessage/{1}", azureHostBaseAddress, "all");
                string json = GetDataJson(uri);

                List<Azure_AdminMessage> msgs = JSONHelper.Deserialize<List<Azure_AdminMessage>>(json);

                return msgs;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error(2) in XMLServiceQueue.SendData: {0}");
            }

            return null;
        }

        #endregion

        #region User Info

        public static void Send_UserInfo()
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            Azure_UserInfo uinfo = GetUserInfoData();
            if (uinfo == null) return;

            string uri = string.Format(@"http://{0}/api/userinfo", azureHostBaseAddress);
            string json = JSONHelper.Serialize<Azure_UserInfo>(uinfo);
            SendData(uri, json, "POST");
        }

        #endregion

        #region Helpers

        private static string SendData(string uri, string json, string verb)
        {
            string ret = string.Empty;
            WebRequest req = null;
            WebResponse rsp = null;
            try
            {
                DateTime start = DateTime.Now;

                req = WebRequest.Create(uri);
                //req.Method = "POST";        // Post method
                req.Method = verb; // Post method, or PUT
                req.ContentType = "application/json; charset=UTF-8"; // content type
                req.Proxy = null;

                // Wrap the request stream with a text-based writer
                Encoding encoding = null;
                encoding = Encoding.UTF8;

                StreamWriter writer = new StreamWriter(req.GetRequestStream(), encoding);
                // Write the XML text into the stream
                writer.WriteLine(json);
                writer.Close();
                // Send the data to the webserver
                rsp = req.GetResponse();

                TimeSpan ts = DateTime.Now - start;
                logger.Trace("Sent Web Cache Update in {0} ms: {1}", ts.TotalMilliseconds, uri);
            }
            catch (WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = webEx.Response as HttpWebResponse;
                    if (response != null)
                    {
						if (!uri.Contains("Admin") || (int)response.StatusCode != 400)
							logger.Error("HTTP Status Code: " + (int) response.StatusCode);
                        ret = response.StatusCode.ToString();
                    }
                    else
                    {
                        // no http status code available
                    }
                }
				if(!uri.Contains("Admin"))
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

            return ret;
        }

        private static string GetDataJson(string uri)
        {
            try
            {
                DateTime start = DateTime.Now;

                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(uri);
                webReq.Timeout = 60000; // 60 seconds
                webReq.Proxy = null;
                webReq.Method = "GET";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "application/json; charset=UTF-8"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                HttpWebResponse WebResponse = (HttpWebResponse) webReq.GetResponse();

                Stream responseStream = WebResponse.GetResponseStream();
                Encoding encoding = Encoding.UTF8;
                StreamReader Reader = new StreamReader(responseStream, encoding);

                string output = Reader.ReadToEnd();
                output = HttpUtility.HtmlDecode(output);


                WebResponse.Close();
                responseStream.Close();

                return output;
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error(2) in AzureWebAPI.GetData: {0}");
            }

            return "";
        }

        private static string DeleteDataJson(string uri)
        {
            try
            {
                DateTime start = DateTime.Now;

                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(uri);
                webReq.Timeout = 60000; // 60 seconds
                webReq.Proxy = null;
                webReq.Method = "DELETE";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "application/json; charset=UTF-8"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                HttpWebResponse WebResponse = (HttpWebResponse) webReq.GetResponse();

                Stream responseStream = WebResponse.GetResponseStream();
                Encoding encoding = Encoding.UTF8;
                StreamReader Reader = new StreamReader(responseStream, encoding);

                string output = Reader.ReadToEnd();
                output = HttpUtility.HtmlDecode(output);


                WebResponse.Close();
                responseStream.Close();

                return output;
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error(2) in AzureWebAPI.GetData: {0}");
            }

            return "";
        }

        private static string GetDataXML(string uri)
        {
            try
            {
                DateTime start = DateTime.Now;

                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(uri);
                webReq.Timeout = 60000; // 60 seconds
                webReq.Proxy = null;
                webReq.Method = "GET";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "text/xml"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                HttpWebResponse WebResponse = (HttpWebResponse) webReq.GetResponse();

                Stream responseStream = WebResponse.GetResponseStream();
                String enco = WebResponse.CharacterSet;
                Encoding encoding = null;
                if (!String.IsNullOrEmpty(enco))
                    encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
                if (encoding == null)
                    encoding = Encoding.Default;
                StreamReader Reader = new StreamReader(responseStream, encoding);

                string output = Reader.ReadToEnd();
                output = HttpUtility.HtmlDecode(output);


                WebResponse.Close();
                responseStream.Close();

                return output;
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error(2) in AzureWebAPI.GetData: {0}");
            }

            return "";
        }

        public static Azure_UserInfo GetUserInfoData(string dashType = "", string vidPlayer = "")
        {
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.AniDB_Username)) return null;

                Azure_UserInfo uinfo = new Azure_UserInfo();

                uinfo.DateTimeUpdated = DateTime.Now;
                uinfo.DateTimeUpdatedUTC = 0;

                // Optional JMM Desktop data
                uinfo.DashboardType = null;
                uinfo.VideoPlayer = vidPlayer;

                System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
                try
                {
                    if (a != null) uinfo.JMMServerVersion = Utils.GetApplicationVersion(a);
                }
                catch
                {
                }

                uinfo.UsernameHash = Utils.GetMd5Hash(ServerSettings.AniDB_Username);
                uinfo.DatabaseType = ServerSettings.DatabaseType;
                uinfo.WindowsVersion = Utils.GetOSInfo();
                uinfo.TraktEnabled = ServerSettings.Trakt_IsEnabled ? 1 : 0;
                uinfo.MALEnabled = string.IsNullOrEmpty(ServerSettings.MAL_Username) ? 0 : 1;

                uinfo.CountryLocation = "";

                // this field is not actually used
                uinfo.LastEpisodeWatchedAsDate = DateTime.Now.AddDays(-5);

                uinfo.LocalUserCount = (int)RepoFactory.JMMUser.GetTotalRecordCount();

                uinfo.FileCount = RepoFactory.VideoLocal.GetTotalRecordCount();

                SVR_AnimeEpisode_User rec = RepoFactory.AnimeEpisode_User.GetLastWatchedEpisode();
                uinfo.LastEpisodeWatched = 0;
                if (rec != null)
                    uinfo.LastEpisodeWatched = AniDB.GetAniDBDateAsSeconds(rec.WatchedDate);

                return uinfo;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        #endregion

        #region Admin - General

        public static string Admin_AuthUser()
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin/{1}?p={2}", azureHostBaseAddress, username,
                ServerSettings.WebCacheAuthKey);
            //string uri = string.Format(@"http://{0}/api/Admin/{1}?p={2}", azureHostBaseAddress, username, "");
            string json = string.Empty;

            return SendData(uri, json, "POST");
        }

        #endregion

        #region Admin - TvDB

        public static List<Azure_CrossRef_AniDB_TvDB> Admin_Get_CrossRefAniDBTvDB(int animeID)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;


            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}",
                azureHostBaseAddress,
                animeID, username, ServerSettings.WebCacheAuthKey);
            string msg = string.Format("Getting AniDB/TvDB Cross Ref From Cache: {0}", animeID);

            string json = GetDataJson(uri);

            List<Azure_CrossRef_AniDB_TvDB> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_AniDB_TvDB>>(json);

            return xrefs;
        }

        public static string Admin_Approve_CrossRefAniDBTvDB(int crossRef_AniDB_TvDBId)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}",
                azureHostBaseAddress,
                crossRef_AniDB_TvDBId, username, ServerSettings.WebCacheAuthKey);
            string json = string.Empty;

            return SendData(uri, json, "POST");
        }

        public static string Admin_Revoke_CrossRefAniDBTvDB(int crossRef_AniDB_TvDBId)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}",
                azureHostBaseAddress,
                crossRef_AniDB_TvDBId, username, ServerSettings.WebCacheAuthKey);
            string json = string.Empty;

            return SendData(uri, json, "PUT");
        }

        public static Azure_AnimeLink Admin_GetRandomTvDBLinkForApproval()
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}&p3=dummy",
                azureHostBaseAddress, (int) AzureLinkType.TvDB, username, ServerSettings.WebCacheAuthKey);
            string json = GetDataJson(uri);

            return JSONHelper.Deserialize<Azure_AnimeLink>(json);
        }

        #endregion

        #region Admin - Trakt

        public static List<Azure_CrossRef_AniDB_Trakt> Admin_Get_CrossRefAniDBTrakt(int animeID)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;


            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}",
                azureHostBaseAddress,
                animeID, username, ServerSettings.WebCacheAuthKey);
            string msg = string.Format("Getting AniDB/Trakt Cross Ref From Cache: {0}", animeID);

            string json = GetDataJson(uri);

            List<Azure_CrossRef_AniDB_Trakt> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_AniDB_Trakt>>(json);

            return xrefs;
        }

        public static string Admin_Approve_CrossRefAniDBTrakt(int crossRef_AniDB_TraktId)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}",
                azureHostBaseAddress,
                crossRef_AniDB_TraktId, username, ServerSettings.WebCacheAuthKey);
            string json = string.Empty;

            return SendData(uri, json, "POST");
        }

        public static string Admin_Revoke_CrossRefAniDBTrakt(int crossRef_AniDB_TraktId)
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}",
                azureHostBaseAddress,
                crossRef_AniDB_TraktId, username, ServerSettings.WebCacheAuthKey);
            string json = string.Empty;

            return SendData(uri, json, "PUT");
        }

        public static Azure_AnimeLink Admin_GetRandomTraktLinkForApproval()
        {
            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            string uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}&p3=dummy",
                azureHostBaseAddress, (int) AzureLinkType.Trakt, username, ServerSettings.WebCacheAuthKey);
            string json = GetDataJson(uri);

            return JSONHelper.Deserialize<Azure_AnimeLink>(json);
        }

        #endregion

        #region File Hashes


        public static void Send_FileHash(List<SVR_AniDB_File> aniFiles)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/FileHash", azureHostBaseAddress);

            List<Azure_FileHash_Request> inputs = new List<Azure_FileHash_Request>();
            // send a max of 25 at a time
            foreach (SVR_AniDB_File aniFile in aniFiles)
            {
                Azure_FileHash_Request input = aniFile.ToHashRequest();
                if (inputs.Count < 25)
                    inputs.Add(input);
                else
                {
                    string json = JSONHelper.Serialize<List<Azure_FileHash_Request>>(inputs);
                    SendData(uri, json, "POST");
                    inputs.Clear();
                }
            }

            if (inputs.Count > 0)
            {
                string json = JSONHelper.Serialize<List<Azure_FileHash_Request>>(inputs);
                SendData(uri, json, "POST");
            }

        }
        public static void Send_FileHash(List<SVR_VideoLocal> locals)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/FileHash", azureHostBaseAddress);

            List<Azure_FileHash_Request> inputs = new List<Azure_FileHash_Request>();
            // send a max of 25 at a time
            foreach (SVR_VideoLocal v in locals)
            {
                Azure_FileHash_Request input = v.ToHashRequest();
                if (inputs.Count < 25)
                    inputs.Add(input);
                else
                {
                    string json = JSONHelper.Serialize<List<Azure_FileHash_Request>>(inputs);
                    SendData(uri, json, "POST");
                    inputs.Clear();
                }
            }

            if (inputs.Count > 0)
            {
                string json = JSONHelper.Serialize<List<Azure_FileHash_Request>>(inputs);
                SendData(uri, json, "POST");
            }

        }
        /// <summary>
        /// Get File hash details from the web cache
        /// When the hash type is a CRC, the hash details value should be a combination of the CRC and the FileSize with an under score in between
        /// e.g. CRC32 = 8b4b52f4, File Size = 380580947.......hashDetails = 8b4b52f4_380580947
        /// </summary>
        /// <param name="hashType"></param>
        /// <param name="hashDetails"></param>
        /// <returns></returns>
        public static List<Azure_FileHash> Get_FileHash(FileHashType hashType, string hashDetails)
        {
            
            string uri = string.Format(@"http://{0}/api/FileHash/{1}?p={2}", azureHostBaseAddress, (int)hashType,
                hashDetails);
            string msg = string.Format("Getting File Hash From Cache: {0} - {1}", hashType, hashDetails);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Got File Hash From Cache: {0} - {1}", hashDetails, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<Azure_FileHash> hashes = JsonConvert.DeserializeObject<List<Azure_FileHash>>(json) ?? new List<Azure_FileHash>();
            return hashes;
        }

        #endregion

        #region Media

        public static void Send_Media(List<SVR_VideoLocal> locals)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/Media", azureHostBaseAddress);

            List<Azure_Media_Request> inputs = new List<Azure_Media_Request>();
            // send a max of 25 at a time
            // send a max of 25 at a time
            foreach (SVR_VideoLocal v in locals.Where(a=>a.MediaBlob!=null && a.MediaBlob.Length>0 && a.MediaVersion==SVR_VideoLocal.MEDIA_VERSION && !string.IsNullOrEmpty(a.ED2KHash)))
            {
                Azure_Media_Request input = v.ToMediaRequest();
                if (inputs.Count < 25)
                    inputs.Add(input);
                else
                {
                    string json = JsonConvert.SerializeObject(inputs);
                    //json = Newtonsoft.Json.JsonConvert.SerializeObject(inputs);
                    SendData(uri, json, "POST");
                    inputs.Clear();
                }
            }

            if (inputs.Count > 0)
            {
                string json = JsonConvert.SerializeObject(inputs);
                SendData(uri, json, "POST");
            }

        }
        public static void Send_Media(string ed2k, Shoko.Models.PlexAndKodi.Media media)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = string.Format(@"http://{0}/api/Media", azureHostBaseAddress);

            List<Azure_Media_Request> inputs = new List<Azure_Media_Request>();
            Azure_Media_Request input = media.ToMediaRequest(ed2k);
            inputs.Add(input);
            string json = JsonConvert.SerializeObject(inputs);
            SendData(uri, json, "POST");

        }
        public static List<Azure_Media> Get_Media(string ed2k)
        {

            string uri = string.Format(@"http://{0}/api/Media/{1}/{2}", azureHostBaseAddress, ed2k,SVR_VideoLocal.MEDIA_VERSION);
            string msg = string.Format("Getting Media Info From Cache for ED2K: {0} Version : {1}", ed2k,SVR_VideoLocal.MEDIA_VERSION);

            DateTime start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            string json = GetDataJson(uri);

            TimeSpan ts = DateTime.Now - start;
            msg = string.Format("Getting Media Info From Cache for ED2K: {0} - {1}", ed2k, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            List<Azure_Media> medias = JsonConvert.DeserializeObject<List<Azure_Media>>(json) ?? new List<Azure_Media>();
            
            return medias;
        }
        #endregion
    }


}