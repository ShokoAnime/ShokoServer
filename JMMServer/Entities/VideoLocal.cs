using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMServer.Repositories;
using JMMContracts;
using System.IO;
using JMMServer.Commands;
using NLog;
using BinaryNorthwest;
using JMMServer.Commands.MAL;

namespace JMMServer.Entities
{
	public class VideoLocal : IHash
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public int VideoLocalID { get; private set; }
		public string FilePath { get; set; }
		public int ImportFolderID { get; set; }
		public string Hash { get; set; }
		public string CRC32 { get; set; }
		public string MD5 { get; set; }
		public string SHA1 { get; set; }
		public int HashSource { get; set; }
		public long FileSize { get; set; }
		public int IsIgnored { get; set; }
		public DateTime DateTimeUpdated { get; set; }

		public string ToStringDetailed()
		{
			StringBuilder sb = new StringBuilder("");
			sb.Append(Environment.NewLine);
			sb.Append("VideoLocalID: " + VideoLocalID.ToString());
			sb.Append(Environment.NewLine);
			sb.Append("FilePath: " + FilePath);
			sb.Append(Environment.NewLine);
			sb.Append("ImportFolderID: " + ImportFolderID.ToString());
			sb.Append(Environment.NewLine);
			sb.Append("Hash: " + Hash);
			sb.Append(Environment.NewLine);
			sb.Append("FileSize: " + FileSize.ToString());
			sb.Append(Environment.NewLine);

			try
			{
				if (ImportFolder != null)
					sb.Append("ImportFolderLocation: " + ImportFolder.ImportFolderLocation);
			}
			catch (Exception ex)
			{
				sb.Append("ImportFolderLocation: " + ex.ToString());
			}
			
			sb.Append(Environment.NewLine);

			return sb.ToString();
		}

		public string ED2KHash
		{
			get { return Hash; }
			set { Hash = value; }
		}

		public string Info
		{
			get
			{
				if (string.IsNullOrEmpty(FilePath))
					return "";
				return FilePath;
			}
		}

		public ImportFolder ImportFolder
		{
			get
			{
				ImportFolderRepository repNS = new ImportFolderRepository();
				return repNS.GetByID(ImportFolderID);
			}
		}

		public string FullServerPath
		{
			get
			{
				return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
			}
		}

		public AniDB_File AniDBFile
		{
			get
			{
				AniDB_FileRepository repAniFile = new AniDB_FileRepository();
				return repAniFile.GetByHash(Hash);
			}
		}

		public VideoInfo VideoInfo
		{
			get
			{
				VideoInfoRepository repVI = new VideoInfoRepository();
				return repVI.GetByHash(Hash);
			}
		}

		public VideoLocal_User GetUserRecord(int userID)
		{
			VideoLocal_UserRepository repVidUser = new VideoLocal_UserRepository();
			return repVidUser.GetByUserIDAndVideoLocalID(userID, this.VideoLocalID);
		}

		public AniDB_ReleaseGroup ReleaseGroup
		{
			get
			{
				AniDB_File anifile = AniDBFile;
				if (anifile == null) return null;

				AniDB_ReleaseGroupRepository repRG = new AniDB_ReleaseGroupRepository();
				return repRG.GetByGroupID(anifile.GroupID);
			}
		}

		public List<AnimeEpisode> AnimeEpisodes
		{
			get
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				return repEps.GetByHash(Hash);
			}
		}

		public List<CrossRef_File_Episode> EpisodeCrossRefs
		{
			get
			{
				if (Hash.Length == 0) return new List<CrossRef_File_Episode>();

				CrossRef_File_EpisodeRepository rep = new CrossRef_File_EpisodeRepository();
				return rep.GetByHash(Hash);
			}
		}

		private void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
		{
			VideoLocal_UserRepository repVidUsers = new VideoLocal_UserRepository();
			VideoLocal_User vidUserRecord = this.GetUserRecord(userID);
			if (watched)
			{
				if (vidUserRecord == null)
				{
					vidUserRecord = new VideoLocal_User();
					vidUserRecord.WatchedDate = DateTime.Now;
				}
				vidUserRecord.JMMUserID = userID;
				vidUserRecord.VideoLocalID = this.VideoLocalID;

				if (watchedDate.HasValue)
				{
					if (updateWatchedDate)
						vidUserRecord.WatchedDate = watchedDate.Value;
				}

				repVidUsers.Save(vidUserRecord);
			}
			else
			{
				if (vidUserRecord != null)
					repVidUsers.Delete(vidUserRecord.VideoLocal_UserID);
			}
		}

		public void ToggleWatchedStatus(bool watched, int userID)
		{
			ToggleWatchedStatus(watched, true, null, true, true, userID, true, true);
		}

		public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, bool updateStatsCache, int userID, 
			bool scrobbleTrakt, bool updateWatchedDate)
		{
			VideoLocalRepository repVids = new VideoLocalRepository();
			AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
			AniDB_FileRepository repAniFile = new AniDB_FileRepository();
			CrossRef_File_EpisodeRepository repCross = new CrossRef_File_EpisodeRepository();
			VideoLocal_UserRepository repVidUsers = new VideoLocal_UserRepository();
			JMMUserRepository repUsers = new JMMUserRepository();
			AnimeEpisode_UserRepository repEpisodeUsers = new AnimeEpisode_UserRepository();

			JMMUser user = repUsers.GetByID(userID);
			if (user == null) return;

			List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();

			// update the video file to watched
			int mywatched = watched ? 1 : 0;

			if (user.IsAniDBUser == 0)
				SaveWatchedStatus(watched, userID, watchedDate, updateWatchedDate);
			else
			{
				// if the user is AniDB user we also want to update any other AniDB
				// users to keep them in sync
				foreach (JMMUser juser in aniDBUsers)
				{
					if (juser.IsAniDBUser == 1)
						SaveWatchedStatus(watched, juser.JMMUserID, watchedDate, updateWatchedDate);
				}
			}
			

			// now lets find all the associated AniDB_File record if there is one
			if (user.IsAniDBUser == 1)
			{
				AniDB_File aniFile = repAniFile.GetByHash(this.Hash);
				if (aniFile != null)
				{
					aniFile.IsWatched = mywatched;

					if (watched)
					{
						if (watchedDate.HasValue)
							aniFile.WatchedDate = watchedDate;
						else
							aniFile.WatchedDate = DateTime.Now;
					}
					else
						aniFile.WatchedDate = null;


					repAniFile.Save(aniFile, false);

					
				}

				if (updateOnline)
				{
					if ((watched && ServerSettings.AniDB_MyList_SetWatched) || (!watched && ServerSettings.AniDB_MyList_SetUnwatched))
					{
						CommandRequest_UpdateMyListFileStatus cmd = new CommandRequest_UpdateMyListFileStatus(this.Hash, watched, false, 
							watchedDate.HasValue ? Utils.GetAniDBDateAsSeconds(watchedDate) : 0);
						cmd.Save();
					}
				}
			}

			// now find all the episode records associated with this video file
			// but we also need to check if theer are any other files attached to this episode with a watched
			// status, 


			AnimeSeries ser = null;
			// get all files associated with this episode
			List<CrossRef_File_Episode> xrefs = repCross.GetByHash(this.Hash);
			if (watched)
			{
				foreach (CrossRef_File_Episode xref in xrefs)
				{
					// if setting a file watched, only update the episode to watched when the
					// the file is the last file for the episode
					// handles an episode being split across multiple files
					if (xref.Percentage == 100)
					{
						AnimeEpisode ep = repEpisodes.GetByAniDBEpisodeID(xref.EpisodeID);

						if (ep == null) continue;
						if (ser == null) ser = ep.AnimeSeries;

						if (user.IsAniDBUser == 0)
							ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
						else
						{
							// if the user is AniDB user we also want to update any other AniDB
							// users to keep them in sync
							foreach (JMMUser juser in aniDBUsers)
							{
								if (juser.IsAniDBUser == 1)
									ep.SaveWatchedStatus(true, juser.JMMUserID, watchedDate, updateWatchedDate);
							}
						}

						if (scrobbleTrakt && !string.IsNullOrEmpty(ServerSettings.Trakt_Username) && !string.IsNullOrEmpty(ServerSettings.Trakt_Password))
						{
							CommandRequest_TraktShowScrobble cmdScrobble = new CommandRequest_TraktShowScrobble(ep.AnimeEpisodeID);
							cmdScrobble.Save();
						}

						if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) && !string.IsNullOrEmpty(ServerSettings.MAL_Password))
						{
							CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
							cmdMAL.Save();
						}
					}
				}
			}
			else
			{
				// if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
				foreach (CrossRef_File_Episode xrefEp in xrefs)
				{
					AnimeEpisode ep = repEpisodes.GetByAniDBEpisodeID(xrefEp.EpisodeID);
					if (ep == null) continue;
					if (ser == null) ser = ep.AnimeSeries;

					bool anyFilesWatched = false;
					foreach (VideoLocal thisvid in ep.VideoLocals)
					{
						VideoLocal_User vidUserRecordTemp = thisvid.GetUserRecord(userID);
						if (vidUserRecordTemp != null)
						{
							anyFilesWatched = true;
							break;
						}
					}

					if (!anyFilesWatched)
					{
						if (user.IsAniDBUser == 0)
							ep.SaveWatchedStatus(false, userID, watchedDate, true);
						else
						{
							// if the user is AniDB user we also want to update any other AniDB
							// users to keep them in sync
							foreach (JMMUser juser in aniDBUsers)
							{
								if (juser.IsAniDBUser == 1)
									ep.SaveWatchedStatus(false, juser.JMMUserID, watchedDate, true);
							}
						}

						CommandRequest_TraktShowEpisodeUnseen cmdUnseen = new CommandRequest_TraktShowEpisodeUnseen(ep.AnimeEpisodeID);
						cmdUnseen.Save();
					}
				}
			}
			

			// update stats for groups and series
			if (ser != null && updateStats)
			{
				// update all the groups above this series in the heirarchy
				ser.UpdateStats(true, true, true);
				//ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
			}

			if (ser != null && updateStatsCache)
				StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
		}

		public override string ToString()
		{
			return string.Format("{0} --- {1}", FullServerPath, Hash);
		}

		

		public void MoveFileIfRequired()
		{
			// check if this file is in the drop folder
			// otherwise we don't need to move it
			if (this.ImportFolder.IsDropSource == 0) return;

			if (!File.Exists(this.FullServerPath)) return;

			// find the default destination
			ImportFolder destFolder = null;
			ImportFolderRepository repFolders = new ImportFolderRepository();
			foreach (ImportFolder fldr in repFolders.GetAll())
			{
				if (fldr.IsDropDestination == 1)
				{
					destFolder = fldr;
					break;
				}
			}

			if (destFolder == null) return;

			if (!Directory.Exists(destFolder.ImportFolderLocation)) return;

			// we can only move the file if it has an anime associated with it
			List<CrossRef_File_Episode> xrefs = this.EpisodeCrossRefs;
			if (xrefs.Count == 0) return;
			CrossRef_File_Episode xref = xrefs[0];

			// find the series associated with this episode
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AnimeSeries series = repSeries.GetByAnimeID(xref.AnimeID);
			if (series == null) return;

			// find where the other files are stored for this series
			// if there are no other files except for this one, it means we need to create a new location
			bool foundLocation = false;
			string newFullPath = "";

			// sort the episodes by air date, so that we will move the file to the location of the latest episode
			List<AnimeEpisode> allEps = series.AnimeEpisodes;
			List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
			sortCriteria.Add(new SortPropOrFieldAndDirection("AniDB_EpisodeID", true, SortType.eInteger));
			allEps = Sorting.MultiSort<AnimeEpisode>(allEps, sortCriteria);

			foreach (AnimeEpisode ep in allEps)
			{
				foreach (VideoLocal vid in ep.VideoLocals)
				{
					if (vid.VideoLocalID != this.VideoLocalID)
					{
						// make sure this folder is not the drop source
						if (vid.ImportFolder.IsDropSource == 1) continue;

						string thisFileName = vid.FullServerPath;
						string folderName = Path.GetDirectoryName(thisFileName);

						if (Directory.Exists(folderName))
						{
							newFullPath = folderName;
							foundLocation = true;
							break;
						}
					}
				}
				if (foundLocation) break;
			}

			if (!foundLocation)
			{
				// we need to create a new folder
				string newFolderName = Utils.RemoveInvalidFolderNameCharacters(series.Anime.MainTitle);
				newFullPath = Path.Combine(destFolder.ImportFolderLocation, newFolderName);
				if (!Directory.Exists(newFullPath))
					Directory.CreateDirectory(newFullPath);
			}

			int newFolderID = 0;
			string newPartialPath = "";
			string newFullServerPath = Path.Combine(newFullPath, Path.GetFileName(this.FullServerPath));

			DataAccessHelper.GetShareAndPath(newFullServerPath, repFolders.GetAll(), ref newFolderID, ref newPartialPath);

			logger.Info("Moving file from {0} to {1}", this.FullServerPath, newFullServerPath);

			if (File.Exists(newFullServerPath))
			{
				// if the file already exists, we can just delete the source file instead
				// this is safer than deleting and moving
				File.Delete(this.FullServerPath);
			}
			else
			{
				// now move the file
				File.Move(this.FullServerPath, newFullServerPath);
			}

			this.ImportFolderID = newFolderID;
			this.FilePath = newPartialPath;
			VideoLocalRepository repVids = new VideoLocalRepository();
			repVids.Save(this);

		}

		public Contract_VideoLocal ToContract(int userID)
		{
			Contract_VideoLocal contract = new Contract_VideoLocal();
			contract.CRC32 = this.CRC32;
			contract.DateTimeUpdated = this.DateTimeUpdated;
			contract.FilePath = this.FilePath;
			contract.FileSize = this.FileSize;
			contract.Hash = this.Hash;
			contract.HashSource = this.HashSource;
			contract.ImportFolder = this.ImportFolder.ToContract();
			contract.ImportFolderID = this.ImportFolderID;
			contract.IsIgnored = this.IsIgnored;
			contract.MD5 = this.MD5;
			contract.SHA1 = this.SHA1;
			contract.VideoLocalID = this.VideoLocalID;

			VideoLocal_User userRecord = this.GetUserRecord(userID);
			if (userRecord == null)
			{
				contract.IsWatched = 0;
				contract.WatchedDate = null;
			}
			else
			{
				contract.IsWatched = 1;
				contract.WatchedDate = userRecord.WatchedDate;
			}

			return contract;
		}

		public Contract_VideoDetailed ToContractDetailed(int userID)
		{
			Contract_VideoDetailed contract = new Contract_VideoDetailed();

			// get the cross ref episode
			List<CrossRef_File_Episode> xrefs = this.EpisodeCrossRefs;
			if (xrefs.Count == 0) return null;

			contract.Percentage = xrefs[0].Percentage;
			contract.EpisodeOrder = xrefs[0].EpisodeOrder;
			contract.CrossRefSource = xrefs[0].CrossRefSource;
			contract.AnimeEpisodeID = xrefs[0].EpisodeID;

			contract.VideoLocal_FilePath = this.FilePath;
			contract.VideoLocal_Hash = this.Hash;
			contract.VideoLocal_FileSize = this.FileSize;
			contract.VideoLocalID = this.VideoLocalID;
			contract.VideoLocal_IsIgnored = this.IsIgnored;

			VideoLocal_User userRecord = this.GetUserRecord(userID);
			if (userRecord == null)
				contract.VideoLocal_IsWatched = 0;
			else
				contract.VideoLocal_IsWatched = 1;

			// Import Folder
			ImportFolder ns = this.ImportFolder; // to prevent multiple db calls
			if (ns != null)
			{
				contract.ImportFolderID = ns.ImportFolderID;
				contract.ImportFolderLocation = ns.ImportFolderLocation;
				contract.ImportFolderName = ns.ImportFolderName;
			}

			// video info
			VideoInfo vi = this.VideoInfo; // to prevent multiple db calls
			if (vi != null)
			{
				contract.VideoInfo_AudioBitrate = vi.AudioBitrate;
				contract.VideoInfo_AudioCodec = vi.AudioCodec;
				contract.VideoInfo_Duration = vi.Duration;
				contract.VideoInfo_VideoBitrate = vi.VideoBitrate;
				contract.VideoInfo_VideoBitDepth = vi.VideoBitDepth;
				contract.VideoInfo_VideoCodec = vi.VideoCodec;
				contract.VideoInfo_VideoFrameRate = vi.VideoFrameRate;
				contract.VideoInfo_VideoResolution = vi.VideoResolution;
				contract.VideoInfo_VideoInfoID = vi.VideoInfoID;
			}

			// AniDB File
			AniDB_File anifile = this.AniDBFile; // to prevent multiple db calls
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

				// languages
				contract.LanguagesAudio = "";
				contract.LanguagesSubtitle = "";
			}




			AniDB_ReleaseGroup relGroup = this.ReleaseGroup; // to prevent multiple db calls
			if (relGroup != null)
				contract.ReleaseGroup = relGroup.ToContract();
			else
				contract.ReleaseGroup = null;

			return contract;
		}

		public Contract_VideoLocalManualLink ToContractManualLink(int userID)
		{
			Contract_VideoLocalManualLink contract = new Contract_VideoLocalManualLink();
			contract.CRC32 = this.CRC32;
			contract.DateTimeUpdated = this.DateTimeUpdated;
			contract.FilePath = this.FilePath;
			contract.FileSize = this.FileSize;
			contract.Hash = this.Hash;
			contract.HashSource = this.HashSource;
			contract.ImportFolder = this.ImportFolder.ToContract();
			contract.ImportFolderID = this.ImportFolderID;
			contract.IsIgnored = this.IsIgnored;
			contract.MD5 = this.MD5;
			contract.SHA1 = this.SHA1;
			contract.VideoLocalID = this.VideoLocalID;

			VideoLocal_User userRecord = this.GetUserRecord(userID);
			if (userRecord == null)
			{
				contract.IsWatched = 0;
				contract.WatchedDate = null;
			}
			else
			{
				contract.IsWatched = 1;
				contract.WatchedDate = userRecord.WatchedDate;
			}

			return contract;
		}
	}
}
