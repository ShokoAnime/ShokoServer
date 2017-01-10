using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using AniDBAPI;
using ICSharpCode.SharpZipLib.Zip;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Entities;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Models.TvDB
{
    public class TvDBHelper
    {
        // http://thetvdb.com
        //API Key: B178B8940CAF4A2C

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private bool initialised = false;
        private static UTF8Encoding enc = new UTF8Encoding();
        static Dictionary<int, WebClient> webClientList = new Dictionary<int, WebClient>();
        static int nDownloadGUIDGenerator = 1;

        public string urlMirrorsList
        {
            get { return @"http://www.thetvdb.com/api/" + Shoko.Server.Constants.TvDBURLs.apiKey + @"/mirrors.xml"; }
        }

        public string urlServerTime
        {
            get { return @"http://www.thetvdb.com/api/Updates.php?type=none"; }
        }

        /*
       public string urlUpdatesList
       {
           get { return @"http://www.thetvdb.com/api/Updates.php?type=all&time={0}"; }
       }
       */
        private string urlMirror = "http://thetvdb.com";
        /*
        public string UrlMirror
       {
           get
           {
               Init();
               return urlMirror;
           }
        }
       */

        public static string URLMirror
        {
            get { return "http://thetvdb.com"; // they have said now that this will never change
            }
        }

        /*
		public static string GetRootImagesPath()
        {
			return ImageUtils.GetTvDBImagePath();
        }
        */
        private string serverTime = "";


        public TvDBHelper()
        {
        }

        public string CurrentServerTime
        {
            get
            {
                try
                {
                    string xmlServerTime = Utils.DownloadWebPage(urlServerTime);
                    XmlDocument docServer = new XmlDocument();
                    docServer.LoadXml(xmlServerTime);

                    string serverTime = docServer["Items"]["Time"].InnerText;

                    return serverTime;
                }
                catch
                {
                    return "";
                }
            }
        }

        private void Init()
        {
            try
            {
                if (initialised) return;

                // 01. try and download the list of mirrors
                string xmlMirrors = Utils.DownloadWebPage(urlMirrorsList);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlMirrors);

                XmlNodeList mirrorItems = xmlDoc["Mirrors"].GetElementsByTagName("Mirror");

                if (mirrorItems.Count <= 0)
                    return;

                string id = mirrorItems[0]["id"].InnerText;
                urlMirror = mirrorItems[0]["mirrorpath"].InnerText;
                string typemask = mirrorItems[0]["typemask"].InnerText;

                logger.Info("TVDB Mirror: {0}", urlMirror);

                // 02. get the server time
                string xmlServerTime = Utils.DownloadWebPage(urlServerTime);
                XmlDocument docServer = new XmlDocument();
                docServer.LoadXml(xmlServerTime);

                serverTime = docServer["Items"]["Time"].InnerText;

                logger.Info("serverTime: {0}", serverTime);

                initialised = true;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TVDBHelper.Init: " + ex.ToString());
            }
        }

/*
		public static bool ConfirmTvDBOnline()
		{
			TvDB_Series tvser = GetSeriesInfoOnline(73255);
			if (tvser == null)
				return false;
			else
				return true;
		}
        */

        public static TvDB_Series GetSeriesInfoOnline(int seriesID)
        {
            try
            {
                //Init();

                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlSeriesBaseXML, URLMirror, Shoko.Server.Constants.TvDBURLs.apiKey,
                    seriesID,
                    ServerSettings.TvDB_Language);
                logger.Trace("GetSeriesInfo: {0}", url);

                // Search for a series
                string xmlSeries = Utils.DownloadWebPage(url);
                logger.Trace("GetSeriesInfo RESULT: {0}", xmlSeries);

                if (xmlSeries.Trim().Length == 0) return null;

                XmlDocument docSeries = new XmlDocument();
                docSeries.LoadXml(xmlSeries);

                TvDB_Series tvSeries = null;
                if (docSeries != null)
                {
                    tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(seriesID);
                    if (tvSeries == null)
                        tvSeries = new TvDB_Series();

                    tvSeries.PopulateFromSeriesInfo(docSeries);
                    RepoFactory.TvDB_Series.Save(tvSeries);
                }

                return tvSeries;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TVDBHelper.GetSeriesInfoOnline: " + ex.ToString());
            }

            return null;
        }

        public XmlDocument GetSeriesBannersOnline(int seriesID)
        {
            try
            {
                Init();

                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlBannersXML, urlMirror, Shoko.Server.Constants.TvDBURLs.apiKey,
                    seriesID);
                logger.Trace("GetSeriesBannersOnline: {0}", url);

                // Search for a series
                string xmlSeries = Utils.DownloadWebPage(url);

                XmlDocument docBanners = new XmlDocument();
                docBanners.LoadXml(xmlSeries);

                return docBanners;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TVDBHelper.GetSeriesBannersOnline: " + ex.ToString());
            }

            return null;
        }

        public Dictionary<string, XmlDocument> GetFullSeriesInfo(int seriesID)
        {
            try
            {
                Init();

                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlFullSeriesData, urlMirror, Shoko.Server.Constants.TvDBURLs.apiKey,
                    seriesID,
                    ServerSettings.TvDB_Language);
                logger.Trace("GetFullSeriesInfo: {0}", url);

                Stream data = Utils.DownloadWebBinary(url);
                if (data != null)
                {
                    // this will get following xml files: en.xml, actors.xml, banners.xml
                    return DecompressZipToXmls(data);
                }
                else
                    logger.Trace("GetFullSeriesInfo: data was null");
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TVDBHelper.GetFullSeriesInfo: " + ex.ToString());
            }

            return null;
        }

        private static Dictionary<string, XmlDocument> DecompressZipToXmls(Stream s)
        {
            int bytes = 2048;
            byte[] data = new byte[2048];
            Dictionary<string, XmlDocument> docsInZip = new Dictionary<string, XmlDocument>();
            ZipInputStream zis = new ZipInputStream(s);
            ZipEntry currEntry = null;
            StringBuilder b = new StringBuilder();

            while ((currEntry = zis.GetNextEntry()) != null)
            {
                //BaseConfig.MyAnimeLog.Write("Decompressing Entry: {0}", currEntry.Name);
                XmlDocument d = new XmlDocument();
                while ((bytes = zis.Read(data, 0, data.Length)) > 0)
                    b.Append(enc.GetString(data, 0, bytes));

                //BaseConfig.MyAnimeLog.Write("Decompression done, now loading as XML...");
                try
                {
                    d.LoadXml(b.ToString());
                    //BaseConfig.MyAnimeLog.Write("Loaded as valid XML");
                    docsInZip.Add(currEntry.Name, d);
                }
                catch (XmlException e)
                {
                    logger.Error( e,"Error in TVDBHelper.DecompressZipToXmls: " + e.ToString());
                }
                b.Remove(0, b.Length);
            }
            return docsInZip;
        }

        /*
            public List<TvDB_ImageFanart> GetFanart(int seriesID, bool forceRefresh)
            {
                List<TvDB_ImageFanart> fanarts = new List<TvDB_ImageFanart>();

                if (forceRefresh)
                {
                    fanarts = GetFanartOnline(seriesID);
                }
                else
                {
                    TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
                    fanarts = repFanart.GetBySeriesID(seriesID);
                    if (fanarts.Count == 0)
                        fanarts = GetFanartOnline(seriesID);
                }

                return fanarts;
            }

            public List<TvDB_ImageFanart> GetFanartOnline(int seriesID)
            {
                List<TvDB_ImageFanart> fanarts = new List<TvDB_ImageFanart>();

                XmlDocument doc = GetSeriesBannersOnline(seriesID);
                List<object> banners = ParseBanners(seriesID, doc);

                foreach (object obj in banners)
                {
                    if (obj.GetType() == typeof(TvDB_ImageFanart))
                        fanarts.Add((TvDB_ImageFanart)obj);
                }

                return fanarts;
            }

            public List<TvDB_ImageWideBanner> GetWideBannersOnline(int seriesID)
            {
                List<TvDB_ImageWideBanner> wideBanners = new List<TvDB_ImageWideBanner>();

                XmlDocument doc = GetSeriesBannersOnline(seriesID);
                List<object> banners = ParseBanners(seriesID, doc);

                foreach (object obj in banners)
                {
                    if (obj.GetType() == typeof(TvDB_ImageWideBanner))
                        wideBanners.Add((TvDB_ImageWideBanner)obj);
                }

                return wideBanners;
            }

            public List<TvDB_ImagePoster> GetPostersOnline(int seriesID)
            {
                //BaseConfig.MyAnimeLog.Write("Getting posters online: {0}", seriesID);
                List<TvDB_ImagePoster> posters = new List<TvDB_ImagePoster>();

                XmlDocument doc = GetSeriesBannersOnline(seriesID);
                List<object> banners = ParseBanners(seriesID, doc);

                foreach (object obj in banners)
                {
                    if (obj.GetType() == typeof(TvDB_ImagePoster))
                        posters.Add((TvDB_ImagePoster)obj);
                }

                return posters;
            }
             */

        public List<TvDB_Language> GetLanguages()
        {
            List<TvDB_Language> languages = new List<TvDB_Language>();

            try
            {
                Init();

                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlLanguagesXML, urlMirror, Shoko.Server.Constants.TvDBURLs.apiKey);
                logger.Trace("GetLanguages: {0}", url);

                // Search for a series
                string xmlSeries = Utils.DownloadWebPage(url, Encoding.UTF8);

                XmlDocument docLanguages = new XmlDocument();
                
                docLanguages.LoadXml(xmlSeries);

                XmlNodeList lanItems = docLanguages["Languages"].GetElementsByTagName("Language");

                //BaseConfig.MyAnimeLog.Write("Found {0} banner nodes", bannerItems.Count);

                if (lanItems.Count <= 0)
                    return languages;

                foreach (XmlNode node in lanItems)
                {
                    TvDB_Language lan = new TvDB_Language();

                    lan.Name = node["name"].InnerText.Trim();
                    lan.Abbreviation = node["abbreviation"].InnerText.Trim();
                    languages.Add(lan);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TVDBHelper.GetSeriesBannersOnline: " + ex.ToString());
            }

            return languages;
        }

        public void DownloadAutomaticImages(int seriesID, bool forceDownload)
        {
            XmlDocument doc = GetSeriesBannersOnline(seriesID);
            DownloadAutomaticImages(doc, seriesID, forceDownload);
        }

        public void DownloadAutomaticImages(XmlDocument doc, int seriesID, bool forceDownload)
        {
            List<object> banners = ParseBanners(seriesID, doc);

            int numFanartDownloaded = 0;
            int numPostersDownloaded = 0;
            int numBannersDownloaded = 0;

            // find out how many images we already have locally


            
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();

                foreach (TvDB_ImageFanart fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(sessionWrapper, seriesID))
                {
                    if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                        numFanartDownloaded++;
                }

                foreach (TvDB_ImagePoster poster in RepoFactory.TvDB_ImagePoster.GetBySeriesID(sessionWrapper, seriesID))
                {
                    if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && File.Exists(poster.GetFullImagePath()))
                        numPostersDownloaded++;
                }

                foreach (TvDB_ImageWideBanner banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(session, seriesID))
                {
                    if (!string.IsNullOrEmpty(banner.GetFullImagePath()) && File.Exists(banner.GetFullImagePath()))
                        numBannersDownloaded++;
                }
            }


            foreach (object obj in banners)
            {
                if (obj.GetType() == typeof(TvDB_ImageFanart))
                {
                    TvDB_ImageFanart img = obj as TvDB_ImageFanart;
                    if (ServerSettings.TvDB_AutoFanart && numFanartDownloaded < ServerSettings.TvDB_AutoFanartAmount)
                    {
                        bool fileExists = File.Exists(img.GetFullImagePath());
                        if (!fileExists || (fileExists && forceDownload))
                        {
                            CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImageFanartID,
                                JMMImageType.TvDB_FanArt, forceDownload);
                            cmd.Save();
                            numFanartDownloaded++;
                        }
                    }
                    else
                    {
                        //The TvDB_AutoFanartAmount point to download less images than its available
                        // we should clean those image that we didn't download because those dont exists in local repo
                        // first we check if file was downloaded
                        if (!File.Exists(img.GetFullImagePath()))
                        {
                            RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageFanartID);
                        }
                    }
                }

                if (obj.GetType() == typeof(TvDB_ImagePoster))
                {
                    TvDB_ImagePoster img = obj as TvDB_ImagePoster;
                    if (ServerSettings.TvDB_AutoPosters && numPostersDownloaded < ServerSettings.TvDB_AutoPostersAmount)
                    {
                        bool fileExists = File.Exists(img.GetFullImagePath());
                        if (!fileExists || (fileExists && forceDownload))
                        {
                            CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImagePosterID,
                                JMMImageType.TvDB_Cover, forceDownload);
                            cmd.Save();
                            numPostersDownloaded++;
                        }
                    }
                    else
                    {
                        //The TvDB_AutoPostersAmount point to download less images than its available
                        // we should clean those image that we didn't download because those dont exists in local repo
                        // first we check if file was downloaded
                        if (!File.Exists(img.GetFullImagePath()))
                        {
                            RepoFactory.TvDB_ImagePoster.Delete(img.TvDB_ImagePosterID);
                        }
                    }
                }

                if (obj.GetType() == typeof(TvDB_ImageWideBanner))
                {
                    TvDB_ImageWideBanner img = obj as TvDB_ImageWideBanner;
                    if (ServerSettings.TvDB_AutoWideBanners &&
                        numBannersDownloaded < ServerSettings.TvDB_AutoWideBannersAmount)
                    {
                        bool fileExists = File.Exists(img.GetFullImagePath());
                        if (!fileExists || (fileExists && forceDownload))
                        {
                            CommandRequest_DownloadImage cmd =
                                new CommandRequest_DownloadImage(img.TvDB_ImageWideBannerID,
                                    JMMImageType.TvDB_Banner, forceDownload);
                            cmd.Save();
                            numBannersDownloaded++;
                        }
                    }
                    else
                    {
                        //The TvDB_AutoWideBannersAmount point to download less images than its available
                        // we should clean those image that we didn't download because those dont exists in local repo
                        // first we check if file was downloaded
                        if (!File.Exists(img.GetFullImagePath()))
                        {
                            RepoFactory.TvDB_ImageWideBanner.Delete(img.TvDB_ImageWideBannerID);
                        }
                    }
                }
            }
        }

        private List<object> ParseBanners(int seriesID, XmlDocument xmlDoc)
        {
            List<object> banners = new List<object>();
            try
            {
                XmlNodeList bannerItems = xmlDoc["Banners"].GetElementsByTagName("Banner");

                //BaseConfig.MyAnimeLog.Write("Found {0} banner nodes", bannerItems.Count);

                if (bannerItems.Count <= 0)
                    return banners;

                // banner types
                // series = wide banner
                // fanart = fanart
                // poster = filmstrip poster/dvd cover





                List<int> validFanartIDs = new List<int>();
                List<int> validPosterIDs = new List<int>();
                List<int> validBannerIDs = new List<int>();

                foreach (XmlNode node in bannerItems)
                {
                    JMMImageType imageType = JMMImageType.TvDB_Cover;

                    string bannerType = node["BannerType"].InnerText.Trim().ToUpper();
                    string bannerType2 = node["BannerType2"].InnerText.Trim().ToUpper();


                    if (bannerType == "FANART")
                        imageType = JMMImageType.TvDB_FanArt;
                    else if (bannerType == "POSTER")
                        imageType = JMMImageType.TvDB_Cover;
                    else if (bannerType == "SEASON")
                    {
                        if (bannerType2 == "SEASON")
                            imageType = JMMImageType.TvDB_Cover;
                        else
                            imageType = JMMImageType.TvDB_Banner;
                    }
                    else if (bannerType == "SERIES")
                    {
                        if (bannerType2 == "SEASONWIDE" || bannerType2 == "GRAPHICAL" || bannerType2 == "TEXT" ||
                            bannerType2 == "BLANK")
                            imageType = JMMImageType.TvDB_Banner;
                        else
                            imageType = JMMImageType.TvDB_Cover;
                    }

                    if (imageType == JMMImageType.TvDB_FanArt)
                    {
                        int id = int.Parse(node["id"].InnerText);
                        TvDB_ImageFanart img = RepoFactory.TvDB_ImageFanart.GetByTvDBID(id);
                        if (img == null)
                        {
                            img = new TvDB_ImageFanart();
                            img.Enabled = 1;
                        }

                        img.Populate(seriesID, node);
                        RepoFactory.TvDB_ImageFanart.Save(img);

                        banners.Add(img);
                        validFanartIDs.Add(id);
                    }

                    if (imageType == JMMImageType.TvDB_Banner)
                    {
                        int id = int.Parse(node["id"].InnerText);

                        TvDB_ImageWideBanner img = RepoFactory.TvDB_ImageWideBanner.GetByTvDBID(id);
                        if (img == null)
                        {
                            img = new TvDB_ImageWideBanner();
                            img.Enabled = 1;
                        }

                        img.Populate(seriesID, node, TvDBImageNodeType.Series);
                        RepoFactory.TvDB_ImageWideBanner.Save(img);

                        banners.Add(img);
                        validBannerIDs.Add(id);
                    }

                    if (imageType == JMMImageType.TvDB_Cover)
                    {
                        int id = int.Parse(node["id"].InnerText);

                        TvDB_ImagePoster img = RepoFactory.TvDB_ImagePoster.GetByTvDBID(id);
                        if (img == null)
                        {
                            img = new TvDB_ImagePoster();
                            img.Enabled = 1;
                        }

                        TvDBImageNodeType nodeType = TvDBImageNodeType.Series;
                        if (bannerType == "SEASON") nodeType = TvDBImageNodeType.Season;


                        img.Populate(seriesID, node, nodeType);
                        RepoFactory.TvDB_ImagePoster.Save(img);

                        banners.Add(img);
                        validPosterIDs.Add(id);
                    }
                }

                // delete any banners from the database which are no longer valid
                foreach (TvDB_ImageFanart img in RepoFactory.TvDB_ImageFanart.GetBySeriesID(seriesID))
                {
                    if (!validFanartIDs.Contains(img.Id))
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageFanartID);
                }

                foreach (TvDB_ImagePoster img in RepoFactory.TvDB_ImagePoster.GetBySeriesID(seriesID))
                {
                    if (!validPosterIDs.Contains(img.Id))
                        RepoFactory.TvDB_ImagePoster.Delete(img.TvDB_ImagePosterID);
                }

                foreach (TvDB_ImageWideBanner img in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(seriesID))
                {
                    if (!validBannerIDs.Contains(img.Id))
                        RepoFactory.TvDB_ImageWideBanner.Delete(img.TvDB_ImageWideBannerID);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in ParseBanners: " + ex.ToString());
            }

            return banners;
        }


        public List<TVDB_Series_Search_Response> SearchSeries(string criteria)
        {
            List<TVDB_Series_Search_Response> results = new List<TVDB_Series_Search_Response>();

            try
            {
                Init();

                if (!initialised) return results;

                // Search for a series
                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlSeriesSearch, criteria);
                logger.Trace("Search TvDB Series: {0}", url);

                string xmlSeries = Utils.DownloadWebPage(url);

                XmlDocument docSeries = new XmlDocument();
                docSeries.LoadXml(xmlSeries);

                bool hasData = docSeries["Data"].HasChildNodes;
                if (hasData)
                {
                    XmlNodeList seriesItems = docSeries["Data"].GetElementsByTagName("Series");

                    foreach (XmlNode series in seriesItems)
                    {
                        TVDB_Series_Search_Response searchResult = new TVDB_Series_Search_Response();
                        searchResult.Populate(series);

                        results.Add(searchResult);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in SearchSeries: " + ex.ToString());
            }

            return results;
        }


        public static List<int> GetUpdatedSeriesList(string serverTime)
        {
            List<int> seriesList = new List<int>();
            try
            {
                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlUpdatesList, URLMirror, serverTime);

                // Search for a series
                string xmlUpdateList = Utils.DownloadWebPage(url);
                //BaseConfig.MyAnimeLog.Write("GetSeriesInfo RESULT: {0}", xmlSeries);

                XmlDocument docUpdates = new XmlDocument();
                docUpdates.LoadXml(xmlUpdateList);

                XmlNodeList nodes = docUpdates["Items"].GetElementsByTagName("Series");
                foreach (XmlNode node in nodes)
                {
                    string sid = node.InnerText;
                    int id = -1;
                    int.TryParse(sid, out id);
                    if (id > 0) seriesList.Add(id);

                    //BaseConfig.MyAnimeLog.Write("Updated series: {0}", sid);
                }

                return seriesList;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in GetUpdatedSeriesList: " + ex.ToString());
                return seriesList;
            }
        }


        /// <summary>
        /// Updates the followung
        /// 1. Series Info
        /// 2. Episode Info
        /// 3. Episode Images
        /// 4. Fanart, Poster and Wide Banner Images
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="forceRefresh"></param>
        public void UpdateAllInfoAndImages(int seriesID, bool forceRefresh, bool downloadImages)
        {
          

            string fileName = string.Format("{0}.xml", ServerSettings.TvDB_Language);

            Dictionary<string, XmlDocument> docSeries = GetFullSeriesInfo(seriesID);
            if (docSeries.ContainsKey(fileName))
            {
                try
                {
                    // update the series info
                    XmlDocument xmlDoc = docSeries[fileName];
                    if (xmlDoc != null)
                    {
                        TvDB_Series tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(seriesID);
                        if (tvSeries == null)
                            tvSeries = new TvDB_Series();

                        tvSeries.PopulateFromSeriesInfo(xmlDoc);
                        RepoFactory.TvDB_Series.Save(tvSeries);
                    }

                    if (downloadImages)
                    {
                        // get all fanart, posters and wide banners
                        if (docSeries.ContainsKey("banners.xml"))
                        {
                            XmlDocument xmlDocBanners = docSeries["banners.xml"];
                            if (xmlDocBanners != null)
                                DownloadAutomaticImages(xmlDocBanners, seriesID, forceRefresh);
                        }
                    }

                    // update all the episodes and download episode images
                    XmlNodeList episodeItems = xmlDoc["Data"].GetElementsByTagName("Episode");
                    logger.Trace("Found {0} Episode nodes", episodeItems.Count.ToString());

                    List<int> existingEpIds = new List<int>();
                    foreach (XmlNode node in episodeItems)
                    {
                        try
                        {
                            // the episode id
                            int id = int.Parse(node["id"].InnerText.Trim());
                            existingEpIds.Add(id);

                            TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByTvDBID(id);
                            if (ep == null)
                                ep = new TvDB_Episode();
                            ep.Populate(node);
                            RepoFactory.TvDB_Episode.Save(ep);

                            //BaseConfig.MyAnimeLog.Write("Refreshing episode info for: {0}", ep.ToString());

                            if (downloadImages)
                            {
                                // download the image for this episode
                                if (!string.IsNullOrEmpty(ep.Filename))
                                {
                                    bool fileExists = File.Exists(ep.GetFullImagePath());
                                    if (!fileExists || (fileExists && forceRefresh))
                                    {
                                        CommandRequest_DownloadImage cmd =
                                            new CommandRequest_DownloadImage(ep.TvDB_EpisodeID,
                                                JMMImageType.TvDB_Episode, forceRefresh);
                                        cmd.Save();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error( ex,"Error in TVDBHelper.GetEpisodes: " + ex.ToString());
                        }
                    }

                    // get all the existing tvdb episodes, to see if any have been deleted
                    List<TvDB_Episode> allEps = RepoFactory.TvDB_Episode.GetBySeriesID(seriesID);
                    foreach (TvDB_Episode oldEp in allEps)
                    {
                        if (!existingEpIds.Contains(oldEp.Id))
                            RepoFactory.TvDB_Episode.Delete(oldEp.TvDB_EpisodeID);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error( ex,"Error in TVDBHelper.GetEpisodes: " + ex.ToString());
                }
            }
        }


        public static string LinkAniDBTvDB(int animeID, enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber, bool excludeFromWebCache, bool additiveLink=false)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
	            if(!additiveLink)
					// remove all current links
					RemoveAllAniDBTvDBLinks(session.Wrap(), animeID);

                // check if we have this information locally
                // if not download it now
                TvDB_Series tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(tvDBID);
                if (tvSeries == null)
                {
                    // we download the series info here just so that we have the basic info in the
                    // database before the queued task runs later
                    tvSeries = GetSeriesInfoOnline(tvDBID);
                }

                // download and update series info, episode info and episode images
                // will also download fanart, posters and wide banners
                CommandRequest_TvDBUpdateSeriesAndEpisodes cmdSeriesEps =
                    new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvDBID,
                        false);
                //Optimize for batch updates, if there are a lot of LinkAniDBTvDB commands queued 
                //this will cause only one updateSeriesAndEpisodes command to be created
                if (RepoFactory.CommandRequest.GetByCommandID(cmdSeriesEps.CommandID) == null)
                {
                    cmdSeriesEps.Save();
                }

                SVR_CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(session, tvDBID, tvSeasonNumber, tvEpNumber,
                    animeID,
                    (int) aniEpType, aniEpNumber);
                if (xref == null)
                    xref = new SVR_CrossRef_AniDB_TvDBV2();

                xref.AnimeID = animeID;
                xref.AniDBStartEpisodeType = (int) aniEpType;
                xref.AniDBStartEpisodeNumber = aniEpNumber;

                xref.TvDBID = tvDBID;
                xref.TvDBSeasonNumber = tvSeasonNumber;
                xref.TvDBStartEpisodeNumber = tvEpNumber;
                if (tvSeries != null)
                    xref.TvDBTitle = tvSeries.SeriesName;

                if (excludeFromWebCache)
                    xref.CrossRefSource = (int) CrossRefSource.WebCache;
                else
                    xref.CrossRefSource = (int) CrossRefSource.User;

                RepoFactory.CrossRef_AniDB_TvDBV2.Save(xref);

                SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

                logger.Trace("Changed tvdb association: {0}", animeID);

                if (!excludeFromWebCache)
                {
                    CommandRequest_WebCacheSendXRefAniDBTvDB req =
                        new CommandRequest_WebCacheSendXRefAniDBTvDB(xref.CrossRef_AniDB_TvDBV2ID);
                    req.Save();
                }

                if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                {
                    if (RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID).Count == 0)
                    {
                        // check for Trakt associations
                        CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(animeID, false);
                        cmd2.Save(session);
                    }
                }
            }

            return "";
        }

        public static void LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID)
        {
            CrossRef_AniDB_TvDB_Episode xref = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(aniDBID);
            if (xref == null)
                xref = new CrossRef_AniDB_TvDB_Episode();

            xref.AnimeID = animeID;
            xref.AniDBEpisodeID = aniDBID;
            xref.TvDBEpisodeID = tvDBID;
            RepoFactory.CrossRef_AniDB_TvDB_Episode.Save(xref);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniDBID);
            RepoFactory.AnimeEpisode.Save(ep);

            logger.Trace("Changed tvdb episode association: {0}", aniDBID);
        }

        // Removes all TVDB information from a series, bringing it back to a blank state.
        public static void RemoveLinkAniDBTvDB(int animeID, enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber)
        {
            SVR_CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvDBID, tvSeasonNumber, tvEpNumber, animeID,
                (int) aniEpType,
                aniEpNumber);
            if (xref == null) return;

            RepoFactory.CrossRef_AniDB_TvDBV2.Delete(xref.CrossRef_AniDB_TvDBV2ID);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(animeID,
                (int) aniEpType, aniEpNumber,
                tvDBID, tvSeasonNumber, tvEpNumber);
            req.Save();
        }

	    public static void RemoveAllAniDBTvDBLinks(ISessionWrapper session, int animeID, int aniEpType=-1)
	    {
		    List<SVR_CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(session, animeID);
		    if (xrefs == null || xrefs.Count == 0) return;

		    foreach (SVR_CrossRef_AniDB_TvDBV2 xref in xrefs)
		    {
			    if (aniEpType != -1 && aniEpType == xref.AniDBStartEpisodeType) continue;

			    RepoFactory.CrossRef_AniDB_TvDBV2.Delete(xref.CrossRef_AniDB_TvDBV2ID);

			    if (aniEpType == -1)
			    {
				    foreach (enEpisodeType eptype in Enum.GetValues(typeof(enEpisodeType)))
				    {
					    CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(animeID,
						    (int)eptype, xref.AniDBStartEpisodeNumber,
						    xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
					    req.Save();
				    }
			    }
			    else
			    {
				    CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(animeID,
					    aniEpType, xref.AniDBStartEpisodeNumber,
					    xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
				    req.Save();
			    }

		    }

		    SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
	    }

	    /*
		public static void DownloadAllEpisodes()
		{
			CrossRef_AniDB_TvDBV2Repository repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
			List<CrossRef_AniDB_TvDBV2> allCrossRefs = repCrossRef.GetAll();

			List<int> tvDBIDs = new List<int>();
			foreach (CrossRef_AniDB_TvDBV2 xref in allCrossRefs)
			{
				if (!tvDBIDs.Contains(xref.TvDBID)) tvDBIDs.Add(xref.TvDBID);
			}

			DownloadAllEpisodes(tvDBIDs);
		}

	    public static void DownloadAllEpisodes(List<int> tvDBIDs)
		{
			foreach (int tvid in tvDBIDs)
			{
				CommandRequest_TvDBUpdateSeriesAndEpisodes cmd = new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvid, false);
				cmd.Save();
			}
		}
	    */

        public static void ScanForMatches()
        {
            IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();

            IReadOnlyList<SVR_CrossRef_AniDB_TvDBV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetAll();
            List<int> alreadyLinked = new List<int>();
            foreach (SVR_CrossRef_AniDB_TvDBV2 xref in allCrossRefs)
            {
                alreadyLinked.Add(xref.AnimeID);
            }

            foreach (SVR_AnimeSeries ser in allSeries)
            {
                if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

                SVR_AniDB_Anime anime = ser.GetAnime();

                if (anime != null)
                {
	                if (!anime.GetSearchOnTvDB()) continue; // Don't log if it isn't supposed to be there
	                logger.Trace("Found anime without tvDB association: " + anime.MainTitle);
                    if (anime.GetIsTvDBLinkDisabled())
                    {
                        logger.Trace("Skipping scan tvDB link because it is disabled: " + anime.MainTitle);
                        continue;
                    }
                }

                CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }
        }

        public static void UpdateAllInfo(bool force)
        {
            IReadOnlyList<SVR_CrossRef_AniDB_TvDBV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetAll();
            List<int> alreadyLinked = new List<int>();
            foreach (SVR_CrossRef_AniDB_TvDBV2 xref in allCrossRefs)
            {
                CommandRequest_TvDBUpdateSeriesAndEpisodes cmd =
                    new CommandRequest_TvDBUpdateSeriesAndEpisodes(xref.TvDBID, force);
                cmd.Save();
            }
        }

        /// <summary>
        /// Used to get a list of TvDB Series ID's that require updating
        /// </summary>
        /// <param name="tvDBIDs">The list Of Series ID's that need to be updated. Pass in an empty list</param>
        /// <returns>The current server time before the update started</returns>
        public string IncrementalTvDBUpdate(ref List<int> tvDBIDs, ref bool tvDBOnline)
        {
            // check if we have record of doing an automated update for the TvDB previously
            // if we have then we have kept a record of the server time and can do a delta update
            // otherwise we need to do a full update and keep a record of the time

            List<int> allTvDBIDs = new List<int>();
            tvDBIDs = new List<int>();
            tvDBOnline = true;

            try
            {

                // record the tvdb server time when we started
                // we record the time now instead of after we finish, to include any possible misses
                string currentTvDBServerTime = CurrentServerTime;
                if (currentTvDBServerTime.Length == 0)
                {
                    tvDBOnline = false;
                    return currentTvDBServerTime;
                }

                foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                {
                    List<SVR_CrossRef_AniDB_TvDBV2> xrefs = ser.GetCrossRefTvDBV2();
                    if (xrefs == null) continue;

                    foreach (SVR_CrossRef_AniDB_TvDBV2 xref in xrefs)
                    {
                        if (!allTvDBIDs.Contains(xref.TvDBID)) allTvDBIDs.Add(xref.TvDBID);
                    }
                }

                // get the time we last did a TvDB update
                // if this is the first time it will be null
                // update the anidb info ever 24 hours
               
                ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TvDBInfo);

                string lastServerTime = "";
                if (sched != null)
                {
                    TimeSpan ts = DateTime.Now - sched.LastUpdate;
                    logger.Trace("Last tvdb info update was {0} hours ago", ts.TotalHours.ToString());
                    if (!string.IsNullOrEmpty(sched.UpdateDetails))
                        lastServerTime = sched.UpdateDetails;

                    // the UpdateDetails field for this type will actually contain the last server time from
                    // TheTvDB that a full update was performed
                }


                // get a list of updates from TvDB since that time
                if (lastServerTime.Length > 0)
                {
                    List<int> seriesList = GetUpdatedSeriesList(lastServerTime);
                    logger.Trace("{0} series have been updated since last download", seriesList.Count.ToString());
                    logger.Trace("{0} TvDB series locally", allTvDBIDs.Count.ToString());

                    foreach (int id in seriesList)
                    {
                        if (allTvDBIDs.Contains(id)) tvDBIDs.Add(id);
                    }
                    logger.Trace("{0} TvDB local series have been updated since last download", tvDBIDs.Count.ToString());
                }
                else
                {
                    // use the full list
                    tvDBIDs = allTvDBIDs;
                }

                return currentTvDBServerTime;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"IncrementalTvDBUpdate: " + ex.ToString());
                return "";
            }
        }
    }
}