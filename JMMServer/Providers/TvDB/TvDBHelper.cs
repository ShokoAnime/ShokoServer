using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using JMMServer.ImageDownload;
using System.Xml;
using NLog;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Commands;

namespace JMMServer.Providers.TvDB
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
			get { return @"http://www.thetvdb.com/api/" + Constants.TvDBURLs.apiKey + @"/mirrors.xml"; }
		}

		public string urlServerTime
		{
			get { return @"http://www.thetvdb.com/api/Updates.php?type=none"; }
		}

		public string urlUpdatesList
		{
			get { return @"http://www.thetvdb.com/api/Updates.php?type=all&time={0}"; }
		}

		private string urlMirror = "http://thetvdb.com";
		public string UrlMirror
		{
			get
			{
				Init();
				return urlMirror;
			}
		}

		public static string URLMirror
		{
			get
			{
				return "http://thetvdb.com"; // they have said now that this will never change
			}
		}

		public static string GetRootImagesPath()
        {
			return ImageUtils.GetTvDBImagePath();
        }

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
				logger.ErrorException("Error in TVDBHelper.Init: " + ex.ToString(), ex);
            }
        }

		public static bool ConfirmTvDBOnline()
		{
			TvDB_Series tvser = GetSeriesInfoOnline(73255);
			if (tvser == null)
				return false;
			else
				return true;
		}

		public static TvDB_Series GetSeriesInfoOnline(int seriesID)
		{
			try
			{
				//Init();

				string url = string.Format(Constants.TvDBURLs.urlSeriesBaseXML, URLMirror, Constants.TvDBURLs.apiKey, seriesID, ServerSettings.TvDB_Language);
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
					TvDB_SeriesRepository repSeries = new TvDB_SeriesRepository();
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
				logger.ErrorException("Error in TVDBHelper.GetSeriesInfoOnline: " + ex.ToString(), ex);
			}

			return null;
		}

		public XmlDocument GetSeriesBannersOnline(int seriesID)
		{
			try
			{
				Init();

				string url = string.Format(Constants.TvDBURLs.urlBannersXML, urlMirror, Constants.TvDBURLs.apiKey, seriesID);
				logger.Trace("GetSeriesBannersOnline: {0}", url);

				// Search for a series
				string xmlSeries = Utils.DownloadWebPage(url);

				XmlDocument docBanners = new XmlDocument();
				docBanners.LoadXml(xmlSeries);

				return docBanners;

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TVDBHelper.GetSeriesBannersOnline: " + ex.ToString(), ex);
			}

			return null;
		}

		public Dictionary<string, XmlDocument> GetFullSeriesInfo(int seriesID)
		{
			try
			{
				Init();

				string url = string.Format(Constants.TvDBURLs.urlFullSeriesData, urlMirror, Constants.TvDBURLs.apiKey, seriesID, ServerSettings.TvDB_Language);
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
				logger.ErrorException("Error in TVDBHelper.GetFullSeriesInfo: " + ex.ToString(), ex);
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
					logger.ErrorException("Error in TVDBHelper.DecompressZipToXmls: " + e.ToString(), e);
				}
				b.Remove(0, b.Length);
			}
			return docsInZip;
		}
		
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

		public List<TvDBLanguage> GetLanguages()
		{
			List<TvDBLanguage> languages = new List<TvDBLanguage>();

			try
			{
				Init();

				string url = string.Format(Constants.TvDBURLs.urlLanguagesXML, urlMirror, Constants.TvDBURLs.apiKey);
				logger.Trace("GetLanguages: {0}", url);

				// Search for a series
				string xmlSeries = Utils.DownloadWebPage(url);

				XmlDocument docLanguages = new XmlDocument();
				docLanguages.LoadXml(xmlSeries);

				XmlNodeList lanItems = docLanguages["Languages"].GetElementsByTagName("Language");

				//BaseConfig.MyAnimeLog.Write("Found {0} banner nodes", bannerItems.Count);

				if (lanItems.Count <= 0)
					return languages;

				foreach (XmlNode node in lanItems)
				{
					TvDBLanguage lan = new TvDBLanguage();

					lan.Name = node["name"].InnerText.Trim();
					lan.Abbreviation = node["abbreviation"].InnerText.Trim();
					languages.Add(lan);
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TVDBHelper.GetSeriesBannersOnline: " + ex.ToString(), ex);
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
			foreach (object obj in banners)
			{
				if (obj.GetType() == typeof(TvDB_ImageFanart))
				{
					TvDB_ImageFanart img = obj as TvDB_ImageFanart;
					if (ServerSettings.TvDB_AutoFanart && numFanartDownloaded < ServerSettings.TvDB_AutoFanartAmount)
					{
						bool fileExists = File.Exists(img.FullImagePath);
						if (!fileExists || (fileExists && forceDownload))
						{
							CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImageFanartID, JMMImageType.TvDB_FanArt, forceDownload);
							cmd.Save();
							numFanartDownloaded++;
						}
					}
				}

				if (obj.GetType() == typeof(TvDB_ImagePoster))
				{
					TvDB_ImagePoster img = obj as TvDB_ImagePoster;
					if (ServerSettings.TvDB_AutoPosters)
					{
						bool fileExists = File.Exists(img.FullImagePath);
						if (!fileExists || (fileExists && forceDownload))
						{
							CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImagePosterID, JMMImageType.TvDB_Cover, forceDownload);
							cmd.Save();
						}
					}
				}

				if (obj.GetType() == typeof(TvDB_ImageWideBanner))
				{
					TvDB_ImageWideBanner img = obj as TvDB_ImageWideBanner;
					if (ServerSettings.TvDB_AutoWideBanners)
					{
						bool fileExists = File.Exists(img.FullImagePath);
						if (!fileExists || (fileExists && forceDownload))
						{
							CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImageWideBannerID, JMMImageType.TvDB_Banner, forceDownload);
							cmd.Save();
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

				TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
				TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();
				TvDB_ImageWideBannerRepository repWideBanners = new TvDB_ImageWideBannerRepository();

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
						if (bannerType2 == "SEASONWIDE" || bannerType2 == "GRAPHICAL" || bannerType2 == "TEXT" || bannerType2 == "BLANK")
							imageType = JMMImageType.TvDB_Banner;
						else
							imageType = JMMImageType.TvDB_Cover;
					}

					if (imageType == JMMImageType.TvDB_FanArt)
					{
						int id = int.Parse(node["id"].InnerText);
						TvDB_ImageFanart img = repFanart.GetByTvDBID(id);
						if (img == null)
						{
							img = new TvDB_ImageFanart();
							img.Enabled = 1;
						}

						img.Populate(seriesID, node);
						repFanart.Save(img);

						banners.Add(img);
					}

					if (imageType == JMMImageType.TvDB_Banner)
					{
						int id = int.Parse(node["id"].InnerText);

						TvDB_ImageWideBanner img = repWideBanners.GetByTvDBID(id);
						if (img == null)
						{
							img = new TvDB_ImageWideBanner();
							img.Enabled = 1;
						}

						img.Populate(seriesID, node, TvDBImageNodeType.Series);
						repWideBanners.Save(img);

						banners.Add(img);
					}

					if (imageType == JMMImageType.TvDB_Cover)
					{
						int id = int.Parse(node["id"].InnerText);

						TvDB_ImagePoster img = repPosters.GetByTvDBID(id);
						if (img == null)
						{
							img = new TvDB_ImagePoster();
							img.Enabled = 1;
						}

						TvDBImageNodeType nodeType = TvDBImageNodeType.Series;
						if (bannerType == "SEASON") nodeType = TvDBImageNodeType.Season;


						img.Populate(seriesID, node, nodeType);
						repPosters.Save(img);

						banners.Add(img);
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in ParseBanners: " + ex.ToString(), ex);
			}

			return banners;
		}


		public List<TVDBSeriesSearchResult> SearchSeries(string criteria)
		{
			List<TVDBSeriesSearchResult> results = new List<TVDBSeriesSearchResult>();

			try
			{
				Init();

				if (!initialised) return results;

				// Search for a series
				string url = string.Format(Constants.TvDBURLs.urlSeriesSearch, criteria);
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
						TVDBSeriesSearchResult searchResult = new TVDBSeriesSearchResult(series);
						results.Add(searchResult);
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
			}

			return results;
		}


		public static List<int> GetUpdatedSeriesList(string serverTime)
		{
			List<int> seriesList = new List<int>();
			try
			{
				string url = string.Format(Constants.TvDBURLs.urlUpdatesList, URLMirror, serverTime);

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
				logger.ErrorException("Error in GetUpdatedSeriesList: " + ex.ToString(), ex);
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
			TvDB_EpisodeRepository repEpisodes = new TvDB_EpisodeRepository();
			TvDB_SeriesRepository repSeries = new TvDB_SeriesRepository();

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
						TvDB_Series tvSeries = repSeries.GetByTvDBID(seriesID);
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

							TvDB_Episode ep = repEpisodes.GetByTvDBID(id);
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
									bool fileExists = File.Exists(ep.FullImagePath);
									if (!fileExists || (fileExists && forceRefresh))
									{
										CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(ep.TvDB_EpisodeID, JMMImageType.TvDB_Episode, forceRefresh);
										cmd.Save();
									}
								}
							}
						}
						catch (Exception ex)
						{
							logger.ErrorException("Error in TVDBHelper.GetEpisodes: " + ex.ToString(), ex);
						}
					}

					// get all the existing tvdb episodes, to see if any have been deleted
					List<TvDB_Episode> allEps = repEpisodes.GetBySeriesID(seriesID);
					foreach (TvDB_Episode oldEp in allEps)
					{
						if (!existingEpIds.Contains(oldEp.Id))
							repEpisodes.Delete(oldEp.TvDB_EpisodeID);
					}


				}
				catch (Exception ex)
				{
					logger.ErrorException("Error in TVDBHelper.GetEpisodes: " + ex.ToString(), ex);
				}
			}
		}


		public static void LinkAniDBTvDB(int animeID, int tvDBID, int seasonNumber, bool fromWebCache)
		{
			CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
			CrossRef_AniDB_TvDB xrefTemp = repCrossRef.GetByTvDBID(tvDBID, seasonNumber);
			if (xrefTemp != null)
			{
				string msg = string.Format("Not using TvDB link as one already exists {0} ({1}) - {2}", tvDBID, seasonNumber, animeID);
				logger.Warn(msg);
				return;
			}

			// check if we have this information locally
			// if not download it now
			TvDB_SeriesRepository repSeries = new TvDB_SeriesRepository();
			TvDB_Series tvSeries = repSeries.GetByTvDBID(tvDBID);
			if (tvSeries == null)
			{
				// we download the series info here just so that we have the basic info in the
				// database before the queued task runs later
				tvSeries = GetSeriesInfoOnline(tvDBID);
			}

			// download and update series info, episode info and episode images
			// will also download fanart, posters and wide banners
			CommandRequest_TvDBUpdateSeriesAndEpisodes cmdSeriesEps = new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvDBID, false);
			cmdSeriesEps.Save();

			CrossRef_AniDB_TvDB xref = repCrossRef.GetByAnimeID(animeID);
			if (xref == null)
				xref = new CrossRef_AniDB_TvDB();
			
			xref.AnimeID = animeID;
			if (fromWebCache)
				xref.CrossRefSource = (int)CrossRefSource.WebCache;
			else
				xref.CrossRefSource = (int)CrossRefSource.User;

			xref.TvDBID = tvDBID;
			xref.TvDBSeasonNumber = seasonNumber;
			repCrossRef.Save(xref);

			StatsCache.Instance.UpdateUsingAnime(animeID);

			logger.Trace("Changed tvdb association: {0}", animeID);

			CommandRequest_WebCacheSendXRefAniDBTvDB req = new CommandRequest_WebCacheSendXRefAniDBTvDB(xref.CrossRef_AniDB_TvDBID);
			req.Save();
		}

		public static void LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID)
		{
			CrossRef_AniDB_TvDB_EpisodeRepository repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
			CrossRef_AniDB_TvDB_Episode xref = repCrossRef.GetByAniDBEpisodeID(aniDBID);
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
		public static void RemoveLinkAniDBTvDB(AnimeSeries ser)
		{
			CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
			CrossRef_AniDB_TvDB xref = repCrossRef.GetByAnimeID(ser.AniDB_ID);
			if (xref == null) return;
			
			repCrossRef.Delete(xref.CrossRef_AniDB_TvDBID);

			StatsCache.Instance.UpdateUsingAnime(ser.AniDB_ID);

			CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(ser.AniDB_ID);
			req.Save();
		}

		public static void DownloadAllEpisodes()
		{
			CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
			List<CrossRef_AniDB_TvDB> allCrossRefs = repCrossRef.GetAll();

			List<int> tvDBIDs = new List<int>();
			foreach (CrossRef_AniDB_TvDB xref in allCrossRefs)
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

		public static void ScanForMatches()
		{
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			List<AnimeSeries> allSeries = repSeries.GetAll();

			CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
			List<CrossRef_AniDB_TvDB> allCrossRefs = repCrossRef.GetAll();
			List<int> alreadyLinked = new List<int>();
			foreach (CrossRef_AniDB_TvDB xref in allCrossRefs)
			{
				alreadyLinked.Add(xref.AnimeID);
			}

			foreach (AnimeSeries ser in allSeries)
			{
				if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

				AniDB_Anime anime = ser.GetAnime();

				if (anime!= null)
				{
					logger.Trace("Found anime without tvDB association: " + anime.MainTitle);
					if (!anime.SearchOnTvDB) continue;
					if (anime.IsTvDBLinkDisabled)
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
			CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
			List<CrossRef_AniDB_TvDB> allCrossRefs = repCrossRef.GetAll();
			List<int> alreadyLinked = new List<int>();
			foreach (CrossRef_AniDB_TvDB xref in allCrossRefs)
			{
				CommandRequest_TvDBUpdateSeriesAndEpisodes cmd = new CommandRequest_TvDBUpdateSeriesAndEpisodes(xref.TvDBID, force);
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
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

				// record the tvdb server time when we started
				// we record the time now instead of after we finish, to include any possible misses
				string currentTvDBServerTime = CurrentServerTime;
				if (currentTvDBServerTime.Length == 0)
				{
					tvDBOnline = false;
					return currentTvDBServerTime;
				}

				foreach (AnimeSeries ser in repSeries.GetAll())
				{
					CrossRef_AniDB_TvDB xref = ser.GetCrossRefTvDB();
					if (xref == null) continue;

					if (!allTvDBIDs.Contains(xref.TvDBID)) allTvDBIDs.Add(xref.TvDBID);
				}

				// get the time we last did a TvDB update
				// if this is the first time it will be null
				// update the anidb info ever 24 hours
				ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
				ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);

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
				logger.ErrorException("IncrementalTvDBUpdate: "+ ex.ToString(), ex);
				return "";
			}
		}

		/*public static void RemoveEpisodeAssociation(int aniDB_EpisodeID)
		{
			CrossRef_Episode_AniDB_TvDB xref = new CrossRef_Episode_AniDB_TvDB();
			if (xref.Load(aniDB_EpisodeID))
			{
				xref.Delete();
			}
		}

		public static TvDB_Episode PromptForEpisode(int tvDBID, AniDB_Episode aniep)
		{
			// get all the seasons for this episode
			try
			{
				Dictionary<int, int> seasons = new Dictionary<int, int>();
				List<int> seasonNumbers = new List<int>();
				List<string[]> info = TvDB_Episode.GetFromTvDBID(tvDBID);
				if (info.Count == 0)
				{
					GUIDialogOK dlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
					if (null == dlgOK)
						return null;
					dlgOK.SetHeading("Error");
					dlgOK.SetLine(1, string.Empty);
					dlgOK.SetLine(2, "No series found");
					dlgOK.DoModal(GUIWindowManager.ActiveWindow);
					return null;
				}

				IDialogbox dlg2 = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
				dlg2.Reset();
				dlg2.SetHeading("Select Season");
				GUIListItem pItem2 = null;

				for (int i = 0; i < info.Count; i++)
				{
					int id = int.Parse(info[i][0]);
					int season = int.Parse(info[i][1]);
					seasons[id] = season;
					seasonNumbers.Add(season);

					pItem2 = new GUIListItem(string.Format("Season {0}", season.ToString()));
					dlg2.Add(pItem2);
				}

				dlg2.DoModal(GUIWindowManager.ActiveWindow);

				if (dlg2.SelectedId > 0)
				{
					int season = seasonNumbers[dlg2.SelectedId - 1];
					List<TvDB_Episode> eps = TvDB_Episode.GetEpisodesForSeason(tvDBID, season);

					IDialogbox dlg3 = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
					dlg3.Reset();
					dlg3.SetHeading(string.Format("{0} - {1}", aniep.EpisodeNumber, aniep.DefaultEpisodeName));
					GUIListItem pItem3 = null;

					foreach (TvDB_Episode ep in eps)
					{
						pItem3 = new GUIListItem(string.Format("Ep: {0} - {1}", ep.EpisodeNumber, ep.EpisodeName));
						dlg3.Add(pItem3);
					}
					dlg3.DoModal(GUIWindowManager.ActiveWindow);
					if (dlg3.SelectedId > 0)
					{
						TvDB_Episode selEp = eps[dlg3.SelectedId - 1];
						CrossRef_Episode_AniDB_TvDB xref = new CrossRef_Episode_AniDB_TvDB();
						if (!xref.Load(aniep.EpisodeID))
						{
							xref.AniDB_ID = aniep.EpisodeID;
						}
						xref.TvDB_ID = selEp.Id;
						xref.Save();
						XMLService.Send_CrossRef_Episode_AniDB_TvDB(xref);
					}
				}
			}
			catch (Exception ex)
			{
				BaseConfig.MyAnimeLog.Write("Error in PromptForEpisode: {0}", ex.ToString());
				return null;
			}

			return null;
		}*/
	}
}
