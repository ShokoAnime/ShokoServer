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
using Shoko.Commons;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Extensions;
using Shoko.Server.Plex;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation : IShokoServer, IHttpContextAccessor
    {
        HttpContext _ctx;
        public HttpContext HttpContext { get => _ctx; set => _ctx = value; }

        //TODO Split this file into subfiles with partial class, Move #region funcionality from the interface to those subfiles. Also move this to API folder

        private static Logger logger = LogManager.GetCurrentClassLogger();

        #region Bookmarks

        public List<CL_BookmarkedAnime> GetAllBookmarkedAnime()
        {
            List<CL_BookmarkedAnime> baList = new List<CL_BookmarkedAnime>();
            try
            {
                return Repo.BookmarkedAnime.GetAll().Select(a => ModelClients.ToClient(a)).ToList();
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
                using (var upd = Repo.BookmarkedAnime.BeginAddOrUpdate(() => Repo.BookmarkedAnime.GetByID(contract.AnimeID)))
                {
                    if (contract.AnimeID != 0 && upd.Original == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Bookmark with ID: " +contract.AnimeID;
                        return contractRet;
                    }
                    upd.Entity.AnimeID = contract.AnimeID;
                    upd.Entity.Priority = contract.Priority;
                    upd.Entity.Notes = contract.Notes;
                    upd.Entity.Downloading = contract.Downloading;
                    contractRet.Result = upd.Commit().ToClient();
                }                
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
                if (!Repo.BookmarkedAnime.FindAndDelete(()=> Repo.BookmarkedAnime.GetByID(bookmarkedAnimeID)))
                    return "Bookmarked not found";

                Repo.BookmarkedAnime.Delete(bookmarkedAnimeID);

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
                return ModelClients.ToClient(Repo.BookmarkedAnime.GetByID(bookmarkedAnimeID));
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
                    Repo.GroupFilter.GetChangeTracker(),
                    Repo.AnimeGroup.GetChangeTracker(),
                    Repo.AnimeGroup_User.GetChangeTracker(userID),
                    Repo.AnimeSeries.GetChangeTracker(),
                    Repo.AnimeSeries_User.GetChangeTracker(userID)
                }, date);
                c.Filters = new CL_Changes<CL_GroupFilter>
                {
                    ChangedItems = changes[0]
                    .ChangedItems.Select(a => Repo.GroupFilter.GetByID(a)?.ToClient())
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
                            CL_GroupFilter cag = Repo.GroupFilter.GetByID(ag.ParentGroupFilterID.Value)?
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
                    .ChangedItems.Select(a => Repo.AnimeGroup.GetByID(a))
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
                    .ChangedItems.Select(a => Repo.AnimeSeries.GetByID(a))
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
                Changes<int> changes = Repo.GroupFilter.GetChangeTracker().GetChanges(date);
                c.ChangedItems = changes.ChangedItems.Select(a => Repo.GroupFilter.GetByID(a).ToClient())
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

                var helper = ShokoService.AnidbProcessor;
                if (helper.IsHttpBanned)
                {
                    contract.IsBanned = true;
                    contract.BanReason = helper.HttpBanTime.ToString();
                    contract.BanOrigin = @"HTTP";
                }
                else if (helper.IsUdpBanned)
                {
                    contract.IsBanned = true;
                    contract.BanReason = helper.UdpBanTime.ToString();
                    contract.BanOrigin = @"UDP";
                }
                else
                {
                    contract.IsBanned = false;
                    contract.BanReason = string.Empty;
                    contract.BanOrigin = string.Empty;
                }
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
                    Repo.AnimeSeries.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
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
                    Repo.AnimeSeries.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
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

                foreach (AniDB_Tag tag in Repo.AniDB_Tag.GetAll())
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
                    IFileSystem ff = CloudFileSystemPluginFactory.Instance.List
                        .FirstOrDefault(a => a.Name == "Local File System")
                        ?.Init("", null);
                    if (ff!=null && ff.Status==Status.Ok)
                        n = ff;
                }
                else
                {
                    SVR_CloudAccount cl = Repo.CloudAccount.GetByID(cloudaccountid);
                    if (cl != null)
                        n = cl.FileSystem;
                }
                if (n != null)
                {
                    IObject dirr;
                    if (n is LocalFileSystem && path.Equals("null"))
                    {
                        if (n.Directories == null) return result;
                        return n.Directories.Select(a => a.FullName).OrderByNatural(a => a).ToList();
                    }
                    if (path.Equals("null"))
                    {
                        path = string.Empty;
                    }
                    dirr = n.Resolve(path);
                    if (dirr.Status!=Status.Ok || dirr is IFile)
                        return null;
                    IDirectory dir = dirr as IDirectory;
                    FileSystemResult fr = dir.Populate();
                    if (fr.Status!=Status.Ok)
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
                Repo.CloudAccount.GetAll().ForEach(a => ls.Add(a.ToClient()));
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
                if (ushort.TryParse(contractIn.AniDB_ClientPort, out ushort newAniDB_ClientPort) && newAniDB_ClientPort != ServerSettings.Instance.AniDb.ClientPort)
                {
                    anidbSettingsChanged = true;
                    contract.ErrorMessage += "AniDB Client Port must be numeric and greater than 0" +
                                            Environment.NewLine;
                }

                if (ushort.TryParse(contractIn.AniDB_ServerPort, out ushort newAniDB_ServerPort) && newAniDB_ServerPort != ServerSettings.Instance.AniDb.ServerPort)
                {
                    anidbSettingsChanged = true;
                    contract.ErrorMessage += "AniDB Server Port must be numeric and greater than 0" +
                                            Environment.NewLine;
                }

                if (contractIn.AniDB_Username != ServerSettings.Instance.AniDb.Username)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_Username))
                    {
                        contract.ErrorMessage += "AniDB User Name must have a value" + Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_Password != ServerSettings.Instance.AniDb.Password)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_Password))
                    {
                        contract.ErrorMessage += "AniDB Password must have a value" + Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_ServerAddress != ServerSettings.Instance.AniDb.ServerAddress)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_ServerAddress))
                    {
                        contract.ErrorMessage += "AniDB Server Address must have a value" + Environment.NewLine;
                    }
                }

                if (ushort.TryParse(contractIn.AniDB_AVDumpClientPort, out ushort newAniDB_AVDumpClientPort))
                {
                    contract.ErrorMessage += "AniDB AVDump port must be a valid port" + Environment.NewLine;
                }


                if (contract.ErrorMessage.Length > 0) return contract;

                ServerSettings.Instance.AniDb.ClientPort = newAniDB_ClientPort;
                ServerSettings.Instance.AniDb.Password = contractIn.AniDB_Password;
                ServerSettings.Instance.AniDb.ServerAddress = contractIn.AniDB_ServerAddress;
                ServerSettings.Instance.AniDb.ServerPort = newAniDB_ServerPort;
                ServerSettings.Instance.AniDb.Username = contractIn.AniDB_Username;
                ServerSettings.Instance.AniDb.AVDumpClientPort = newAniDB_AVDumpClientPort;
                ServerSettings.Instance.AniDb.AVDumpKey = contractIn.AniDB_AVDumpKey;

                ServerSettings.Instance.AniDb.DownloadRelatedAnime = contractIn.AniDB_DownloadRelatedAnime;
                ServerSettings.Instance.AniDb.DownloadReleaseGroups = contractIn.AniDB_DownloadReleaseGroups;
                ServerSettings.Instance.AniDb.DownloadReviews = contractIn.AniDB_DownloadReviews;
                ServerSettings.Instance.AniDb.DownloadSimilarAnime = contractIn.AniDB_DownloadSimilarAnime;

                ServerSettings.Instance.AniDb.MyList_AddFiles = contractIn.AniDB_MyList_AddFiles;
                ServerSettings.Instance.AniDb.MyList_ReadUnwatched = contractIn.AniDB_MyList_ReadUnwatched;
                ServerSettings.Instance.AniDb.MyList_ReadWatched = contractIn.AniDB_MyList_ReadWatched;
                ServerSettings.Instance.AniDb.MyList_SetUnwatched = contractIn.AniDB_MyList_SetUnwatched;
                ServerSettings.Instance.AniDb.MyList_SetWatched = contractIn.AniDB_MyList_SetWatched;
                ServerSettings.Instance.AniDb.MyList_StorageState = (AniDBFile_State) contractIn.AniDB_MyList_StorageState;
                ServerSettings.Instance.AniDb.MyList_DeleteType = (AniDBFileDeleteType) contractIn.AniDB_MyList_DeleteType;
                //ServerSettings.Instance.AniDb.MaxRelationDepth = contractIn.AniDB_MaxRelationDepth;

                ServerSettings.Instance.AniDb.MyList_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_MyList_UpdateFrequency;
                ServerSettings.Instance.AniDb.Calendar_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_Calendar_UpdateFrequency;
                ServerSettings.Instance.AniDb.Anime_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_Anime_UpdateFrequency;
                ServerSettings.Instance.AniDb.MyListStats_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_MyListStats_UpdateFrequency;
                ServerSettings.Instance.AniDb.File_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_File_UpdateFrequency;

                ServerSettings.Instance.AniDb.DownloadCharacters = contractIn.AniDB_DownloadCharacters;
                ServerSettings.Instance.AniDb.DownloadCreators = contractIn.AniDB_DownloadCreators;

                // Web Cache
                ServerSettings.Instance.WebCache.Address = contractIn.WebCache_Address;
                ServerSettings.Instance.WebCache.Anonymous = contractIn.WebCache_Anonymous;
                ServerSettings.Instance.WebCache.XRefFileEpisode_Get = contractIn.WebCache_XRefFileEpisode_Get;
                ServerSettings.Instance.WebCache.XRefFileEpisode_Send = contractIn.WebCache_XRefFileEpisode_Send;
                ServerSettings.Instance.WebCache.TvDB_Get = contractIn.WebCache_TvDB_Get;
                ServerSettings.Instance.WebCache.TvDB_Send = contractIn.WebCache_TvDB_Send;
                ServerSettings.Instance.WebCache.Trakt_Get = contractIn.WebCache_Trakt_Get;
                ServerSettings.Instance.WebCache.Trakt_Send = contractIn.WebCache_Trakt_Send;
                ServerSettings.Instance.WebCache.UserInfo = contractIn.WebCache_UserInfo;

                // TvDB
                ServerSettings.Instance.TvDB.AutoLink = contractIn.TvDB_AutoLink;
                ServerSettings.Instance.TvDB.AutoFanart = contractIn.TvDB_AutoFanart;
                ServerSettings.Instance.TvDB.AutoFanartAmount = contractIn.TvDB_AutoFanartAmount;
                ServerSettings.Instance.TvDB.AutoPosters = contractIn.TvDB_AutoPosters;
                ServerSettings.Instance.TvDB.AutoPostersAmount = contractIn.TvDB_AutoPostersAmount;
                ServerSettings.Instance.TvDB.AutoWideBanners = contractIn.TvDB_AutoWideBanners;
                ServerSettings.Instance.TvDB.AutoWideBannersAmount = contractIn.TvDB_AutoWideBannersAmount;
                ServerSettings.Instance.TvDB.UpdateFrequency = (ScheduledUpdateFrequency) contractIn.TvDB_UpdateFrequency;
                ServerSettings.Instance.TvDB.Language = contractIn.TvDB_Language;

                // MovieDB
                ServerSettings.Instance.MovieDb.AutoFanart = contractIn.MovieDB_AutoFanart;
                ServerSettings.Instance.MovieDb.AutoFanartAmount = contractIn.MovieDB_AutoFanartAmount;
                ServerSettings.Instance.MovieDb.AutoPosters = contractIn.MovieDB_AutoPosters;
                ServerSettings.Instance.MovieDb.AutoPostersAmount = contractIn.MovieDB_AutoPostersAmount;

                // Import settings
                ServerSettings.Instance.Import.VideoExtensions = contractIn.VideoExtensions.Split(',');
                ServerSettings.Instance.Import.UseExistingFileWatchedStatus = contractIn.Import_UseExistingFileWatchedStatus;
                ServerSettings.Instance.AutoGroupSeries = contractIn.AutoGroupSeries;
                ServerSettings.Instance.AutoGroupSeriesUseScoreAlgorithm = contractIn.AutoGroupSeriesUseScoreAlgorithm;
                ServerSettings.Instance.AutoGroupSeriesRelationExclusions = contractIn.AutoGroupSeriesRelationExclusions;
                ServerSettings.Instance.FileQualityFilterEnabled = contractIn.FileQualityFilterEnabled;
                if (!string.IsNullOrEmpty(contractIn.FileQualityFilterPreferences))
                    ServerSettings.Instance.FileQualityFilterPreferences = JsonConvert.DeserializeObject<FileQualityPreferences>(contractIn.FileQualityFilterPreferences);
                ServerSettings.Instance.Import.RunOnStart = contractIn.RunImportOnStart;
                ServerSettings.Instance.Import.ScanDropFoldersOnStart = contractIn.ScanDropFoldersOnStart;
                ServerSettings.Instance.Import.Hash_CRC32 = contractIn.Hash_CRC32;
                ServerSettings.Instance.Import.Hash_MD5 = contractIn.Hash_MD5;
                ServerSettings.Instance.Import.Hash_SHA1 = contractIn.Hash_SHA1;

                // Language
                ServerSettings.Instance.LanguagePreference = contractIn.LanguagePreference.Split(',');
                ServerSettings.Instance.LanguageUseSynonyms = contractIn.LanguageUseSynonyms;
                ServerSettings.Instance.EpisodeTitleSource = (DataSourceType) contractIn.EpisodeTitleSource;
                ServerSettings.Instance.SeriesDescriptionSource = (DataSourceType) contractIn.SeriesDescriptionSource;
                ServerSettings.Instance.SeriesNameSource = (DataSourceType) contractIn.SeriesNameSource;

                // Trakt
                ServerSettings.Instance.TraktTv.Enabled = contractIn.Trakt_IsEnabled;
                ServerSettings.Instance.TraktTv.AuthToken = contractIn.Trakt_AuthToken;
                ServerSettings.Instance.TraktTv.RefreshToken = contractIn.Trakt_RefreshToken;
                ServerSettings.Instance.TraktTv.TokenExpirationDate = contractIn.Trakt_TokenExpirationDate;
                ServerSettings.Instance.TraktTv.UpdateFrequency = (ScheduledUpdateFrequency) contractIn.Trakt_UpdateFrequency;
                ServerSettings.Instance.TraktTv.SyncFrequency = (ScheduledUpdateFrequency) contractIn.Trakt_SyncFrequency;

                //Plex
                ServerSettings.Instance.Plex.Server = contractIn.Plex_ServerHost;
                ServerSettings.Instance.Plex.Libraries = contractIn.Plex_Sections.Length > 0
                    ? contractIn.Plex_Sections.Split(',').Select(int.Parse).ToArray()
                    : new int[0];


                if (anidbSettingsChanged)
                {
                    ShokoService.AnidbProcessor.ForceLogout();
                    ShokoService.AnidbProcessor.CloseConnections();

                    Thread.Sleep(1000);
                    ShokoService.AnidbProcessor.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password,
                        ServerSettings.Instance.AniDb.ServerAddress,
                        ServerSettings.Instance.AniDb.ServerPort, ServerSettings.Instance.AniDb.ClientPort);
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
                return ServerSettings.Instance.ToContract();
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

                Repo.CommandRequest.ClearHasherQueue();
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

                Repo.CommandRequest.ClearImageQueue();
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

                Repo.CommandRequest.ClearGeneralQueue();
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
                ShokoService.AnidbProcessor.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password,
                    ServerSettings.Instance.AniDb.ServerAddress,
                    ServerSettings.Instance.AniDb.ServerPort, ServerSettings.Instance.AniDb.ClientPort);

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
                return Repo.Adhoc.GetAllVideoQuality();
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
                return Repo.Adhoc.GetAllUniqueAudioLanguages();
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
                return Repo.Adhoc.GetAllUniqueSubtitleLanguages();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<string>();
            }
        }

        #region Plex

        public string LoginUrl(int userID)
        {
            JMMUser user = Repo.JMMUser.GetByID(userID);
            return PlexHelper.GetForUser(user).LoginUrl;
        }

        public bool IsPlexAuthenticated(int userID)
        {
            JMMUser user = Repo.JMMUser.GetByID(userID);
            return PlexHelper.GetForUser(user).IsAuthenticated;
        }

        public bool RemovePlexAuth(int userID)
        {
            JMMUser user = Repo.JMMUser.GetByID(userID);
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
                        SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByID(imageID);
                        if (anime == null) return "Could not find anime";
                        using (var upd = Repo.AniDB_Anime.BeginAddOrUpdate(() => anime))
                        {
                            upd.Entity.ImageEnabled = enabled ? 1 : 0;
                            upd.Commit();
                        }
                        break;

                    case ImageEntityType.TvDB_Banner:
                        TvDB_ImageWideBanner banner = Repo.TvDB_ImageWideBanner.GetByID(imageID);
                        if (banner == null) return "Could not find image";
                        using (var upd = Repo.TvDB_ImageWideBanner.BeginAddOrUpdate(() => banner))
                        {
                            upd.Entity.Enabled = enabled ? 1 : 0;
                            upd.Commit();
                        }
                        break;

                    case ImageEntityType.TvDB_Cover:
                        TvDB_ImagePoster poster = Repo.TvDB_ImagePoster.GetByID(imageID);
                        if (poster == null) return "Could not find image";
                        using (var upd = Repo.TvDB_ImagePoster.BeginAddOrUpdate(() => poster))
                        {
                            upd.Entity.Enabled = enabled ? 1 : 0;
                            upd.Commit();
                        }
                        break;

                    case ImageEntityType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = Repo.TvDB_ImageFanart.GetByID(imageID);
                        if (fanart == null) return "Could not find image";
                        using (var upd = Repo.TvDB_ImageFanart.BeginAddOrUpdate(() => fanart))
                        {
                            upd.Entity.Enabled = enabled ? 1 : 0;
                            upd.Commit();
                        }
                        break;

                    case ImageEntityType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = Repo.MovieDB_Poster.GetByID(imageID);
                        if (moviePoster == null) return "Could not find image";
                        using (var upd = Repo.MovieDB_Poster.BeginAddOrUpdate(() => moviePoster))
                        {
                            upd.Entity.Enabled = enabled ? 1 : 0;
                            upd.Commit();
                        }
                        break;

                    case ImageEntityType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = Repo.MovieDB_Fanart.GetByID(imageID);
                        if (movieFanart == null) return "Could not find image";
                        using (var upd = Repo.MovieDB_Fanart.BeginAddOrUpdate(() => movieFanart))
                        {
                            upd.Entity.Enabled = enabled ? 1 : 0;
                            upd.Commit();
                        }
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
                        Repo.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, (int) sizeType);
                    if (img != null)
                        Repo.AniDB_Anime_DefaultImage.Delete(img.AniDB_Anime_DefaultImageID);
                }
                else
                {
                    // making the image the default for it's type (poster, fanart etc)
                    using (var txn = Repo.AniDB_Anime_DefaultImage.BeginAddOrUpdate(() => Repo.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, (int)sizeType)))
                    {
                        txn.Entity.AnimeID = animeID;
                        txn.Entity.ImageParentID = imageID;
                        txn.Entity.ImageParentType = (int)imgType;
                        txn.Entity.ImageType = (int)sizeType;
                        txn.Commit();
                    }
                }

                SVR_AnimeSeries series = Repo.AnimeSeries.GetByAnimeID(animeID);
                Repo.AnimeSeries.Touch(() => series, (false, false, false, false));

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
                SVR_JMMUser user = Repo.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                List<SVR_AniDB_Anime> animes = Repo.AniDB_Anime.GetForDate(
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
                SVR_JMMUser user = Repo.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                DateTime startDate = new DateTime(year, month, 1, 0, 0, 0);
                DateTime endDate = startDate.AddMonths(1);
                endDate = endDate.AddMinutes(-10);

                List<SVR_AniDB_Anime> animes = Repo.AniDB_Anime.GetForDate(startDate, endDate);
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
                SVR_JMMUser juser = Repo.JMMUser.GetByID(userID);
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
                List<IgnoreAnime> ignored = Repo.IgnoreAnime.GetByUserAndType(userID, ignoreType);
                Dictionary<int, IgnoreAnime> dictIgnored = new Dictionary<int, IgnoreAnime>();
                foreach (IgnoreAnime ign in ignored)
                    dictIgnored[ign.AnimeID] = ign;


                // find all the series which the user has rated
                List<AniDB_Vote> allVotes = Repo.AniDB_Vote.GetAll()
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
                    SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByID(vote.EntityID);
                    if (anime == null) continue;

                    // get similar anime
                    List<AniDB_Anime_Similar> simAnime = anime.GetSimilarAnime()
                        .OrderByDescending(a => a.GetApprovalPercentage())
                        .ToList();
                    // sort by the highest approval

                    foreach (AniDB_Anime_Similar link in simAnime)
                    {
                        if (dictIgnored.ContainsKey(link.SimilarAnimeID)) continue;

                        SVR_AniDB_Anime animeLink = Repo.AniDB_Anime.GetByID(link.SimilarAnimeID);
                        if (animeLink != null)
                            if (!juser.AllowedAnime(animeLink)) continue;

                        // don't recommend to watch anime that the user doesn't have
                        if (animeLink == null && recommendationType == 1) continue;

                        // don't recommend to watch series that the user doesn't have
                        SVR_AnimeSeries ser = Repo.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);
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

                        SVR_AnimeSeries serBasedOn = Repo.AnimeSeries.GetByAnimeID(anime.AnimeID);
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
                SVR_AnimeSeries series = Repo.AnimeSeries.GetByAnimeID(animeID);
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
                List<AniDB_GroupStatus> grpStatuses = Repo.AniDB_GroupStatus.GetByAnimeID(animeID);
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
                SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByID(animeID);
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
                AniDB_Seiyuu seiyuu = Repo.AniDB_Seiyuu.GetByID(aniDB_SeiyuuID);
                if (seiyuu == null) return chars;

                List<AniDB_Character_Seiyuu> links = Repo.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID);

                foreach (AniDB_Character_Seiyuu chrSei in links)
                {
                    AniDB_Character chr = Repo.AniDB_Character.GetByID(chrSei.CharID);
                    if (chr != null)
                    {
                        List<AniDB_Anime_Character> aniChars =
                            Repo.AniDB_Anime_Character.GetByCharID(chr.CharID);
                        if (aniChars.Count > 0)
                        {
                            SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByID(aniChars[0].AnimeID);
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
                return Repo.AniDB_Seiyuu.GetByID(seiyuuID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }
    }
}
