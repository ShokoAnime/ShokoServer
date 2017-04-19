using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AniDBAPI;
using AniDBAPI.Commands;
using Shoko.Models;
using Shoko.Models.Azure;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using NLog;
using Shoko.Server.API.core;
using NutzCode.CloudFileSystem;
using Shoko.Server.Commands;
using Directory = System.IO.Directory;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Models.TvDB;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Tasks;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation : IShokoServer
    {
        //TODO Split this file into subfiles with partial class, Move #region funcionality from the interface to those subfiles. Also move this to API folder

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public List<CL_BookmarkedAnime> GetAllBookmarkedAnime()
        {
            List<CL_BookmarkedAnime> baList = new List<CL_BookmarkedAnime>();
            try
            {
                return RepoFactory.BookmarkedAnime.GetAll().Select(a => ModelClients.ToClient(a)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return baList;
        }

        public CL_Response<CL_BookmarkedAnime> SaveBookmarkedAnime(CL_BookmarkedAnime contract)
        {
            CL_Response<CL_BookmarkedAnime> contractRet = new CL_Response<CL_BookmarkedAnime>();
            contractRet.ErrorMessage = "";

            try
            {
                BookmarkedAnime ba = null;
                if (contract.BookmarkedAnimeID != 0)
                {
                    ba = RepoFactory.BookmarkedAnime.GetByID(contract.BookmarkedAnimeID);
                    if (ba == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Bookmark with ID: " +
                                                   contract.BookmarkedAnimeID.ToString();
                        return contractRet;
                    }
                }
                else
                {
                    // if a new record, check if it is allowed
                    BookmarkedAnime baTemp = RepoFactory.BookmarkedAnime.GetByAnimeID(contract.AnimeID);
                    if (baTemp != null)
                    {
                        contractRet.ErrorMessage = "A bookmark with the AnimeID already exists: " +
                                                   contract.AnimeID.ToString();
                        return contractRet;
                    }

                    ba = new BookmarkedAnime();
                }

                ba.AnimeID = contract.AnimeID;
                ba.Priority = contract.Priority;
                ba.Notes = contract.Notes;
                ba.Downloading = contract.Downloading;

                RepoFactory.BookmarkedAnime.Save(ba);

                contractRet.Result = ModelClients.ToClient(ba);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {
                BookmarkedAnime ba = RepoFactory.BookmarkedAnime.GetByID(bookmarkedAnimeID);
                if (ba == null)
                    return "Bookmarked not found";

                RepoFactory.BookmarkedAnime.Delete(bookmarkedAnimeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {
                return ModelClients.ToClient(RepoFactory.BookmarkedAnime.GetByID(bookmarkedAnimeID));
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string UpdateCalendarData()
        {
            try
            {
                Importer.CheckForCalendarUpdate(true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return "";
        }

        public string UpdateTvDBData(int seriesID)
        {
            try
            {
                ShokoService.TvdbHelper.UpdateAllInfoAndImages(seriesID, false, true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return "";
        }

        public string UpdateTraktData(string traktD)
        {
            try
            {
                TraktTVHelper.UpdateAllInfoAndImages(traktD, true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return "";
        }

        public string SyncTraktSeries(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (ser == null) return "Could not find Anime Series";

                CommandRequest_TraktSyncCollectionSeries cmd =
                    new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID,
                        ser.GetSeriesName());
                cmd.Save();

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string UpdateMovieDBData(int movieD)
        {
            try
            {
                MovieDBHelper.UpdateMovieInfo(movieD, true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return "";
        }

        public CL_Changes<CL_GroupFilter> GetGroupFilterChanges(DateTime date)
        {
            CL_Changes<CL_GroupFilter> c = new CL_Changes<CL_GroupFilter>();
            try
            {
                Changes<int> changes = RepoFactory.GroupFilter.GetChangeTracker().GetChanges(date);
                c.ChangedItems = changes.ChangedItems.Select(a => RepoFactory.GroupFilter.GetByID(a).ToClient())
                    .Where(a => a != null)
                    .ToList();
                c.RemovedItems = changes.RemovedItems.ToList();
                c.LastChange = changes.LastChange;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return c;
        }

        public CL_MainChanges GetAllChanges(DateTime date, int userID)
        {
            CL_MainChanges c = new CL_MainChanges();
            try
            {
                List<Changes<int>> changes = ChangeTracker<int>.GetChainedChanges(new List<ChangeTracker<int>>
                {
                    RepoFactory.GroupFilter.GetChangeTracker(),
                    RepoFactory.AnimeGroup.GetChangeTracker(),
                    RepoFactory.AnimeGroup_User.GetChangeTracker(userID),
                    RepoFactory.AnimeSeries.GetChangeTracker(),
                    RepoFactory.AnimeSeries_User.GetChangeTracker(userID)
                }, date);
                c.Filters = new CL_Changes<CL_GroupFilter>();
                c.Filters.ChangedItems = changes[0]
                    .ChangedItems.Select(a => RepoFactory.GroupFilter.GetByID(a).ToClient())
                    .Where(a => a != null)
                    .ToList();
                c.Filters.RemovedItems = changes[0].RemovedItems.ToList();
                c.Filters.LastChange = changes[0].LastChange;

                //Add Group Filter that one of his child changed.
                bool end;
                do
                {
                    end = true;
                    foreach (CL_GroupFilter ag in c.Filters.ChangedItems
                        .Where(a => a.ParentGroupFilterID.HasValue && a.ParentGroupFilterID.Value != 0)
                        .ToList())
                    {
                        if (!c.Filters.ChangedItems.Any(a => a.GroupFilterID == ag.ParentGroupFilterID.Value))
                        {
                            end = false;
                            CL_GroupFilter cag = RepoFactory.GroupFilter.GetByID(ag.ParentGroupFilterID.Value)
                                .ToClient();
                            if (cag != null)
                                c.Filters.ChangedItems.Add(cag);
                        }
                    }
                } while (!end);

                c.Groups = new CL_Changes<CL_AnimeGroup_User>();
                changes[1].ChangedItems.UnionWith(changes[2].ChangedItems);
                changes[1].ChangedItems.UnionWith(changes[2].RemovedItems);
                if (changes[2].LastChange > changes[1].LastChange)
                    changes[1].LastChange = changes[2].LastChange;
                c.Groups.ChangedItems = changes[1]
                    .ChangedItems.Select(a => RepoFactory.AnimeGroup.GetByID(a))
                    .Where(a => a != null)
                    .Select(a => a.GetUserContract(userID))
                    .ToList();


                c.Groups.RemovedItems = changes[1].RemovedItems.ToList();
                c.Groups.LastChange = changes[1].LastChange;
                c.Series = new CL_Changes<CL_AnimeSeries_User>();
                changes[3].ChangedItems.UnionWith(changes[4].ChangedItems);
                changes[3].ChangedItems.UnionWith(changes[4].RemovedItems);
                if (changes[4].LastChange > changes[3].LastChange)
                    changes[3].LastChange = changes[4].LastChange;
                c.Series.ChangedItems = changes[3]
                    .ChangedItems.Select(a => RepoFactory.AnimeSeries.GetByID(a))
                    .Where(a => a != null)
                    .Select(a => a.GetUserContract(userID))
                    .ToList();
                c.Series.RemovedItems = changes[3].RemovedItems.ToList();
                c.Series.LastChange = changes[3].LastChange;
                c.LastChange = c.Filters.LastChange;
                if (c.Groups.LastChange > c.LastChange)
                    c.LastChange = c.Groups.LastChange;
                if (c.Series.LastChange > c.LastChange)
                    c.LastChange = c.Series.LastChange;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return c;
        }



        public List<string> GetAllTagNames()
        {
            List<string> allTagNames = new List<string>();

            try
            {
                DateTime start = DateTime.Now;

                foreach (AniDB_Tag tag in RepoFactory.AniDB_Tag.GetAll())
                {
                    allTagNames.Add(tag.TagName);
                }
                allTagNames.Sort();


                TimeSpan ts = DateTime.Now - start;
                logger.Info("GetAllTagNames  in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return allTagNames;
        }

        public List<string> DirectoriesFromImportFolderPath(int cloudaccountid, string path)
        {
            List<string> result = new List<string>();
            try
            {
                IFileSystem n = null;
                if (cloudaccountid == 0)
                {
                    FileSystemResult<IFileSystem> ff = CloudFileSystemPluginFactory.Instance.List
                        .FirstOrDefault(a => a.Name == "Local File System")
                        ?.Init("", null, null);
                    if (ff.IsOk)
                        n = ff.Result;
                }
                else
                {
                    SVR_CloudAccount cl = RepoFactory.CloudAccount.GetByID(cloudaccountid);
                    if (cl != null)
                        n = cl.FileSystem;
                }
                if (n != null)
                {
                    FileSystemResult<IObject> dirr = n.Resolve(path);
                    if (dirr == null || !dirr.IsOk || dirr.Result is IFile)
                        return null;
                    IDirectory dir = dirr.Result as IDirectory;
                    FileSystemResult fr = dir.Populate();
                    if (!fr.IsOk)
                        return result;
                    return dir.Directories.Select(a => a.FullName).OrderBy(a => a).ToList();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return result;
        }

        public List<CL_CloudAccount> GetCloudProviders()
        {
            List<CL_CloudAccount> ls = new List<CL_CloudAccount>();
            try
            {
                ls.Add(SVR_CloudAccount.CreateLocalFileSystemAccount().ToClient());
                RepoFactory.CloudAccount.GetAll().ForEach(a => ls.Add(a.ToClient()));
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return ls;
        }

        public int TraktScrobble(int animeId, int type, int progress, int status)
        {
            try
            {
                Providers.TraktTV.ScrobblePlayingStatus statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                float progressTrakt;

                switch (status)
                {
                    case (int) Providers.TraktTV.ScrobblePlayingStatus.Start:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                        break;
                    case (int) Providers.TraktTV.ScrobblePlayingStatus.Pause:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Pause;
                        break;
                    case (int) Providers.TraktTV.ScrobblePlayingStatus.Stop:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Stop;
                        break;
                }

                bool isValidProgress = float.TryParse(progress.ToString(), out progressTrakt);

                if (isValidProgress)
                {
                    switch (type)
                    {
                        // Movie
                        case (int) Providers.TraktTV.ScrobblePlayingType.movie:
                            return Providers.TraktTV.TraktTVHelper.Scrobble(
                                Providers.TraktTV.ScrobblePlayingType.movie, animeId.ToString(),
                                statusTraktV2, progressTrakt);
                        // TV episode
                        case (int) Providers.TraktTV.ScrobblePlayingType.episode:
                            return Providers.TraktTV.TraktTVHelper.Scrobble(
                                Providers.TraktTV.ScrobblePlayingType.episode,
                                animeId.ToString(), statusTraktV2, progressTrakt);
                        default:
                            return 500;
                    }
                }
                else
                {
                    return 500;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return 500;
            }
        }

        public CL_ServerStatus GetServerStatus()
        {
            CL_ServerStatus contract = new CL_ServerStatus();

            try
            {
                contract.HashQueueCount = ShokoService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState =
                    ShokoService.CmdProcessorHasher.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.HashQueueStateId = (int) ShokoService.CmdProcessorHasher.QueueState.queueState;
                contract.HashQueueStateParams = ShokoService.CmdProcessorHasher.QueueState.extraParams;

                contract.GeneralQueueCount = ShokoService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState =
                    ShokoService.CmdProcessorGeneral.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.GeneralQueueStateId = (int) ShokoService.CmdProcessorGeneral.QueueState.queueState;
                contract.GeneralQueueStateParams = ShokoService.CmdProcessorGeneral.QueueState.extraParams;

                contract.ImagesQueueCount = ShokoService.CmdProcessorImages.QueueCount;
                contract.ImagesQueueState =
                    ShokoService.CmdProcessorImages.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.ImagesQueueStateId = (int) ShokoService.CmdProcessorImages.QueueState.queueState;
                contract.ImagesQueueStateParams = ShokoService.CmdProcessorImages.QueueState.extraParams;

                contract.IsBanned = ShokoService.AnidbProcessor.IsBanned;
                contract.BanReason = ShokoService.AnidbProcessor.BanTime.ToString();
                contract.BanOrigin = ShokoService.AnidbProcessor.BanOrigin;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contract;
        }

        public CL_Response SaveServerSettings(CL_ServerSettings contractIn)
        {
            CL_Response contract = new CL_Response();
            contract.ErrorMessage = "";

            try
            {
                // validate the settings
                bool anidbSettingsChanged = false;
                if (contractIn.AniDB_ClientPort != ServerSettings.AniDB_ClientPort)
                {
                    anidbSettingsChanged = true;
                    int cport = 0;
                    int.TryParse(contractIn.AniDB_ClientPort, out cport);
                    if (cport <= 0)
                    {
                        contract.ErrorMessage = "AniDB Client Port must be numeric and greater than 0" +
                                                Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_ServerPort != ServerSettings.AniDB_ServerPort)
                {
                    anidbSettingsChanged = true;
                    int sport = 0;
                    int.TryParse(contractIn.AniDB_ServerPort, out sport);
                    if (sport <= 0)
                    {
                        contract.ErrorMessage = "AniDB Server Port must be numeric and greater than 0" +
                                                Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_Username != ServerSettings.AniDB_Username)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_Username))
                    {
                        contract.ErrorMessage = "AniDB User Name must have a value" + Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_Password != ServerSettings.AniDB_Password)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_Password))
                    {
                        contract.ErrorMessage = "AniDB Password must have a value" + Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_ServerAddress != ServerSettings.AniDB_ServerAddress)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_ServerAddress))
                    {
                        contract.ErrorMessage = "AniDB Server Address must have a value" + Environment.NewLine;
                    }
                }

                if (contract.ErrorMessage.Length > 0) return contract;

                ServerSettings.AniDB_ClientPort = contractIn.AniDB_ClientPort;
                ServerSettings.AniDB_Password = contractIn.AniDB_Password;
                ServerSettings.AniDB_ServerAddress = contractIn.AniDB_ServerAddress;
                ServerSettings.AniDB_ServerPort = contractIn.AniDB_ServerPort;
                ServerSettings.AniDB_Username = contractIn.AniDB_Username;
                ServerSettings.AniDB_AVDumpClientPort = contractIn.AniDB_AVDumpClientPort;
                ServerSettings.AniDB_AVDumpKey = contractIn.AniDB_AVDumpKey;

                ServerSettings.AniDB_DownloadRelatedAnime = contractIn.AniDB_DownloadRelatedAnime;
                ServerSettings.AniDB_DownloadReleaseGroups = contractIn.AniDB_DownloadReleaseGroups;
                ServerSettings.AniDB_DownloadReviews = contractIn.AniDB_DownloadReviews;
                ServerSettings.AniDB_DownloadSimilarAnime = contractIn.AniDB_DownloadSimilarAnime;

                ServerSettings.AniDB_MyList_AddFiles = contractIn.AniDB_MyList_AddFiles;
                ServerSettings.AniDB_MyList_ReadUnwatched = contractIn.AniDB_MyList_ReadUnwatched;
                ServerSettings.AniDB_MyList_ReadWatched = contractIn.AniDB_MyList_ReadWatched;
                ServerSettings.AniDB_MyList_SetUnwatched = contractIn.AniDB_MyList_SetUnwatched;
                ServerSettings.AniDB_MyList_SetWatched = contractIn.AniDB_MyList_SetWatched;
                ServerSettings.AniDB_MyList_StorageState = (AniDBFileStatus) contractIn.AniDB_MyList_StorageState;
                ServerSettings.AniDB_MyList_DeleteType = (AniDBFileDeleteType) contractIn.AniDB_MyList_DeleteType;

                ServerSettings.AniDB_MyList_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_MyList_UpdateFrequency;
                ServerSettings.AniDB_Calendar_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_Calendar_UpdateFrequency;
                ServerSettings.AniDB_Anime_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_Anime_UpdateFrequency;
                ServerSettings.AniDB_MyListStats_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_MyListStats_UpdateFrequency;
                ServerSettings.AniDB_File_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_File_UpdateFrequency;

                ServerSettings.AniDB_DownloadCharacters = contractIn.AniDB_DownloadCharacters;
                ServerSettings.AniDB_DownloadCreators = contractIn.AniDB_DownloadCreators;

                // Web Cache
                ServerSettings.WebCache_Address = contractIn.WebCache_Address;
                ServerSettings.WebCache_Anonymous = contractIn.WebCache_Anonymous;
                ServerSettings.WebCache_XRefFileEpisode_Get = contractIn.WebCache_XRefFileEpisode_Get;
                ServerSettings.WebCache_XRefFileEpisode_Send = contractIn.WebCache_XRefFileEpisode_Send;
                ServerSettings.WebCache_TvDB_Get = contractIn.WebCache_TvDB_Get;
                ServerSettings.WebCache_TvDB_Send = contractIn.WebCache_TvDB_Send;
                ServerSettings.WebCache_Trakt_Get = contractIn.WebCache_Trakt_Get;
                ServerSettings.WebCache_Trakt_Send = contractIn.WebCache_Trakt_Send;
                ServerSettings.WebCache_MAL_Get = contractIn.WebCache_MAL_Get;
                ServerSettings.WebCache_MAL_Send = contractIn.WebCache_MAL_Send;
                ServerSettings.WebCache_UserInfo = contractIn.WebCache_UserInfo;

                // TvDB
                ServerSettings.TvDB_AutoFanart = contractIn.TvDB_AutoFanart;
                ServerSettings.TvDB_AutoFanartAmount = contractIn.TvDB_AutoFanartAmount;
                ServerSettings.TvDB_AutoPosters = contractIn.TvDB_AutoPosters;
                ServerSettings.TvDB_AutoPostersAmount = contractIn.TvDB_AutoPostersAmount;
                ServerSettings.TvDB_AutoWideBanners = contractIn.TvDB_AutoWideBanners;
                ServerSettings.TvDB_AutoWideBannersAmount = contractIn.TvDB_AutoWideBannersAmount;
                ServerSettings.TvDB_UpdateFrequency = (ScheduledUpdateFrequency) contractIn.TvDB_UpdateFrequency;
                ServerSettings.TvDB_Language = contractIn.TvDB_Language;

                // MovieDB
                ServerSettings.MovieDB_AutoFanart = contractIn.MovieDB_AutoFanart;
                ServerSettings.MovieDB_AutoFanartAmount = contractIn.MovieDB_AutoFanartAmount;
                ServerSettings.MovieDB_AutoPosters = contractIn.MovieDB_AutoPosters;
                ServerSettings.MovieDB_AutoPostersAmount = contractIn.MovieDB_AutoPostersAmount;

                // Import settings
                ServerSettings.VideoExtensions = contractIn.VideoExtensions;
                ServerSettings.Import_UseExistingFileWatchedStatus = contractIn.Import_UseExistingFileWatchedStatus;
                ServerSettings.AutoGroupSeries = contractIn.AutoGroupSeries;
                ServerSettings.AutoGroupSeriesUseScoreAlgorithm = contractIn.AutoGroupSeriesUseScoreAlgorithm;
                ServerSettings.AutoGroupSeriesRelationExclusions = contractIn.AutoGroupSeriesRelationExclusions;
                ServerSettings.FileQualityFilterEnabled = contractIn.FileQualityFilterEnabled;
                ServerSettings.FileQualityFilterPreferences = contractIn.FileQualityFilterPreferences;
                ServerSettings.RunImportOnStart = contractIn.RunImportOnStart;
                ServerSettings.ScanDropFoldersOnStart = contractIn.ScanDropFoldersOnStart;
                ServerSettings.Hash_CRC32 = contractIn.Hash_CRC32;
                ServerSettings.Hash_MD5 = contractIn.Hash_MD5;
                ServerSettings.Hash_SHA1 = contractIn.Hash_SHA1;

                // Language
                ServerSettings.LanguagePreference = contractIn.LanguagePreference;
                ServerSettings.LanguageUseSynonyms = contractIn.LanguageUseSynonyms;
                ServerSettings.EpisodeTitleSource = (DataSourceType) contractIn.EpisodeTitleSource;
                ServerSettings.SeriesDescriptionSource = (DataSourceType) contractIn.SeriesDescriptionSource;
                ServerSettings.SeriesNameSource = (DataSourceType) contractIn.SeriesNameSource;

                // Trakt
                ServerSettings.Trakt_IsEnabled = contractIn.Trakt_IsEnabled;
                ServerSettings.Trakt_AuthToken = contractIn.Trakt_AuthToken;
                ServerSettings.Trakt_RefreshToken = contractIn.Trakt_RefreshToken;
                ServerSettings.Trakt_TokenExpirationDate = contractIn.Trakt_TokenExpirationDate;
                ServerSettings.Trakt_UpdateFrequency = (ScheduledUpdateFrequency) contractIn.Trakt_UpdateFrequency;
                ServerSettings.Trakt_SyncFrequency = (ScheduledUpdateFrequency) contractIn.Trakt_SyncFrequency;
                ServerSettings.Trakt_DownloadEpisodes = contractIn.Trakt_DownloadEpisodes;
                ServerSettings.Trakt_DownloadFanart = contractIn.Trakt_DownloadFanart;
                ServerSettings.Trakt_DownloadPosters = contractIn.Trakt_DownloadPosters;

                // MAL
                ServerSettings.MAL_Username = contractIn.MAL_Username;
                ServerSettings.MAL_Password = contractIn.MAL_Password;
                ServerSettings.MAL_UpdateFrequency = (ScheduledUpdateFrequency) contractIn.MAL_UpdateFrequency;
                ServerSettings.MAL_NeverDecreaseWatchedNums = contractIn.MAL_NeverDecreaseWatchedNums;

                //Plex
                ServerSettings.Plex_Server = contractIn.Plex_ServerHost;
                ServerSettings.Plex_Libraries = contractIn.Plex_Sections.Length > 0
                    ? contractIn.Plex_Sections.Split(',').Select(int.Parse).ToArray()
                    : new int[0];


                if (anidbSettingsChanged)
                {
                    ShokoService.AnidbProcessor.ForceLogout();
                    ShokoService.AnidbProcessor.CloseConnections();

                    Thread.Sleep(1000);
                    ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                        ServerSettings.AniDB_ServerAddress,
                        ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);
                }
            }
            catch (Exception ex)
            {
                contract.ErrorMessage = ex.Message;
                logger.Error(ex, ex.ToString());
            }
            return contract;
        }

        public CL_ServerSettings GetServerSettings()
        {
            CL_ServerSettings contract = new CL_ServerSettings();

            try
            {
                return ServerSettings.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contract;
        }

        public void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                anime.DisableExternalLinksFlag = flags;
                RepoFactory.AniDB_Anime.Save(anime);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<ImportFolder> GetImportFolders()
        {
            try
            {
                return RepoFactory.ImportFolder.GetAll().Cast<ImportFolder>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<ImportFolder>();
        }

        public CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract)
        {
            CL_Response<ImportFolder> response = new CL_Response<ImportFolder>();
            response.ErrorMessage = "";
            response.Result = null;

            try
            {
                SVR_ImportFolder ns = null;
                if (contract.ImportFolderID != 0)
                {
                    // update
                    ns = RepoFactory.ImportFolder.GetByID(contract.ImportFolderID);
                    if (ns == null)
                    {
                        response.ErrorMessage = "Could not find Import Folder ID: " +
                                                contract.ImportFolderID.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    ns = new SVR_ImportFolder();
                }

                if (string.IsNullOrEmpty(contract.ImportFolderName))
                {
                    response.ErrorMessage = "Must specify an Import Folder name";
                    return response;
                }

                if (string.IsNullOrEmpty(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Must specify an Import Folder location";
                    return response;
                }

                if (contract.CloudID == null && !Directory.Exists(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Cannot find Import Folder location";
                    return response;
                }

                if (contract.ImportFolderID == 0)
                {
                    SVR_ImportFolder nsTemp =
                        RepoFactory.ImportFolder.GetByImportLocation(contract.ImportFolderLocation);
                    if (nsTemp != null)
                    {
                        response.ErrorMessage = "An entry already exists for the specified Import Folder location";
                        return response;
                    }
                }

                if (contract.IsDropDestination == 1 && contract.IsDropSource == 1)
                {
                    response.ErrorMessage = "A folder cannot be a drop source and a drop destination at the same time";
                    return response;
                }

                // check to make sure we don't have multiple drop folders
                IReadOnlyList<SVR_ImportFolder> allFolders = RepoFactory.ImportFolder.GetAll();

                if (contract.IsDropDestination == 1)
                {
                    foreach (SVR_ImportFolder imf in allFolders)
                    {
                        if (contract.CloudID == imf.CloudID && imf.IsDropDestination == 1 &&
                            (contract.ImportFolderID == 0 || (contract.ImportFolderID != imf.ImportFolderID)))
                        {
                            imf.IsDropDestination = 0;
                            RepoFactory.ImportFolder.Save(imf);
                        }
                    }
                }

                ns.ImportFolderName = contract.ImportFolderName;
                ns.ImportFolderLocation = contract.ImportFolderLocation;
                ns.IsDropDestination = contract.IsDropDestination;
                ns.IsDropSource = contract.IsDropSource;
                ns.IsWatched = contract.IsWatched;
                ns.ImportFolderType = contract.ImportFolderType;
                ns.CloudID = contract.CloudID.HasValue && contract.CloudID == 0 ? null : contract.CloudID;
                ;
                RepoFactory.ImportFolder.Save(ns);

                response.Result = ns;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ServerInfo.Instance.RefreshImportFolders();
                });
                ShokoServer.StopWatchingFiles();
                ShokoServer.StartWatchingFiles();

                return response;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string DeleteImportFolder(int importFolderID)
        {
            ShokoServer.DeleteImportFolder(importFolderID);
            return "";
        }

        public void RunImport()
        {
            ShokoServer.RunImport();
        }

        public void SyncHashes()
        {
            ShokoServer.SyncHashes();
        }

        public void ScanDropFolders()
        {
            Importer.RunImport_DropFolders();
        }

        public void ScanFolder(int importFolderID)
        {
            ShokoServer.ScanFolder(importFolderID);
        }

        public void RemoveMissingFiles()
        {
            ShokoServer.RemoveMissingFiles();
        }

        public void SyncMyList()
        {
            ShokoServer.SyncMyList();
        }

        public void SyncVotes()
        {
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
        }

        public void SyncMALUpload()
        {
            CommandRequest_MALUploadStatusToMAL cmd = new CommandRequest_MALUploadStatusToMAL();
            cmd.Save();
        }

        public void SyncMALDownload()
        {
            CommandRequest_MALDownloadStatusFromMAL cmd = new CommandRequest_MALDownloadStatusFromMAL();
            cmd.Save();
        }

        public void SetCommandProcessorHasherPaused(bool paused)
        {
            ShokoService.CmdProcessorHasher.Paused = paused;
        }

        public void SetCommandProcessorGeneralPaused(bool paused)
        {
            ShokoService.CmdProcessorGeneral.Paused = paused;
        }

        public void SetCommandProcessorImagesPaused(bool paused)
        {
            ShokoService.CmdProcessorImages.Paused = paused;
        }

        public void ClearHasherQueue()
        {
            try
            {
                ShokoService.CmdProcessorHasher.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestHasher());

                ShokoService.CmdProcessorHasher.Init();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void ClearImagesQueue()
        {
            try
            {
                ShokoService.CmdProcessorImages.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestImages());
                ShokoService.CmdProcessorImages.Init();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void ClearGeneralQueue()
        {
            try
            {
                ShokoService.CmdProcessorGeneral.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestGeneral());
                ShokoService.CmdProcessorGeneral.Init();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void RehashFile(int videoLocalID)
        {
            SVR_VideoLocal vl = RepoFactory.VideoLocal.GetByID(videoLocalID);

            if (vl != null)
            {
                SVR_VideoLocal_Place pl = vl.GetBestVideoLocalPlace();
                if (pl == null)
                {
                    logger.Error("Unable to hash videolocal with id = {videoLocalID}, it has no assigned place");
                    return;
                }
                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(pl.FullServerPath, true);
                cr_hashfile.Save();
            }
        }

        public string TestAniDBConnection()
        {
            string log = "";
            try
            {
                log += "Disposing..." + Environment.NewLine;
                ShokoService.AnidbProcessor.ForceLogout();
                ShokoService.AnidbProcessor.CloseConnections();
                Thread.Sleep(1000);

                log += "Init..." + Environment.NewLine;
                ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                    ServerSettings.AniDB_ServerAddress,
                    ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

                log += "Login..." + Environment.NewLine;
                if (ShokoService.AnidbProcessor.Login())
                {
                    log += "Login Success!" + Environment.NewLine;
                    log += "Logout..." + Environment.NewLine;
                    ShokoService.AnidbProcessor.ForceLogout();
                    log += "Logged out" + Environment.NewLine;
                }
                else
                {
                    log += "Login FAILED!" + Environment.NewLine;
                }

                return log;
            }
            catch (Exception ex)
            {
                log += ex.Message + Environment.NewLine;
            }

            return log;
        }

        public string EnterTraktPIN(string pin)
        {
            try
            {
                return TraktTVHelper.EnterTraktPIN(pin);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in EnterTraktPIN: " + ex.ToString());
                return ex.Message;
            }
        }

        public string TestMALLogin()
        {
            try
            {
                if (MALHelper.VerifyCredentials())
                    return "";

                return "Login is not valid";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TestMALLogin: " + ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<bool> TraktFriendRequestDeny(string friendUsername)
        {
            return new CL_Response<bool> {Result = false};
            /*
			try
			{
				return TraktTVHelper.FriendRequestDeny(friendUsername, ref returnMessage);
			}
			catch (Exception ex)
			{
				logger.Error( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
				returnMessage = ex.Message;
				return false;
			}*/
        }

        public CL_Response<bool> TraktFriendRequestApprove(string friendUsername)
        {
            return new CL_Response<bool> {Result = false};
            /*
			try
			{
				return TraktTVHelper.FriendRequestApprove(friendUsername, ref returnMessage);
			}
			catch (Exception ex)
			{
				logger.Error( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
				returnMessage = ex.Message;
				return false;
			}*/
        }

        public string RenameAllGroups()
        {
            try
            {
                SVR_AnimeGroup.RenameAllGroups();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return string.Empty;
        }

        public List<string> GetAllUniqueVideoQuality()
        {
            try
            {
                return RepoFactory.Adhoc.GetAllVideoQuality();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<string>();
            }
        }

        public List<string> GetAllUniqueAudioLanguages()
        {
            try
            {
                return RepoFactory.Adhoc.GetAllUniqueAudioLanguages();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<string>();
            }
        }

        public List<string> GetAllUniqueSubtitleLanguages()
        {
            try
            {
                return RepoFactory.Adhoc.GetAllUniqueSubtitleLanguages();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<string>();
            }
        }

        /// <summary>
        /// Delets the VideoLocal record and the associated physical file
        /// </summary>
        /// <param name="videoLocalID"></param>
        /// <returns></returns>
        public string DeleteVideoLocalPlaceAndFile(int videolocalplaceid)
        {
            try
            {
                SVR_VideoLocal_Place place = RepoFactory.VideoLocalPlace.GetByID(videolocalplaceid);
                if ((place == null) || (place.VideoLocal == null))
                    return "Database entry does not exist";
                SVR_VideoLocal vid = place.VideoLocal;
                logger.Info("Deleting video local place record and file: {0}", (place.FullServerPath ?? place.VideoLocal_Place_ID.ToString()));

                IFileSystem fileSystem = place.ImportFolder?.FileSystem;
                if (fileSystem == null)
                {
                    logger.Error("Unable to delete file, filesystem not found. Removing record.");
                    place.RemoveRecord();
                    return "Unable to delete file, filesystem not found. Removing record.";
                }
                if (place.FullServerPath == null)
                {
                    logger.Error("Unable to delete file, FullServerPath is null. Removing record.");
                    place.RemoveRecord();
                    return "Unable to delete file, FullServerPath is null. Removing record.";
                }
                FileSystemResult<IObject> fr = fileSystem.Resolve(place.FullServerPath);
                if (fr == null || !fr.IsOk)
                {
                    logger.Error($"Unable to find file. Removing Record: {place.FullServerPath}");
                    place.RemoveRecord();
                    return $"Unable to find file. Removing record.";
                }
                IFile file = fr.Result as IFile;
                if (file == null)
                {
                    logger.Error($"Seems '{place.FullServerPath}' is a directory.");
                    place.RemoveRecord();
                    return $"Seems '{place.FullServerPath}' is a directory.";
                }
                FileSystemResult fs = file.Delete(false);
                if (fs == null || !fs.IsOk)
                {
                    logger.Error($"Unable to delete file '{place.FullServerPath}'");
                    return $"Unable to delete file '{place.FullServerPath}'";
                }
                place.RemoveRecord();
                // For deletion of files from Trakt, we will rely on the Daily sync

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
        {
            CL_AniDB_AnimeCrossRefs result = new CL_AniDB_AnimeCrossRefs
            {
                CrossRef_AniDB_TvDB = new List<CrossRef_AniDB_TvDBV2>(),
                TvDBSeries = new List<TvDB_Series>(),
                TvDBEpisodes = new List<TvDB_Episode>(),
                TvDBImageFanarts = new List<TvDB_ImageFanart>(),
                TvDBImagePosters = new List<TvDB_ImagePoster>(),
                TvDBImageWideBanners = new List<TvDB_ImageWideBanner>(),

                CrossRef_AniDB_MovieDB = null,
                MovieDBMovie = null,
                MovieDBFanarts = new List<MovieDB_Fanart>(),
                MovieDBPosters = new List<MovieDB_Poster>(),

                CrossRef_AniDB_MAL = null,

                CrossRef_AniDB_Trakt = new List<CrossRef_AniDB_TraktV2>(),
                TraktShows = new List<CL_Trakt_Show>(),
                TraktImageFanarts = new List<Trakt_ImageFanart>(),
                TraktImagePosters = new List<Trakt_ImagePoster>(),
                AnimeID = animeID
            };

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime == null) return result;


                    // TvDB
                    foreach (CrossRef_AniDB_TvDBV2 xref in anime.GetCrossRefTvDBV2())
                    {
                        result.CrossRef_AniDB_TvDB.Add(xref);

                        TvDB_Series ser = RepoFactory.TvDB_Series.GetByTvDBID(sessionWrapper, xref.TvDBID);
                        if (ser != null)
                            result.TvDBSeries.Add(ser);

                        foreach (TvDB_Episode ep in anime.GetTvDBEpisodes())
                            result.TvDBEpisodes.Add(ep);

                        foreach (TvDB_ImageFanart fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(sessionWrapper,
                            xref.TvDBID))
                            result.TvDBImageFanarts.Add(fanart);

                        foreach (TvDB_ImagePoster poster in RepoFactory.TvDB_ImagePoster.GetBySeriesID(sessionWrapper,
                            xref.TvDBID))
                            result.TvDBImagePosters.Add(poster);

                        foreach (TvDB_ImageWideBanner banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(xref
                            .TvDBID))
                            result.TvDBImageWideBanners.Add(banner);
                    }

                    // Trakt


                    foreach (CrossRef_AniDB_TraktV2 xref in anime.GetCrossRefTraktV2())
                    {
                        result.CrossRef_AniDB_Trakt.Add(xref);

                        Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(session, xref.TraktID);
                        if (show != null)
                        {
                            result.TraktShows.Add(show.ToClient());

                            foreach (Trakt_ImageFanart fanart in RepoFactory.Trakt_ImageFanart.GetByShowID(session,
                                show.Trakt_ShowID))
                                result.TraktImageFanarts.Add(fanart);

                            foreach (Trakt_ImagePoster poster in RepoFactory.Trakt_ImagePoster.GetByShowID(session,
                                show.Trakt_ShowID)
                            )
                                result.TraktImagePosters.Add(poster);
                        }
                    }


                    // MovieDB
                    CrossRef_AniDB_Other xrefMovie = anime.GetCrossRefMovieDB();
                    if (xrefMovie == null)
                        result.CrossRef_AniDB_MovieDB = null;
                    else
                        result.CrossRef_AniDB_MovieDB = xrefMovie;


                    result.MovieDBMovie = anime.GetMovieDBMovie();


                    foreach (MovieDB_Fanart fanart in anime.GetMovieDBFanarts())
                    {
                        if (fanart.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBFanarts.Add(fanart);
                    }

                    foreach (MovieDB_Poster poster in anime.GetMovieDBPosters())
                    {
                        if (poster.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBPosters.Add(poster);
                    }

                    // MAL
                    List<CrossRef_AniDB_MAL> xrefMAL = anime.GetCrossRefMAL();
                    if (xrefMAL == null)
                        result.CrossRef_AniDB_MAL = null;
                    else
                    {
                        result.CrossRef_AniDB_MAL = new List<Shoko.Models.Server.CrossRef_AniDB_MAL>();
                        foreach (CrossRef_AniDB_MAL xrefTemp in xrefMAL)
                            result.CrossRef_AniDB_MAL.Add(xrefTemp);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return result;
            }
        }

        public string LinkToPlex(int userID)
        {
            JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            return PlexHelper.GetForUser(user).Authenticate();
        }

        public bool IsPlexAuthenticated(int userID)
        {
            JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            return PlexHelper.GetForUser(user).IsAuthenticated;
        }

        public bool RemovePlexAuth(int userID)
        {
            JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            PlexHelper.GetForUser(user).InvalidateToken();
            return true;
        }

        public string EnableDisableImage(bool enabled, int imageID, int imageType)
        {
            try
            {
                JMMImageType imgType = (JMMImageType) imageType;

                switch (imgType)
                {
                    case JMMImageType.AniDB_Cover:
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageID);
                        if (anime == null) return "Could not find anime";
                        anime.ImageEnabled = enabled ? 1 : 0;
                        RepoFactory.AniDB_Anime.Save(anime);
                        break;

                    case JMMImageType.TvDB_Banner:
                        TvDB_ImageWideBanner banner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);
                        if (banner == null) return "Could not find image";
                        banner.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImageWideBanner.Save(banner);
                        break;

                    case JMMImageType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);
                        if (poster == null) return "Could not find image";
                        poster.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImagePoster.Save(poster);
                        break;

                    case JMMImageType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);
                        if (fanart == null) return "Could not find image";
                        fanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImageFanart.Save(fanart);
                        break;

                    case JMMImageType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(imageID);
                        if (moviePoster == null) return "Could not find image";
                        moviePoster.Enabled = enabled ? 1 : 0;
                        RepoFactory.MovieDB_Poster.Save(moviePoster);
                        break;

                    case JMMImageType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(imageID);
                        if (movieFanart == null) return "Could not find image";
                        movieFanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.MovieDB_Fanart.Save(movieFanart);
                        break;

                    case JMMImageType.Trakt_Poster:
                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(imageID);
                        if (traktPoster == null) return "Could not find image";
                        traktPoster.Enabled = enabled ? 1 : 0;
                        RepoFactory.Trakt_ImagePoster.Save(traktPoster);
                        break;

                    case JMMImageType.Trakt_Fanart:
                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(imageID);
                        if (traktFanart == null) return "Could not find image";
                        traktFanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.Trakt_ImageFanart.Save(traktFanart);
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType)
        {
            try
            {
                JMMImageType imgType = (JMMImageType) imageType;
                ImageSizeType sizeType = ImageSizeType.Poster;

                switch (imgType)
                {
                    case JMMImageType.AniDB_Cover:
                    case JMMImageType.TvDB_Cover:
                    case JMMImageType.MovieDB_Poster:
                    case JMMImageType.Trakt_Poster:
                        sizeType = ImageSizeType.Poster;
                        break;

                    case JMMImageType.TvDB_Banner:
                        sizeType = ImageSizeType.WideBanner;
                        break;

                    case JMMImageType.TvDB_FanArt:
                    case JMMImageType.MovieDB_FanArt:
                    case JMMImageType.Trakt_Fanart:
                        sizeType = ImageSizeType.Fanart;
                        break;
                }

                if (!isDefault)
                {
                    // this mean we are removing an image as deafult
                    // which esssential means deleting the record

                    AniDB_Anime_DefaultImage img =
                        RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, (int) sizeType);
                    if (img != null)
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(img.AniDB_Anime_DefaultImageID);
                }
                else
                {
                    // making the image the default for it's type (poster, fanart etc)
                    AniDB_Anime_DefaultImage img =
                        RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, (int) sizeType);
                    if (img == null)
                        img = new AniDB_Anime_DefaultImage();

                    img.AnimeID = animeID;
                    img.ImageParentID = imageID;
                    img.ImageParentType = (int) imgType;
                    img.ImageType = (int) sizeType;
                    RepoFactory.AniDB_Anime_DefaultImage.Save(img);
                }

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                RepoFactory.AnimeSeries.Save(series, false);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #region Web Cache Admin

        public bool IsWebCacheAdmin()
        {
            try
            {
                string res = AzureWebAPI.Admin_AuthUser();
                return string.IsNullOrEmpty(res);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }
        }

        public Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType)
        {
            try
            {
                AzureLinkType lType = (AzureLinkType) linkType;
                Azure_AnimeLink link = null;

                switch (lType)
                {
                    case AzureLinkType.TvDB:
                        link = AzureWebAPI.Admin_GetRandomTvDBLinkForApproval();
                        break;
                    case AzureLinkType.Trakt:
                        link = AzureWebAPI.Admin_GetRandomTraktLinkForApproval();
                        break;
                }


                if (link != null)
                    return link;

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<Azure_AdminMessage> GetAdminMessages()
        {
            try
            {
                return ServerInfo.Instance.AdminMessages?.ToList() ?? new List<Azure_AdminMessage>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Azure_AdminMessage>();
            }
        }

        #region Admin - TvDB

        public string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return AzureWebAPI.Admin_Revoke_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Sends the current user's TvDB links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTvDBLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                List<CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<Azure_CrossRef_AniDB_TvDB> results =
                    AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (Azure_CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            foundLinks = true;
                            break;
                        }
                    }
                }
                if (foundLinks) return "Links already exist, please approve them instead";

                // send the links to the web cache
                foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                if (results != null)
                {
                    List<Azure_CrossRef_AniDB_TvDB> linksToApprove =
                        new List<Azure_CrossRef_AniDB_TvDB>();
                    foreach (Azure_CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (Azure_CrossRef_AniDB_TvDB xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(
                            xref.CrossRef_AniDB_TvDBV2ID);
                    }
                    return "Success";
                }
                else
                    return "Failure to send links to web cache";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region Admin - Trakt

        public string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
        {
            try
            {
                return AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
        {
            try
            {
                return AzureWebAPI.Admin_Revoke_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Sends the current user's Trakt links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTraktLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                List<CrossRef_AniDB_TraktV2> xrefs = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<Azure_CrossRef_AniDB_Trakt> results =
                    AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (Azure_CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            foundLinks = true;
                            break;
                        }
                    }
                }
                if (foundLinks) return "Links already exist, please approve them instead";

                // send the links to the web cache
                foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                if (results != null)
                {
                    List<Azure_CrossRef_AniDB_Trakt> linksToApprove =
                        new List<Azure_CrossRef_AniDB_Trakt>();
                    foreach (Azure_CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (Azure_CrossRef_AniDB_Trakt xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(
                            xref.CrossRef_AniDB_TraktV2ID);
                    }
                    return "Success";
                }
                else
                    return "Failure to send links to web cache";

                //return JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #endregion

        #region TvDB

        public List<Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                if (isAdmin)
                    return AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                else
                    return AzureWebAPI.Get_CrossRefAniDBTvDB(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Azure_CrossRef_AniDB_TvDB>();
            }
        }


        public List<CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID).Cast<CrossRef_AniDB_TvDBV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetTVDBCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }


        public List<TVDB_Series_Search_Response> SearchTheTvDB(string criteria)
        {
            try
            {
                return ShokoService.TvdbHelper.SearchSeries(criteria);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TVDB_Series_Search_Response>();
            }
        }


        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh data from TvDB
                ShokoService.TvdbHelper.UpdateAllInfoAndImages(seriesID, true, false);

                seasonNumbers = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(seriesID);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return seasonNumbers;
            }
        }

        public string LinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber, int? crossRef_AniDB_TvDBV2ID)
        {
            try
            {
                CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvDBID, tvSeasonNumber,
                    tvEpNumber, animeID, aniEpType,
                    aniEpNumber);
                if (xref != null && !crossRef_AniDB_TvDBV2ID.HasValue)
                {
                    string msg = string.Format("You have already linked Anime ID {0} to this TvDB show/season/ep",
                        xref.AnimeID);
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this TvDB show/season/ep",
                            anime.MainTitle,
                            xref.AnimeID);
                    }
                    return msg;
                }

                // we don't need to proactively remove the link here anymore, as all links are removed when it is not marked as additive

                CommandRequest_LinkAniDBTvDB cmdRequest = new CommandRequest_LinkAniDBTvDB(animeID,
                    (enEpisodeType) aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber, false, !crossRef_AniDB_TvDBV2ID.HasValue);
                cmdRequest.Save();

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID)
        {
            try
            {
                TvDBHelper.LinkAniDBTvDBEpisode(aniDBID, tvDBID, animeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        /// <summary>
        /// Removes all tvdb links for one anime
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public string RemoveLinkAniDBTvDBForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                List<CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID);
                if (xrefs == null) return "";

                foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
                {
                    // check if there are default images used associated
                    List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                    foreach (AniDB_Anime_DefaultImage image in images)
                    {
                        if (image.ImageParentType == (int) JMMImageType.TvDB_Banner ||
                            image.ImageParentType == (int) JMMImageType.TvDB_Cover ||
                            image.ImageParentType == (int) JMMImageType.TvDB_FanArt)
                        {
                            if (image.ImageParentID == xref.TvDBID)
                                RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                        }
                    }

                    TvDBHelper.RemoveLinkAniDBTvDB(xref.AnimeID, (enEpisodeType) xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) JMMImageType.TvDB_Banner ||
                        image.ImageParentType == (int) JMMImageType.TvDB_Cover ||
                        image.ImageParentType == (int) JMMImageType.TvDB_FanArt)
                    {
                        if (image.ImageParentID == tvDBID)
                            RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TvDBHelper.RemoveLinkAniDBTvDB(animeID, (enEpisodeType) aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID)
        {
            try
            {
                AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);

                if (ep == null) return "Could not find Episode";

                CrossRef_AniDB_TvDB_Episode xref =
                    RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(aniDBEpisodeID);
                if (xref == null) return "Could not find Link!";


                RepoFactory.CrossRef_AniDB_TvDB_Episode.Delete(xref.CrossRef_AniDB_TvDB_EpisodeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID)
        {
            List<TvDB_ImagePoster> allImages = new List<TvDB_ImagePoster>();
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImagePoster.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImagePoster.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_ImagePoster>();
            }
        }

        public List<TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImageWideBanner.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_ImageWideBanner>();
            }
        }

        public List<TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImageFanart.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImageFanart.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_ImageFanart>();
            }
        }

        public List<TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_Episode.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_Episode.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_Episode>();
            }
        }

        #endregion

        #region Trakt

        public List<Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID)
        {
            List<Trakt_ImageFanart> allImages = new List<Trakt_ImageFanart>();
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_ImageFanart.GetByShowID(traktShowID.Value);
                else
                    return RepoFactory.Trakt_ImageFanart.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Trakt_ImageFanart>();
            }
        }

        public List<Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID)
        {
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_ImagePoster.GetByShowID(traktShowID.Value);
                else
                    return RepoFactory.Trakt_ImagePoster.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Trakt_ImagePoster>();
            }
        }

        public List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
        {
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_Episode.GetByShowID(traktShowID.Value).ToList();
                else
                    return RepoFactory.Trakt_Episode.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Trakt_Episode>();
            }
        }

        public List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
        {
            try
            {
                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show != null)
                    return GetAllTraktEpisodes(show.Trakt_ShowID);

                return new List<Trakt_Episode>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Trakt_Episode>();
            }
        }

        public List<Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                if (isAdmin)
                    return AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                else
                    return AzureWebAPI.Get_CrossRefAniDBTrakt(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Azure_CrossRef_AniDB_Trakt>();
            }
        }

        public string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
            int traktEpNumber, int? crossRef_AniDB_TraktV2ID)
        {
            try
            {
                if (crossRef_AniDB_TraktV2ID.HasValue)
                {
                    CrossRef_AniDB_TraktV2 xrefTemp =
                        RepoFactory.CrossRef_AniDB_TraktV2.GetByID(crossRef_AniDB_TraktV2ID.Value);
                    // delete the existing one if we are updating
                    TraktTVHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (enEpisodeType) xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }

                CrossRef_AniDB_TraktV2 xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(traktID, seasonNumber,
                    traktEpNumber, animeID,
                    aniEpType,
                    aniEpNumber);
                if (xref != null)
                {
                    string msg = string.Format("You have already linked Anime ID {0} to this Trakt show/season/ep",
                        xref.AnimeID);
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this Trakt show/season/ep",
                            anime.MainTitle,
                            xref.AnimeID);
                    }
                    return msg;
                }

                return TraktTVHelper.LinkAniDBTrakt(animeID, (enEpisodeType) aniEpType, aniEpNumber, traktID,
                    seasonNumber,
                    traktEpNumber, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }


        public List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID).Cast<CrossRef_AniDB_TraktV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Trakt_Episode.GetByAnimeID(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_TraktTVShowResponse> SearchTrakt(string criteria)
        {
            List<CL_TraktTVShowResponse> results = new List<CL_TraktTVShowResponse>();
            try
            {
                List<TraktV2SearchShowResult> traktResults = TraktTVHelper.SearchShowV2(criteria);

                foreach (TraktV2SearchShowResult res in traktResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return results;
            }
        }

        public string RemoveLinkAniDBTraktForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) JMMImageType.Trakt_Fanart ||
                        image.ImageParentType == (int) JMMImageType.Trakt_Poster)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                foreach (CrossRef_AniDB_TraktV2 xref in RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID))
                {
                    TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (enEpisodeType) xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID,
            int traktSeasonNumber,
            int traktEpNumber)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) JMMImageType.Trakt_Fanart ||
                        image.ImageParentType == (int) JMMImageType.Trakt_Poster)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (enEpisodeType) aniEpType, aniEpNumber,
                    traktID, traktSeasonNumber, traktEpNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<int> GetSeasonNumbersForTrakt(string traktID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh show info including season numbers from trakt
                TraktV2ShowExtended tvshow = TraktTVHelper.GetShowInfoV2(traktID);

                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show == null) return seasonNumbers;

                foreach (Trakt_Season season in show.GetSeasons())
                    seasonNumbers.Add(season.Season);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return seasonNumbers;
            }
        }

        #endregion

        #region MAL

        public CL_CrossRef_AniDB_MAL_Response GetMALCrossRefWebCache(int animeID)
        {
            try
            {
                return AzureWebAPI.Get_CrossRefAniDBMAL(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_MALAnime_Response> SearchMAL(string criteria)
        {
            List<CL_MALAnime_Response> results = new List<CL_MALAnime_Response>();
            try
            {
                anime malResults = MALHelper.SearchAnimesByTitle(criteria);

                foreach (animeEntry res in malResults.entry)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return results;
            }
        }


        public string LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber)
        {
            try
            {
                CrossRef_AniDB_MAL xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByMALID(malID);
                if (xrefTemp != null)
                {
                    string animeName = "";
                    try
                    {
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xrefTemp.AnimeID);
                        if (anime != null) animeName = anime.MainTitle;
                    }
                    catch
                    {
                    }
                    return string.Format("Not using MAL link as this MAL ID ({0}) is already in use by {1} ({2})",
                        malID,
                        xrefTemp.AnimeID, animeName);
                }

                xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeConstraint(animeID, epType, epNumber);
                if (xrefTemp != null)
                {
                    // delete the link first because we are over-writing it
                    RepoFactory.CrossRef_AniDB_MAL.Delete(xrefTemp.CrossRef_AniDB_MALID);
                    //return string.Format("Not using MAL link as this Anime ID ({0}) is already in use by {1}/{2}/{3} ({4})", animeID, xrefTemp.MALID, epType, epNumber, xrefTemp.MALTitle);
                }

                MALHelper.LinkAniDBMAL(animeID, malID, malTitle, epType, epNumber, false);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string LinkAniDBMALUpdated(int animeID, int malID, string malTitle, int oldEpType, int oldEpNumber,
            int newEpType, int newEpNumber)
        {
            try
            {
                CrossRef_AniDB_MAL xrefTemp =
                    RepoFactory.CrossRef_AniDB_MAL.GetByAnimeConstraint(animeID, oldEpType, oldEpNumber);
                if (xrefTemp == null)
                    return string.Format("Could not find MAL link ({0}/{1}/{2})", animeID, oldEpType, oldEpNumber);

                RepoFactory.CrossRef_AniDB_MAL.Delete(xrefTemp.CrossRef_AniDB_MALID);

                return LinkAniDBMAL(animeID, malID, malTitle, newEpType, newEpNumber);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }


        public string RemoveLinkAniDBMAL(int animeID, int epType, int epNumber)
        {
            try
            {
                MALHelper.RemoveLinkAniDBMAL(animeID, epType, epNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region Other Cross Refs

        public CL_CrossRef_AniDB_Other_Response GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
        {
            try
            {
                return AzureWebAPI.Get_CrossRefAniDBOther(animeID, (CrossRefType) crossRefType);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, (CrossRefType) crossRefType);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string LinkAniDBOther(int animeID, int movieID, int crossRefType)
        {
            try
            {
                CrossRefType xrefType = (CrossRefType) crossRefType;

                switch (xrefType)
                {
                    case CrossRefType.MovieDB:
                        MovieDBHelper.LinkAniDBMovieDB(animeID, movieID, false);
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBOther(int animeID, int crossRefType)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                if (anime == null) return "Could not find Anime!";

                CrossRefType xrefType = (CrossRefType) crossRefType;
                switch (xrefType)
                {
                    case CrossRefType.MovieDB:

                        // check if there are default images used associated
                        List<AniDB_Anime_DefaultImage> images =
                            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                        foreach (AniDB_Anime_DefaultImage image in images)
                        {
                            if (image.ImageParentType == (int) JMMImageType.MovieDB_FanArt ||
                                image.ImageParentType == (int) JMMImageType.MovieDB_Poster)
                            {
                                RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                            }
                        }
                        MovieDBHelper.RemoveLinkAniDBMovieDB(animeID);
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region MovieDB

        public List<CL_MovieDBMovieSearch_Response> SearchTheMovieDB(string criteria)
        {
            List<CL_MovieDBMovieSearch_Response> results = new List<CL_MovieDBMovieSearch_Response>();
            try
            {
                List<MovieDB_Movie_Result> movieResults = MovieDBHelper.Search(criteria);

                foreach (MovieDB_Movie_Result res in movieResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return results;
            }
        }

        public List<MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
        {
            try
            {
                if (movieID.HasValue)
                    return RepoFactory.MovieDB_Poster.GetByMovieID(movieID.Value);
                else
                    return RepoFactory.MovieDB_Poster.GetAllOriginal();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<MovieDB_Poster>();
            }
        }

        public List<MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
        {
            try
            {
                if (movieID.HasValue)
                    return RepoFactory.MovieDB_Fanart.GetByMovieID(movieID.Value);
                else
                    return RepoFactory.MovieDB_Fanart.GetAllOriginal();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<MovieDB_Fanart>();
            }
        }

        #endregion

        public List<CL_AniDB_Anime> GetMiniCalendar(int jmmuserID, int numberOfDays)
        {
            // get all the series
            List<CL_AniDB_Anime> animeList = new List<CL_AniDB_Anime>();

            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                List<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(
                    DateTime.Today.AddDays(0 - numberOfDays),
                    DateTime.Today.AddDays(numberOfDays));
                foreach (SVR_AniDB_Anime anime in animes)
                {
                    if (anime?.Contract?.AniDBAnime == null)
                        continue;
                    if (!user.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags()))
                        animeList.Add(anime.Contract.AniDBAnime);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return animeList;
        }

        public List<CL_AniDB_Anime> GetAnimeForMonth(int jmmuserID, int month, int year)
        {
            // get all the series
            List<CL_AniDB_Anime> animeList = new List<CL_AniDB_Anime>();

            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                DateTime startDate = new DateTime(year, month, 1, 0, 0, 0);
                DateTime endDate = startDate.AddMonths(1);
                endDate = endDate.AddMinutes(-10);

                List<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(startDate, endDate);
                foreach (SVR_AniDB_Anime anime in animes)
                {
                    if (anime?.Contract?.AniDBAnime == null)
                        continue;
                    if (!user.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags()))
                        animeList.Add(anime.Contract.AniDBAnime);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return animeList;
        }

        /*public List<Contract_AniDBAnime> GetMiniCalendar(int numberOfDays)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			JMMUserRepository repUsers = new JMMUserRepository();

			// get all the series
			List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

			try
			{

				List<AniDB_Anime> animes = repAnime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays), DateTime.Today.AddDays(numberOfDays));
				foreach (AniDB_Anime anime in animes)
				{

						animeList.Add(anime.ToContract());
				}

			}
			catch (Exception ex)
			{
				logger.Error( ex,ex.ToString());
			}
			return animeList;
		}*/

        public List<JMMUser> GetAllUsers()
        {
            try
            {
                return RepoFactory.JMMUser.GetAll().Cast<JMMUser>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<JMMUser>();
            }
        }

        public JMMUser AuthenticateUser(string username, string password)
        {
            try
            {
                return RepoFactory.JMMUser.AuthenticateUser(username, password);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string ChangePassword(int userID, string newPassword)
        {
            return ChangePassword(userID, newPassword, true);
        }

        public string ChangePassword(int userID, string newPassword, bool revokeapikey)
        {
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                jmmUser.Password = Digest.Hash(newPassword);
                RepoFactory.JMMUser.Save(jmmUser, false);
                if (revokeapikey)
                {
                    UserDatabase.RemoveApiKeysForUserID(userID);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public string SaveUser(JMMUser user)
        {
            try
            {
                bool existingUser = false;
                bool updateStats = false;
                bool updateGf = false;
                SVR_JMMUser jmmUser = null;
                if (user.JMMUserID != 0)
                {
                    jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID);
                    if (jmmUser == null) return "User not found";
                    existingUser = true;
                }
                else
                {
                    jmmUser = new SVR_JMMUser();
                    updateStats = true;
                    updateGf = true;
                }

                if (existingUser && jmmUser.IsAniDBUser != user.IsAniDBUser)
                    updateStats = true;

                string hcat = string.Join(",", user.HideCategories);
                if (jmmUser.HideCategories != hcat)
                    updateGf = true;
                jmmUser.HideCategories = hcat;
                jmmUser.IsAniDBUser = user.IsAniDBUser;
                jmmUser.IsTraktUser = user.IsTraktUser;
                jmmUser.IsAdmin = user.IsAdmin;
                jmmUser.Username = user.Username;
                jmmUser.CanEditServerSettings = user.CanEditServerSettings;
                jmmUser.PlexUsers = string.Join(",", user.PlexUsers);
                jmmUser.PlexToken = user.PlexToken;
                if (string.IsNullOrEmpty(user.Password))
                {
                    jmmUser.Password = "";
                }
                else
                {
                    // Additional check for hashed password, if not hashed we hash it
                    if (user.Password.Length < 64)
                        jmmUser.Password = Digest.Hash(user.Password);
                    else
                        jmmUser.Password = user.Password;
                }

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 0)
                {
                    bool adminExists = false;
                    IReadOnlyList<SVR_JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (SVR_JMMUser userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (existingUser)
                            {
                                if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                            }
                            else
                            {
                                //one admin account is needed
                                adminExists = true;
                                break;
                            }
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                RepoFactory.JMMUser.Save(jmmUser, updateGf);

                // update stats
                if (updateStats)
                {
                    foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                        ser.QueueUpdateStats();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public string DeleteUser(int userID)
        {
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 1)
                {
                    bool adminExists = false;
                    IReadOnlyList<SVR_JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (SVR_JMMUser userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                RepoFactory.JMMUser.Delete(userID);

                // delete all user records
                RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetByUserID(userID));
                RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByUserID(userID));
                RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByUserID(userID));
                RepoFactory.VideoLocalUser.Delete(RepoFactory.VideoLocalUser.GetByUserID(userID));
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return "";
        }

        /// <summary>
        /// Returns a list of recommendations based on the users votes
        /// </summary>
        /// <param name="maxResults"></param>
        /// <param name="userID"></param>
        /// <param name="recommendationType">1 = to watch, 2 = to download</param>
        public List<CL_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType)
        {
            List<CL_Recommendation> recs = new List<CL_Recommendation>();

            try
            {
                SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return recs;

                // get all the anime the user has chosen to ignore
                int ignoreType = 1;
                switch (recommendationType)
                {
                    case 1:
                        ignoreType = 1;
                        break;
                    case 2:
                        ignoreType = 2;
                        break;
                }
                List<IgnoreAnime> ignored = RepoFactory.IgnoreAnime.GetByUserAndType(userID, ignoreType);
                Dictionary<int, IgnoreAnime> dictIgnored = new Dictionary<int, IgnoreAnime>();
                foreach (IgnoreAnime ign in ignored)
                    dictIgnored[ign.AnimeID] = ign;


                // find all the series which the user has rated
                List<AniDB_Vote> allVotes = RepoFactory.AniDB_Vote.GetAll()
                    .OrderByDescending(a => a.VoteValue)
                    .ToList();
                if (allVotes.Count == 0) return recs;


                Dictionary<int, CL_Recommendation> dictRecs = new Dictionary<int, CL_Recommendation>();

                List<AniDB_Vote> animeVotes = new List<AniDB_Vote>();
                foreach (AniDB_Vote vote in allVotes)
                {
                    if (vote.VoteType != (int) enAniDBVoteType.Anime &&
                        vote.VoteType != (int) enAniDBVoteType.AnimeTemp)
                        continue;

                    if (dictIgnored.ContainsKey(vote.EntityID)) continue;

                    // check if the user has this anime
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(vote.EntityID);
                    if (anime == null) continue;

                    // get similar anime
                    List<AniDB_Anime_Similar> simAnime = anime.GetSimilarAnime()
                        .OrderByDescending(a => a.GetApprovalPercentage())
                        .ToList();
                    // sort by the highest approval

                    foreach (AniDB_Anime_Similar link in simAnime)
                    {
                        if (dictIgnored.ContainsKey(link.SimilarAnimeID)) continue;

                        SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                        if (animeLink != null)
                            if (!juser.AllowedAnime(animeLink)) continue;

                        // don't recommend to watch anime that the user doesn't have
                        if (animeLink == null && recommendationType == 1) continue;

                        // don't recommend to watch series that the user doesn't have
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);
                        if (ser == null && recommendationType == 1) continue;


                        if (ser != null)
                        {
                            // don't recommend to watch series that the user has already started watching
                            AnimeSeries_User userRecord = ser.GetUserRecord(userID);
                            if (userRecord != null)
                            {
                                if (userRecord.WatchedEpisodeCount > 0 && recommendationType == 1) continue;
                            }

                            // don't recommend to download anime that the user has files for
                            if (ser.LatestLocalEpisodeNumber > 0 && recommendationType == 2) continue;
                        }

                        CL_Recommendation rec = new CL_Recommendation();
                        rec.BasedOnAnimeID = anime.AnimeID;
                        rec.RecommendedAnimeID = link.SimilarAnimeID;

                        // if we don't have the anime locally. lets assume the anime has a high rating
                        decimal animeRating = 850;
                        if (animeLink != null) animeRating = animeLink.GetAniDBRating();

                        rec.Score =
                            CalculateRecommendationScore(vote.VoteValue, link.GetApprovalPercentage(), animeRating);
                        rec.BasedOnVoteValue = vote.VoteValue;
                        rec.RecommendedApproval = link.GetApprovalPercentage();

                        // check if we have added this recommendation before
                        // this might happen where animes are recommended based on different votes
                        // and could end up with different scores
                        if (dictRecs.ContainsKey(rec.RecommendedAnimeID))
                        {
                            if (rec.Score < dictRecs[rec.RecommendedAnimeID].Score) continue;
                        }

                        rec.Recommended_AniDB_Anime = null;
                        if (animeLink != null)
                            rec.Recommended_AniDB_Anime = animeLink.Contract.AniDBAnime;

                        rec.BasedOn_AniDB_Anime = anime.Contract.AniDBAnime;

                        rec.Recommended_AnimeSeries = null;
                        if (ser != null)
                            rec.Recommended_AnimeSeries = ser.GetUserContract(userID);

                        SVR_AnimeSeries serBasedOn = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                        if (serBasedOn == null) continue;

                        rec.BasedOn_AnimeSeries = serBasedOn.GetUserContract(userID);

                        dictRecs[rec.RecommendedAnimeID] = rec;
                    }
                }

                List<CL_Recommendation> tempRecs = new List<CL_Recommendation>();
                foreach (CL_Recommendation rec in dictRecs.Values)
                    tempRecs.Add(rec);

                // sort by the highest score

                int numRecs = 0;
                foreach (CL_Recommendation rec in tempRecs.OrderByDescending(a => a.Score))
                {
                    if (numRecs == maxResults) break;
                    recs.Add(rec);
                    numRecs++;
                }

                if (recs.Count == 0) return recs;

                return recs;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return recs;
            }
        }

        private double CalculateRecommendationScore(int userVoteValue, double approvalPercentage, decimal animeRating)
        {
            double score = (double) userVoteValue;

            score = score + approvalPercentage;

            if (approvalPercentage > 90) score = score + 100;
            if (approvalPercentage > 80) score = score + 100;
            if (approvalPercentage > 70) score = score + 100;
            if (approvalPercentage > 60) score = score + 100;
            if (approvalPercentage > 50) score = score + 100;

            if (animeRating > 900) score = score + 100;
            if (animeRating > 800) score = score + 100;
            if (animeRating > 700) score = score + 100;
            if (animeRating > 600) score = score + 100;
            if (animeRating > 500) score = score + 100;

            return score;
        }

        public List<CL_AniDB_GroupStatus> GetReleaseGroupsForAnime(int animeID)
        {
            List<CL_AniDB_GroupStatus> relGroups = new List<CL_AniDB_GroupStatus>();

            try
            {
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                //List<int> userReleaseGroups = new List<int>();
                Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
                foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    List<SVR_VideoLocal> vids = ep.GetVideoLocals();
                    List<string> hashes = vids.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                    foreach (string h in hashes)
                    {
                        SVR_VideoLocal vid = vids.First(a => a.Hash == h);
                        AniDB_File anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                                userReleaseGroups[anifile.GroupID] = 0;

                            userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                        }
                    }
                }

                // get all the release groups for this series
                List<AniDB_GroupStatus> grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(animeID);
                foreach (AniDB_GroupStatus gs in grpStatuses)
                {
                    CL_AniDB_GroupStatus cl = gs.ToClient();
                    if (userReleaseGroups.ContainsKey(gs.GroupID))
                    {
                        cl.UserCollecting = true;
                        cl.FileCount = userReleaseGroups[gs.GroupID];
                    }
                    else
                    {
                        cl.UserCollecting = false;
                        cl.FileCount = 0;
                    }

                    relGroups.Add(cl);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return relGroups;
        }

        public List<CL_AniDB_Character> GetCharactersForAnime(int animeID)
        {
            List<CL_AniDB_Character> chars = new List<CL_AniDB_Character>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                return anime.GetCharactersContract();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return chars;
        }

        public List<CL_AniDB_Character> GetCharactersForSeiyuu(int aniDB_SeiyuuID)
        {
            List<CL_AniDB_Character> chars = new List<CL_AniDB_Character>();

            try
            {
                AniDB_Seiyuu seiyuu = RepoFactory.AniDB_Seiyuu.GetByID(aniDB_SeiyuuID);
                if (seiyuu == null) return chars;

                List<AniDB_Character_Seiyuu> links = RepoFactory.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID);

                foreach (AniDB_Character_Seiyuu chrSei in links)
                {
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(chrSei.CharID);
                    if (chr != null)
                    {
                        List<AniDB_Anime_Character> aniChars =
                            RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID);
                        if (aniChars.Count > 0)
                        {
                            SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniChars[0].AnimeID);
                            if (anime != null)
                            {
                                CL_AniDB_Character cl = chr.ToClient(aniChars[0].CharType);
                                cl.Anime = anime.Contract.AniDBAnime;
                                chars.Add(cl);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return chars;
        }

        public List<TvDB_Language> GetTvDBLanguages()
        {
            try
            {
                return ShokoService.TvdbHelper.GetLanguages();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return new List<TvDB_Language>();
        }

        public void RefreshAllMediaInfo()
        {
            ShokoServer.RefreshAllMediaInfo();
        }

        public void RecreateAllGroups(bool resume = false)
        {
            try
            {
                new AnimeGroupCreator().RecreateAllGroups();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public CL_AppVersions GetAppVersions()
        {
            try
            {
                //TODO WHEN WE HAVE A STABLE VERSION REPO, WE NEED TO CODE THE RETRIEVAL HERE.
                return new CL_AppVersions();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return null;
        }

        public AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID)
        {
            try
            {
                return RepoFactory.AniDB_Seiyuu.GetByID(seiyuuID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public CL_Response<bool> PostTraktCommentShow(string traktID, string commentText, bool isSpoiler)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler);
        }
    }
}