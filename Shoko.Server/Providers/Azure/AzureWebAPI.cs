﻿using System;
using System.Collections.Generic;
 using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NLog;
 using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
 using Shoko.Server.Settings;

 namespace Shoko.Server.Providers.Azure
{
    public static class AzureWebAPI
    {
        private static readonly string azureHostBaseAddress = "jmm.azurewebsites.net";
        //private static readonly string azureHostBaseAddress = "localhost:50994";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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
            string uri =
                $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_TvDB/{animeID}?p={
                    ServerSettings.Instance.AniDb.Username
                }&p2={aniDBStartEpisodeType}&p3={aniDBStartEpisodeNumber}&p4={tvDBID}&p5={tvDBSeasonNumber}&p6={
                    tvDBStartEpisodeNumber
                }";


            DeleteDataJson(uri);
        }

        public static void Send_CrossRefAniDBTvDB(CrossRef_AniDB_TvDBV2 data, string animeName)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_TvDB";

            Azure_CrossRef_AniDB_TvDB_Request input = data.ToRequest(animeName);
            string json = JSONHelper.Serialize(input);
            SendData(uri, json, "POST");
        }

        public static List<Azure_CrossRef_AniDB_TvDB> Get_CrossRefAniDBTvDB(int animeID)
        {
            try
            {
                string username = Constants.AnonWebCacheUsername;


                string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_TvDB/{animeID}?p={username}";
                string msg = $"Getting AniDB/TvDB Cross Ref From Cache: {animeID}";

                DateTime start = DateTime.Now;
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                string json = GetDataJson(uri);

                TimeSpan ts = DateTime.Now - start;
                msg = $"Got AniDB/TvDB Cross Ref From Cache: {animeID} - {ts.TotalMilliseconds}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                List<Azure_CrossRef_AniDB_TvDB> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_AniDB_TvDB>>(json);

                return xrefs ?? new List<Azure_CrossRef_AniDB_TvDB>();
            }
            catch
            {
                return new List<Azure_CrossRef_AniDB_TvDB>();
            }
        }

        #endregion

        #region Trakt

        public static List<Azure_CrossRef_AniDB_Trakt> Get_CrossRefAniDBTrakt(int animeID)
        {
            try
            {
                string username = Constants.AnonWebCacheUsername;

                string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_Trakt/{animeID}?p={username}";
                string msg = $"Getting AniDB/Trakt Cross Ref From Cache: {animeID}";

                DateTime start = DateTime.Now;
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                string json = GetDataJson(uri);

                TimeSpan ts = DateTime.Now - start;
                msg = $"Got AniDB/Trakt Cross Ref From Cache: {animeID} - {ts.TotalMilliseconds}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                List<Azure_CrossRef_AniDB_Trakt> xrefs = JSONHelper.Deserialize<List<Azure_CrossRef_AniDB_Trakt>>(json);

                return xrefs ?? new List<Azure_CrossRef_AniDB_Trakt>();
            }
            catch
            {
                return new List<Azure_CrossRef_AniDB_Trakt>();
            }
        }

        public static void Send_CrossRefAniDBTrakt(CrossRef_AniDB_TraktV2 data, string animeName)
        {
            if (!ServerSettings.Instance.WebCache.Trakt_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_Trakt";

            Azure_CrossRef_AniDB_Trakt_Request input = data.ToRequest(animeName);
            string json = JSONHelper.Serialize(input);
            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefAniDBTrakt(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber,
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

            if (!ServerSettings.Instance.WebCache.Trakt_Send) return;

            //localhost:50994
            //jmm.azurewebsites.net
            string uri =
                $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_Trakt/{animeID}?p={
                    ServerSettings.Instance.AniDb.Username
                }&p2={aniDBStartEpisodeType}&p3={aniDBStartEpisodeNumber}&p4={traktID}&p5={traktSeasonNumber}&p6={
                    traktStartEpisodeNumber
                }";


            DeleteDataJson(uri);
        }

        #endregion

        #region Cross Ref Other

        public static Azure_CrossRef_AniDB_Other Get_CrossRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            try
            {
                if (!ServerSettings.Instance.WebCache.TvDB_Get) return null;

                string username = Constants.AnonWebCacheUsername;

                string uri =
                    $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_Other/{animeID}?p={username}&p2={
                            (int) xrefType
                        }";
                string msg = $"Getting AniDB/Other Cross Ref From Cache: {animeID}";

                DateTime start = DateTime.Now;
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                string json = GetDataJson(uri);

                TimeSpan ts = DateTime.Now - start;
                msg = $"Got AniDB/MAL Cross Ref From Cache: {animeID} - {ts.TotalMilliseconds}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                Azure_CrossRef_AniDB_Other xref = JSONHelper.Deserialize<Azure_CrossRef_AniDB_Other>(json);

                return xref;
            }
            catch
            {
                return null;
            }
        }

        public static void Send_CrossRefAniDBOther(CrossRef_AniDB_Other data)
        {
            if (!ServerSettings.Instance.WebCache.TvDB_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_Other";

            Azure_CrossRef_AniDB_Other_Request input = data.ToRequest();
            string json = JSONHelper.Serialize(input);

            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            // id = animeid
            // p = username
            // p2 = AniDBStartEpisodeType
            // p3 = AniDBStartEpisodeNumber

            if (!ServerSettings.Instance.WebCache.TvDB_Send) return;

            string username = Constants.AnonWebCacheUsername;

            string uri =
                $@"http://{azureHostBaseAddress}/api/CrossRef_AniDB_Other/{animeID}?p={username}&p2={(int) xrefType}";


            DeleteDataJson(uri);
        }

        #endregion

        #region Cross Ref File Episode


        public static List<Azure_CrossRef_File_Episode> Get_CrossRefFileEpisode(SVR_VideoLocal vid)
        {
            try
            {
                if (!ServerSettings.Instance.WebCache.XRefFileEpisode_Get) return new List<Azure_CrossRef_File_Episode>();

                string username = Constants.AnonWebCacheUsername;

                string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_File_Episode/{vid.Hash}?p={username}";
                string msg = $"Getting File/Episode Cross Ref From Cache: {vid.Hash}";

                DateTime start = DateTime.Now;
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                string json = GetDataJson(uri);

                TimeSpan ts = DateTime.Now - start;
                msg = $"Got File/Episode Cross Ref From Cache: {vid.Hash} - {ts.TotalMilliseconds}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                List<Azure_CrossRef_File_Episode> xrefs =
                    JSONHelper.Deserialize<List<Azure_CrossRef_File_Episode>>(json);

                return xrefs ?? new List<Azure_CrossRef_File_Episode>();
            }
            catch
            {
                return new List<Azure_CrossRef_File_Episode>();
            }
        }

        public static void Send_CrossRefFileEpisode(CrossRef_File_Episode data)
        {
            if (!ServerSettings.Instance.WebCache.XRefFileEpisode_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_File_Episode";

            Azure_CrossRef_File_Episode_Request input = data.ToRequest();
            string json = JSONHelper.Serialize(input);

            SendData(uri, json, "POST");
        }

        public static void Delete_CrossRefFileEpisode(string hash)
        {
            if (!ServerSettings.Instance.WebCache.XRefFileEpisode_Send) return;

            string username = Constants.AnonWebCacheUsername;

            string uri = $@"http://{azureHostBaseAddress}/api/CrossRef_File_Episode/{hash}?p={username}";


            DeleteDataJson(uri);
        }

        #endregion

        #region Anime

        public static string Get_AnimeXML(int animeID)
        {
            try
            {
                //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

                string uri = $@"http://{azureHostBaseAddress}/api/animexml/{animeID}";

                DateTime start = DateTime.Now;
                string msg = $"Getting Anime XML Data From Cache: {animeID}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                string xml = GetDataXML(uri);

                // remove the string container
                int iStart = xml.IndexOf("<?xml", StringComparison.Ordinal);
                if (iStart > 0)
                {
                    int iEnd = xml.IndexOf("</string>", StringComparison.Ordinal);
                    if (iEnd > 0) xml = xml.Substring(iStart, iEnd - iStart - 1);
                }

                TimeSpan ts = DateTime.Now - start;
                string content = xml;
                if (content.Length > 100) content = content.Substring(0, 100);
                msg = $"Got Anime XML Data From Cache: {animeID} - {ts.TotalMilliseconds} - {content}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                return xml;
            }
            catch
            {
                return null;
            }
        }

        public static void Send_AnimeFull(SVR_AniDB_Anime data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/animefull";
            Azure_AnimeFull obj = data.ToAzure();
            string json = JSONHelper.Serialize(obj);
            SendData(uri, json, "POST");
        }

        public static void Send_AnimeXML(Azure_AnimeXML data)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/animexml";
            string json = JSONHelper.Serialize(data);
            SendData(uri, json, "POST");
        }

        #endregion

        #region Admin Messages

        public static List<Azure_AdminMessage> Get_AdminMessages()
        {
            try
            {
                string uri = $@"http://{azureHostBaseAddress}/api/AdminMessage/{"all"}";
                string json = GetDataJson(uri);

                List<Azure_AdminMessage> msgs = JSONHelper.Deserialize<List<Azure_AdminMessage>>(json);

                return msgs ?? new List<Azure_AdminMessage>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error(2) in XMLServiceQueue.SendData: {0}");
            }

            return new List<Azure_AdminMessage>();
        }

        #endregion

        #region User Info

        public static void Send_UserInfo()
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            Azure_UserInfo uinfo = GetUserInfoData();
            if (uinfo == null) return;

            string uri = $@"http://{azureHostBaseAddress}/api/userinfo";
            string json = JSONHelper.Serialize(uinfo);
            SendData(uri, json, "POST");
        }

        #endregion

        #region Helpers

        private static string SendData(string uri, string json, string verb)
        {
            string ret = string.Empty;
            return ret;
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
                var encoding = Encoding.UTF8;

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
                        if (!uri.Contains("Admin") || (int) response.StatusCode != 400)
                            logger.Error("HTTP Status Code: " + (int) response.StatusCode);
                        ret = response.StatusCode.ToString();
                    }
                }
                if (!uri.Contains("Admin"))
                    logger.Error("Error(1) in XMLServiceQueue.SendData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error(2) in XMLServiceQueue.SendData: {0}");
            }
            finally
            {
                req?.GetRequestStream().Close();
                rsp?.GetResponseStream()?.Close();
            }

            return ret;
        }

        private static string GetDataJson(string uri)
        {
            return String.Empty;
            try
            {
                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(uri);
                webReq.Timeout = 30000; // 30 seconds
                webReq.Proxy = null;
                webReq.Method = "GET";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "application/json; charset=UTF-8"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse webResponse = (HttpWebResponse) webReq.GetResponse())
                {
                    using (Stream responseStream = webResponse.GetResponseStream())
                    {
                        if (responseStream == null) return string.Empty;
                        Encoding encoding = Encoding.UTF8;
                        StreamReader Reader = new StreamReader(responseStream, encoding);

                        string output = Reader.ReadToEnd();
                        output = HttpUtility.HtmlDecode(output);

                        return output;
                    }
                }
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error(2) in AzureWebAPI.GetData: {0}");
            }

            return string.Empty;
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private static string DeleteDataJson(string uri)
        {
            try
            {
                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(uri);
                webReq.Timeout = 30000; // 30 seconds
                webReq.Proxy = null;
                webReq.Method = "DELETE";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "application/json; charset=UTF-8"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse WebResponse = (HttpWebResponse) webReq.GetResponse())
                {
                    using (Stream responseStream = WebResponse.GetResponseStream())
                    {
                        if (responseStream == null) return string.Empty;
                        Encoding encoding = Encoding.UTF8;
                        StreamReader Reader = new StreamReader(responseStream, encoding);

                        string output = Reader.ReadToEnd();
                        output = HttpUtility.HtmlDecode(output);

                        return output;
                    }
                }
            }
            catch (WebException webEx)
            {
                logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error(2) in AzureWebAPI.GetData: {0}");
            }

            return string.Empty;
        }

        private static string GetDataXML(string uri)
        {
            try
            {
                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(uri);
                webReq.Timeout = 30000; // 30 seconds
                webReq.Proxy = null;
                webReq.Method = "GET";
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.ContentType = "text/xml"; // content type
                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (var webResponse = (HttpWebResponse) webReq.GetResponse())
                {
                    using (Stream responseStream = webResponse.GetResponseStream())
                    {
                        if (responseStream == null) return string.Empty;
                        string enco = webResponse.CharacterSet;
                        Encoding encoding = null;
                        if (!string.IsNullOrEmpty(enco))
                            encoding = Encoding.GetEncoding(enco);
                        if (encoding == null)
                            encoding = Encoding.Default;
                        StreamReader Reader = new StreamReader(responseStream, encoding);

                        string output = Reader.ReadToEnd();
                        output = HttpUtility.HtmlDecode(output);

                        return output;
                    }
                }
            }
            catch (WebException webEx)
            {
                // Azure is broken here, just suppress it
                // logger.Error("WebError in AzureWebAPI.GetData: {0}", webEx);
            }
            catch (Exception ex)
            {
                logger.Error($"Error in AzureWebAPI.GetData: {ex}");
            }

            return string.Empty;
        }

        public static Azure_UserInfo GetUserInfoData(string vidPlayer = "")
        {
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.Instance.AniDb.Username)) return null;

                Azure_UserInfo uinfo = new Azure_UserInfo
                {
                    DateTimeUpdated = DateTime.Now,
                    DateTimeUpdatedUTC = 0,

                    // Optional JMM Desktop data
                    DashboardType = null,
                    VideoPlayer = vidPlayer
                };
                System.Reflection.Assembly a = System.Reflection.Assembly.GetEntryAssembly();
                try
                {
                    if (a != null) uinfo.JMMServerVersion = Utils.GetApplicationVersion(a);
                }
                catch
                {
                    // ignored
                }

                uinfo.UsernameHash = Utils.GetMd5Hash(ServerSettings.Instance.AniDb.Username);
                uinfo.DatabaseType = ServerSettings.Instance.Database.Type;
                uinfo.WindowsVersion = Utils.GetOSInfo();
                uinfo.TraktEnabled = ServerSettings.Instance.TraktTv.Enabled ? 1 : 0;

                uinfo.CountryLocation = string.Empty;

                // this field is not actually used
                uinfo.LastEpisodeWatchedAsDate = DateTime.Now.AddDays(-5);

                uinfo.LocalUserCount = (int) RepoFactory.JMMUser.GetTotalRecordCount();

                uinfo.FileCount = RepoFactory.VideoLocal.GetTotalRecordCount();

                SVR_AnimeEpisode_User rec = RepoFactory.AnimeEpisode_User.GetLastWatchedEpisode();
                uinfo.LastEpisodeWatched = 0;
                if (rec != null)
                    uinfo.LastEpisodeWatched = Commons.Utils.AniDB.GetAniDBDateAsSeconds(rec.WatchedDate);

                return uinfo;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        #endregion

        #region File Hashes

        // ReSharper disable once UnusedMember.Global
        public static void Send_FileHash(List<SVR_AniDB_File> aniFiles)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/FileHash";

            List<Azure_FileHash_Request> inputs = new List<Azure_FileHash_Request>();
            // send a max of 25 at a time
            foreach (SVR_AniDB_File aniFile in aniFiles)
            {
                Azure_FileHash_Request input = aniFile.ToHashRequest();
                if (inputs.Count < 25)
                    inputs.Add(input);
                else
                {
                    string json = JSONHelper.Serialize(inputs);
                    SendData(uri, json, "POST");
                    inputs.Clear();
                }
            }

            if (inputs.Count > 0)
            {
                string json = JSONHelper.Serialize(inputs);
                SendData(uri, json, "POST");
            }
        }

        public static void Send_FileHash(List<SVR_VideoLocal> locals)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

            string uri = $@"http://{azureHostBaseAddress}/api/FileHash";

            List<Azure_FileHash_Request> inputs = new List<Azure_FileHash_Request>();
            // send a max of 25 at a time
            foreach (SVR_VideoLocal v in locals)
            {
                Azure_FileHash_Request input = v.ToHashRequest();
                if (inputs.Count < 25)
                    inputs.Add(input);
                else
                {
                    string json = JSONHelper.Serialize(inputs);
                    SendData(uri, json, "POST");
                    inputs.Clear();
                }
            }

            if (inputs.Count > 0)
            {
                string json = JSONHelper.Serialize(inputs);
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
            return Get_FileHashWithTaskAsync(hashType, hashDetails).Result;
        }

        // TODO wrap the rest of these in timeout tasks
        public static async Task<List<Azure_FileHash>> Get_FileHashWithTaskAsync(FileHashType hashType, string hashDetails)
        {
            Task<List<Azure_FileHash>> task = new Task<List<Azure_FileHash>>(() =>
            {
                try
                {
                    string uri = $@"http://{azureHostBaseAddress}/api/FileHash/{(int) hashType}?p={hashDetails}";
                    string msg = $"Getting File Hash From Cache: {hashType} - {hashDetails}";

                    DateTime start = DateTime.Now;
                    ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                    string json = GetDataJson(uri);

                    TimeSpan ts = DateTime.Now - start;
                    msg = $"Got File Hash From Cache: {hashDetails} - {ts.TotalMilliseconds}";
                    ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                    return JSONHelper.Deserialize<List<Azure_FileHash>>(json);
                }
                catch
                {
                    return new List<Azure_FileHash>();
                }
            });

            if (await Task.WhenAny(task, Task.Delay(30000)) == task) return await task;
            return await Task.FromResult(new List<Azure_FileHash>());
        }

        #endregion

        #region Media

        public static void Send_Media(List<SVR_VideoLocal> locals)
        {
            //if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
            if (locals == null || locals.Count == 0) return;

            try
            {
                string uri = $@"http://{azureHostBaseAddress}/api/Media";
    
                List<Azure_Media_Request> inputs = new List<Azure_Media_Request>();
                // send a max of 25 at a time
                // send a max of 25 at a time
                foreach (SVR_VideoLocal v in locals.Where(a => a.MediaBlob != null && a.MediaBlob.Length > 0 &&
                                                               a.MediaVersion == SVR_VideoLocal.MEDIA_VERSION &&
                                                               !string.IsNullOrEmpty(a.ED2KHash)))
                {
                    Azure_Media_Request input = v.ToMediaRequest();
                    if (inputs.Count < 25)
                        inputs.Add(input);
                    else
                    {
                        string json = JSONHelper.Serialize(inputs);
                        //json = Newtonsoft.Json.JsonConvert.SerializeObject(inputs);
                        SendData(uri, json, "POST");
                        inputs.Clear();
                    }
                }
    
                if (inputs.Count <= 0)
                {
                    string json = JSONHelper.Serialize(inputs);
                    SendData(uri, json, "POST");
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"There was an error sending MediaInfo to WebCache for {locals.FirstOrDefault().ED2KHash}: {ex.Message}");
            }
        }

        public static void Send_Media(string ed2k, Shoko.Models.PlexAndKodi.Media media)
        {
            if (string.IsNullOrEmpty(ed2k)) return;

            try
            {
                string uri = $@"http://{azureHostBaseAddress}/api/Media";

                List<Azure_Media_Request> inputs = new List<Azure_Media_Request>();
                Azure_Media_Request input = media.ToMediaRequest(ed2k);
                inputs.Add(input);
                string json = JSONHelper.Serialize(inputs);
                SendData(uri, json, "POST");
            }
            catch (Exception ex)
            {
                logger.Warn($"There was an error sending MediaInfo to WebCache for {ed2k}: {ex.Message}");
            }
        }

        public static List<Azure_Media> Get_Media(string ed2k)
        {
            if (string.IsNullOrEmpty(ed2k)) return new List<Azure_Media>();
            try
            {
                string uri = $@"http://{azureHostBaseAddress}/api/Media/{ed2k}/{SVR_VideoLocal.MEDIA_VERSION}";
                string msg = $"Getting Media Info From Cache for ED2K: {ed2k} Version : {SVR_VideoLocal.MEDIA_VERSION}";

                DateTime start = DateTime.Now;
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                string json = GetDataJson(uri);

                TimeSpan ts = DateTime.Now - start;
                msg = $"Getting Media Info From Cache for ED2K: {ed2k} - {ts.TotalMilliseconds}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAzureHTTP, msg);

                List<Azure_Media> medias = JSONHelper.Deserialize<List<Azure_Media>>(json) ??
                                           new List<Azure_Media>();

                return medias;
            }catch
            {
                return new List<Azure_Media>();
            }
        }

        #endregion
    }
}
