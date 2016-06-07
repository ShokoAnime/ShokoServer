using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Providers.Azure
{
    public class AzureWebAPI
    {
        private static readonly string azureHostBaseAddress = "jmm.azurewebsites.net";
        //private static readonly string azureHostBaseAddress = "localhost:50994";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        #region Admin Messages

        public static List<AdminMessage> Get_AdminMessages()
        {
            try
            {
                var uri = string.Format(@"http://{0}/api/AdminMessage/{1}", azureHostBaseAddress, "all");
                var json = GetDataJson(uri);

                var msgs = JSONHelper.Deserialize<List<AdminMessage>>(json);

                return msgs;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error(2) in XMLServiceQueue.SendData: {0}", ex);
            }

            return null;
        }

        #endregion

        #region User Info

        public static void Send_UserInfo()
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uinfo = GetUserInfoData();
            if (uinfo == null) return;

            var uri = string.Format(@"http://{0}/api/userinfo", azureHostBaseAddress);
            var json = JSONHelper.Serialize(uinfo);
            SendData(uri, json, "POST");
        }

        #endregion

        #region Admin - General

        public static string Admin_AuthUser()
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin/{1}?p={2}", azureHostBaseAddress, username,
                ServerSettings.WebCacheAuthKey);
            //string uri = string.Format(@"http://{0}/api/Admin/{1}?p={2}", azureHostBaseAddress, username, "");
            var json = string.Empty;

            return SendData(uri, json, "POST");
        }

        #endregion

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
            var uri =
                string.Format(
                    @"http://{0}/api/CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}&p3={4}&p4={5}&p5={6}&p6={7}&p7={8}",
                    azureHostBaseAddress,
                    animeID, ServerSettings.AniDB_Username, aniDBStartEpisodeType, aniDBStartEpisodeNumber, tvDBID,
                    tvDBSeasonNumber, tvDBStartEpisodeNumber, ServerSettings.WebCacheAuthKey);


            var json = DeleteDataJson(uri);
        }

        public static void Send_CrossRefAniDBTvDB(CrossRef_AniDB_TvDBV2 data, string animeName)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_TvDB", azureHostBaseAddress);

            var input = new CrossRef_AniDB_TvDBInput(data, animeName);
            var json = JSONHelper.Serialize(input);
            SendData(uri, json, "POST");
        }

        public static List<CrossRef_AniDB_TvDB> Get_CrossRefAniDBTvDB(int animeID)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;


            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_TvDB/{1}?p={2}", azureHostBaseAddress, animeID,
                username);
            var msg = string.Format("Getting AniDB/TvDB Cross Ref From Cache: {0}", animeID);

            var start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var json = GetDataJson(uri);

            var ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/TvDB Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var xrefs = JSONHelper.Deserialize<List<CrossRef_AniDB_TvDB>>(json);

            return xrefs;
        }

        #endregion

        #region Trakt

        public static List<CrossRef_AniDB_Trakt> Get_CrossRefAniDBTrakt(int animeID)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Trakt/{1}?p={2}", azureHostBaseAddress, animeID,
                username);
            var msg = string.Format("Getting AniDB/Trakt Cross Ref From Cache: {0}", animeID);

            var start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var json = GetDataJson(uri);

            var ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/Trakt Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var xrefs = JSONHelper.Deserialize<List<CrossRef_AniDB_Trakt>>(json);

            return xrefs;
        }

        public static void Send_CrossRefAniDBTrakt(CrossRef_AniDB_TraktV2 data, string animeName)
        {
            if (!ServerSettings.WebCache_Trakt_Send) return;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Trakt", azureHostBaseAddress);

            var input = new CrossRef_AniDB_TraktInput(data, animeName);
            var json = JSONHelper.Serialize(input);
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
            var uri =
                string.Format(
                    @"http://{0}/api/CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}&p3={4}&p4={5}&p5={6}&p6={7}&p7={8}",
                    azureHostBaseAddress,
                    animeID, ServerSettings.AniDB_Username, aniDBStartEpisodeType, aniDBStartEpisodeNumber, traktID,
                    traktSeasonNumber, traktStartEpisodeNumber, ServerSettings.WebCacheAuthKey);


            var json = DeleteDataJson(uri);
        }

        #endregion

        #region MAL

        public static void Send_CrossRefAniDBMAL(Entities.CrossRef_AniDB_MAL data)
        {
            if (!ServerSettings.WebCache_MAL_Send) return;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_MAL", azureHostBaseAddress);

            var input = new CrossRef_AniDB_MALInput(data);
            var json = JSONHelper.Serialize(input);

            SendData(uri, json, "POST");
        }

        public static CrossRef_AniDB_MAL Get_CrossRefAniDBMAL(int animeID)
        {
            if (!ServerSettings.WebCache_MAL_Get) return null;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_MAL/{1}?p={2}", azureHostBaseAddress, animeID,
                username);
            var msg = string.Format("Getting AniDB/MAL Cross Ref From Cache: {0}", animeID);

            var start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var json = GetDataJson(uri);

            var ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/MAL Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var xref = JSONHelper.Deserialize<CrossRef_AniDB_MAL>(json);

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
            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_MAL/{1}?p={2}&p2={3}&p3={4}", azureHostBaseAddress,
                animeID, ServerSettings.AniDB_Username, epType, epNumber);


            var json = DeleteDataJson(uri);
        }

        #endregion

        #region Cross Ref Other

        public static CrossRef_AniDB_Other Get_CrossRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            if (!ServerSettings.WebCache_TvDB_Get) return null;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Other/{1}?p={2}&p2={3}", azureHostBaseAddress,
                animeID, username, (int)xrefType);
            var msg = string.Format("Getting AniDB/Other Cross Ref From Cache: {0}", animeID);

            var start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var json = GetDataJson(uri);

            var ts = DateTime.Now - start;
            msg = string.Format("Got AniDB/MAL Cross Ref From Cache: {0} - {1}", animeID, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var xref = JSONHelper.Deserialize<CrossRef_AniDB_Other>(json);

            return xref;
        }

        public static void Send_CrossRefAniDBOther(Entities.CrossRef_AniDB_Other data)
        {
            if (!ServerSettings.WebCache_TvDB_Send) return;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Other", azureHostBaseAddress);

            var input = new CrossRef_AniDB_OtherInput(data);
            var json = JSONHelper.Serialize(input);

            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            // id = animeid
            // p = username
            // p2 = AniDBStartEpisodeType
            // p3 = AniDBStartEpisodeNumber

            if (!ServerSettings.WebCache_TvDB_Send) return;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/CrossRef_AniDB_Other/{1}?p={2}&p2={3}", azureHostBaseAddress,
                animeID, username, (int)xrefType);


            var json = DeleteDataJson(uri);
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

        public static List<CrossRef_File_Episode> Get_CrossRefFileEpisode(VideoLocal vid)
        {
            if (!ServerSettings.WebCache_XRefFileEpisode_Get) return null;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/CrossRef_File_Episode/{1}?p={2}", azureHostBaseAddress, vid.Hash,
                username);
            var msg = string.Format("Getting File/Episode Cross Ref From Cache: {0}", vid.Hash);

            var start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var json = GetDataJson(uri);

            var ts = DateTime.Now - start;
            msg = string.Format("Got File/Episode Cross Ref From Cache: {0} - {1}", vid.Hash, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var xrefs = JSONHelper.Deserialize<List<CrossRef_File_Episode>>(json);

            return xrefs;
        }

        public static void Send_CrossRefFileEpisode(Entities.CrossRef_File_Episode data)
        {
            if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uri = string.Format(@"http://{0}/api/CrossRef_File_Episode", azureHostBaseAddress);

            var input = new CrossRef_File_EpisodeInput(data);
            var json = JSONHelper.Serialize(input);

            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefFileEpisode(string hash)
        {
            if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/CrossRef_File_Episode/{1}?p={2}", azureHostBaseAddress, hash,
                username);


            var json = DeleteDataJson(uri);
        }

        #endregion

        #region Anime

        public static string Get_AnimeXML(int animeID)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uri = string.Format(@"http://{0}/api/animexml/{1}", azureHostBaseAddress, animeID);

            var start = DateTime.Now;
            var msg = string.Format("Getting Anime XML Data From Cache: {0}", animeID);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var xml = GetDataXML(uri);

            // remove the string container
            var iStart = xml.IndexOf("<?xml");
            if (iStart > 0)
            {
                var end = "</string>";
                var iEnd = xml.IndexOf(end);
                if (iEnd > 0)
                {
                    xml = xml.Substring(iStart, iEnd - iStart - 1);
                }
            }

            var ts = DateTime.Now - start;
            var content = xml;
            if (content.Length > 100) content = content.Substring(0, 100);
            msg = string.Format("Got Anime XML Data From Cache: {0} - {1} - {2}", animeID, ts.TotalMilliseconds, content);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            return xml;
        }

        public static void Send_AnimeFull(AniDB_Anime data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uri = string.Format(@"http://{0}/api/animefull", azureHostBaseAddress);
            var obj = data.ToContractAzure();
            var json = JSONHelper.Serialize(obj);
            SendData(uri, json, "POST");
        }

        public static void Send_AnimeXML(AnimeXML data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uri = string.Format(@"http://{0}/api/animexml", azureHostBaseAddress);
            var json = JSONHelper.Serialize(data);
            SendData(uri, json, "POST");
        }

        #endregion

        #region Anime Titles

        public static void Send_AnimeTitle(AnimeIDTitle data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            var uri = string.Format(@"http://{0}/api/animeidtitle", azureHostBaseAddress);
            var json = JSONHelper.Serialize(data);
            SendData(uri, json, "POST");
        }

        public static List<AnimeIDTitle> Get_AnimeTitle(string query)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
            var uri = string.Format(@"http://{0}/api/animeidtitle/{1}", azureHostBaseAddress, query);
            var msg = string.Format("Getting Anime Title Data From Cache: {0}", query);

            var start = DateTime.Now;
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var json = GetDataJson(uri);

            var ts = DateTime.Now - start;
            msg = string.Format("Got Anime Title Data From Cache: {0} - {1}", query, ts.TotalMilliseconds);
            JMMService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

            var titles = JSONHelper.Deserialize<List<AnimeIDTitle>>(json);

            return titles;
        }

        #endregion

        #region Helpers

        private static string SendData(string uri, string json, string verb)
        {
            var ret = string.Empty;
            WebRequest req = null;
            WebResponse rsp = null;
            try
            {
                var start = DateTime.Now;

                req = WebRequest.Create(uri);
                //req.Method = "POST";        // Post method
                req.Method = verb; // Post method, or PUT
                req.ContentType = "application/json; charset=UTF-8"; // content type
                req.Proxy = null;

                // Wrap the request stream with a text-based writer
                Encoding encoding = null;
                encoding = Encoding.UTF8;

                var writer = new StreamWriter(req.GetRequestStream(), encoding);
                // Write the XML text into the stream
                writer.WriteLine(json);
                writer.Close();
                // Send the data to the webserver
                rsp = req.GetResponse();

                var ts = DateTime.Now - start;
                logger.Trace("Sent Web Cache Update in {0} ms: {1}", ts.TotalMilliseconds, uri);
            }
            catch (WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = webEx.Response as HttpWebResponse;
                    if (response != null)
                    {
                        Console.WriteLine("HTTP Status Code: " + (int)response.StatusCode);
                        ret = response.StatusCode.ToString();
                    }
                }

                logger.Error("Error(1) in XMLServiceQueue.SendData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error(2) in XMLServiceQueue.SendData: {0}", ex);
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
                var start = DateTime.Now;

                var webReq = (HttpWebRequest)WebRequest.Create(uri);
                webReq.Timeout = 60000; // 60 seconds
                webReq.Proxy = null;
                webReq.Method = "GET";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "application/json; charset=UTF-8"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                var WebResponse = (HttpWebResponse)webReq.GetResponse();

                var responseStream = WebResponse.GetResponseStream();
                var encoding = Encoding.UTF8;
                var Reader = new StreamReader(responseStream, encoding);

                var output = Reader.ReadToEnd();
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
                logger.ErrorException("Error(2) in AzureWebAPI.GetData: {0}", ex);
            }

            return "";
        }

        private static string DeleteDataJson(string uri)
        {
            try
            {
                var start = DateTime.Now;

                var webReq = (HttpWebRequest)WebRequest.Create(uri);
                webReq.Timeout = 60000; // 60 seconds
                webReq.Proxy = null;
                webReq.Method = "DELETE";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "application/json; charset=UTF-8"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                var WebResponse = (HttpWebResponse)webReq.GetResponse();

                var responseStream = WebResponse.GetResponseStream();
                var encoding = Encoding.UTF8;
                var Reader = new StreamReader(responseStream, encoding);

                var output = Reader.ReadToEnd();
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
                logger.ErrorException("Error(2) in AzureWebAPI.GetData: {0}", ex);
            }

            return "";
        }

        private static string GetDataXML(string uri)
        {
            try
            {
                var start = DateTime.Now;

                var webReq = (HttpWebRequest)WebRequest.Create(uri);
                webReq.Timeout = 60000; // 60 seconds
                webReq.Proxy = null;
                webReq.Method = "GET";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "text/xml"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                var WebResponse = (HttpWebResponse)webReq.GetResponse();

                var responseStream = WebResponse.GetResponseStream();
                var enco = WebResponse.CharacterSet;
                Encoding encoding = null;
                if (!string.IsNullOrEmpty(enco))
                    encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
                if (encoding == null)
                    encoding = Encoding.Default;
                var Reader = new StreamReader(responseStream, encoding);

                var output = Reader.ReadToEnd();
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
                logger.ErrorException("Error(2) in AzureWebAPI.GetData: {0}", ex);
            }

            return "";
        }

        public static UserInfo GetUserInfoData(string dashType = "", string vidPlayer = "")
        {
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.AniDB_Username)) return null;

                var uinfo = new UserInfo();

                uinfo.DateTimeUpdated = DateTime.Now;
                uinfo.DateTimeUpdatedUTC = 0;

                // Optional JMM Desktop data
                uinfo.DashboardType = null;
                uinfo.VideoPlayer = vidPlayer;

                var a = Assembly.GetExecutingAssembly();
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

                var repUsers = new JMMUserRepository();
                uinfo.LocalUserCount = (int)repUsers.GetTotalRecordCount();

                var repVids = new VideoLocalRepository();
                uinfo.FileCount = repVids.GetTotalRecordCount();

                var repEps = new AnimeEpisode_UserRepository();
                var recs = repEps.GetLastWatchedEpisode();
                uinfo.LastEpisodeWatched = 0;
                if (recs.Count > 0)
                    uinfo.LastEpisodeWatched = Utils.GetAniDBDateAsSeconds(recs[0].WatchedDate);

                return uinfo;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        #endregion

        #region Admin - TvDB

        public static List<CrossRef_AniDB_TvDB> Admin_Get_CrossRefAniDBTvDB(int animeID)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;


            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}", azureHostBaseAddress,
                animeID, username, ServerSettings.WebCacheAuthKey);
            var msg = string.Format("Getting AniDB/TvDB Cross Ref From Cache: {0}", animeID);

            var json = GetDataJson(uri);

            var xrefs = JSONHelper.Deserialize<List<CrossRef_AniDB_TvDB>>(json);

            return xrefs;
        }

        public static string Admin_Approve_CrossRefAniDBTvDB(int crossRef_AniDB_TvDBId)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}", azureHostBaseAddress,
                crossRef_AniDB_TvDBId, username, ServerSettings.WebCacheAuthKey);
            var json = string.Empty;

            return SendData(uri, json, "POST");
        }

        public static string Admin_Revoke_CrossRefAniDBTvDB(int crossRef_AniDB_TvDBId)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}", azureHostBaseAddress,
                crossRef_AniDB_TvDBId, username, ServerSettings.WebCacheAuthKey);
            var json = string.Empty;

            return SendData(uri, json, "PUT");
        }

        public static Azure_AnimeLink Admin_GetRandomTvDBLinkForApproval()
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_TvDB/{1}?p={2}&p2={3}&p3=dummy",
                azureHostBaseAddress, (int)AzureLinkType.TvDB, username, ServerSettings.WebCacheAuthKey);
            var json = GetDataJson(uri);

            return JSONHelper.Deserialize<Azure_AnimeLink>(json);
        }

        #endregion

        #region Admin - Trakt

        public static List<CrossRef_AniDB_Trakt> Admin_Get_CrossRefAniDBTrakt(int animeID)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;


            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}", azureHostBaseAddress,
                animeID, username, ServerSettings.WebCacheAuthKey);
            var msg = string.Format("Getting AniDB/Trakt Cross Ref From Cache: {0}", animeID);

            var json = GetDataJson(uri);

            var xrefs = JSONHelper.Deserialize<List<CrossRef_AniDB_Trakt>>(json);

            return xrefs;
        }

        public static string Admin_Approve_CrossRefAniDBTrakt(int crossRef_AniDB_TraktId)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}", azureHostBaseAddress,
                crossRef_AniDB_TraktId, username, ServerSettings.WebCacheAuthKey);
            var json = string.Empty;

            return SendData(uri, json, "POST");
        }

        public static string Admin_Revoke_CrossRefAniDBTrakt(int crossRef_AniDB_TraktId)
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}", azureHostBaseAddress,
                crossRef_AniDB_TraktId, username, ServerSettings.WebCacheAuthKey);
            var json = string.Empty;

            return SendData(uri, json, "PUT");
        }

        public static Azure_AnimeLink Admin_GetRandomTraktLinkForApproval()
        {
            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            var uri = string.Format(@"http://{0}/api/Admin_CrossRef_AniDB_Trakt/{1}?p={2}&p2={3}&p3=dummy",
                azureHostBaseAddress, (int)AzureLinkType.Trakt, username, ServerSettings.WebCacheAuthKey);
            var json = GetDataJson(uri);

            return JSONHelper.Deserialize<Azure_AnimeLink>(json);
        }

        #endregion
    }
}