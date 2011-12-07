using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.Commands;
using NLog;
using System.IO;
using JMMFileHelper;
using JMMServer.Providers.TvDB;
using JMMServer.Providers.MovieDB;
using JMMServer.Providers.TraktTV;
using System.Threading;

namespace JMMServer
{
	public class Importer
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static void RunImport_IntegrityCheck()
		{
			VideoLocalRepository repVidLocals = new VideoLocalRepository();
			AniDB_FileRepository repAniFile = new AniDB_FileRepository();
			AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
			AniDB_AnimeRepository repAniAnime = new AniDB_AnimeRepository();

			// files which have not been hashed yet
			// or files which do not have a VideoInfo record
			List<VideoLocal> filesToHash = repVidLocals.GetVideosWithoutHash();
			Dictionary<int, VideoLocal> dictFilesToHash = new Dictionary<int, VideoLocal>();
			foreach (VideoLocal vl in filesToHash)
			{
				dictFilesToHash[vl.VideoLocalID] = vl;
				CommandRequest_HashFile cmd = new CommandRequest_HashFile(vl.FullServerPath, false);
				cmd.Save();
			}

			List<VideoLocal> filesToRehash = repVidLocals.GetVideosWithoutVideoInfo();
			Dictionary<int, VideoLocal> dictFilesToRehash = new Dictionary<int, VideoLocal>();
			foreach (VideoLocal vl in filesToHash)
			{
				dictFilesToRehash[vl.VideoLocalID] = vl;
				// don't use if it is in the previous list
				if (!dictFilesToHash.ContainsKey(vl.VideoLocalID))
				{
					CommandRequest_HashFile cmd = new CommandRequest_HashFile(vl.FullServerPath, false);
					cmd.Save();
				}
			}

			// files which have been hashed, but don't have an associated episode
			List<VideoLocal> filesWithoutEpisode = repVidLocals.GetVideosWithoutEpisode();
			Dictionary<int, VideoLocal> dictFilesWithoutEpisode = new Dictionary<int, VideoLocal>();
			foreach (VideoLocal vl in filesWithoutEpisode)
				dictFilesWithoutEpisode[vl.VideoLocalID] = vl;


			// check that all the episode data is populated
			List<VideoLocal> filesAll = repVidLocals.GetAll();
			Dictionary<string, VideoLocal> dictFilesAllExisting = new Dictionary<string, VideoLocal>();
			foreach (VideoLocal vl in filesAll)
			{
				dictFilesAllExisting[vl.FullServerPath] = vl;
				// check if it has an episode
				if (dictFilesWithoutEpisode.ContainsKey(vl.VideoLocalID))
				{
					CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID);
					cmd.Save();
					continue;
				}

				// if the file is not manually associated, then check for AniDB_File info
				AniDB_File aniFile = repAniFile.GetByHash(vl.Hash);
				foreach (CrossRef_File_Episode xref in vl.EpisodeCrossRefs)
				{
					if (xref.CrossRefSource != (int)CrossRefSource.AniDB) continue;
					if (aniFile == null)
					{
						CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID);
						cmd.Save();
						continue;
					}
				}

				if (aniFile == null) continue;

				// the cross ref is created before the actually episode data is downloaded
				// so lets check for that
				bool missingEpisodes = false;
				foreach (CrossRef_File_Episode xref in aniFile.EpisodeCrossRefs)
				{
					AniDB_Episode ep = repAniEps.GetByEpisodeID(xref.EpisodeID);
					if (ep == null) missingEpisodes = true;
				}

				if (missingEpisodes)
				{
					// this will then download the anime etc
					CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID);
					cmd.Save();
					continue;
				}
			}
		}

		public static void RunImport_ScanFolder(int importFolderID)
		{
			// get a complete list of files
			List<string> fileList = new List<string>();
			ImportFolderRepository repFolders = new ImportFolderRepository();
			int filesFound = 0, videosFound = 0;
			int i = 0;

			try
			{
				ImportFolder fldr = repFolders.GetByID(importFolderID);
				if (fldr == null) return;

				logger.Debug("ImportFolder: {0} || {1}", fldr.ImportFolderName, fldr.ImportFolderLocation);

				if (Directory.Exists(fldr.ImportFolderLocation))
					fileList.AddRange(Directory.GetFiles(fldr.ImportFolderLocation, "*.*", SearchOption.AllDirectories));

				// get a list of all files in the share
				foreach (string fileName in fileList)
				{
					i++;
					filesFound++;
					logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

					if (!FileHashHelper.IsVideo(fileName)) continue;

					videosFound++;

					CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
					cr_hashfile.Save();

				}
				logger.Debug("Found {0} files", filesFound);
				logger.Debug("Found {0} videos", videosFound);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
		}

		public static void RunImport_DropFolders()
		{
			// get a complete list of files
			List<string> fileList = new List<string>();
			ImportFolderRepository repNetShares = new ImportFolderRepository();
			foreach (ImportFolder share in repNetShares.GetAll())
			{
				if (!share.FolderIsDropSource) continue;

				logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
				try
				{
					if (Directory.Exists(share.ImportFolderLocation))
						fileList.AddRange(Directory.GetFiles(share.ImportFolderLocation, "*.*", SearchOption.AllDirectories));
				}
				catch (Exception ex)
				{
					logger.ErrorException(ex.ToString(), ex);
				}
			}

			// get a list of all the shares we are looking at
			int filesFound = 0, videosFound = 0;
			int i = 0;

			// get a list of all files in the share
			foreach (string fileName in fileList)
			{
				i++;
				filesFound++;
				logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

				if (!FileHashHelper.IsVideo(fileName)) continue;

				videosFound++;

				CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
				cr_hashfile.Save();

			}
			logger.Debug("Found {0} files", filesFound);
			logger.Debug("Found {0} videos", videosFound);
		}

		public static void RunImport_NewFiles()
		{
			VideoLocalRepository repVidLocals = new VideoLocalRepository();

			// first build a list of files that we already know about, as we don't want to process them again
			List<VideoLocal> filesAll = repVidLocals.GetAll();
			Dictionary<string, VideoLocal> dictFilesExisting = new Dictionary<string, VideoLocal>();
			foreach (VideoLocal vl in filesAll)
			{
				dictFilesExisting[vl.FullServerPath] = vl;
			}


			// Steps for processing a file
			// 1. Check if it is a video file
			// 2. Check if we have a VideoLocal record for that file
			// .........

			// get a complete list of files
			List<string> fileList = new List<string>();
			ImportFolderRepository repNetShares = new ImportFolderRepository();
			foreach (ImportFolder share in repNetShares.GetAll())
			{
				logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
				try
				{
					if (Directory.Exists(share.ImportFolderLocation))
						fileList.AddRange(Directory.GetFiles(share.ImportFolderLocation, "*.*", SearchOption.AllDirectories));
				}
				catch (Exception ex)
				{
					logger.ErrorException(ex.ToString(), ex);
				}
			}

			// get a list fo files that we haven't processed before
			List<string> fileListNew = new List<string>();
			foreach (string fileName in fileList)
			{
				if (!dictFilesExisting.ContainsKey(fileName))
					fileListNew.Add(fileName);
			}

			// get a list of all the shares we are looking at
			int filesFound = 0, videosFound = 0;
			int i = 0;

			// get a list of all files in the share
			foreach (string fileName in fileListNew)
			{
				i++;
				filesFound++;
				logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

				if (!FileHashHelper.IsVideo(fileName)) continue;

				videosFound++;

				CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
				cr_hashfile.Save();

			}
			logger.Debug("Found {0} files", filesFound);
			logger.Debug("Found {0} videos", videosFound);
		}

		public static void RunImport_GetImages()
		{
			// AniDB posters
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			foreach (AniDB_Anime anime in repAnime.GetAll())
			{
				if (anime.AnimeID == 8580)
					Console.Write("");

				if (string.IsNullOrEmpty(anime.PosterPath)) continue;

				bool fileExists = File.Exists(anime.PosterPath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(anime.AniDB_AnimeID, JMMImageType.AniDB_Cover, false);
					cmd.Save();
				}
			}

			// TvDB Posters
			TvDB_ImagePosterRepository repTvPosters = new TvDB_ImagePosterRepository();
			foreach (TvDB_ImagePoster tvPoster in repTvPosters.GetAll())
			{
				if (string.IsNullOrEmpty(tvPoster.FullImagePath)) continue;
				bool fileExists = File.Exists(tvPoster.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvPoster.TvDB_ImagePosterID, JMMImageType.TvDB_Cover, false);
					cmd.Save();
				}
			}

			// TvDB Fanart
			TvDB_ImageFanartRepository repTvFanart = new TvDB_ImageFanartRepository();
			foreach (TvDB_ImageFanart tvFanart in repTvFanart.GetAll())
			{
				if (tvFanart.SeriesID == 88651)
					Console.Write("");

				if (string.IsNullOrEmpty(tvFanart.FullImagePath)) continue;
				bool fileExists = File.Exists(tvFanart.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvFanart.TvDB_ImageFanartID, JMMImageType.TvDB_FanArt, false);
					cmd.Save();
				}
			}

			// TvDB Fanart
			TvDB_ImageWideBannerRepository repTvBanners = new TvDB_ImageWideBannerRepository();
			foreach (TvDB_ImageWideBanner tvBanner in repTvBanners.GetAll())
			{
				if (string.IsNullOrEmpty(tvBanner.FullImagePath)) continue;
				bool fileExists = File.Exists(tvBanner.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvBanner.TvDB_ImageWideBannerID, JMMImageType.TvDB_Banner, false);
					cmd.Save();
				}
			}

			// TvDB Episodes
			TvDB_EpisodeRepository repTvEpisodes = new TvDB_EpisodeRepository();
			foreach (TvDB_Episode tvEpisode in repTvEpisodes.GetAll())
			{
				if (string.IsNullOrEmpty(tvEpisode.FullImagePath)) continue;
				bool fileExists = File.Exists(tvEpisode.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvEpisode.TvDB_EpisodeID, JMMImageType.TvDB_Episode, false);
					cmd.Save();
				}
			}

			// MovieDB Posters
			MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
			foreach (MovieDB_Poster moviePoster in repMoviePosters.GetAll())
			{
				if (string.IsNullOrEmpty(moviePoster.FullImagePath)) continue;
				bool fileExists = File.Exists(moviePoster.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(moviePoster.MovieDB_PosterID, JMMImageType.MovieDB_Poster, false);
					cmd.Save();
				}
			}

			// MovieDB Fanart
			MovieDB_FanartRepository repMovieFanarts = new MovieDB_FanartRepository();
			foreach (MovieDB_Fanart movieFanart in repMovieFanarts.GetAll())
			{
				if (string.IsNullOrEmpty(movieFanart.FullImagePath)) continue;
				bool fileExists = File.Exists(movieFanart.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(movieFanart.MovieDB_FanartID, JMMImageType.MovieDB_FanArt, false);
					cmd.Save();
				}
			}

			// Trakt Posters
			Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
			foreach (Trakt_ImagePoster traktPoster in repTraktPosters.GetAll())
			{
				if (string.IsNullOrEmpty(traktPoster.FullImagePath)) continue;
				bool fileExists = File.Exists(traktPoster.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktPoster.Trakt_ImagePosterID, JMMImageType.Trakt_Poster, false);
					cmd.Save();
				}
			}

			// Trakt Fanart
			Trakt_ImageFanartRepository repTraktFanarts = new Trakt_ImageFanartRepository();
			foreach (Trakt_ImageFanart traktFanart in repTraktFanarts.GetAll())
			{
				if (string.IsNullOrEmpty(traktFanart.FullImagePath)) continue;
				bool fileExists = File.Exists(traktFanart.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktFanart.Trakt_ImageFanartID, JMMImageType.Trakt_Fanart, false);
					cmd.Save();
				}
			}

			// Trakt Episode
			Trakt_EpisodeRepository repTraktEpisodes = new Trakt_EpisodeRepository();
			foreach (Trakt_Episode traktEp in repTraktEpisodes.GetAll())
			{
				if (string.IsNullOrEmpty(traktEp.FullImagePath)) continue;
				bool fileExists = File.Exists(traktEp.FullImagePath);
				if (!fileExists)
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktEp.Trakt_EpisodeID, JMMImageType.Trakt_Episode, false);
					cmd.Save();
				}
			}
		}

		public static void RunImport_ScanTvDB()
		{
			TvDBHelper.ScanForMatches();
		}

		public static void RunImport_ScanTrakt()
		{
			TraktTVHelper.ScanForMatches();
		}

		public static void RunImport_ScanMovieDB()
		{
			MovieDBHelper.ScanForMatches();
		}

		public static void RunImport_UpdateTvDB(bool forced)
		{
			TvDBHelper.UpdateAllInfo(forced);
		}

		public static void RemoveRecordsWithoutPhysicalFiles()
		{
			VideoLocalRepository repVidLocals = new VideoLocalRepository();
			CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();

			// get a full list of files
			List<VideoLocal> filesAll = repVidLocals.GetAll();
			foreach (VideoLocal vl in filesAll)
			{
				if (!File.Exists(vl.FullServerPath))
				{
					// delete video local record
					logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", vl.FullServerPath);
					repVidLocals.Delete(vl.VideoLocalID);
				}
			}

			UpdateAllStats();
		}

		public static string DeleteImportFolder(int importFolderID)
		{
			try
			{
				ImportFolderRepository repNS = new ImportFolderRepository();
				ImportFolder ns = repNS.GetByID(importFolderID);

				if (ns == null) return "Could not find Import Folder ID: " + importFolderID;

				// first delete all the files attached  to this import folder
				Dictionary<int, AnimeSeries> affectedSeries = new Dictionary<int, AnimeSeries>();

				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetByImportFolder(importFolderID))
				{
					//Thread.Sleep(5000);
					logger.Info("Deleting video local record: {0}", vid.FullServerPath);

					AnimeSeries ser = null;
					if (vid.AnimeEpisodes.Count > 0)
					{
						ser = vid.AnimeEpisodes[0].AnimeSeries;
						if (ser != null && !affectedSeries.ContainsKey(ser.AnimeSeriesID))
							affectedSeries.Add(ser.AnimeSeriesID, ser);
					}

					repVids.Delete(vid.VideoLocalID);
				}

				// delete any duplicate file records which reference this folder
				DuplicateFileRepository repDupFiles = new DuplicateFileRepository();
				foreach (DuplicateFile df in repDupFiles.GetByImportFolder1(importFolderID))
					repDupFiles.Delete(df.DuplicateFileID);

				foreach (DuplicateFile df in repDupFiles.GetByImportFolder2(importFolderID))
					repDupFiles.Delete(df.DuplicateFileID);

				// delete the import folder
				repNS.Delete(importFolderID);
				ServerInfo.Instance.RefreshImportFolders();

				foreach (AnimeSeries ser in affectedSeries.Values)
				{
					ser.UpdateStats(true, true, true);
					StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
				}

				

				return "";
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return ex.Message;
			}
		}

		public static void UpdateAllStats()
		{
			AnimeGroupRepository repGroups = new AnimeGroupRepository();
			foreach (AnimeGroup grp in repGroups.GetAllTopLevelGroups())
			{
				grp.UpdateStatsFromTopLevel(true, true);
			}
		}

		

		public static void CheckForTvDBUpdates(bool forceRefresh)
		{
			if (ServerSettings.TvDB_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
			int freqHours = Utils.GetScheduledHours(ServerSettings.TvDB_UpdateFrequency);

			// update tvdb info every 12 hours
			ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();

			ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);
			if (sched != null)
			{
				// if we have run this in the last 12 hours and are not forcing it, then exit
				TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
				if (tsLastRun.TotalHours < freqHours)
				{
					if (!forceRefresh) return;
				}
			}

			List<int> tvDBIDs = new List<int>();
			bool tvDBOnline = false;
			string serverTime = JMMService.TvdbHelper.IncrementalTvDBUpdate(ref tvDBIDs, ref tvDBOnline);

			if (tvDBOnline)
			{
				foreach (int tvid in tvDBIDs)
				{
					// download and update series info, episode info and episode images
					// will also download fanart, posters and wide banners
					CommandRequest_TvDBUpdateSeriesAndEpisodes cmdSeriesEps = new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvid, false);
					cmdSeriesEps.Save();
				}
			}

			if (sched == null)
			{
				sched = new ScheduledUpdate();
				sched.UpdateType = (int)ScheduledUpdateType.TvDBInfo;
			}

			sched.LastUpdate = DateTime.Now;
			sched.UpdateDetails = serverTime;
			repSched.Save(sched);

			TvDBHelper.ScanForMatches();
		}

		public static void CheckForCalendarUpdate(bool forceRefresh)
		{
			if (ServerSettings.AniDB_Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
			int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Calendar_UpdateFrequency);

			// update the calendar every 12 hours
			// we will always assume that an anime was downloaded via http first
			ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

			ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
			if (sched != null)
			{
				// if we have run this in the last 12 hours and are not forcing it, then exit
				TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
				if (tsLastRun.TotalHours < freqHours)
				{
					if (!forceRefresh) return;
				}
			}

			CommandRequest_GetCalendar cmd = new CommandRequest_GetCalendar(forceRefresh);
			cmd.Save();
		}

		public static void CheckForAnimeUpdate(bool forceRefresh)
		{
			if (ServerSettings.AniDB_Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
			int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Anime_UpdateFrequency);

			// check for any updated anime info every 12 hours
			ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

			ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
			if (sched != null)
			{
				// if we have run this in the last 12 hours and are not forcing it, then exit
				TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
				if (tsLastRun.TotalHours < freqHours)
				{
					if (!forceRefresh) return;
				}
			}

			CommandRequest_GetUpdated cmd = new CommandRequest_GetUpdated(true);
			cmd.Save();
		}

		public static void CheckForMyListSyncUpdate(bool forceRefresh)
		{
			if (ServerSettings.AniDB_MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
			int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyList_UpdateFrequency);

			// update the calendar every 24 hours
			ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();

			ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
			if (sched != null)
			{
				// if we have run this in the last 24 hours and are not forcing it, then exit
				TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
				logger.Trace("Last AniDB MyList Sync: {0} minutes ago", tsLastRun.TotalMinutes);
				if (tsLastRun.TotalHours < freqHours)
				{
					if (!forceRefresh) return;
				}
			}

			CommandRequest_SyncMyList cmd = new CommandRequest_SyncMyList(forceRefresh);
			cmd.Save();
		}

		public static void CheckForTraktSyncUpdate(bool forceRefresh)
		{
			if (ServerSettings.Trakt_SyncFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
			int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_SyncFrequency);

			// update the calendar every xxx hours
			ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();

			ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
			if (sched != null)
			{
				// if we have run this in the last xxx hours and are not forcing it, then exit
				TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
				logger.Trace("Last Trakt Sync: {0} minutes ago", tsLastRun.TotalMinutes);
				if (tsLastRun.TotalHours < freqHours)
				{
					if (!forceRefresh) return;
				}
			}

			CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(false);
			cmd.Save();
		}

		public static void CheckForTraktAllSeriesUpdate(bool forceRefresh)
		{
			if (ServerSettings.Trakt_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
			int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_UpdateFrequency);

			// update the calendar every xxx hours
			ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();

			ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
			if (sched != null)
			{
				// if we have run this in the last xxx hours and are not forcing it, then exit
				TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
				logger.Trace("Last Trakt Update: {0} minutes ago", tsLastRun.TotalMinutes);
				if (tsLastRun.TotalHours < freqHours)
				{
					if (!forceRefresh) return;
				}
			}

			CommandRequest_TraktUpdateAllSeries cmd = new CommandRequest_TraktUpdateAllSeries(false);
			cmd.Save();
		}
	}
}
