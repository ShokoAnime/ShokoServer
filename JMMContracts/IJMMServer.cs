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
		List<string> GetAllCategoryNames();

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
		List<Contract_AnimeEpisode> GetAllEpisodesWithMultipleFiles(int userID);

		[OperationContract]
		void ReevaluateDuplicateFiles();

		[OperationContract]
		List<Contract_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID);

		[OperationContract]
		string DeleteVideoLocalAndFile(int videoLocalID);

		[OperationContract]
		void RescanUnlinkedFiles();

		[OperationContract]
		List<Contract_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName, string resolution, string videoSource, int userID);

		[OperationContract]
		Contract_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID);

		[OperationContract]
		Contract_CrossRef_AniDB_TvDBResult GetTVDBCrossRefWebCache(int animeID);

		[OperationContract]
		Contract_CrossRef_AniDB_TvDB GetTVDBCrossRef(int animeID);

		[OperationContract]
		List<Contract_TVDBSeriesSearchResult> SearchTheTvDB(string criteria);

		[OperationContract]
		List<int> GetSeasonNumbersForSeries(int seriesID);

		[OperationContract]
		string LinkAniDBTvDB(int animeID, int tvDBID, int seasonNumber);

		[OperationContract]
		string RemoveLinkAniDBTvDB(int animeID);

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
		string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles);

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
		string TestTraktLogin();

		[OperationContract]
		List<Contract_Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID);

		[OperationContract]
		List<Contract_Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID);

		[OperationContract]
		List<Contract_Trakt_Episode> GetAllTraktEpisodes(int? traktShowID);

		[OperationContract]
		Contract_CrossRef_AniDB_TraktResult GetTraktCrossRefWebCache(int animeID);

		[OperationContract]
		Contract_CrossRef_AniDB_Trakt GetTraktCrossRef(int animeID);

		[OperationContract]
		List<Contract_TraktTVShowResponse> SearchTrakt(string criteria);

		[OperationContract]
		string LinkAniDBTrakt(int animeID, string traktID, int seasonNumber);

		[OperationContract]
		string RemoveLinkAniDBTrakt(int animeID);

		[OperationContract]
		List<int> GetSeasonNumbersForTrakt(string traktID);

		[OperationContract]
		string UpdateTraktData(string traktD);

		[OperationContract]
		Contract_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID);

		[OperationContract]
		List<Contract_AnimeGroup> GetAnimeGroupsForFilter(int groupFilterID, int userID);

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
	}

}
