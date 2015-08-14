using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.IO;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServer
	{
        [OperationContract]
        Contract_AnimeEpisode GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID);

        [OperationContract]
        string UseMyTraktLinksWebCache(int animeID);

        [OperationContract]
        string UseMyTvDBLinksWebCache(int animeID);

        [OperationContract]
        List<Contract_CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs();

        [OperationContract]
        bool CheckTraktLinkValidity(string slug, bool removeDBEntries);

        [OperationContract]
        Contract_Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType);

        [OperationContract]
        bool IsWebCacheAdmin();

        [OperationContract]
        string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId);

        [OperationContract]
        string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId);

        [OperationContract]
        string UpdateCalendarData();

        [OperationContract]
        string UpdateEpisodeData(int episodeID);

        [OperationContract]
        string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID);

        [OperationContract]
        Contract_CrossRef_CustomTag_SaveResponse SaveCustomTagCrossRef(Contract_CrossRef_CustomTag contract);

        [OperationContract]
        string DeleteCustomTagCrossRefByID(int xrefID);

        [OperationContract]
        List<Contract_CustomTag> GetAllCustomTags();

        [OperationContract]
        Contract_CustomTag_SaveResponse SaveCustomTag(Contract_CustomTag contract);

        [OperationContract]
        string DeleteCustomTag(int customTagID);

        [OperationContract]
        Contract_CustomTag GetCustomTag(int customTagID);

        [OperationContract]
        List<Contract_AdminMessage> GetAdminMessages();

		[OperationContract]
		List<Contract_AnimeEpisode> GetContinueWatchingFilter(int userID, int maxRecords);

		[OperationContract]
		string RemoveLinkAniDBTvDBForAnime(int animeID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesForSeriesOld(int animeSeriesID);

		[OperationContract]
		Contract_AnimeEpisode GetEpisode(int animeEpisodeID, int userID);

		[OperationContract]
		string RemoveAssociationOnFile(int videoLocalID, int animeEpisodeID);

		[OperationContract]
		string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored);

		[OperationContract]
		Contract_AnimeSeries_SaveResponse CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID);

		[OperationContract]
		string UpdateAnimeData(int animeID);

		[OperationContract]
		string AssociateSingleFile(int videoLocalID, int animeEpisodeID);

		[OperationContract]
		string  AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startEpNum, int endEpNum);

		[OperationContract]
		string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber, bool singleEpisode);

		[OperationContract]
		List<Contract_AnimeGroup> GetAllGroups(int userID);

		[OperationContract]
		Contract_AnimeGroup_SaveResponse SaveGroup(Contract_AnimeGroup_Save grp, int userID);

		[OperationContract]
		Contract_AnimeGroup GetGroup(int animeGroupID, int userID);

		[OperationContract]
		List<Contract_AnimeGroup> GetAllGroupsAboveSeries(int animeSeriesID, int userID);

		[OperationContract]
		List<Contract_AnimeGroup> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID);

		[OperationContract]
		List<Contract_AnimeSeries> GetAllSeries(int userID);

		[OperationContract]
		Contract_AnimeSeries_SaveResponse SaveSeries(Contract_AnimeSeries_Save contract, int userID);

		[OperationContract]
		Contract_AnimeSeries_SaveResponse MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID);

		[OperationContract]
		List<Contract_AniDBAnime> GetAllAnime();

		[OperationContract]
		List<Contract_AniDB_AnimeDetailed> GetAllAnimeDetailed();

		[OperationContract]
		Contract_AniDB_AnimeDetailed GetAnimeDetailed(int animeID);

		[OperationContract]
		List<Contract_AnimeSeries> GetSeriesForGroup(int animeGroupID, int userID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesForSeries(int animeSeriesID, int userID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesForFile(int videoLocalID, int userID);

		[OperationContract]
		List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);



		[OperationContract]
		List<Contract_AniDBReleaseGroup> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID);

		[OperationContract]
		List<Contract_ImportFolder> GetImportFolders();

		[OperationContract]
		Contract_ServerStatus GetServerStatus();

		[OperationContract]
		Contract_ServerSettings GetServerSettings();

		[OperationContract]
		Contract_ServerSettings_SaveResponse SaveServerSettings(Contract_ServerSettings contractIn);

		[OperationContract]
		string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID);

		[OperationContract]
		Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID, bool watchedStatus, int userID);

		[OperationContract]
		Contract_VideoDetailed GetVideoDetailed(int videoLocalID, int userID);

		[OperationContract]
		Contract_ImportFolder_SaveResponse SaveImportFolder(Contract_ImportFolder contract);

		[OperationContract]
		string DeleteImportFolder(int importFolderID);

		[OperationContract]
		Contract_AnimeSeries GetSeries(int animeSeriesID, int userID);

		[OperationContract]
		void RunImport();

		[OperationContract]
		void RemoveMissingFiles();

		[OperationContract]
		void SyncMyList();

		[OperationContract]
		void RehashFile(int videoLocalID);

		[OperationContract]
		void SetCommandProcessorHasherPaused(bool paused);

		[OperationContract]
		void SetCommandProcessorGeneralPaused(bool paused);

		[OperationContract]
		void SetCommandProcessorImagesPaused(bool paused);

		[OperationContract]
		List<Contract_VideoLocal> GetUnrecognisedFiles(int userID);

		[OperationContract]
		List<Contract_VideoLocal> GetManuallyLinkedFiles(int userID);

		[OperationContract]
		List<Contract_VideoLocal> GetIgnoredFiles(int userID);

		[OperationContract]
		string TestAniDBConnection();

		[OperationContract]
		string RenameAllGroups();

		[OperationContract]
		List<Contract_GroupFilter> GetAllGroupFilters();

		[OperationContract]
		Contract_GroupFilter_SaveResponse SaveGroupFilter(Contract_GroupFilter contract);

		[OperationContract]
		string DeleteGroupFilter(int groupFilterID);

		[OperationContract]
        List<string> GetAllTagNames();

		[OperationContract]
		void ScanFolder(int importFolderID);

		[OperationContract]
		void SyncVotes();

		[OperationContract]
		void VoteAnime(int animeID, decimal voteValue, int voteType);

		[OperationContract]
		void VoteAnimeRevoke(int animeID);

		[OperationContract]
		string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber, int episodeType, int userID);

		[OperationContract]
		List<string> GetAllUniqueVideoQuality();

		[OperationContract]
		List<string> GetAllUniqueAudioLanguages();

		[OperationContract]
		List<string> GetAllUniqueSubtitleLanguages();

		[OperationContract]
		List<Contract_DuplicateFile> GetAllDuplicateFiles();

		[OperationContract]
		string DeleteDuplicateFile(int duplicateFileID, int fileNumber);

		[OperationContract]
		List<Contract_VideoLocal> GetAllManuallyLinkedFiles(int userID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries, bool ignoreVariations);

		[OperationContract]
		void ReevaluateDuplicateFiles();

		[OperationContract]
		List<Contract_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID);

		[OperationContract]
		string DeleteVideoLocalAndFile(int videoLocalID);

		[OperationContract]
		void RescanUnlinkedFiles();

		[OperationContract]
		List<Contract_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName, string resolution, string videoSource, int videoBitDepth, int userID);

		[OperationContract]
		Contract_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID);

		[OperationContract]
        List<Contract_Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin);

		[OperationContract]
		List<Contract_CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID);

		[OperationContract]
		List<Contract_TVDBSeriesSearchResult> SearchTheTvDB(string criteria);

		[OperationContract]
		List<int> GetSeasonNumbersForSeries(int seriesID);

		[OperationContract]
		string LinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber, int tvEpNumber, int? crossRef_AniDB_TvDBV2ID);

		[OperationContract]
		string RemoveLinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber, int tvEpNumber);

		[OperationContract]
		List<Contract_TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID);

		[OperationContract]
		List<Contract_TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID);

		[OperationContract]
		List<Contract_TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID);

		[OperationContract]
		List<Contract_TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID);

		[OperationContract]
		string UpdateTvDBData(int seriesID);

		[OperationContract]
		string EnableDisableImage(bool enabled, int imageID, int imageType);

		[OperationContract]
		Contract_CrossRef_AniDB_OtherResult GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType);

		[OperationContract]
		Contract_CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType);

		[OperationContract]
		List<Contract_MovieDBMovieSearchResult> SearchTheMovieDB(string criteria);

		[OperationContract]
		string LinkAniDBOther(int animeID, int movieID, int crossRefType);

		[OperationContract]
		string RemoveLinkAniDBOther(int animeID, int crossRefType);

		[OperationContract]
		List<Contract_MovieDB_Poster> GetAllMovieDBPosters(int? movieID);

		[OperationContract]
		List<Contract_MovieDB_Fanart> GetAllMovieDBFanart(int? movieID);

		[OperationContract]
		Contract_AniDBAnime GetAnime(int animeID);

		[OperationContract]
		string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType);

		[OperationContract]
		Contract_AnimeEpisode GetNextUnwatchedEpisode(int animeSeriesID, int userID);

		[OperationContract]
		Contract_AnimeEpisode GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int jmmuserID);

		[OperationContract]
		string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup);

		[OperationContract]
		string DeleteAnimeGroup(int animeGroupID, bool deleteFiles);

		[OperationContract]
		List<Contract_AnimeSeries> GetSeriesWithMissingEpisodes(int maxRecords, int jmmuserID);

		[OperationContract]
		List<Contract_AniDBAnime> GetMiniCalendar(int jmmuserID, int numberOfDays);

		[OperationContract]
		List<Contract_JMMUser> GetAllUsers();

		[OperationContract]
		Contract_JMMUser AuthenticateUser(string username, string password);

		[OperationContract]
		string SaveUser(Contract_JMMUser user);

		[OperationContract]
		string DeleteUser(int userID);

		[OperationContract]
		string EnterTraktPIN(string pin);

		[OperationContract]
		List<Contract_Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID);

		[OperationContract]
		List<Contract_Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID);

		[OperationContract]
		List<Contract_Trakt_Episode> GetAllTraktEpisodes(int? traktShowID);

        [OperationContract]
        List<Contract_Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID);

        [OperationContract]
        List<Contract_Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin);

        [OperationContract]
        string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId);

        [OperationContract]
        string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId);


        [OperationContract]
		List<Contract_CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID);

        [OperationContract]
        List<Contract_CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID);

		[OperationContract]
		List<Contract_TraktTVShowResponse> SearchTrakt(string criteria);

		[OperationContract]
        string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber, int? crossRef_AniDB_TraktV2ID);

		[OperationContract]
		string RemoveLinkAniDBTraktForAnime(int animeID);

        [OperationContract]
        string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int traktSeasonNumber, int traktEpNumber);

		[OperationContract]
		List<int> GetSeasonNumbersForTrakt(string traktID);

		[OperationContract]
		string UpdateTraktData(string traktD);

        [OperationContract]
        string SyncTraktSeries(int animeID);

        [OperationContract]
        string UpdateMovieDBData(int movieD);

		[OperationContract]
		Contract_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID);

		[OperationContract]
		List<Contract_AnimeGroup> GetAnimeGroupsForFilter(int groupFilterID, int userID, bool getSingleSeriesGroups);

		[OperationContract]
		List<Contract_GroupFilterExtended> GetAllGroupFiltersExtended(int userID);

		[OperationContract]
		List<Contract_AnimeGroup> GetSubGroupsForGroup(int animeGroupID, int userID);

		[OperationContract]
		List<Contract_AnimeSeries> GetSeriesForGroupRecursive(int animeGroupID, int userID);

		[OperationContract]
		bool GetSeriesExistingForAnime(int animeID);

		[OperationContract]
		List<Contract_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID);

		[OperationContract]
		List<Contract_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID);

		[OperationContract]
		List<Contract_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType);

		[OperationContract]
		List<Contract_AniDBReleaseGroup> GetReleaseGroupsForAnime(int animeID);

		[OperationContract]
		List<Contract_AniDBAnime> GetAnimeForMonth(int jmmuserID, int month, int year);

		[OperationContract]
		Contract_AnimeSeries GetSeriesForAnime(int animeID, int userID);

		[OperationContract]
		List<Contract_AniDB_Character> GetCharactersForAnime(int animeID);

		[OperationContract]
		void ForceAddFileToMyList(string hash);

		[OperationContract]
		List<Contract_MissingFile> GetMyListFilesForRemoval(int userID);

		[OperationContract]
		void RemoveMissingMyListFiles(List<Contract_MissingFile> myListFiles);

		[OperationContract]
		List<Contract_AnimeSeries> GetSeriesWithoutAnyFiles(int userID);

		[OperationContract]
		void DeleteFileFromMyList(int fileID);

		[OperationContract]
		List<Contract_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly, int airingState);

		[OperationContract]
		void IgnoreAnime(int animeID, int ignoreType, int userID);

		[OperationContract]
		Contract_AniDBVote GetUserVote(int animeID);

		[OperationContract]
		void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType);

		[OperationContract]
		List<Contract_IgnoreAnime> GetIgnoredAnime(int userID);

		[OperationContract]
		void RemoveIgnoreAnime(int ignoreAnimeID);

		[OperationContract]
		void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID);

		[OperationContract]
		void RemoveDefaultSeriesForGroup(int animeGroupID);

		[OperationContract]
		List<Contract_TvDBLanguage> GetTvDBLanguages();

		[OperationContract]
		void ScanDropFolders();

		[OperationContract]
		void RefreshAllMediaInfo();

		[OperationContract]
		bool TraktFriendRequestDeny(string friendUsername, ref string returnMessage);

		[OperationContract]
		bool TraktFriendRequestApprove(string friendUsername, ref string returnMessage);

		[OperationContract]
		string ChangePassword(int userID, string newPassword);

		[OperationContract]
		List<Contract_Trakt_ShoutUser> GetTraktShoutsForAnime(int animeID);

		[OperationContract]
        bool PostShoutShow(string traktID, string shoutText, bool isSpoiler, ref string returnMessage);

		[OperationContract]
		Contract_AnimeGroup GetTopLevelGroupForSeries(int animeSeriesID, int userID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesRecentlyWatched(int maxRecords, int jmmuserID);

		[OperationContract]
		List<Contract_MALAnimeResponse> SearchMAL(string criteria);

		[OperationContract]
		string TestMALLogin();

		[OperationContract]
		Contract_CrossRef_AniDB_MALResult GetMALCrossRefWebCache(int animeID);

		[OperationContract]
		string LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber);

		[OperationContract]
		string RemoveLinkAniDBMAL(int animeID ,int epType, int epNumber);

		[OperationContract]
		string LinkAniDBMALUpdated(int animeID, int malID, string malTitle, int oldEpType, int oldEpNumber, int newEpType, int newEpNumber);

		[OperationContract]
		void SyncMALUpload();

		[OperationContract]
		void SyncMALDownload();

		[OperationContract]
		void RecreateAllGroups();

		[OperationContract]
		List<Contract_Playlist> GetAllPlaylists();

		[OperationContract]
		Contract_Playlist_SaveResponse SavePlaylist(Contract_Playlist contract);

		[OperationContract]
		string DeletePlaylist(int playlistID);

		[OperationContract]
		Contract_Playlist GetPlaylist(int playlistID);

		[OperationContract]
		Contract_AppVersions GetAppVersions();

		[OperationContract]
		string UpdateFileData(int videoLocalID);

		[OperationContract]
		string RescanFile(int videoLocalID);

		[OperationContract]
		List<Contract_BookmarkedAnime> GetAllBookmarkedAnime();

		[OperationContract]
		Contract_BookmarkedAnime_SaveResponse SaveBookmarkedAnime(Contract_BookmarkedAnime contract);

		[OperationContract]
		string DeleteBookmarkedAnime(int bookmarkedAnimeID);

		[OperationContract]
		Contract_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesRecentlyAdded(int maxRecords, int jmmuserID);

		[OperationContract]
		List<Contract_AnimeSeries> GetSeriesRecentlyAdded(int maxRecords, int jmmuserID);

		[OperationContract]
		string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID);

		[OperationContract]
		List<Contract_CrossRef_AniDB_TvDB_Episode> GetTVDBCrossRefEpisode(int animeID);

		[OperationContract]
		string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID);

		[OperationContract]
		List<Contract_AniDB_Character> GetCharactersForSeiyuu(int seiyuuID);

		[OperationContract]
		Contract_AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID);

		[OperationContract]
		Contract_AnimeEpisode GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID);

		[OperationContract]
		Contract_AnimeEpisode GetEpisodeByAniDBEpisodeID(int episodeID, int userID);

		[OperationContract]
		Contract_FileFfdshowPreset GetFFDPreset(int videoLocalID);

		[OperationContract]
		void DeleteFFDPreset(int videoLocalID);

		[OperationContract]
		void SaveFFDPreset(Contract_FileFfdshowPreset preset);

		[OperationContract]
		void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags);

		[OperationContract]
		List<Contract_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID);

		[OperationContract]
		Contract_VideoLocalRenamed RenameFilePreview(int videoLocalID, string renameRules);

		[OperationContract]
		Contract_VideoLocalRenamed RenameFile(int videoLocalID, string renameRules);

		[OperationContract]
		List<Contract_VideoLocal> RandomFileRenamePreview(int maxResults, int userID);

		[OperationContract]
		List<Contract_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID);

		[OperationContract]
		List<Contract_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID);

		[OperationContract]
		List<Contract_RenameScript> GetAllRenameScripts();

		[OperationContract]
		Contract_RenameScript_SaveResponse SaveRenameScript(Contract_RenameScript contract);

		[OperationContract]
		string DeleteRenameScript(int renameScriptID);

		[OperationContract]
		void ClearHasherQueue();

		[OperationContract]
		void ClearImagesQueue();

		[OperationContract]
		void ClearGeneralQueue();

		[OperationContract]
		int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly);

		[OperationContract]
		List<Contract_GroupFileSummary> GetGroupFileSummary(int animeID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetAllUnwatchedEpisodes(int animeSeriesID, int userID);

		[OperationContract]
		List<Contract_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID);

		[OperationContract]
		List<Contract_AnimeEpisode> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID);

		[OperationContract]
		List<Contract_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState, int userID);

		[OperationContract]
		string SetVariationStatusOnFile(int videoLocalID, bool isVariation);

		[OperationContract]
		List<Contract_AniDB_Recommendation> GetAniDBRecommendations(int animeID);

		[OperationContract]
		void RescanManuallyLinkedFiles();

		[OperationContract]
		List<Contract_LogMessage> GetLogMessages(string logType);

		[OperationContract]
		List<Contract_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery);

		[OperationContract]
		List<Contract_AniDB_Episode> GetAniDBEpisodesForAnime(int animeID);
	}

}
