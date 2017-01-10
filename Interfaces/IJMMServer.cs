using System;
using System.Collections.Generic;
using Shoko.Models.Client;
using Shoko.Models.Azure;
using Shoko.Models.Server;
using Shoko.Models.TvDB;

namespace Shoko.Models.Interfaces
{

    public interface IJMMServer
    {
        
        Client.CL_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID);

        
        string UseMyTraktLinksWebCache(int animeID);

        
        string UseMyTvDBLinksWebCache(int animeID);

        
        List<CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs();

        
        bool CheckTraktLinkValidity(string slug, bool removeDBEntries);

        
        Azure.Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType);

        
        bool IsWebCacheAdmin();

        
        string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId);

        
        string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId);

        
        string UpdateCalendarData();

        
        string UpdateEpisodeData(int episodeID);

        
        string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID);

        
        CL_Response<CrossRef_CustomTag> SaveCustomTagCrossRef(CrossRef_CustomTag contract);

        
        string DeleteCustomTagCrossRefByID(int xrefID);

        
        List<CustomTag> GetAllCustomTags();

        
        CL_Response<CustomTag> SaveCustomTag(CustomTag contract);

        
        string DeleteCustomTag(int customTagID);

        
        CustomTag GetCustomTag(int customTagID);

        
        List<Azure_AdminMessage> GetAdminMessages();

        
        List<Client.CL_AnimeEpisode_User> GetContinueWatchingFilter(int userID, int maxRecords);

        
        string RemoveLinkAniDBTvDBForAnime(int animeID);

        
        List<Client.CL_AnimeEpisode_User> GetEpisodesForSeriesOld(int animeSeriesID);

        
        Client.CL_AnimeEpisode_User GetEpisode(int animeEpisodeID, int userID);

        
        string RemoveAssociationOnFile(int videoLocalID, int animeEpisodeID);

        
        string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored);

        
        CL_Response<CL_AnimeSeries_User> CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID);

        
        string UpdateAnimeData(int animeID);

        
        string AssociateSingleFile(int videoLocalID, int animeEpisodeID);

        
        string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startEpNum, int endEpNum);

        
        string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber,
            bool singleEpisode);

        
        List<Client.CL_AnimeGroup_User> GetAllGroups(int userID);

        
        CL_Response<CL_AnimeGroup_User> SaveGroup(Client.CL_AnimeGroup_Save_Request grp, int userID);

        
        Client.CL_AnimeGroup_User GetGroup(int animeGroupID, int userID);

        
        List<Client.CL_AnimeGroup_User> GetAllGroupsAboveSeries(int animeSeriesID, int userID);

        
        List<Client.CL_AnimeGroup_User> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID);

        
        List<Client.CL_AnimeSeries_User> GetAllSeries(int userID);

        
        CL_MainChanges GetAllChanges(DateTime date, int userID);


        Client.CL_Changes<CL_GroupFilter> GetGroupFilterChanges(DateTime date);


        CL_Response<CL_AnimeSeries_User> SaveSeries(Client.CL_AnimeSeries_Save_Request request, int userID);


        CL_Response<CL_AnimeSeries_User> MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID);

        
        List<Client.CL_AniDB_Anime> GetAllAnime();

        
        List<Client.CL_AniDB_AnimeDetailed> GetAllAnimeDetailed();

        
        Client.CL_AniDB_AnimeDetailed GetAnimeDetailed(int animeID);

        
        List<Client.CL_AnimeSeries_User> GetSeriesForGroup(int animeGroupID, int userID);

        
        List<Client.CL_AnimeEpisode_User> GetEpisodesForSeries(int animeSeriesID, int userID);

        
        List<Client.CL_AnimeEpisode_User> GetEpisodesForFile(int videoLocalID, int userID);

        
        List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);


        
        List<Client.CL_AniDB_GroupStatus> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID);

        
        List<ImportFolder> GetImportFolders();

        
        CL_ServerStatus GetServerStatus();

        
        CL_ServerSettings GetServerSettings();

        
        CL_Response SaveServerSettings(CL_ServerSettings contractIn);

        
        string SetResumePositionOnVideo(int videoLocalID, long resumeposition, int userID);

        
        string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID);

        
        CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus,
            int userID);

        
        CL_VideoDetailed GetVideoDetailed(int videoLocalID, int userID);


        CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract);

        
        string DeleteImportFolder(int importFolderID);

        
        Client.CL_AnimeSeries_User GetSeries(int animeSeriesID, int userID);

        
        void RunImport();

        
        void RemoveMissingFiles();

        
        void SyncMyList();

        
        void RehashFile(int videoLocalID);

        
        void SetCommandProcessorHasherPaused(bool paused);

        
        void SetCommandProcessorGeneralPaused(bool paused);

        
        void SetCommandProcessorImagesPaused(bool paused);

        
        List<CL_VideoLocal> GetUnrecognisedFiles(int userID);

        
        List<CL_VideoLocal> GetManuallyLinkedFiles(int userID);

        
        List<CL_VideoLocal> GetIgnoredFiles(int userID);

        
        string TestAniDBConnection();

        
        string RenameAllGroups();

        
        List<CL_GroupFilter> GetAllGroupFilters();

        
        List<CL_GroupFilter> GetGroupFilters(int gfparentid = 0);

        
        CL_GroupFilter GetGroupFilter(int gf);

        
        List<CL_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0);

        
        CL_GroupFilter EvaluateGroupFilter(CL_GroupFilter contract);

        
        CL_Response<CL_GroupFilter> SaveGroupFilter(CL_GroupFilter contract);

        
        string DeleteGroupFilter(int groupFilterID);

        
        List<string> GetAllTagNames();

        
        void ScanFolder(int importFolderID);

        
        void SyncVotes();

        
        void VoteAnime(int animeID, decimal voteValue, int voteType);

        
        void VoteAnimeRevoke(int animeID);

        
        string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber, int episodeType,
            int userID);

        
        List<string> GetAllUniqueVideoQuality();

        
        List<string> GetAllUniqueAudioLanguages();

        
        List<string> GetAllUniqueSubtitleLanguages();

        
        List<CL_DuplicateFile> GetAllDuplicateFiles();

        
        string DeleteDuplicateFile(int duplicateFileID, int fileNumber);

        
        List<CL_VideoLocal> GetAllManuallyLinkedFiles(int userID);

        
        List<Client.CL_AnimeEpisode_User> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries,
            bool ignoreVariations);

        
        void ReevaluateDuplicateFiles();

        
        List<CL_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID);

        
        string DeleteVideoLocalPlaceAndFile(int videoplaceid);

        
        void RescanUnlinkedFiles();

        
        void SyncHashes();

        
        List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName, string resolution,
            string videoSource, int videoBitDepth, int userID);

        
        Client.CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID);

        
        List<Azure.Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin);

        
        List<CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID);

        
        List<TVDB_Series_Search_Response> SearchTheTvDB(string criteria);

        
        List<int> GetSeasonNumbersForSeries(int seriesID);

        
        string LinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber, int tvEpNumber,
            int? crossRef_AniDB_TvDBV2ID);

        
        string RemoveLinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber);

        
        List<TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID);

        
        List<TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID);

        
        List<TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID);

        
        List<TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID);

        
        string UpdateTvDBData(int seriesID);

        
        string EnableDisableImage(bool enabled, int imageID, int imageType);

        
        CL_CrossRef_AniDB_Other_Response GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType);

        
        CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType);

        
        List<CL_MovieDBMovieSearch_Response> SearchTheMovieDB(string criteria);

        
        string LinkAniDBOther(int animeID, int movieID, int crossRefType);

        
        string RemoveLinkAniDBOther(int animeID, int crossRefType);

        
        List<MovieDB_Poster> GetAllMovieDBPosters(int? movieID);

        
        List<MovieDB_Fanart> GetAllMovieDBFanart(int? movieID);

        
        Client.CL_AniDB_Anime GetAnime(int animeID);

        
        string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType);

        
        Client.CL_AnimeEpisode_User GetNextUnwatchedEpisode(int animeSeriesID, int userID);

        
        Client.CL_AnimeEpisode_User GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID);
        
        
        List<Client.CL_AnimeEpisode_User> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int jmmuserID);

        
        string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup);

        
        string DeleteAnimeGroup(int animeGroupID, bool deleteFiles);

        
        List<Client.CL_AnimeSeries_User> GetSeriesWithMissingEpisodes(int maxRecords, int jmmuserID);

        
        List<Client.CL_AniDB_Anime> GetMiniCalendar(int jmmuserID, int numberOfDays);

        
        List<JMMUser> GetAllUsers();

        
        JMMUser AuthenticateUser(string username, string password);

        
        string SaveUser(JMMUser user);

        
        string DeleteUser(int userID);

        
        string EnterTraktPIN(string pin);

        
        List<Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID);

        
        List<Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID);

        
        List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID);

        
        List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID);

        
        List<Azure.Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin);

        
        string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId);

        
        string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId);


        
        List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID);

        
        List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID);

        
        List<CL_TraktTVShowResponse> SearchTrakt(string criteria);

        
        string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
            int traktEpNumber,
            int? crossRef_AniDB_TraktV2ID);

        
        string RemoveLinkAniDBTraktForAnime(int animeID);

        
        string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int traktSeasonNumber,
            int traktEpNumber);

        
        List<int> GetSeasonNumbersForTrakt(string traktID);

        
        string UpdateTraktData(string traktD);

        
        string SyncTraktSeries(int animeID);

        
        string UpdateMovieDBData(int movieD);

        
        CL_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID);

        
        List<Client.CL_AnimeGroup_User> GetAnimeGroupsForFilter(int groupFilterID, int userID, bool getSingleSeriesGroups);

        
        List<CL_GroupFilterExtended> GetAllGroupFiltersExtended(int userID);

        
        List<Client.CL_AnimeGroup_User> GetSubGroupsForGroup(int animeGroupID, int userID);

        
        List<Client.CL_AnimeSeries_User> GetSeriesForGroupRecursive(int animeGroupID, int userID);

        
        bool GetSeriesExistingForAnime(int animeID);

        
        List<Client.CL_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID);

        
        List<Client.CL_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID);

        
        List<CL_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType);

        
        List<Client.CL_AniDB_GroupStatus> GetReleaseGroupsForAnime(int animeID);

        
        List<Client.CL_AniDB_Anime> GetAnimeForMonth(int jmmuserID, int month, int year);

        
        Client.CL_AnimeSeries_User GetSeriesForAnime(int animeID, int userID);

        
        List<Client.CL_AniDB_Character> GetCharactersForAnime(int animeID);

        
        void ForceAddFileToMyList(string hash);

        
        List<CL_MissingFile> GetMyListFilesForRemoval(int userID);

        
        void RemoveMissingMyListFiles(List<CL_MissingFile> myListFiles);

        
        List<Client.CL_AnimeSeries_User> GetSeriesWithoutAnyFiles(int userID);

        
        void DeleteFileFromMyList(int fileID);

        
        List<CL_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly,
            int airingState);

        
        void IgnoreAnime(int animeID, int ignoreType, int userID);

        
        AniDB_Vote GetUserVote(int animeID);

        
        void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType);

        
        List<CL_IgnoreAnime> GetIgnoredAnime(int userID);

        
        void RemoveIgnoreAnime(int ignoreAnimeID);

        
        void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID);

        
        void RemoveDefaultSeriesForGroup(int animeGroupID);

        
        List<TvDB_Language> GetTvDBLanguages();

        
        void ScanDropFolders();

        
        void RefreshAllMediaInfo();

        
        bool TraktFriendRequestDeny(string friendUsername, ref string returnMessage);

        
        bool TraktFriendRequestApprove(string friendUsername, ref string returnMessage);

        
        string ChangePassword(int userID, string newPassword);

        
        List<CL_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID);

        
        bool PostTraktCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage);

        
        Client.CL_AnimeGroup_User GetTopLevelGroupForSeries(int animeSeriesID, int userID);

        
        List<Client.CL_AnimeEpisode_User> GetEpisodesRecentlyWatched(int maxRecords, int jmmuserID);

        
        List<CL_MALAnime_Response> SearchMAL(string criteria);

        
        string TestMALLogin();

        
        CL_CrossRef_AniDB_MAL_Response GetMALCrossRefWebCache(int animeID);

        
        string LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber);

        
        string RemoveLinkAniDBMAL(int animeID, int epType, int epNumber);

        
        string LinkAniDBMALUpdated(int animeID, int malID, string malTitle, int oldEpType, int oldEpNumber,
            int newEpType,
            int newEpNumber);

        
        void SyncMALUpload();

        
        void SyncMALDownload();

        
        void RecreateAllGroups(bool resume=false);

        
        List<Playlist> GetAllPlaylists();

        
        CL_Response<Playlist> SavePlaylist(Playlist contract);

        
        string DeletePlaylist(int playlistID);

        
        Playlist GetPlaylist(int playlistID);

        
        Client.CL_AppVersions GetAppVersions();

        
        string UpdateFileData(int videoLocalID);

        
        string RescanFile(int videoLocalID);

        
        List<Client.CL_BookmarkedAnime> GetAllBookmarkedAnime();

        
        CL_Response<CL_BookmarkedAnime> SaveBookmarkedAnime(Client.CL_BookmarkedAnime cl);

        
        string DeleteBookmarkedAnime(int bookmarkedAnimeID);

        
        Client.CL_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID);

        
        List<Client.CL_AnimeEpisode_User> GetEpisodesRecentlyAdded(int maxRecords, int jmmuserID);

        
        List<Client.CL_AnimeSeries_User> GetSeriesRecentlyAdded(int maxRecords, int jmmuserID);

        
        string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID);

        
        List<CrossRef_AniDB_TvDB_Episode> GetTVDBCrossRefEpisode(int animeID);

        
        string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID);

        
        List<Client.CL_AniDB_Character> GetCharactersForSeiyuu(int seiyuuID);

        
        AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID);

        
        Client.CL_AnimeEpisode_User GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID);

        
        Client.CL_AnimeEpisode_User GetEpisodeByAniDBEpisodeID(int episodeID, int userID);

        
        FileFfdshowPreset GetFFDPreset(int videoLocalID);

        
        void DeleteFFDPreset(int videoLocalID);

        
        void SaveFFDPreset(FileFfdshowPreset preset);

        
        void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags);

        
        List<CL_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID);

        
        CL_VideoLocal_Renamed RenameFilePreview(int videoLocalID, string renameRules);

        
        CL_VideoLocal_Renamed RenameFile(int videoLocalID, string renameRules);

        
        List<CL_VideoLocal> RandomFileRenamePreview(int maxResults, int userID);

        
        List<CL_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID);

        
        List<CL_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID);

        
        List<RenameScript> GetAllRenameScripts();

        
        CL_Response<RenameScript> SaveRenameScript(RenameScript contract);

        
        string DeleteRenameScript(int renameScriptID);

        
        void ClearHasherQueue();

        
        void ClearImagesQueue();

        
        void ClearGeneralQueue();

        
        int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly);

        

        
        List<Client.CL_AnimeEpisode_User> GetAllUnwatchedEpisodes(int animeSeriesID, int userID);

        
        List<CL_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID);

        
        List<Client.CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID);

        
        List<Client.CL_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState,
            int userID);

        
        string SetVariationStatusOnFile(int videoLocalID, bool isVariation);

        
        List<AniDB_Recommendation> GetAniDBRecommendations(int animeID);

        
        void RescanManuallyLinkedFiles();

        
        List<Client.CL_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery);

        
        List<AniDB_Episode> GetAniDBEpisodesForAnime(int animeID);

        
        List<string> DirectoriesFromImportFolderPath(int cloudaccountid, string path);

        
        List<CL_CloudAccount> GetCloudProviders();

        
        void SetResumePosition(int videolocalid, int jmmuserID, long position);

        
        void TraktScrobble(int animeId, int type, int progress, int status);
    }
}