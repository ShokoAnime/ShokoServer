using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using AniDBAPI;
using ICSharpCode.SharpZipLib.Zip;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Providers.TvDB
{
    public class TvDBHelper
    {
        // http://thetvdb.com
        //API Key: B178B8940CAF4A2C

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly UTF8Encoding enc = new UTF8Encoding();
        private static Dictionary<int, WebClient> webClientList = new Dictionary<int, WebClient>();
        private static int nDownloadGUIDGenerator = 1;
        private bool initialised;
        /*
		public static string GetRootImagesPath()
        {
			return ImageUtils.GetTvDBImagePath();
        }
        */
        private string serverTime = "";
        /*
       public string urlUpdatesList
       {
           get { return @"http://www.thetvdb.com/api/Updates.php?type=all&time={0}"; }
       }
       */
        private string urlMirror = "http://thetvdb.com";


        public string urlMirrorsList
        {
            get { return @"http://www.thetvdb.com/api/" + Constants.TvDBURLs.apiKey + @"/mirrors.xml"; }
        }

        public string urlServerTime
        {
            get { return @"http://www.thetvdb.com/api/Updates.php?type=none"; }
        }

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
            get
            {
                return "http://thetvdb.com"; // they have said now that this will never change
            }
        }

        public string CurrentServerTime
        {
            get
            {
                try
                {
                    var xmlServerTime = Utils.DownloadWebPage(urlServerTime);
                    var docServer = new XmlDocument();
                    docServer.LoadXml(xmlServerTime);

                    var serverTime = docServer["Items"]["Time"].InnerText;

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
                var xmlMirrors = Utils.DownloadWebPage(urlMirrorsList);

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlMirrors);

                var mirrorItems = xmlDoc["Mirrors"].GetElementsByTagName("Mirror");

                if (mirrorItems.Count <= 0)
                    return;

                var id = mirrorItems[0]["id"].InnerText;
                urlMirror = mirrorItems[0]["mirrorpath"].InnerText;
                var typemask = mirrorItems[0]["typemask"].InnerText;

                logger.Info("TVDB Mirror: {0}", urlMirror);

                // 02. get the server time
                var xmlServerTime = Utils.DownloadWebPage(urlServerTime);
                var docServer = new XmlDocument();
                docServer.LoadXml(xmlServerTime);

                serverTime = docServer["Items"]["Time"].InnerText;

                logger.Info("serverTime: {0}", serverTime);

                initialised = true;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TVDBHelper.Init: " + ex, ex);
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

                var url = string.Format(Constants.TvDBURLs.urlSeriesBaseXML, URLMirror, Constants.TvDBURLs.apiKey,
                    seriesID, ServerSettings.TvDB_Language);
                logger.Trace("GetSeriesInfo: {0}", url);

                // Search for a series
                var xmlSeries = Utils.DownloadWebPage(url);
                logger.Trace("GetSeriesInfo RESULT: {0}", xmlSeries);

                if (xmlSeries.Trim().Length == 0) return null;

                var docSeries = new XmlDocument();
                docSeries.LoadXml(xmlSeries);

                TvDB_Series tvSeries = null;
                if (docSeries != null)
                {
                    var repSeries = new TvDB_SeriesRepository();
                    tvSeries = repSeries.GetByTvDBID(seriesID);
                    if (tvSeries == null)
                        tvSeries = new TvDB_Series();

                    tvSeries.PopulateFromSeriesInfo(docSeries);
                    repSeries.Save(tvSeries);
                }

                return tvSeries;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TVDBHelper.GetSeriesInfoOnline: " + ex, ex);
            }

            return null;
        }

        public XmlDocument GetSeriesBannersOnline(int seriesID)
        {
            try
            {
                Init();

                var url = string.Format(Constants.TvDBURLs.urlBannersXML, urlMirror, Constants.TvDBURLs.apiKey, seriesID);
                logger.Trace("GetSeriesBannersOnline: {0}", url);

                // Search for a series
                var xmlSeries = Utils.DownloadWebPage(url);

                var docBanners = new XmlDocument();
                docBanners.LoadXml(xmlSeries);

                return docBanners;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TVDBHelper.GetSeriesBannersOnline: " + ex, ex);
            }

            return null;
        }

        public Dictionary<string, XmlDocument> GetFullSeriesInfo(int seriesID)
        {
            try
            {
                Init();

                var url = string.Format(Constants.TvDBURLs.urlFullSeriesData, urlMirror, Constants.TvDBURLs.apiKey,
                    seriesID, ServerSettings.TvDB_Language);
                logger.Trace("GetFullSeriesInfo: {0}", url);

                var data = Utils.DownloadWebBinary(url);
                if (data != null)
                {
                    // this will get following xml files: en.xml, actors.xml, banners.xml
                    return DecompressZipToXmls(data);
                }
                logger.Trace("GetFullSeriesInfo: data was null");
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TVDBHelper.GetFullSeriesInfo: " + ex, ex);
            }

            return null;
        }

        private static Dictionary<string, XmlDocument> DecompressZipToXmls(Stream s)
        {
            var bytes = 2048;
            var data = new byte[2048];
            var docsInZip = new Dictionary<string, XmlDocument>();
            var zis = new ZipInputStream(s);
            ZipEntry currEntry = null;
            var b = new StringBuilder();

            while ((currEntry = zis.GetNextEntry()) != null)
            {
                //BaseConfig.MyAnimeLog.Write("Decompressing Entry: {0}", currEntry.Name);
                var d = new XmlDocument();
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
                    logger.ErrorException("Error in TVDBHelper.DecompressZipToXmls: " + e, e);
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

        public List<TvDBLanguage> GetLanguages()
        {
            var languages = new List<TvDBLanguage>();

            try
            {
                Init();

                var url = string.Format(Constants.TvDBURLs.urlLanguagesXML, urlMirror, Constants.TvDBURLs.apiKey);
                logger.Trace("GetLanguages: {0}", url);

                // Search for a series
                var xmlSeries = Utils.DownloadWebPage(url);

                var docLanguages = new XmlDocument();
                docLanguages.LoadXml(xmlSeries);

                var lanItems = docLanguages["Languages"].GetElementsByTagName("Language");

                //BaseConfig.MyAnimeLog.Write("Found {0} banner nodes", bannerItems.Count);

                if (lanItems.Count <= 0)
                    return languages;

                foreach (XmlNode node in lanItems)
                {
                    var lan = new TvDBLanguage();

                    lan.Name = node["name"].InnerText.Trim();
                    lan.Abbreviation = node["abbreviation"].InnerText.Trim();
                    languages.Add(lan);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TVDBHelper.GetSeriesBannersOnline: " + ex, ex);
            }

            return languages;
        }

        public void DownloadAutomaticImages(int seriesID, bool forceDownload)
        {
            var doc = GetSeriesBannersOnline(seriesID);
            DownloadAutomaticImages(doc, seriesID, forceDownload);
        }

        public void DownloadAutomaticImages(XmlDocument doc, int seriesID, bool forceDownload)
        {
            var banners = ParseBanners(seriesID, doc);

            var numFanartDownloaded = 0;
            var numPostersDownloaded = 0;
            var numBannersDownloaded = 0;

            // find out how many images we already have locally
            var repFanart = new TvDB_ImageFanartRepository();
            var repPosters = new TvDB_ImagePosterRepository();
            var repBanners = new TvDB_ImageWideBannerRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                foreach (var fanart in repFanart.GetBySeriesID(session, seriesID))
                {
                    if (!string.IsNullOrEmpty(fanart.FullImagePath) && File.Exists(fanart.FullImagePath))
                        numFanartDownloaded++;
                }

                foreach (var poster in repPosters.GetBySeriesID(session, seriesID))
                {
                    if (!string.IsNullOrEmpty(poster.FullImagePath) && File.Exists(poster.FullImagePath))
                        numPostersDownloaded++;
                }

                foreach (var banner in repBanners.GetBySeriesID(session, seriesID))
                {
                    if (!string.IsNullOrEmpty(banner.FullImagePath) && File.Exists(banner.FullImagePath))
                        numBannersDownloaded++;
                }
            }


            foreach (var obj in banners)
            {
                if (obj.GetType() == typeof(TvDB_ImageFanart))
                {
                    var img = obj as TvDB_ImageFanart;
                    if (ServerSettings.TvDB_AutoFanart && numFanartDownloaded < ServerSettings.TvDB_AutoFanartAmount)
                    {
                        var fileExists = File.Exists(img.FullImagePath);
                        if (!fileExists || (fileExists && forceDownload))
                        {
                            var cmd = new CommandRequest_DownloadImage(img.TvDB_ImageFanartID, JMMImageType.TvDB_FanArt,
                                forceDownload);
                            cmd.Save();
                            numFanartDownloaded++;
                        }
                    }
                }

                if (obj.GetType() == typeof(TvDB_ImagePoster))
                {
                    var img = obj as TvDB_ImagePoster;
                    if (ServerSettings.TvDB_AutoPosters && numPostersDownloaded < ServerSettings.TvDB_AutoPostersAmount)
                    {
                        var fileExists = File.Exists(img.FullImagePath);
                        if (!fileExists || (fileExists && forceDownload))
                        {
                            var cmd = new CommandRequest_DownloadImage(img.TvDB_ImagePosterID, JMMImageType.TvDB_Cover,
                                forceDownload);
                            cmd.Save();
                            numPostersDownloaded++;
                        }
                    }
                }

                if (obj.GetType() == typeof(TvDB_ImageWideBanner))
                {
                    var img = obj as TvDB_ImageWideBanner;
                    if (ServerSettings.TvDB_AutoWideBanners &&
                        numBannersDownloaded < ServerSettings.TvDB_AutoWideBannersAmount)
                    {
                        var fileExists = File.Exists(img.FullImagePath);
                        if (!fileExists || (fileExists && forceDownload))
                        {
                            var cmd = new CommandRequest_DownloadImage(img.TvDB_ImageWideBannerID,
                                JMMImageType.TvDB_Banner, forceDownload);
                            cmd.Save();
                            numBannersDownloaded++;
                        }
                    }
                }
            }
        }

        private List<object> ParseBanners(int seriesID, XmlDocument xmlDoc)
        {
            var banners = new List<object>();
            try
            {
                var bannerItems = xmlDoc["Banners"].GetElementsByTagName("Banner");

                //BaseConfig.MyAnimeLog.Write("Found {0} banner nodes", bannerItems.Count);

                if (bannerItems.Count <= 0)
                    return banners;

                // banner types
                // series = wide banner
                // fanart = fanart
                // poster = filmstrip poster/dvd cover

                var repFanart = new TvDB_ImageFanartRepository();
                var repPosters = new TvDB_ImagePosterRepository();
                var repWideBanners = new TvDB_ImageWideBannerRepository();

                var validFanartIDs = new List<int>();
                var validPosterIDs = new List<int>();
                var validBannerIDs = new List<int>();

                foreach (XmlNode node in bannerItems)
                {
                    var imageType = JMMImageType.TvDB_Cover;

                    var bannerType = node["BannerType"].InnerText.Trim().ToUpper();
                    var bannerType2 = node["BannerType2"].InnerText.Trim().ToUpper();


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
                        var id = int.Parse(node["id"].InnerText);
                        var img = repFanart.GetByTvDBID(id);
                        if (img == null)
                        {
                            img = new TvDB_ImageFanart();
                            img.Enabled = 1;
                        }

                        img.Populate(seriesID, node);
                        repFanart.Save(img);

                        banners.Add(img);
                        validFanartIDs.Add(id);
                    }

                    if (imageType == JMMImageType.TvDB_Banner)
                    {
                        var id = int.Parse(node["id"].InnerText);

                        var img = repWideBanners.GetByTvDBID(id);
                        if (img == null)
                        {
                            img = new TvDB_ImageWideBanner();
                            img.Enabled = 1;
                        }

                        img.Populate(seriesID, node, TvDBImageNodeType.Series);
                        repWideBanners.Save(img);

                        banners.Add(img);
                        validBannerIDs.Add(id);
                    }

                    if (imageType == JMMImageType.TvDB_Cover)
                    {
                        var id = int.Parse(node["id"].InnerText);

                        var img = repPosters.GetByTvDBID(id);
                        if (img == null)
                        {
                            img = new TvDB_ImagePoster();
                            img.Enabled = 1;
                        }

                        var nodeType = TvDBImageNodeType.Series;
                        if (bannerType == "SEASON") nodeType = TvDBImageNodeType.Season;


                        img.Populate(seriesID, node, nodeType);
                        repPosters.Save(img);

                        banners.Add(img);
                        validPosterIDs.Add(id);
                    }
                }

                // delete any banners from the database which are no longer valid
                foreach (var img in repFanart.GetBySeriesID(seriesID))
                {
                    if (!validFanartIDs.Contains(img.Id))
                        repFanart.Delete(img.TvDB_ImageFanartID);
                }

                foreach (var img in repPosters.GetBySeriesID(seriesID))
                {
                    if (!validPosterIDs.Contains(img.Id))
                        repPosters.Delete(img.TvDB_ImagePosterID);
                }

                foreach (var img in repWideBanners.GetBySeriesID(seriesID))
                {
                    if (!validBannerIDs.Contains(img.Id))
                        repWideBanners.Delete(img.TvDB_ImageWideBannerID);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in ParseBanners: " + ex, ex);
            }

            return banners;
        }


        public List<TVDBSeriesSearchResult> SearchSeries(string criteria)
        {
            var results = new List<TVDBSeriesSearchResult>();

            try
            {
                Init();

                if (!initialised) return results;

                // Search for a series
                var url = string.Format(Constants.TvDBURLs.urlSeriesSearch, criteria);
                logger.Trace("Search TvDB Series: {0}", url);

                var xmlSeries = Utils.DownloadWebPage(url);

                var docSeries = new XmlDocument();
                docSeries.LoadXml(xmlSeries);

                var hasData = docSeries["Data"].HasChildNodes;
                if (hasData)
                {
                    var seriesItems = docSeries["Data"].GetElementsByTagName("Series");

                    foreach (XmlNode series in seriesItems)
                    {
                        var searchResult = new TVDBSeriesSearchResult(series);
                        results.Add(searchResult);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in SearchSeries: " + ex, ex);
            }

            return results;
        }


        public static List<int> GetUpdatedSeriesList(string serverTime)
        {
            var seriesList = new List<int>();
            try
            {
                var url = string.Format(Constants.TvDBURLs.urlUpdatesList, URLMirror, serverTime);

                // Search for a series
                var xmlUpdateList = Utils.DownloadWebPage(url);
                //BaseConfig.MyAnimeLog.Write("GetSeriesInfo RESULT: {0}", xmlSeries);

                var docUpdates = new XmlDocument();
                docUpdates.LoadXml(xmlUpdateList);

                var nodes = docUpdates["Items"].GetElementsByTagName("Series");
                foreach (XmlNode node in nodes)
                {
                    var sid = node.InnerText;
                    var id = -1;
                    int.TryParse(sid, out id);
                    if (id > 0) seriesList.Add(id);

                    //BaseConfig.MyAnimeLog.Write("Updated series: {0}", sid);
                }

                return seriesList;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in GetUpdatedSeriesList: " + ex, ex);
                return seriesList;
            }
        }


        /// <summary>
        ///     Updates the followung
        ///     1. Series Info
        ///     2. Episode Info
        ///     3. Episode Images
        ///     4. Fanart, Poster and Wide Banner Images
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="forceRefresh"></param>
        public void UpdateAllInfoAndImages(int seriesID, bool forceRefresh, bool downloadImages)
        {
            var repEpisodes = new TvDB_EpisodeRepository();
            var repSeries = new TvDB_SeriesRepository();

            var fileName = string.Format("{0}.xml", ServerSettings.TvDB_Language);

            var docSeries = GetFullSeriesInfo(seriesID);
            if (docSeries.ContainsKey(fileName))
            {
                try
                {
                    // update the series info
                    var xmlDoc = docSeries[fileName];
                    if (xmlDoc != null)
                    {
                        var tvSeries = repSeries.GetByTvDBID(seriesID);
                        if (tvSeries == null)
                            tvSeries = new TvDB_Series();

                        tvSeries.PopulateFromSeriesInfo(xmlDoc);
                        repSeries.Save(tvSeries);
                    }

                    if (downloadImages)
                    {
                        // get all fanart, posters and wide banners
                        if (docSeries.ContainsKey("banners.xml"))
                        {
                            var xmlDocBanners = docSeries["banners.xml"];
                            if (xmlDocBanners != null)
                                DownloadAutomaticImages(xmlDocBanners, seriesID, forceRefresh);
                        }
                    }

                    // update all the episodes and download episode images
                    var episodeItems = xmlDoc["Data"].GetElementsByTagName("Episode");
                    logger.Trace("Found {0} Episode nodes", episodeItems.Count.ToString());

                    var existingEpIds = new List<int>();
                    foreach (XmlNode node in episodeItems)
                    {
                        try
                        {
                            // the episode id
                            var id = int.Parse(node["id"].InnerText.Trim());
                            existingEpIds.Add(id);

                            var ep = repEpisodes.GetByTvDBID(id);
                            if (ep == null)
                                ep = new TvDB_Episode();
                            ep.Populate(node);
                            repEpisodes.Save(ep);

                            //BaseConfig.MyAnimeLog.Write("Refreshing episode info for: {0}", ep.ToString());

                            if (downloadImages)
                            {
                                // download the image for this episode
                                if (!string.IsNullOrEmpty(ep.Filename))
                                {
                                    var fileExists = File.Exists(ep.FullImagePath);
                                    if (!fileExists || (fileExists && forceRefresh))
                                    {
                                        var cmd = new CommandRequest_DownloadImage(ep.TvDB_EpisodeID,
                                            JMMImageType.TvDB_Episode, forceRefresh);
                                        cmd.Save();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.ErrorException("Error in TVDBHelper.GetEpisodes: " + ex, ex);
                        }
                    }

                    // get all the existing tvdb episodes, to see if any have been deleted
                    var allEps = repEpisodes.GetBySeriesID(seriesID);
                    foreach (var oldEp in allEps)
                    {
                        if (!existingEpIds.Contains(oldEp.Id))
                            repEpisodes.Delete(oldEp.TvDB_EpisodeID);
                    }
                }
                catch (Exception ex)
                {
                    logger.ErrorException("Error in TVDBHelper.GetEpisodes: " + ex, ex);
                }
            }
        }


        public static string LinkAniDBTvDB(int animeID, enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber, bool excludeFromWebCache)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var xrefTemps = repCrossRef.GetByAnimeIDEpTypeEpNumber(session, animeID, (int)aniEpType, aniEpNumber);
                if (xrefTemps != null && xrefTemps.Count > 0)
                {
                    foreach (var xrefTemp in xrefTemps)
                    {
                        // delete the existing one if we are updating
                        RemoveLinkAniDBTvDB(xrefTemp.AnimeID, (enEpisodeType)xrefTemp.AniDBStartEpisodeType,
                            xrefTemp.AniDBStartEpisodeNumber,
                            xrefTemp.TvDBID, xrefTemp.TvDBSeasonNumber, xrefTemp.TvDBStartEpisodeNumber);
                    }
                }

                // check if we have this information locally
                // if not download it now
                var repSeries = new TvDB_SeriesRepository();
                var tvSeries = repSeries.GetByTvDBID(tvDBID);
                if (tvSeries == null)
                {
                    // we download the series info here just so that we have the basic info in the
                    // database before the queued task runs later
                    tvSeries = GetSeriesInfoOnline(tvDBID);
                }

                // download and update series info, episode info and episode images
                // will also download fanart, posters and wide banners
                var cmdSeriesEps = new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvDBID, false);
                cmdSeriesEps.Save();

                var xref = repCrossRef.GetByTvDBID(session, tvDBID, tvSeasonNumber, tvEpNumber, animeID, (int)aniEpType,
                    aniEpNumber);
                if (xref == null)
                    xref = new CrossRef_AniDB_TvDBV2();

                xref.AnimeID = animeID;
                xref.AniDBStartEpisodeType = (int)aniEpType;
                xref.AniDBStartEpisodeNumber = aniEpNumber;

                xref.TvDBID = tvDBID;
                xref.TvDBSeasonNumber = tvSeasonNumber;
                xref.TvDBStartEpisodeNumber = tvEpNumber;
                if (tvSeries != null)
                    xref.TvDBTitle = tvSeries.SeriesName;

                if (excludeFromWebCache)
                    xref.CrossRefSource = (int)CrossRefSource.WebCache;
                else
                    xref.CrossRefSource = (int)CrossRefSource.User;

                repCrossRef.Save(xref);

                StatsCache.Instance.UpdateUsingAnime(animeID);

                logger.Trace("Changed tvdb association: {0}", animeID);

                if (!excludeFromWebCache)
                {
                    var req = new CommandRequest_WebCacheSendXRefAniDBTvDB(xref.CrossRef_AniDB_TvDBV2ID);
                    req.Save();
                }

                if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                {
                    var repTraktXrefs = new CrossRef_AniDB_TraktV2Repository();
                    if (repTraktXrefs.GetByAnimeID(animeID).Count == 0)
                    {
                        // check for Trakt associations
                        var cmd2 = new CommandRequest_TraktSearchAnime(animeID, false);
                        cmd2.Save(session);
                    }
                }
            }

            return "";
        }

        public static void LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID)
        {
            var repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
            var xref = repCrossRef.GetByAniDBEpisodeID(aniDBID);
            if (xref == null)
                xref = new CrossRef_AniDB_TvDB_Episode();

            xref.AnimeID = animeID;
            xref.AniDBEpisodeID = aniDBID;
            xref.TvDBEpisodeID = tvDBID;
            repCrossRef.Save(xref);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            logger.Trace("Changed tvdb episode association: {0}", aniDBID);
        }

        // Removes all TVDB information from a series, bringing it back to a blank state.
        public static void RemoveLinkAniDBTvDB(int animeID, enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber)
        {
            var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
            var xref = repCrossRef.GetByTvDBID(tvDBID, tvSeasonNumber, tvEpNumber, animeID, (int)aniEpType, aniEpNumber);
            if (xref == null) return;

            repCrossRef.Delete(xref.CrossRef_AniDB_TvDBV2ID);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            var req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(animeID, (int)aniEpType, aniEpNumber,
                tvDBID, tvSeasonNumber, tvEpNumber);
            req.Save();
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
            var repSeries = new AnimeSeriesRepository();
            var allSeries = repSeries.GetAll();

            var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
            var allCrossRefs = repCrossRef.GetAll();
            var alreadyLinked = new List<int>();
            foreach (var xref in allCrossRefs)
            {
                alreadyLinked.Add(xref.AnimeID);
            }

            foreach (var ser in allSeries)
            {
                if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

                var anime = ser.GetAnime();

                if (anime != null)
                {
                    logger.Trace("Found anime without tvDB association: " + anime.MainTitle);
                    if (!anime.SearchOnTvDB) continue;
                    if (anime.IsTvDBLinkDisabled)
                    {
                        logger.Trace("Skipping scan tvDB link because it is disabled: " + anime.MainTitle);
                        continue;
                    }
                }

                var cmd = new CommandRequest_TvDBSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }
        }

        public static void UpdateAllInfo(bool force)
        {
            var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
            var allCrossRefs = repCrossRef.GetAll();
            var alreadyLinked = new List<int>();
            foreach (var xref in allCrossRefs)
            {
                var cmd = new CommandRequest_TvDBUpdateSeriesAndEpisodes(xref.TvDBID, force);
                cmd.Save();
            }
        }

        /// <summary>
        ///     Used to get a list of TvDB Series ID's that require updating
        /// </summary>
        /// <param name="tvDBIDs">The list Of Series ID's that need to be updated. Pass in an empty list</param>
        /// <returns>The current server time before the update started</returns>
        public string IncrementalTvDBUpdate(ref List<int> tvDBIDs, ref bool tvDBOnline)
        {
            // check if we have record of doing an automated update for the TvDB previously
            // if we have then we have kept a record of the server time and can do a delta update
            // otherwise we need to do a full update and keep a record of the time

            var allTvDBIDs = new List<int>();
            tvDBIDs = new List<int>();
            tvDBOnline = true;

            try
            {
                var repCrossRef = new CrossRef_AniDB_TvDBRepository();
                var repSeries = new AnimeSeriesRepository();

                // record the tvdb server time when we started
                // we record the time now instead of after we finish, to include any possible misses
                var currentTvDBServerTime = CurrentServerTime;
                if (currentTvDBServerTime.Length == 0)
                {
                    tvDBOnline = false;
                    return currentTvDBServerTime;
                }

                foreach (var ser in repSeries.GetAll())
                {
                    var xrefs = ser.GetCrossRefTvDBV2();
                    if (xrefs == null) continue;

                    foreach (var xref in xrefs)
                    {
                        if (!allTvDBIDs.Contains(xref.TvDBID)) allTvDBIDs.Add(xref.TvDBID);
                    }
                }

                // get the time we last did a TvDB update
                // if this is the first time it will be null
                // update the anidb info ever 24 hours
                var repSched = new ScheduledUpdateRepository();
                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);

                var lastServerTime = "";
                if (sched != null)
                {
                    var ts = DateTime.Now - sched.LastUpdate;
                    logger.Trace("Last tvdb info update was {0} hours ago", ts.TotalHours.ToString());
                    if (!string.IsNullOrEmpty(sched.UpdateDetails))
                        lastServerTime = sched.UpdateDetails;

                    // the UpdateDetails field for this type will actually contain the last server time from
                    // TheTvDB that a full update was performed
                }


                // get a list of updates from TvDB since that time
                if (lastServerTime.Length > 0)
                {
                    var seriesList = GetUpdatedSeriesList(lastServerTime);
                    logger.Trace("{0} series have been updated since last download", seriesList.Count.ToString());
                    logger.Trace("{0} TvDB series locally", allTvDBIDs.Count.ToString());

                    foreach (var id in seriesList)
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
                logger.ErrorException("IncrementalTvDBUpdate: " + ex, ex);
                return "";
            }
        }
    }
}