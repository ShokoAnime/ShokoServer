using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Commands;
using AniDBAPI;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using Newtonsoft.Json;
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



        public int PlexContractVersion { get; set; }
        public string PlexContractString { get; set; }


        public const int PLEXCONTRACT_VERSION = 3;


        private Video _plexcontract = null;
        internal virtual Video PlexContract
        {
            get
            {
                if ((_plexcontract == null) && PlexContractVersion == PLEXCONTRACT_VERSION)
                {
                    Video vids = Newtonsoft.Json.JsonConvert.DeserializeObject<Video>(PlexContractString);
                    if (vids != null)
                        _plexcontract = vids;
                }
                return _plexcontract;
            }
            set
            {
                _plexcontract = value;
                if (value != null)
                {
                    PlexContractVersion = AnimeGroup_User.PLEXCONTRACT_VERSION;
                    PlexContractString = Newtonsoft.Json.JsonConvert.SerializeObject(PlexContract, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                }
            }
        }


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
            AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
            return repEpUser.GetByUserIDAndEpisodeID(userID, this.AnimeEpisodeID);
        }

        public AnimeEpisode_User GetUserRecord(ISession session, int userID)
		{
			AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
			return repEpUser.GetByUserIDAndEpisodeID(userID, this.AnimeEpisodeID);
		}

        
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

	    public Contract_AnimeEpisode GetUserContract(int userid)
	    {
	        AnimeEpisode_User rr = GetUserRecord(userid);
	        if (rr != null)
	            return rr.Contract;
            rr=new AnimeEpisode_User();
            rr.PlayedCount = 0;
            rr.StoppedCount = 0;
            rr.WatchedCount = 0;
            rr.AnimeEpisodeID = this.AnimeEpisodeID;
            rr.AnimeSeriesID = this.AnimeSeriesID;
            rr.JMMUserID = userid;
	        rr.WatchedDate = null;
            AnimeEpisode_UserRepository repo = new AnimeEpisode_UserRepository();
            repo.Save(rr);
            return rr.Contract;
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
