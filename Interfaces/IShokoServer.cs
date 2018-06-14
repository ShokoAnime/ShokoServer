using System;
using System.Collections.Generic;
using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Models.TvDB;

// ReSharper disable InconsistentNaming
namespace Shoko.Models.Interfaces
{
    [RestBasePath("/v1")]
    public interface IShokoServer 
    {

        #region GroupsFilter

        [Rest("GroupFilter", Verbs.Get)]
        List<CL_GroupFilter> GetAllGroupFilters();

        [Rest("GroupFilter/{gf}", Verbs.Get)]
        CL_GroupFilter GetGroupFilter(int gf);

        [Rest("GroupFilter/Changes/{date}", Verbs.Get)]
        CL_Changes<CL_GroupFilter> GetGroupFilterChanges(DateTime date);

        [Rest("GroupFilter/Parent/{gfparentid}", Verbs.Get)]
        List<CL_GroupFilter> GetGroupFilters(int gfparentid = 0);

        [Rest("GroupFilter/Detailed/ForUser/{userID}", Verbs.Get)]
        List<CL_GroupFilterExtended> GetAllGroupFiltersExtended(int userID);

        [Rest("GroupFilter/Detailed/ForUser/{userID}/{gfparentid}", Verbs.Get)]
        List<CL_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0);

        [Rest("GroupFilter/Detailed/{groupFilterID}/{userID}", Verbs.Get)]
        CL_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID);

        [Rest("GroupFilter/Evaluate", Verbs.Post)]
        CL_GroupFilter EvaluateGroupFilter(CL_GroupFilter contract);

        [Rest("GroupFilter", Verbs.Post)]
        CL_Response<CL_GroupFilter> SaveGroupFilter(CL_GroupFilter contract);

        [Rest("GroupFilter/{groupFilterID}", Verbs.Delete)]
        string DeleteGroupFilter(int groupFilterID);

        #endregion

        #region Groups

        [Rest("Group/{userID}", Verbs.Get)]
        List<CL_AnimeGroup_User> GetAllGroups(int userID);

        [Rest("Group/{animeGroupID}/{userID}", Verbs.Get)]
        CL_AnimeGroup_User GetGroup(int animeGroupID, int userID);

        [Rest("Group/AboveSeries/{animeSeriesID}/{userID}", Verbs.Get)]
        List<CL_AnimeGroup_User> GetAllGroupsAboveSeries(int animeSeriesID, int userID);

        [Rest("Group/AboveGroup/{animeGroupID}/{userID}", Verbs.Get)]
        List<CL_AnimeGroup_User> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID);

        [Rest("Group/ForFilter/{groupFilterID}/{userID}/{getSingleSeriesGroups}", Verbs.Get)]
        List<CL_AnimeGroup_User> GetAnimeGroupsForFilter(int groupFilterID, int userID, bool getSingleSeriesGroups);

        [Rest("Group/SubGroup/{animeGroupID}/{userID}", Verbs.Get)]
        List<CL_AnimeGroup_User> GetSubGroupsForGroup(int animeGroupID, int userID);

        [Rest("Group/{userID}", Verbs.Post)]
        CL_Response<CL_AnimeGroup_User> SaveGroup(CL_AnimeGroup_Save_Request grp, int userID);

        [Rest("Group/DefaultSerie/{animeGroupID}/{animeSeriesID}", Verbs.Post)]
        void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID);

        [Rest("Group/DefaultSerie/{animeGroupID}", Verbs.Delete)]
        void RemoveDefaultSeriesForGroup(int animeGroupID);

        [Rest("Group/Rename", Verbs.Post, TimeOutSeconds = 180)]
        string RenameAllGroups();

        [Rest("Group/{animeGroupID}/{deleteFiles}", Verbs.Delete)]
        string DeleteAnimeGroup(int animeGroupID, bool deleteFiles);

        [Rest("Group/ForSerie/{animeSeriesID}/{userID}", Verbs.Get)]
        CL_AnimeGroup_User GetTopLevelGroupForSeries(int animeSeriesID, int userID);

        [Rest("Group/Recreate/{resume}", Verbs.Post, TimeOutSeconds = 300)]
        void RecreateAllGroups(bool resume);

        [Rest("Group/Summary/{animeID}", Verbs.Get)]
        List<CL_GroupFileSummary> GetGroupFileSummary(int animeID);

        #endregion

        #region Series

        [Rest("Serie/CreateFromAnime/{animeID}/{userID}/{animeGroupID?}/{forceOverwrite}", Verbs.Post, TimeOutSeconds = 300)]
        CL_Response<CL_AnimeSeries_User> CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID, bool forceOverwrite);

        [Rest("Serie/{userID}", Verbs.Get)]
        List<CL_AnimeSeries_User> GetAllSeries(int userID);

        [Rest("Serie/{userID}", Verbs.Post)]
        CL_Response<CL_AnimeSeries_User> SaveSeries(CL_AnimeSeries_Save_Request request, int userID);

        [Rest("Serie/Move/{animeSeriesID}/{newAnimeGroupID}/{userID}", Verbs.Post)]
        CL_Response<CL_AnimeSeries_User> MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID);

        [Rest("Serie/ForGroup/{animeGroupID}/{userID}", Verbs.Get)]
        List<CL_AnimeSeries_User> GetSeriesForGroup(int animeGroupID, int userID);

        [Rest("Serie/{animeSeriesID}/{userID}", Verbs.Get)]
        CL_AnimeSeries_User GetSeries(int animeSeriesID, int userID);

        [Rest("Serie/Watch/{animeSeriesID}/{watchedStatus}/{maxEpisodeNumber}/{episodeType}/{userID}", Verbs.Post)]
        string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber, int episodeType, int userID);

        [Rest("Serie/Seasons/{seriesID}", Verbs.Get)]
        List<int> GetSeasonNumbersForSeries(int seriesID);

        [Rest("Serie/{animeSeriesID}/{deleteFiles}/{deleteParentGroup}", Verbs.Delete)]
        string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup);

        [Rest("Serie/TvDB/Refresh/{seriesID}", Verbs.Post)]
        string UpdateTvDBData(int seriesID);

        [Rest("Serie/MissingEpisodes/{maxRecords}/{userID}", Verbs.Get)]
        List<CL_AnimeSeries_User> GetSeriesWithMissingEpisodes(int maxRecords, int userID);

        [Rest("Serie/ForGroupRecursive/{animeGroupID}/{userID}", Verbs.Get)]
        List<CL_AnimeSeries_User> GetSeriesForGroupRecursive(int animeGroupID, int userID);

        [Rest("Serie/ForAnime/{animeID}/{userID}", Verbs.Get)]
        CL_AnimeSeries_User GetSeriesForAnime(int animeID, int userID);

        [Rest("Serie/WithoutFiles/{userID}", Verbs.Get)]
        List<CL_AnimeSeries_User> GetSeriesWithoutAnyFiles(int userID);

        [Rest("Serie/RecentlyAdded/{maxRecords}/{userID}", Verbs.Get)]
        List<CL_AnimeSeries_User> GetSeriesRecentlyAdded(int maxRecords, int userID);

        [Rest("Serie/ExistingForAnime/{animeID}", Verbs.Get)]
        bool GetSeriesExistingForAnime(int animeID);

        [Rest("Serie/SearchFilename/{uid}", Verbs.Post)]
        List<CL_AnimeSeries_User> SearchSeriesWithFilename(int uid, string query);

        #endregion

        #region Episodes

        [Rest("Episode/LastWatched/{animeSeriesID}/{jmmuserID}", Verbs.Get)]
        CL_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID);

        [Rest("Episode/ContinueWatching/{userID}/{maxRecords}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetContinueWatchingFilter(int userID, int maxRecords);

        [Rest("Episode/Old/{animeSeriesID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesForSeriesOld(int animeSeriesID);

        [Rest("Episode/{animeEpisodeID}/{userID}", Verbs.Get)]
        CL_AnimeEpisode_User GetEpisode(int animeEpisodeID, int userID);

        [Rest("Episode/ForSerie/{animeSeriesID}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesForSeries(int animeSeriesID, int userID);

        [Rest("Episode/ForSingleFile/{videoLocalID}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesForFile(int videoLocalID, int userID);

        [Rest("Episode/Watch/{animeEpisodeID}/{watchedStatus}/{userID}", Verbs.Post)]
        CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID, bool watchedStatus, int userID);

        [Rest("Episode/ForMultipleFiles/{userID}/{onlyFinishedSeries}/{ignoreVariations}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries, bool ignoreVariations);

        [Rest("Episode/NextForSeries/{animeSeriesID}/{userID}", Verbs.Get)]
        CL_AnimeEpisode_User GetNextUnwatchedEpisode(int animeSeriesID, int userID);

        [Rest("Episode/NextForGroup/{animeGroupID}/{userID}", Verbs.Get)]
        CL_AnimeEpisode_User GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID);

        [Rest("Episode/WatchedToWatch/{maxRecords}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int userID);

        [Rest("Episode/Watched/{maxRecords}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesRecentlyWatched(int maxRecords, int userID);

        [Rest("Episode/IncrementStats/{animeEpisodeID}/{userID}/{statCountType}", Verbs.Get)]
        void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType);

        [Rest("Episode/RecentlyAdded/{maxRecords}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesRecentlyAdded(int maxRecords, int userID);

        [Rest("Episode/PreviousEpisode/{animeSeriesID}/{userID}", Verbs.Get)]
        CL_AnimeEpisode_User GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID);

        [Rest("Episode/AniDB/{episodeID}/{userID}", Verbs.Get)]
        CL_AnimeEpisode_User GetEpisodeByAniDBEpisodeID(int episodeID, int userID);

        [Rest("Episode/Unwatched/{animeSeriesID}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetAllUnwatchedEpisodes(int animeSeriesID, int userID);

        [Rest("Episode/RecentlyAdded/Summary/{maxRecords}/{userID}", Verbs.Get)]
        List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int userID);

        [Rest("Episode/Missing/{userID}/{onlyMyGroups}/{regularEpisodesOnly}/{airingState}", Verbs.Get)]
        List<CL_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly, int airingState);

        #endregion

        #region CustomTag

        [Rest("CustomTag/CrossRef/{customTagID}/{crossRefType}/{crossRefID}", Verbs.Delete)]
        string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID);

        [Rest("CustomTag/CrossRef", Verbs.Post)]
        CL_Response<CrossRef_CustomTag> SaveCustomTagCrossRef(CrossRef_CustomTag contract);

        [Rest("CustomTag/CrossRef/{xrefID}", Verbs.Delete)]
        string DeleteCustomTagCrossRefByID(int xrefID);

        [Rest("CustomTag", Verbs.Get)]
        List<CustomTag> GetAllCustomTags();

        [Rest("CustomTag", Verbs.Post)]
        CL_Response<CustomTag> SaveCustomTag(CustomTag contract);

        [Rest("CustomTag/{customTagID}", Verbs.Delete)]
        string DeleteCustomTag(int customTagID);

        [Rest("CustomTag/{customTagID}", Verbs.Get)]
        CustomTag GetCustomTag(int customTagID);

        #endregion

        #region WebCache

        [Rest("WebCache/Trakt/UseLinks/{animeID}", Verbs.Post)]
        string UseMyTraktLinksWebCache(int animeID);

        [Rest("WebCache/TvDB/UseLinks/{animeID}", Verbs.Post)]
        string UseMyTvDBLinksWebCache(int animeID);

        [Rest("WebCache/RandomLinkForApproval/{linkType}", Verbs.Get)]
        Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType);

        [Rest("WebCache/IsAdmin", Verbs.Get)]
        bool IsWebCacheAdmin();

        [Rest("WebCache/AdminMessages", Verbs.Get)]
        List<Azure_AdminMessage> GetAdminMessages();

        [Rest("WebCache/CrossRef/TvDB/{animeID}/{isAdmin}",Verbs.Get)]
        List<Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin);

        [Rest("WebCache/CrossRef/Other/{animeID}/{crossRefType}", Verbs.Get)]
        CL_CrossRef_AniDB_Other_Response GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType);

        [Rest("WebCache/CrossRef/TvDB/{crossRef_AniDB_TvDBId}", Verbs.Post)]
        string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId);

        [Rest("WebCache/CrossRef/TvDB/{crossRef_AniDB_TvDBId}", Verbs.Delete)]
        string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId);

        [Rest("WebCache/CrossRef/Trakt/{animeID}/{isAdmin}", Verbs.Get)]
        List<Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin);

        [Rest("WebCache/CrossRef/Trakt/{crossRef_AniDB_TraktId}", Verbs.Post)]
        string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId);

        [Rest("WebCache/CrossRef/Trakt/{crossRef_AniDB_TraktId}", Verbs.Delete)]
        string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId);

        #endregion

        #region Files

        [Rest("File/Association/{videoLocalID}/{animeEpisodeID}", Verbs.Delete)]
        string RemoveAssociationOnFile(int videoLocalID, int animeEpisodeID);

        [Rest("File/Status/{videoLocalID}/{isIgnored}", Verbs.Post)]
        string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored);

        [Rest("File/Association/{videoLocalID}/{animeEpisodeID}", Verbs.Post)]
        string AssociateSingleFile(int videoLocalID, int animeEpisodeID);

        [Rest("File/Association/{videoLocalID}/{animeSeriesID}/{startingEpisodeNumber}/{endEpisodeNumber}", Verbs.Post)]
        string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startingEpisodeNumber, int endEpisodeNumber);

        [Rest("File/Association/{animeSeriesID}/{startingEpisodeNumber}/{singleEpisode}", Verbs.Post)]
        string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber, bool singleEpisode);

        [Rest("File/Resume/{videoLocalID}/{resumeposition}/{userID}", Verbs.Post)]
        string SetResumePosition(int videoLocalID, long resumeposition, int userID);

        [Rest("File/Watch/{videoLocalID}/{watchedStatus}/{userID}", Verbs.Post)]
        string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID);

        [Rest("File/Detailed/{videoLocalID}/{userID}", Verbs.Post)]
        CL_VideoDetailed GetVideoDetailed(int videoLocalID, int userID);

        [Rest("File/Rehash/{videoLocalID}", Verbs.Post)]
        void RehashFile(int videoLocalID);

        [Rest("File/Unrecognised/{userID}", Verbs.Get, TimeOutSeconds = 300)]
        List<CL_VideoLocal> GetUnrecognisedFiles(int userID);

        [Rest("File/ManuallyLinked/{userID}", Verbs.Get)]
        List<CL_VideoLocal> GetManuallyLinkedFiles(int userID);

        [Rest("File/Ignored/{userID}", Verbs.Get)]
        List<CL_VideoLocal> GetIgnoredFiles(int userID);

        [Rest("File/Duplicated", Verbs.Get)]
        List<CL_DuplicateFile> GetAllDuplicateFiles();

        [Rest("File/Duplicated/{duplicateFileID}/{fileNumber}", Verbs.Delete)]
        string DeleteDuplicateFile(int duplicateFileID, int fileNumber);

        [Rest("File/ManuallyLinked/{userID}", Verbs.Get)]
        List<CL_VideoLocal> GetAllManuallyLinkedFiles(int userID);

        [Rest("File/PreviewDeleteMultipleFilesWithPreferences/{userID}", Verbs.Get)]
        List<CL_VideoLocal> PreviewDeleteMultipleFilesWithPreferences(int userID);

        [Rest("File/DeleteMultipleFilesWithPreferences/{userID}", Verbs.Get)]
        bool DeleteMultipleFilesWithPreferences(int userID);

        [Rest("File/GetMultipleFilesForDeletionByPreferences/{userID}", Verbs.Get)]
        List<CL_VideoDetailed> GetMultipleFilesForDeletionByPreferences(int userID);

        [Rest("File/Duplicated/Reevaluate", Verbs.Post)]
        void ReevaluateDuplicateFiles();

        [Rest("File/Physical/{videoplaceid}", Verbs.Delete)]
        string DeleteVideoLocalPlaceAndFile(int videoplaceid);

        [Rest("File/Unlinked/Rescan", Verbs.Post)]
        void RescanUnlinkedFiles();

        [Rest("File/Hashes/Sync", Verbs.Post)]
        void SyncHashes();

        [Rest("File/Detailed/{animeID}/{relGroupName}/{resolution}/{videoSource}/{videoBitDepth}/{userID}", Verbs.Get)]
        List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName, string resolution, string videoSource, int videoBitDepth, int userID);

        [Rest("File/Refresh/{videoLocalID}", Verbs.Post)]
        string UpdateFileData(int videoLocalID);

        [Rest("File/Rescan/{videoLocalID}", Verbs.Post)]
        string RescanFile(int videoLocalID);

        [Rest("File/Search/{searchType}/{searchCriteria}/{userID}", Verbs.Get)]
        List<CL_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID);

        [Rest("File/Rename/Preview/{videoLocalID}", Verbs.Get)]
        CL_VideoLocal_Renamed RenameFilePreview(int videoLocalID);

        [Rest("File/Rename/{videoLocalID}/{scriptName}/{move}", Verbs.Get)]
        CL_VideoLocal_Renamed RenameAndMoveFile(int videoLocalID, string scriptName, bool move);

        [Rest("File/Rename/{videoLocalID}/{scriptName}", Verbs.Get)]
        CL_VideoLocal_Renamed RenameFile(int videoLocalID, string scriptName);

        [Rest("File/Rename/RandomPreview/{maxResults}/{userID}", Verbs.Get)]
        List<CL_VideoLocal> RandomFileRenamePreview(int maxResults, int userID);

        [Rest("File/ForEpisode/{episodeID}/{userID}", Verbs.Get)]
        List<CL_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID);

        [Rest("File/ForAnime/{animeID}/{userID}", Verbs.Get)]
        List<CL_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID);

        [Rest("File/Variation/{videoLocalID}/{isVariation}", Verbs.Post)]
        string SetVariationStatusOnFile(int videoLocalID, bool isVariation);

        [Rest("File/Rescan/ManuallyLinked", Verbs.Get)]
        void RescanManuallyLinkedFiles();

        [Rest("File/Detailed/{episodeID}/{userID}", Verbs.Get)]
        List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);

        [Rest("File/ByGroup/{animeID}/{relGroupName}/{userID}", Verbs.Get)]
        List<CL_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID);

        #endregion

        #region Folders

        [Rest("Folder", Verbs.Get)]
        List<ImportFolder> GetImportFolders();

        [Rest("Folder", Verbs.Post)]
        CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract);

        [Rest("Folder/{importFolderID}", Verbs.Delete)]
        string DeleteImportFolder(int importFolderID);

        [Rest("Folder/Import", Verbs.Post)]
        void RunImport();

        [Rest("Folder/RemoveMissing", Verbs.Post)]
        void RemoveMissingFiles();

        [Rest("Folder/Scan/{importFolderID}", Verbs.Post)]
        void ScanFolder(int importFolderID);

        [Rest("Folder/Scan", Verbs.Post)]
        void ScanDropFolders();

        [Rest("Folder/RefreshMediaInfo", Verbs.Post)]
        void RefreshAllMediaInfo();

        #endregion

        #region CloudAccounts

        [Rest("CloudAccount/Directory/{cloudaccountid}",Verbs.Post)]
        List<string> DirectoriesFromImportFolderPath(int cloudaccountid, string path);

        [Rest("CloudAccount", Verbs.Get)]
        List<CL_CloudAccount> GetCloudProviders();


        #endregion

        #region AniDB Anime

        [Rest("AniDB/Anime", Verbs.Get)]
        List<CL_AniDB_Anime> GetAllAnime();

        [Rest("AniDB/Anime/{animeID}", Verbs.Get)]
        CL_AniDB_Anime GetAnime(int animeID);

        [Rest("AniDB/Anime/Detailed", Verbs.Get)]
        List<CL_AniDB_AnimeDetailed> GetAllAnimeDetailed();

        [Rest("AniDB/Anime/Detailed/{animeID}", Verbs.Get)]
        CL_AniDB_AnimeDetailed GetAnimeDetailed(int animeID);

        [Rest("AniDB/Anime/ForMonth/{userID}/{month}/{year}", Verbs.Get)]
        List<CL_AniDB_Anime> GetAnimeForMonth(int userID, int month, int year);

        [Rest("AniDB/Anime/Similar/{animeID}/{userID}", Verbs.Get)]
        List<CL_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID);

        [Rest("AniDB/Anime/Relation/{animeID}/{userID}", Verbs.Get)]
        List<CL_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID);

        [Rest("AniDB/Anime/Search/{titleQuery}", Verbs.Get)]
        List<CL_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery);

        [Rest("AniDB/Anime/Rating/{collectionState}/{watchedState}/{ratingVotedState}/{userID}", Verbs.Get)]
        List<CL_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState, int userID);

        [Rest("AniDB/Anime/Update/{animeID}", Verbs.Post)]
        string UpdateAnimeData(int animeID);

        [Rest("AniDB/Anime/ExternalLinksFlag/{animeID}/{flags}", Verbs.Post)]
        void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags);

        [Rest("AniDB/Anime/Ignore/{userID}", Verbs.Get)]
        List<CL_IgnoreAnime> GetIgnoredAnime(int userID);

        [Rest("AniDB/Anime/Ignore/{animeID}/{ignoreType}/{userID}", Verbs.Post)]
        void IgnoreAnime(int animeID, int ignoreType, int userID);

        [Rest("AniDB/Anime/Ignore/{ignoreAnimeID}", Verbs.Delete)]
        void RemoveIgnoreAnime(int ignoreAnimeID);

        [Rest("ReleaseGroups", Verbs.Get)]
        List<string> GetAllReleaseGroups();

        [Rest("AniDB/Anime/SearchFilename/{uid}", Verbs.Post)]
        List<CL_AniDB_Anime> SearchAnimeWithFilename(int uid, string query);

        #endregion

        #region AniDB Anime Calendar


        [Rest("AniDB/Anime/Calendar/{userID}/{numberOfDays}", Verbs.Get)]
        List<CL_AniDB_Anime> GetMiniCalendar(int userID, int numberOfDays);

        [Rest("AniDB/Anime/Calendar/Update", Verbs.Post)]
        string UpdateCalendarData();

        #endregion

        #region AniDB Release Groups

        [Rest("AniDB/ReleaseGroup/FromEpisode/{aniDBEpisodeID}", Verbs.Get)]
        List<CL_AniDB_GroupStatus> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID);

        [Rest("AniDB/ReleaseGroup/Quality/{animeID}", Verbs.Get)]
        List<CL_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID);

        [Rest("AniDB/ReleaseGroup/{animeID}", Verbs.Get)]
        List<CL_AniDB_GroupStatus> GetReleaseGroupsForAnime(int animeID);

        #endregion

        #region AniDB MyList

        [Rest("AniDB/MyList/Sync", Verbs.Post)]
        void SyncMyList();

        [Rest("AniDB/MyList/{hash}", Verbs.Post)]
        void ForceAddFileToMyList(string hash);

        [Rest("AniDB/MyList/Missing/{userID}", Verbs.Get)]
        List<CL_MissingFile> GetMyListFilesForRemoval(int userID);

        [Rest("AniDB/MyList/Missing", Verbs.Delete)]
        void RemoveMissingMyListFiles(List<CL_MissingFile> myListFiles);

        [Rest("AniDB/MyList/{fileID}", Verbs.Delete)]
        void DeleteFileFromMyList(int fileID);

        #endregion

        #region AniDB Votes

        [Rest("AniDB/Vote/Sync", Verbs.Post)]
        void SyncVotes();

        [Rest("AniDB/Vote/{animeID}/{voteType}", Verbs.Post)]
        void VoteAnime(int animeID, decimal voteValue, int voteType);

        [Rest("AniDB/Vote/{animeID}", Verbs.Delete)]
        void VoteAnimeRevoke(int animeID);

        [Rest("AniDB/Vote/{animeID}", Verbs.Get)]
        AniDB_Vote GetUserVote(int animeID);

        #endregion

        #region AniDB

        [Rest("AniDB/CrossRef/{animeID}",Verbs.Get)]
        CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID);

        [Rest("AniDB/Status", Verbs.Post)]
        string TestAniDBConnection();

        [Rest("AniDB/Character/{animeID}",Verbs.Get)]
        List<CL_AniDB_Character> GetCharactersForAnime(int animeID);

        [Rest("AniDB/Refresh/{missingInfo}/{outOfDate}/{countOnly}", Verbs.Post)]
        int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly);

        [Rest("AniDB/Character/FromSeiyuu/{seiyuuID}",Verbs.Get)]
        List <CL_AniDB_Character> GetCharactersForSeiyuu(int seiyuuID);

        [Rest("AniDB/Seiyuu/{seiyuuID}", Verbs.Get)]
        AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID);

        [Rest("AniDB/Episode/ForAnime/{animeID}", Verbs.Get)]
        List<CL_AniDB_Episode> GetAniDBEpisodesForAnime(int animeID);

        [Rest("AniDB/Recommendation/{animeID}", Verbs.Get)]
        List<AniDB_Recommendation> GetAniDBRecommendations(int animeID);

        [Rest("AniDB/AVDumpFile/{vidLocalID}", Verbs.Get, TimeOutSeconds = 600)]
        string AVDumpFile(int vidLocalID);

        #endregion

        #region TvDB Provider

        [Rest("TvDB/CrossRef/{animeID}", Verbs.Get)]
        List<CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID);

        [Rest("TvDB/CrossRef/Preview/{animeID}/{tvdbID}", Verbs.Get)]
        List<CrossRef_AniDB_TvDB_Episode> GetTvDBEpisodeMatchPreview(int animeID, int tvdbID);

        [Rest("TvDB/CrossRef/{animeID}", Verbs.Delete)]
        string RemoveLinkAniDBTvDBForAnime(int animeID);

        [Rest("TvDB/CrossRef", Verbs.Post)]
        string LinkAniDBTvDB(CrossRef_AniDB_TvDBV2 link);

        [Rest("TvDB/CrossRef", Verbs.Delete)]
        string RemoveLinkAniDBTvDB(CrossRef_AniDB_TvDBV2 link);

        [Rest("TvDB/CrossRef/FromWebCache", Verbs.Post)]
        string LinkTvDBUsingWebCacheLinks(List<CrossRef_AniDB_TvDBV2> links);

        [Rest("TvDB/Search/{criteria}", Verbs.Get)]
        List<TVDB_Series_Search_Response> SearchTheTvDB(string criteria);

        [Rest("TvDB/Poster/{tvDBID?}", Verbs.Get)]
        List<TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID);

        [Rest("TvDB/Banner/{tvDBID?}", Verbs.Get)]
        List<TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID);

        [Rest("TvDB/Fanart/{tvDBID?}", Verbs.Get)]
        List<TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID);

        [Rest("TvDB/Episode/{tvDBID?}", Verbs.Get)]
        List<TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID);

        [Rest("TvDB/Language", Verbs.Get)]
        List<TvDB_Language> GetTvDBLanguages();

        [Rest("TvDB/CrossRef/Episode/{aniDBID}/{tvDBID}", Verbs.Post)]
        string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID);

        [Rest("TvDB/CrossRef/Episode/{animeID}", Verbs.Get)]
        List<CrossRef_AniDB_TvDB_Episode_Override> GetTVDBCrossRefEpisode(int animeID);

        [Rest("TvDB/CrossRef/Episode/{aniDBEpisodeID}", Verbs.Delete)]
        string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID, int tvdbEpisodeID);

        #endregion

        #region Trakt Provider

        [Rest("Trakt/CrossRef/{animeID}", Verbs.Get)]
        List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID);

        [Rest("Trakt/CrossRef", Verbs.Get)]
        List<CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs();

        [Rest("Trakt/CrossRef/Episode/{animeID}", Verbs.Get)]
        List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID);

        [Rest("Trakt/CrossRef/{animeID}/{aniEpType}/{aniEpNumber}/{traktID}/{seasonNumber}/{traktEpNumber}/{crossRef_AniDB_TraktV2ID?}", Verbs.Post)]
        string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber, int? crossRef_AniDB_TraktV2ID);

        [Rest("Trakt/CrossRef/{animeID}", Verbs.Delete)]
        string RemoveLinkAniDBTraktForAnime(int animeID);

        [Rest("Trakt/CrossRef/{animeID}/{aniEpType}/{aniEpNumber}/{traktID}/{traktSeasonNumber}/{traktEpNumber}", Verbs.Delete)]
        string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int traktSeasonNumber, int traktEpNumber);

        [Rest("Trakt/LinkValidity/{slug}/{removeDBEntries}", Verbs.Post)]
        bool CheckTraktLinkValidity(string slug, bool removeDBEntries);

        [Rest("Trakt/DeviceCode", Verbs.Get)]
        CL_TraktDeviceCode GetTraktDeviceCode();

        [Rest("Trakt/Episode/{traktShowID?}", Verbs.Get)]
        List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID);

        [Rest("Trakt/Episode/FromTraktId/{traktID}", Verbs.Get)]
        List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID);

        [Rest("Trakt/Search/{criteria}", Verbs.Get)]
        List<CL_TraktTVShowResponse> SearchTrakt(string criteria);

        [Rest("Trakt/Seasons/{traktID}", Verbs.Get)]
        List<int> GetSeasonNumbersForTrakt(string traktID);

        [Rest("Trakt/Refresh/{traktID}", Verbs.Post)]
        string UpdateTraktData(string traktID);

        [Rest("Trakt/Friend/{friendUsername}", Verbs.Delete)]
        CL_Response<bool> TraktFriendRequestDeny(string friendUsername);

        [Rest("Trakt/Friend/{friendUsername}", Verbs.Post)]
        CL_Response<bool> TraktFriendRequestApprove(string friendUsername);

        [Rest("Trakt/Comment/{animeID}", Verbs.Get)]
        List<CL_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID);

        [Rest("Trakt/Comment/{traktID}/{isSpoiler}", Verbs.Post)]
        CL_Response<bool> PostTraktCommentShow(string traktID, string commentText, bool isSpoiler);

        [Rest("Trakt/Scrobble/{animeId}/{type}/{progress}/{status}", Verbs.Post)]
        int TraktScrobble(int animeId, int type, int progress, int status);

        [Rest("Trakt/Sync/{animeID}", Verbs.Post)]
        string SyncTraktSeries(int animeID);

        #endregion

        #region MovieDB Provider

        [Rest("MovieDB/Search/{criteria}", Verbs.Get)]
        List<CL_MovieDBMovieSearch_Response> SearchTheMovieDB(string criteria);

        [Rest("MovieDB/Poster/{movieID?}", Verbs.Get)]
        List<MovieDB_Poster> GetAllMovieDBPosters(int? movieID);

        [Rest("MovieDB/Fanart/{movieID?}", Verbs.Get)]
        List<MovieDB_Fanart> GetAllMovieDBFanart(int? movieID);

        [Rest("MovieDB/Refresh/{movieID}", Verbs.Post)]
        string UpdateMovieDBData(int movieID);

        #endregion

        #region Other Providers (MovieDB, MAL)

        [Rest("Other/CrossRef/{animeID}/{crossRefType}", Verbs.Get)]
        CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType);

        [Rest("Other/CrossRef/{animeID}/{id}/{crossRefType}", Verbs.Post)]
        string LinkAniDBOther(int animeID, int id, int crossRefType);

        [Rest("Other/CrossRef/{animeID}/{crossRefType}", Verbs.Delete)]
        string RemoveLinkAniDBOther(int animeID, int crossRefType);

        #endregion

        #region Server


        [Rest("Server", Verbs.Get)]
        CL_ServerStatus GetServerStatus();

        [Rest("Server/Settings", Verbs.Get)]
        CL_ServerSettings GetServerSettings();

        [Rest("Server/Settings", Verbs.Post)]
        CL_Response SaveServerSettings(CL_ServerSettings contractIn);

        [Rest("Server/Versions", Verbs.Get)]
        CL_AppVersions GetAppVersions();

        #endregion

        #region Change Tracker

        [Rest("Changes/{date}/{userID}", Verbs.Get)]
        CL_MainChanges GetAllChanges(DateTime date, int userID);

        #endregion

        #region CommandQueue

        [Rest("CommandQueue/Hasher/{paused}", Verbs.Post)]
        void SetCommandProcessorHasherPaused(bool paused);

        [Rest("CommandQueue/General/{paused}", Verbs.Post)]
        void SetCommandProcessorGeneralPaused(bool paused);

        [Rest("CommandQueue/Images/{paused}", Verbs.Post)]
        void SetCommandProcessorImagesPaused(bool paused);

        [Rest("CommandQueue/Hasher", Verbs.Delete)]
        void ClearHasherQueue();

        [Rest("CommandQueue/Images", Verbs.Delete)]
        void ClearImagesQueue();

        [Rest("CommandQueue/General", Verbs.Delete)]
        void ClearGeneralQueue();

        #endregion

        #region Tags

        [Rest("Tags", Verbs.Get)]
        List<string> GetAllTagNames();

        #endregion

        #region Years and Seasons

        [Rest("Years", Verbs.Get)]
        List<string> GetAllYears();

        [Rest("Seasons", Verbs.Get)]
        List<string> GetAllSeasons();

        #endregion

        #region MediaInformation

        [Rest("MediaInfo/Quality", Verbs.Get)]
        List<string> GetAllUniqueVideoQuality();

        [Rest("MediaInfo/AudioLanguages", Verbs.Get)]
        List<string> GetAllUniqueAudioLanguages();

        [Rest("MediaInfo/SubtitleLanguages", Verbs.Get)]
        List<string> GetAllUniqueSubtitleLanguages();

        #endregion

        #region Users

        [Rest("User",Verbs.Get)]
        List<JMMUser> GetAllUsers();

        [Rest("User/{username}", Verbs.Post)]
        JMMUser AuthenticateUser(string username, string password);

        [Rest("User", Verbs.Post)]
        string SaveUser(JMMUser user);

        [Rest("User", Verbs.Delete)]
        string DeleteUser(int userID);

        [Rest("User/ChangePassword/{userID}", Verbs.Post)]
        string ChangePassword(int userID, string newPassword);

        [Rest("User/Plex/LoginUrl/{userID}", Verbs.Get)]
        string LoginUrl(int userID);

        [Rest("User/Plex/Authenticated/{userID}", Verbs.Get)]
        bool IsPlexAuthenticated(int userID);

        [Rest("User/Plex/Remove/{userID}", Verbs.Get)]
        bool RemovePlexAuth(int userID);

        #endregion

        #region Images

        [Rest("Image/Enable/{enabled}/{imageID}/{imageType}",Verbs.Post)]
        string EnableDisableImage(bool enabled, int imageID, int imageType);

        [Rest("Image/Default/{isDefault}/{animeID}/{imageID}/{imageType}/{imageSizeType}", Verbs.Post)]
        string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType);

        #endregion

        #region Recommendations

        [Rest("Recommendation/{maxResults}/{userID}/{recommendationType}",Verbs.Get)]
        List<CL_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType);

        #endregion

        #region Playlists

        [Rest("Playlist",Verbs.Get)]
        List<Playlist> GetAllPlaylists();

        [Rest("Playlist", Verbs.Post)]
        CL_Response<Playlist> SavePlaylist(Playlist contract);

        [Rest("Playlist/{playlistID}", Verbs.Delete)]
        string DeletePlaylist(int playlistID);

        [Rest("Playlist/{playlistID}", Verbs.Get)]
        Playlist GetPlaylist(int playlistID);

        #endregion

        #region Bookmarks

        [Rest("Bookmark",Verbs.Get)]
        List<CL_BookmarkedAnime> GetAllBookmarkedAnime();

        [Rest("Bookmark", Verbs.Post)]
        CL_Response<CL_BookmarkedAnime> SaveBookmarkedAnime(CL_BookmarkedAnime cl);

        [Rest("Bookmark/{bookmarkedAnimeID}", Verbs.Delete)]
        string DeleteBookmarkedAnime(int bookmarkedAnimeID);

        [Rest("Bookmark/{bookmarkedAnimeID}", Verbs.Get)]
        CL_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID);


        #endregion

        #region FFDShow Presets

        [Rest("FFDShowPreset/{videoLocalID}",Verbs.Get)]
        FileFfdshowPreset GetFFDPreset(int videoLocalID);

        [Rest("FFDShowPreset/{videoLocalID}", Verbs.Delete)]
        void DeleteFFDPreset(int videoLocalID);

        [Rest("FFDShowPreset", Verbs.Post)]
        void SaveFFDPreset(FileFfdshowPreset preset);

        #endregion

        #region Rename Scripts

        [Rest("RenameScript",Verbs.Get)]
        List<RenameScript> GetAllRenameScripts();
        
        [Rest("RenameScript", Verbs.Post)]
        CL_Response<RenameScript> SaveRenameScript(RenameScript contract);

        [Rest("RenameScript/{renameScriptID}", Verbs.Delete)]
        string DeleteRenameScript(int renameScriptID);

        [Rest("RenameScript/Types", Verbs.Get)]
        IDictionary<string, string> GetScriptTypes();

        #endregion

    }
}
