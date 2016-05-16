﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Commands;
using AniDBAPI;
using NHibernate;

namespace JMMServer.Entities
{
	public class AnimeEpisode
	{
		public int AnimeEpisodeID { get; private set; }
		public int AnimeSeriesID { get; set; }
		public int AniDB_EpisodeID { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		public DateTime DateTimeCreated { get; set; }




        public enEpisodeType EpisodeTypeEnum
		{
			get
			{
				return (enEpisodeType)AniDB_Episode.EpisodeType;
			}
		}

		public AniDB_Episode AniDB_Episode
		{
			get
			{
				AniDB_EpisodeRepository repEps = new AniDB_EpisodeRepository();
				return repEps.GetByEpisodeID(this.AniDB_EpisodeID);
			}
		}

		public void Populate(AniDB_Episode anidbEp)
		{
			this.AniDB_EpisodeID = anidbEp.EpisodeID;
			this.DateTimeUpdated = DateTime.Now;
			this.DateTimeCreated = DateTime.Now;
		}

		public AnimeEpisode_User GetUserRecord(int userID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetUserRecord(session, userID);
			}
		}

		public AnimeEpisode_User GetUserRecord(ISession session, int userID)
		{
			AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
			return repEpUser.GetByUserIDAndEpisodeID(session, userID, this.AnimeEpisodeID);
		}

        /*
		public Contract_AnimeEpisode ToContract(bool getFileCount, int userID, AnimeSeries_User seruser)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return ToContract(session, getFileCount, userID, seruser);
			}
		}
		public Contract_AnimeEpisode ToContract(ISession session, bool getFileCount, int userID, AnimeSeries_User seruser)
		{
			Contract_AnimeEpisode contract = new Contract_AnimeEpisode();
			contract.AniDB_EpisodeID = this.AniDB_EpisodeID;
			contract.AnimeEpisodeID = this.AnimeEpisodeID;
			contract.AnimeSeriesID = this.AnimeSeriesID;
			contract.DateTimeUpdated = this.DateTimeUpdated;

			AnimeEpisode_User epuser = this.GetUserRecord(session, userID);

			if (epuser == null)
			{
				contract.IsWatched = 0;
				contract.PlayedCount = 0;
				contract.StoppedCount = 0;
				contract.WatchedCount = 0;
				contract.WatchedDate = null;
			}
			else
			{
				contract.IsWatched = epuser.WatchedCount > 0 ? 1 : 0;
				contract.PlayedCount = epuser.PlayedCount;
				contract.StoppedCount = epuser.StoppedCount;
				contract.WatchedCount = epuser.WatchedCount;
				contract.WatchedDate = epuser.WatchedDate;
			}

			if (seruser == null)
				contract.UnwatchedEpCountSeries = 0;
			else
				contract.UnwatchedEpCountSeries = seruser.UnwatchedEpisodeCount;

			AniDB_Episode aniEp = this.AniDB_Episode;
			contract.AniDB_AirDate = aniEp.AirDateAsDate;
			contract.AniDB_EnglishName = aniEp.EnglishName;
			contract.AniDB_LengthSeconds = aniEp.LengthSeconds;
			contract.AniDB_Rating = aniEp.Rating;
			contract.AniDB_RomajiName = aniEp.RomajiName;
			contract.AniDB_Votes = aniEp.Votes;

			contract.EpisodeNumber = aniEp.EpisodeNumber;
			contract.EpisodeNameRomaji = aniEp.RomajiName;
			contract.EpisodeNameEnglish = aniEp.EnglishName;
			contract.EpisodeType = aniEp.EpisodeType;


			// find the number of files we actually have for this episode
			if (getFileCount)
				contract.LocalFileCount = this.GetVideoLocals(session).Count;

			contract.ReleaseGroups = new List<Contract_AniDBReleaseGroup>();



			return contract;
		}

		public Contract_AnimeEpisode ToContract(int userID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return ToContract(session, userID);
			}
		}

		public Contract_AnimeEpisode ToContract(ISession session, int userID)
		{
			return ToContract(session, true, userID, null);
		}

		public Contract_AnimeEpisode ToContract(AniDB_Episode aniEp, List<VideoLocal> epVids, AnimeEpisode_User epuser, AnimeSeries_User seruser)
		{
			Contract_AnimeEpisode contract = new Contract_AnimeEpisode();
			contract.AniDB_EpisodeID = this.AniDB_EpisodeID;
			contract.AnimeEpisodeID = this.AnimeEpisodeID;
			contract.AnimeSeriesID = this.AnimeSeriesID;
			contract.DateTimeUpdated = this.DateTimeUpdated;

			if (epuser == null)
			{
				contract.IsWatched = 0;
				contract.PlayedCount = 0;
				contract.StoppedCount = 0;
				contract.WatchedCount = 0;
				contract.WatchedDate = null;
			}
			else
			{
				contract.IsWatched = epuser.WatchedCount > 0 ? 1 : 0;
				contract.PlayedCount = epuser.PlayedCount;
				contract.StoppedCount = epuser.StoppedCount;
				contract.WatchedCount = epuser.WatchedCount;
				contract.WatchedDate = epuser.WatchedDate;
			}

			if (seruser == null)
				contract.UnwatchedEpCountSeries = 0;
			else
				contract.UnwatchedEpCountSeries = seruser.UnwatchedEpisodeCount;
			
			contract.AniDB_AirDate = aniEp.AirDateAsDate;
			contract.AniDB_EnglishName = aniEp.EnglishName;
			contract.AniDB_LengthSeconds = aniEp.LengthSeconds;
			contract.AniDB_Rating = aniEp.Rating;
			contract.AniDB_RomajiName = aniEp.RomajiName;
			contract.AniDB_Votes = aniEp.Votes;

			contract.EpisodeNumber = aniEp.EpisodeNumber;
			contract.EpisodeNameRomaji = aniEp.RomajiName;
			contract.EpisodeNameEnglish = aniEp.EnglishName;
			contract.EpisodeType = aniEp.EpisodeType;


			// find the number of files we actually have for this episode
			//contract.LocalFileCount = VideoLocals.Count;
			contract.LocalFileCount = epVids.Count;

			contract.ReleaseGroups = new List<Contract_AniDBReleaseGroup>(); 



			return contract;
		}

		public Contract_AnimeEpisode ToContractOld(AniDB_Episode aniEp)
		{
			Contract_AnimeEpisode contract = new Contract_AnimeEpisode();
			contract.AniDB_EpisodeID = this.AniDB_EpisodeID;
			contract.AnimeEpisodeID = this.AnimeEpisodeID;
			contract.AnimeSeriesID = this.AnimeSeriesID;
			contract.DateTimeUpdated = this.DateTimeUpdated;
			//contract.IsWatched = this.IsWatched;
			//contract.PlayedCount = this.PlayedCount;
			//contract.StoppedCount = this.StoppedCount;
			//contract.WatchedCount = this.WatchedCount;
			//contract.WatchedDate = this.WatchedDate;



			contract.AniDB_AirDate = aniEp.AirDateAsDate;
			contract.AniDB_EnglishName = aniEp.EnglishName;
			contract.AniDB_LengthSeconds = aniEp.LengthSeconds;
			contract.AniDB_Rating = aniEp.Rating;
			contract.AniDB_RomajiName = aniEp.RomajiName;
			contract.AniDB_Votes = aniEp.Votes;

			contract.EpisodeNumber = aniEp.EpisodeNumber;
			contract.EpisodeNameRomaji = aniEp.RomajiName;
			contract.EpisodeNameEnglish = aniEp.EnglishName;
			contract.EpisodeType = aniEp.EpisodeType;


			// find the number of files we actually have for this episode
			contract.LocalFileCount = GetVideoLocals().Count;

			contract.ReleaseGroups = new List<Contract_AniDBReleaseGroup>();



			return contract;
		}
        */
        /// <summary>
        /// Gets the AnimeSeries this episode belongs to
        /// </summary>
        public AnimeSeries GetAnimeSeries()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAnimeSeries(session);
			}
		}

		public AnimeSeries GetAnimeSeries(ISession session)
		{
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			return repSeries.GetByID(session, this.AnimeSeriesID);
		}

		public List<VideoLocal> GetVideoLocals()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetVideoLocals(session);
			}
		}

		public List<VideoLocal> GetVideoLocals(ISession session)
		{
			VideoLocalRepository repVidLocals = new VideoLocalRepository();
			return repVidLocals.GetByAniDBEpisodeID(session, AniDB_EpisodeID);
		}

		public List<CrossRef_File_Episode> FileCrossRefs
		{
			get
			{
				CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();
				return repCrossRefs.GetByEpisodeID(AniDB_EpisodeID);
			}
		}

		public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
		{
			AnimeEpisode_UserRepository repEpisodeUsers = new AnimeEpisode_UserRepository();
			AnimeEpisode_User epUserRecord = this.GetUserRecord(userID);

			if (watched)
			{
				// lets check if an update is actually required
				if (epUserRecord != null)
				{
					if (epUserRecord.WatchedDate.HasValue && watchedDate.HasValue &&
						epUserRecord.WatchedDate.Value.Equals(watchedDate.Value))
					{
						// this will happen when we are adding a new file for an episode where we already had another file
						// and the file/episode was watched already
						return;
					}
				}

				if (epUserRecord == null)
				{
					epUserRecord = new AnimeEpisode_User();
					epUserRecord.PlayedCount = 0;
					epUserRecord.StoppedCount = 0;
					epUserRecord.WatchedCount = 0;
				}
				epUserRecord.AnimeEpisodeID = this.AnimeEpisodeID;
				epUserRecord.AnimeSeriesID = this.AnimeSeriesID;
				epUserRecord.JMMUserID = userID;
				epUserRecord.WatchedCount++;

				if (watchedDate.HasValue)
				{
					if (updateWatchedDate)
						epUserRecord.WatchedDate = watchedDate.Value;
				}

				if (!epUserRecord.WatchedDate.HasValue) epUserRecord.WatchedDate = DateTime.Now;

				repEpisodeUsers.Save(epUserRecord);
			}
			else
			{
				if (epUserRecord != null)
					repEpisodeUsers.Delete(epUserRecord.AnimeEpisode_UserID);
			}
		}



		public List<Contract_VideoDetailed> GetVideoDetailedContracts(int userID)
		{
			VideoLocalRepository repVids = new VideoLocalRepository();
			List<Contract_VideoDetailed> contracts = new List<Contract_VideoDetailed>();

			// get all the cross refs
			foreach (CrossRef_File_Episode xref in FileCrossRefs)
			{
				Contract_VideoDetailed contract = new Contract_VideoDetailed();
				contract.Percentage = xref.Percentage;
				contract.EpisodeOrder = xref.EpisodeOrder;
				contract.CrossRefSource = xref.CrossRefSource;
				contract.AnimeEpisodeID = this.AnimeEpisodeID;

				// get the video file
				// we will assume that it is unique by hash/episodeid
				VideoLocal vid = repVids.GetByHash(xref.Hash);
				if (vid != null)
				{
					contract.VideoLocal_FilePath = vid.FilePath;
					contract.VideoLocal_Hash = vid.Hash;
					contract.VideoLocal_FileSize = vid.FileSize;
					contract.VideoLocalID = vid.VideoLocalID;

					contract.VideoLocal_MD5 = vid.MD5;
					contract.VideoLocal_SHA1 = vid.SHA1;
					contract.VideoLocal_CRC32 = vid.CRC32;
					contract.VideoLocal_HashSource = vid.HashSource;

					VideoLocal_User vidUser = vid.GetUserRecord(userID);
					//AnimeEpisode_User userRecord = this.GetUserRecord(userID);
					if (vidUser == null)
					{
						contract.VideoLocal_IsWatched = 0;
						contract.VideoLocal_WatchedDate = null;
					}
					else
					{
						contract.VideoLocal_IsWatched = 1;
						contract.VideoLocal_WatchedDate = vidUser.WatchedDate;
					}
					contract.VideoLocal_IsIgnored = vid.IsIgnored;
					contract.VideoLocal_IsVariation = vid.IsVariation;

					// Import Folder
					ImportFolder ns = vid.ImportFolder; // to prevent multiple db calls
					contract.ImportFolderID = ns.ImportFolderID;
					contract.ImportFolderLocation = ns.ImportFolderLocation;
					contract.ImportFolderName = ns.ImportFolderName;

					// video info
					VideoInfo vi = vid.VideoInfo; // to prevent multiple db calls
					contract.VideoInfo_AudioBitrate = vi.AudioBitrate;
					contract.VideoInfo_AudioCodec = vi.AudioCodec;
					contract.VideoInfo_Duration = vi.Duration;
					contract.VideoInfo_VideoBitrate = vi.VideoBitrate;
					contract.VideoInfo_VideoBitDepth = vi.VideoBitDepth;
					contract.VideoInfo_VideoCodec = vi.VideoCodec;
					contract.VideoInfo_VideoFrameRate = vi.VideoFrameRate;
					contract.VideoInfo_VideoResolution = vi.VideoResolution;
					contract.VideoInfo_VideoInfoID = vi.VideoInfoID;

					// AniDB File
					AniDB_File anifile = vid.GetAniDBFile(); // to prevent multiple db calls
					if (anifile != null)
					{
						contract.AniDB_Anime_GroupName = anifile.Anime_GroupName;
						contract.AniDB_Anime_GroupNameShort = anifile.Anime_GroupNameShort;
						contract.AniDB_AnimeID = anifile.AnimeID;
						contract.AniDB_CRC = anifile.CRC;
						contract.AniDB_Episode_Rating = anifile.Episode_Rating;
						contract.AniDB_Episode_Votes = anifile.Episode_Votes;
						contract.AniDB_File_AudioCodec = anifile.File_AudioCodec;
						contract.AniDB_File_Description = anifile.File_Description;
						contract.AniDB_File_FileExtension = anifile.File_FileExtension;
						contract.AniDB_File_LengthSeconds = anifile.File_LengthSeconds;
						contract.AniDB_File_ReleaseDate = anifile.File_ReleaseDate;
						contract.AniDB_File_Source = anifile.File_Source;
						contract.AniDB_File_VideoCodec = anifile.File_VideoCodec;
						contract.AniDB_File_VideoResolution = anifile.File_VideoResolution;
						contract.AniDB_FileID = anifile.FileID;
						contract.AniDB_GroupID = anifile.GroupID;
						contract.AniDB_MD5 = anifile.MD5;
						contract.AniDB_SHA1 = anifile.SHA1;
						contract.AniDB_File_FileVersion = anifile.FileVersion;
						contract.AniDB_File_IsCensored = anifile.IsCensored;
						contract.AniDB_File_IsDeprecated = anifile.IsDeprecated;
						contract.AniDB_File_InternalVersion = anifile.InternalVersion;

						// languages
						contract.LanguagesAudio = anifile.LanguagesRAW;
						contract.LanguagesSubtitle = anifile.SubtitlesRAW;
					}
					else
					{
						contract.AniDB_Anime_GroupName = "";
						contract.AniDB_Anime_GroupNameShort = "";
						contract.AniDB_CRC = "";
						contract.AniDB_File_AudioCodec = "";
						contract.AniDB_File_Description = "";
						contract.AniDB_File_FileExtension = "";
						contract.AniDB_File_Source = "";
						contract.AniDB_File_VideoCodec = "";
						contract.AniDB_File_VideoResolution = "";
						contract.AniDB_MD5 = "";
						contract.AniDB_SHA1 = "";
						contract.AniDB_File_FileVersion = 1;

						// languages
						contract.LanguagesAudio = "";
						contract.LanguagesSubtitle = "";
					}

						
						

					AniDB_ReleaseGroup relGroup = vid.ReleaseGroup; // to prevent multiple db calls
					if (relGroup != null)
						contract.ReleaseGroup = relGroup.ToContract();
					else
						contract.ReleaseGroup = null;

					contracts.Add(contract);
				}
			}


			return contracts;
		}

		public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID, bool syncTrakt)
		{
			ToggleWatchedStatus(watched, updateOnline, watchedDate, true, true, userID, syncTrakt);
		}

		public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, bool updateStatsCache, int userID, bool syncTrakt)
		{
			foreach (VideoLocal vid in GetVideoLocals())
			{
				vid.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, updateStatsCache, userID, syncTrakt, true);
			}
		}
	}
}
