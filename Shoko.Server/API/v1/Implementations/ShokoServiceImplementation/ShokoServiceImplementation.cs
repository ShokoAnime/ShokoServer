using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AniDBAPI;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using NLog;
using NutzCode.CloudFileSystem;
using NutzCode.CloudFileSystem.Plugins.LocalFileSystem;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Extensions;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation : IShokoServer
    {
        //TODO Split this file into subfiles with partial class, Move #region funcionality from the interface to those subfiles. Also move this to API folder

        private static Logger logger = LogManager.GetCurrentClassLogger();

        #region Bookmarks

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
            CL_Response<CL_BookmarkedAnime> contractRet = new CL_Response<CL_BookmarkedAnime>
            {
                ErrorMessage = string.Empty
            };
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

                return string.Empty;
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

        #endregion

        #region Status and Changes

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
                c.Filters = new CL_Changes<CL_GroupFilter>
                {
                    ChangedItems = changes[0]
                    .ChangedItems.Select(a => RepoFactory.GroupFilter.GetByID(a).ToClient())
                    .Where(a => a != null)
                    .ToList(),
                    RemovedItems = changes[0].RemovedItems.ToList(),
                    LastChange = changes[0].LastChange
                };

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

        #endregion

        public List<string> GetAllYears()
        {
            List<CL_AnimeSeries_User> grps =
                    RepoFactory.AnimeSeries.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
            var allyears = new HashSet<string>(StringComparer.Ordinal);
            foreach (CL_AnimeSeries_User ser in grps)
            {
                int endyear = ser.AniDBAnime.AniDBAnime.EndYear;
                int startyear = ser.AniDBAnime.AniDBAnime.BeginYear;
                if (endyear == 0) endyear = DateTime.Today.Year;
                if (startyear != 0)
                    allyears.UnionWith(Enumerable.Range(startyear,
                            endyear - startyear + 1)
                        .Select(a => a.ToString()));
            }
            return allyears.OrderBy(a => a).ToList();
        }

        public List<string> GetAllSeasons()
        {
            List<CL_AnimeSeries_User> grps =
                    RepoFactory.AnimeSeries.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
            var allseasons = new SortedSet<string>(new SeasonComparator());
            foreach (CL_AnimeSeries_User ser in grps)
            {
                allseasons.UnionWith(ser.AniDBAnime.Stat_AllSeasons);
            }
            return allseasons.ToList();
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
                    if (ff?.IsOk ?? false)
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
                    FileSystemResult<IObject> dirr;
                    if ((n as LocalFileSystem) != null && path.Equals("null"))
                    {
                        if (n.Directories == null) return result;
                        return n.Directories.Select(a => a.FullName).OrderByNatural(a => a).ToList();
                    }
                    if (path.Equals("null"))
                    {
                        path = string.Empty;
                    }
                    dirr = n.Resolve(path);
                    if (dirr == null || !dirr.IsOk || dirr.Result is IFile)
                        return null;
                    IDirectory dir = dirr.Result as IDirectory;
                    FileSystemResult fr = dir.Populate();
                    if (!fr?.IsOk ?? true)
                        return result;
                    return dir?.Directories?.Select(a => a.FullName).OrderByNatural(a => a).ToList();
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

        #region Settings
        public CL_Response SaveServerSettings(CL_ServerSettings contractIn)
        {
            CL_Response contract = new CL_Response
            {
                ErrorMessage = string.Empty
            };
            try
            {
                // validate the settings
                bool anidbSettingsChanged = false;
                if (contractIn.AniDB_ClientPort != ServerSettings.AniDB_ClientPort)
                {
                    anidbSettingsChanged = true;
                    int.TryParse(contractIn.AniDB_ClientPort, out int cport);
                    if (cport <= 0)
                    {
                        contract.ErrorMessage = "AniDB Client Port must be numeric and greater than 0" +
                                                Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_ServerPort != ServerSettings.AniDB_ServerPort)
                {
                    anidbSettingsChanged = true;
                    int.TryParse(contractIn.AniDB_ServerPort, out int sport);
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
                ServerSettings.AniDB_MyList_StorageState = (AniDBFile_State) contractIn.AniDB_MyList_StorageState;
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
                ServerSettings.TvDB_AutoLink = contractIn.TvDB_AutoLink;
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
                if (!string.IsNullOrEmpty(contractIn.FileQualityFilterPreferences))
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

                // MAL
                ServerSettings.MAL_AutoLink = contractIn.MAL_AutoLink;
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
                logger.Error(ex, "Save server settings exception:\n " + ex.ToString());
                contract.ErrorMessage = ex.Message;
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

        #endregion

        #region Actions

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

        public void RefreshAllMediaInfo()
        {
            ShokoServer.RefreshAllMediaInfo();
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

        #endregion

        #region Queue Actions
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
        #endregion

        public string TestAniDBConnection()
        {
            string log = string.Empty;
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

        #region Plex

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

        #endregion

        public string EnableDisableImage(bool enabled, int imageID, int imageType)
        {
            try
            {
                ImageEntityType imgType = (ImageEntityType) imageType;

                switch (imgType)
                {
                    case ImageEntityType.AniDB_Cover:
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageID);
                        if (anime == null) return "Could not find anime";
                        anime.ImageEnabled = enabled ? 1 : 0;
                        RepoFactory.AniDB_Anime.Save(anime);
                        break;

                    case ImageEntityType.TvDB_Banner:
                        TvDB_ImageWideBanner banner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);
                        if (banner == null) return "Could not find image";
                        banner.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImageWideBanner.Save(banner);
                        break;

                    case ImageEntityType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);
                        if (poster == null) return "Could not find image";
                        poster.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImagePoster.Save(poster);
                        break;

                    case ImageEntityType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);
                        if (fanart == null) return "Could not find image";
                        fanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImageFanart.Save(fanart);
                        break;

                    case ImageEntityType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(imageID);
                        if (moviePoster == null) return "Could not find image";
                        moviePoster.Enabled = enabled ? 1 : 0;
                        RepoFactory.MovieDB_Poster.Save(moviePoster);
                        break;

                    case ImageEntityType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(imageID);
                        if (movieFanart == null) return "Could not find image";
                        movieFanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.MovieDB_Fanart.Save(movieFanart);
                        break;
                }

                return string.Empty;
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
                ImageEntityType imgType = (ImageEntityType) imageType;
                ImageSizeType sizeType = ImageSizeType.Poster;

                switch (imgType)
                {
                    case ImageEntityType.AniDB_Cover:
                    case ImageEntityType.TvDB_Cover:
                    case ImageEntityType.MovieDB_Poster:
                        sizeType = ImageSizeType.Poster;
                        break;

                    case ImageEntityType.TvDB_Banner:
                        sizeType = ImageSizeType.WideBanner;
                        break;

                    case ImageEntityType.TvDB_FanArt:
                    case ImageEntityType.MovieDB_FanArt:
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

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #region Calendar (Dashboard)

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
            return string.Empty;
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

        #endregion

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
                    if (vote.VoteType != (int) AniDBVoteType.Anime &&
                        vote.VoteType != (int) AniDBVoteType.AnimeTemp)
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

                        CL_Recommendation rec = new CL_Recommendation
                        {
                            BasedOnAnimeID = anime.AnimeID,
                            RecommendedAnimeID = link.SimilarAnimeID
                        };

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
            double score = userVoteValue;

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
    }
}