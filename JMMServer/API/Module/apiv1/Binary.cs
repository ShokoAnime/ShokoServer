using Nancy;
using System;
using System.Collections.Generic;
using JMMServer.Tasks;

namespace JMMServer.API.Module.apiv1
{
    public class Binnary_Module : NancyModule
    {
        public Binnary_Module() : base("/bin")
        {
            Get["/Admin_GetRandomLinkForApproval/{type}"] = x => { return Admin_GetRandomLinkForApproval((int)x.type); };
            Get["/ApproveTraktCrossRefWebCache/{id}"] = x => { return ApproveTraktCrossRefWebCache((int)x.id); };
            Get["/ApproveTVDBCrossRefWebCache/{id}"] = x => { return ApproveTVDBCrossRefWebCache((int)x.id); };
            Get["/AssociateSingleFile/{a}/{b}"] = x => { return AssociateSingleFile((int)x.a, (int)x.b); };
            Get["/AssociateSingleFileWithMultipleEpisodes/{a}/{b}/{c}/{d}"] = x => { return AssociateSingleFileWithMultipleEpisodes((int)x.a, (int)x.b,(int)x.c, (int)x.d); };
            Get["/AuthenticateUser/{a}/{b}"] = x => { return AuthenticateUser(x.a, x.b); };
            Get["/ChangePassword/{a}/{b}"] = x => { return ChangePassword((int)x.a, x.b); };
            Get["/CheckTraktLinkValidity/{a}/{b}"] = x => { return CheckTraktLinkValidity(x.a, (bool)x.b); };
            Get["/ClearGeneralQueue"] = x => { return ClearGeneralQueue(); };
            Get["/ClearHasherQueue"] = x => { return ClearHasherQueue(); };
            Get["/ClearImagesQueue"] = x => { return ClearImagesQueue(); };
            Get["/CreateSeriesFromAnime/{a}/{b}/{c}"] = x => { return CreateSeriesFromAnime(x.a,x.b,x.c); };
            Get["/DeleteAnimeGroup/{a}/{b}"] = x => { return DeleteAnimeGroup(x.a, (bool)x.b); };
            Get["/DeleteAnimeSeries/{a}/{b}/{c}/{d}"] = x => { return DeleteAnimeSeries(x.a, (bool)x.b, (bool)x.c); };
            Get["/DeleteBookmarkedAnime/{a}"] = x => { return DeleteBookmarkedAnime((int)x.a); };
            Get["/DeleteCustomTag/{a}"] = x => { return DeleteCustomTag((int)x.a); };
            Get["/DeleteCustomTagCrossRef/{a}/{b}/{c}"] = x => { return DeleteCustomTagCrossRef((int)x.a, (int)x.b, (int)x.c); };
            Get["/DeleteCustomTagCrossRefByID/{a}"] = x => { return DeleteCustomTagCrossRefByID((int)x.a); };
            Get["/DeleteDuplicateFile/{a}/{b}"] = x => { return DeleteDuplicateFile((int)x.a, (int)x.b); };
            Get["/DeleteFFDPreset/{a}"] = x => { return DeleteFFDPreset((int)x.a); };
            Get["/DeleteFileFromMyList/{a}"] = x => { return DeleteFileFromMyList((int)x.a); };
            Get["/DeleteGroupFilter/{a}"] = x => { return DeleteGroupFilter((int)x.a); };
            Get["/DeleteImportFolder/{a}"] = x => { return DeleteImportFolder((int)x.a); };
            Get["/DeletePlaylist/{a}"] = x => { return DeletePlaylist((int)x.a); };
            Get["/DeleteRenameScript/{a}"] = x => { return DeleteRenameScript((int)x.a); };
            Get["/DeleteUser/{a}"] = x => { return DeleteUser((int)x.a); };
            Get["/DeleteVideoLocalAndFile/{a}"] = x => { return DeleteVideoLocalAndFile((int)x.a); };
            Get["/EnableDisableImage/{a}/{b}/{c}"] = x => { return EnableDisableImage((bool)x.a, (int)x.b, (int)x.c); };
            Get["/EnterTraktPIN/{a}"] = x => { return EnterTraktPIN(x.a); };
            Get["/ForceAddFileToMyList/{a}"] = x => { return ForceAddFileToMyList(x.a); };
            Get["/GetAdminMessages"] = x => { return GetAdminMessages(); };
            Get["/GetAllAnime"] = x => { return GetAllAnime(); };
            Get["/GetAllAnimeDetailed"] = x => { return GetAllAnimeDetailed(); };
            Get["/GetAllBookmarkedAnime"] = x => { return GetAllBookmarkedAnime(); };
            Get["/GetAllChanges/{a}/{b}"] = x => { return GetAllChanges((DateTime)x.a, (int)x.b); };
            Get["/GetAllCustomTags"] = x => { return GetAllCustomTags(); };
            Get["/GetAllDuplicateFiles"] = x => { return GetAllDuplicateFiles(); };
            Get["/GetAllEpisodesWithMultipleFiles/{a}/{b}/{c}"] = x => { return GetAllEpisodesWithMultipleFiles((int)x.a, (bool)x.b, (bool)x.c); };
            Get["/GetAllGroupFilters"] = x => { return GetAllGroupFilters(); };
            Get["/GetAllGroupFiltersExtended/{a}"] = x => { return GetAllGroupFiltersExtended((int)x.a); };
            Get["/GetAllGroups/{a}"] = x => { return GetAllGroups((int)x.a); };
            Get["/GetAllGroupsAboveGroupInclusive/{a}/{b}"] = x => { return GetAllGroupsAboveGroupInclusive((int)x.a, (int)x.b); };
            Get["/GetAllGroupsAboveSeries/{a}/{b}"] = x => { return GetAllGroupsAboveSeries((int)x.a, (int)x.b); };
            Get["/GetAllManuallyLinkedFiles/{a}"] = x => { return GetAllManuallyLinkedFiles((int)x.a); };
            Get["/GetAllMovieDBFanart/{a}"] = x => { return GetAllMovieDBFanart((int)x.a); };
            Get["/GetAllMovieDBPosters/{a}"] = x => { return GetAllMovieDBPosters((int)x.a); };
            Get["/GetAllPlaylists"] = x => { return GetAllPlaylists(); };
            Get["/GetAllRenameScripts"] = x => { return GetAllRenameScripts(); };
            Get["/GetAllSeries/{a}"] = x => { return GetAllSeries((int)x.a); };
            Get["/GetAllTagNames"] = x => { return GetAllTagNames(); };
            Get["/GetAllTraktCrossRefs"] = x => { return GetAllTraktCrossRefs(); };
            Get["/GetAllTraktEpisodes/{a}"] = x => { return GetAllTraktEpisodes((int)x.a); };
            Get["/GetAllTraktEpisodesByTraktID/{a}"] = x => { return GetAllTraktEpisodesByTraktID(x.a); };
            Get["/GetAllTraktFanart/{a}"] = x => { return GetAllTraktFanart((int)x.a); };
            Get["/GetAllTraktPosters/{a}"] = x => { return GetAllTraktPosters((int)x.a); };
            Get["/GetAllTvDBEpisodes/{a}"] = x => { return GetAllTvDBEpisodes((int)x.a); };
            Get["/GetAllTvDBFanart/{a}"] = x => { return GetAllTvDBFanart((int)x.a); };
            Get["/GetAllTvDBPosters/{a}"] = x => { return GetAllTvDBPosters((int)x.a); };
            Get["/GetAllTvDBWideBanners/{a}"] = x => { return GetAllTvDBWideBanners((int)x.a); };
            Get["/GetAllUniqueAudioLanguages"] = x => { return GetAllUniqueAudioLanguages(); };
            Get["/GetAllUniqueSubtitleLanguages"] = x => { return GetAllUniqueSubtitleLanguages(); };
            Get["/GetAllUniqueVideoQuality"] = x => { return GetAllUniqueVideoQuality(); };
            Get["/GetAllUnwatchedEpisodes/{a}/{b}"] = x => { return GetAllUnwatchedEpisodes((int)x.a, (int)x.b); };
            Get["/GetAllUsers"] = x => { return GetAllUsers(); };
            Get["/GetAniDBEpisodesForAnime/{a}"] = x => { return GetAniDBEpisodesForAnime((int)x.a); };
            Get["/GetAniDBRecommendations/{a}"] = x => { return GetAniDBRecommendations((int)x.a); };
            Get["/GetAniDBSeiyuu/{a}"] = x => { return GetAniDBSeiyuu((int)x.a); };
            Get["/GetAnime/{a}"] = x => { return GetAnime((int)x.a); };
            Get["/GetAnimeDetailed/{a}"] = x => { return GetAnimeDetailed((int)x.a); };
            Get["/GetAnimeForMonth/{a}/{b}/{c}"] = x => { return GetAnimeForMonth((int)x.a, (int)x.b, (int)x.c); };
            Get["/GetAnimeGroupsForFilter/{a}/{b}/{c}"] = x => { return GetAnimeGroupsForFilter((int)x.a, (int)x.b, (bool)x.c); };
            Get["/GetAnimeRatings/{a}/{b}/{c}/{d}"] = x => { return GetAnimeRatings((int)x.a, (int)x.b, (int)x.c, (int)x.d); };
            Get["/GetAppVersions"] = x => { return GetAppVersions(); };
            Get["/GetBookmarkedAnime/{a}"] = x => { return GetBookmarkedAnime((int)x.a); };
            Get["/GetCharactersForAnime/{a}"] = x => { return GetCharactersForAnime((int)x.a); };
            Get["/GetCharactersForSeiyuu/{a}"] = x => { return GetCharactersForSeiyuu((int)x.a); };
            Get["/GetContinueWatchingFilter/{a}/{b}"] = x => { return GetContinueWatchingFilter((int)x.a,(int)x.b); };
            Get["/GetCrossRefDetails/{a}"] = x => { return GetCrossRefDetails((int)x.a); };
            Get["/GetCustomTag/{a}"] = x => { return GetCustomTag((int)x.a); };
            Get["/GetEpisode/{a}/{b}"] = x => { return GetEpisode((int)x.a, (int)x.b); };
            Get["/GetEpisodeByAniDBEpisodeID/{a}/{b}"] = x => { return GetEpisodeByAniDBEpisodeID((int)x.a, (int)x.b); };
            Get["/GetEpisodesForFile/{a}/{b}"] = x => { return GetEpisodesForFile((int)x.a, (int)x.b); };
            Get["/GetEpisodesForSeries/{a}/{b}"] = x => { return GetEpisodesForSeries((int)x.a, (int)x.b); };
            Get["/GetEpisodesForSeriesOld/{a}"] = x => { return GetEpisodesForSeriesOld((int)x.a); };
            Get["/GetEpisodesRecentlyAdded/{a}/{b}"] = x => { return GetEpisodesRecentlyAdded((int)x.a, (int)x.b); };
            Get["/GetEpisodesRecentlyAddedSummary/{a}/{b}"] = x => { return GetEpisodesRecentlyAddedSummary((int)x.a, (int)x.b); };
            Get["/GetEpisodesRecentlyWatched/{a}/{b}"] = x => { return GetEpisodesRecentlyWatched((int)x.a, (int)x.b); };
            Get["/GetEpisodesToWatch_RecentlyWatched/{a}/{b}"] = x => { return GetEpisodesToWatch_RecentlyWatched((int)x.a, (int)x.b); };
            Get["/GetFFDPreset/{a}"] = x => { return GetFFDPreset((int)x.a); };
            Get["/GetFilesByGroup/{a}/{b}"] = x => { return GetFilesByGroup((int)x.a, x.b, (int)x.c); };
            Get["/GetFilesByGroupAndResolution/{a}/{b}/{c}/{d}/{e}/{f}"] = x => { return GetFilesByGroupAndResolution((int)x.a, x.b, x.c, x.d, (int)x.e, (int)x.f); };
            Get["/GetFilesForEpisode/{a}/{b}"] = x => { return GetFilesForEpisode((int)x.a, (int)x.b); };
            Get["/GetGroup/{a}/{b}"] = x => { return GetGroup((int)x.a, (int)x.b); };
            Get["/GetGroupFileSummary/{a}"] = x => { return GetGroupFileSummary((int)x.a); };
            Get["/GetGroupFilter/{a}"] = x => { return GetGroupFilter((int)x.a); };
            Get["/GetGroupFilterChanges/{a}"] = x => { return GetGroupFilterChanges((DateTime)x.a); };
            Get["/GetGroupFilterExtended/{a}/{b}"] = x => { return GetGroupFilterExtended((int)x.a, (int)x.b); };
            Get["/GetGroupFilters/{a}"] = x => { return GetGroupFilters((int)x.a); };
            Get["/GetGroupFiltersExtended/{a}"] = x => { return GetGroupFiltersExtended((int)x.a); };
            Get["/GetGroupVideoQualitySummary/{a}"] = x => { return GetGroupVideoQualitySummary((int)x.a); };
            Get["/GetHashCode"] = x => { return GetHashCode(); };
            Get["/GetIgnoredAnime/{a}"] = x => { return GetIgnoredAnime((int)x.a); };
            Get["/GetIgnoredFiles/{a}"] = x => { return GetIgnoredFiles((int)x.a); };
            Get["/GetImportFolders"] = x => { return GetImportFolders(); };
            Get["/GetLastWatchedEpisodeForSeries/{a}/{b}"] = x => { return GetLastWatchedEpisodeForSeries((int)x.a, (int)x.b); };
            Get["/GetMALCrossRefWebCache/{a}"] = x => { return GetMALCrossRefWebCache((int)x.a); };
            Get["/GetManuallyLinkedFiles/{a}"] = x => { return GetManuallyLinkedFiles((int)x.a); };
            Get["/GetMiniCalendar/{a}/{b}"] = x => { return GetMiniCalendar((int)x.a, (int)x.b); };
            Get["/GetMissingEpisodes/{a}/{b}/{c}/{d}"] = x => { return GetMissingEpisodes((int)x.a, (bool)x.b, (bool)x.c,(int)x.d); };
            Get["/GetMyListFilesForRemoval/{a}"] = x => { return GetMyListFilesForRemoval((int)x.a); };
            Get["/GetMyReleaseGroupsForAniDBEpisode/{a}"] = x => { return GetMyReleaseGroupsForAniDBEpisode((int)x.a); };
            Get["/GetNextUnwatchedEpisode/{a}/{b}"] = x => { return GetNextUnwatchedEpisode((int)x.a, (int)x.b); };
            Get["/GetNextUnwatchedEpisodeForGroup/{a}/{b}"] = x => { return GetNextUnwatchedEpisodeForGroup((int)x.a, (int)x.b); };
            Get["/GetOtherAnimeCrossRef/{a}/{b}"] = x => { return GetOtherAnimeCrossRef((int)x.a, (int)x.b); };
            Get["/GetOtherAnimeCrossRefWebCache/{a}/{b}"] = x => { return GetOtherAnimeCrossRefWebCache((int)x.a, (int)x.b); };
            Get["/GetPlaylist/{a}"] = x => { return GetPlaylist((int)x.a); };
            Get["/GetPreviousEpisodeForUnwatched/{a}/{b}"] = x => { return GetPreviousEpisodeForUnwatched((int)x.a, (int)x.b); };
            Get["/GetRecommendations/{a}/{b}/{c}"] = x => { return GetRecommendations((int)x.a, (int)x.b, (int)x.c); };
            Get["/GetRelatedAnimeLinks/{a}/{b}"] = x => { return GetRelatedAnimeLinks((int)x.a, (int)x.b); };
            Get["/GetReleaseGroupsForAnime/{a}"] = x => { return GetReleaseGroupsForAnime((int)x.a); };
            Get["/GetSeasonNumbersForSeries/{a}"] = x => { return GetSeasonNumbersForSeries((int)x.a); };
            Get["/GetSeasonNumbersForTrakt/{a}"] = x => { return GetSeasonNumbersForTrakt(x.a); };
            Get["/GetSeries/{a}/{b}"] = x => { return GetSeries((int)x.a, (int)x.b); };
            Get["/GetSeriesExistingForAnime/{a}"] = x => { return GetSeriesExistingForAnime((int)x.a); };
            Get["/GetSeriesForAnime/{a}/{b}"] = x => { return GetSeriesForAnime((int)x.a, (int)x.b); };
            Get["/GetSeriesForGroupRecursive/{a}/{b}"] = x => { return GetSeriesForGroupRecursive((int)x.a, (int)x.b); };
            Get["/GetSeriesRecentlyAdded/{a}/{b}"] = x => { return GetSeriesRecentlyAdded((int)x.a, (int)x.b); };
            Get["/GetSeriesWithMissingEpisodes/{a}/{b}"] = x => { return GetSeriesWithMissingEpisodes((int)x.a, (int)x.b); };
            Get["/GetSeriesWithoutAnyFiles/{a}"] = x => { return GetSeriesWithoutAnyFiles((int)x.a); };
            Get["/GetServerSettings"] = x => { return GetServerSettings(); };
            Get["/GetServerStatus"] = x => { return GetServerStatus(); };
            Get["/GetSimilarAnimeLinks/{a}/{b}"] = x => { return GetSimilarAnimeLinks((int)x.a, (int)x.b); };
            Get["/GetSubGroupsForGroup/{a}/{b}"] = x => { return GetSubGroupsForGroup((int)x.a, (int)x.b); };
            Get["/GetTopLevelGroupForSeries/{a}/{b}"] = x => { return GetTopLevelGroupForSeries((int)x.a, (int)x.b); };
            Get["/GetTraktCommentsForAnime/{a}"] = x => { return GetTraktCommentsForAnime((int)x.a); };
            Get["/GetTraktCrossRefEpisode/{a}"] = x => { return GetTraktCrossRefEpisode((int)x.a); };
            Get["/GetTraktCrossRefV2/{a}"] = x => { return GetTraktCrossRefV2((int)x.a); };
            Get["/GetTraktCrossRefWebCache/{a}/{b}"] = x => { return GetTraktCrossRefWebCache((int)x.a, (bool)x.b); };
            Get["/GetTVDBCrossRefEpisode/{a}"] = x => { return GetTVDBCrossRefEpisode((int)x.a); };
            Get["/GetTVDBCrossRefV2/{a}"] = x => { return GetTVDBCrossRefV2((int)x.a); };
            Get["/GetTVDBCrossRefWebCache/{a}/{b}"] = x => { return GetTVDBCrossRefWebCache((int)x.a, (bool)x.b); };
            Get["/GetTvDBLanguages"] = x => { return GetTvDBLanguages(); };
            Get["/GetUnrecognisedFiles/{a}"] = x => { return GetUnrecognisedFiles((int)x.a); };
            Get["/GetUserVote/{a}"] = x => { return GetUserVote((int)x.a); };
            Get["/GetVideoDetailed/{a}/{b}"] = x => { return GetVideoDetailed((int)x.a, (int)x.b); };
            Get["/GetVideoLocalsForAnime/{a}/{b}"] = x => { return GetVideoLocalsForAnime((int)x.a, (int)x.b); };
            Get["/GetVideoLocalsForEpisode/{a}/{b}"] = x => { return GetVideoLocalsForEpisode((int)x.a, (int)x.b); };
            Get["/IgnoreAnime/{a}/{b}/{c}"] = x => { return IgnoreAnime((int)x.a, (int)x.b, (int)x.c); };
            Get["/IncrementEpisodeStats/{a}/{b}/{c}"] = x => { return IncrementEpisodeStats((int)x.a, (int)x.b, (int)x.c); };
            Get["/IsWebCacheAdmin"] = x => { return IsWebCacheAdmin(); };
            Get["/LinkAniDBMAL/{a}/{b}/{c}/{d}/{e}"] = x => { return LinkAniDBMAL((int)x.a, (int)x.b, x.c, (int)x.d, (int)x.e); };
            Get["/LinkAniDBMALUpdated/{a}/{b}/{c}/{d}/{e}/{f}/{g}"] = x => { return LinkAniDBMALUpdated((int)x.a, (int)x.b, x.c, (int)x.d, (int)x.e, (int)x.f, (int)x.g); };
            Get["/LinkAniDBOther/{a}/{b}/{c}"] = x => { return LinkAniDBOther((int)x.a, (int)x.b, (int)x.c); };
            Get["/LinkAniDBTrakt/{a}/{b}/{c}/{d}/{e}/{f}/{g}"] = x => { return LinkAniDBTrakt((int)x.a, (int)x.b, (int)x.c, x.d, (int)x.e, (int)x.f, (int)x.g); };
            Get["/LinkAniDBTvDB/{a}/{b}/{c}/{d}/{e}/{f}/{g}"] = x => { return LinkAniDBTvDB((int)x.a, (int)x.b, (int)x.c, (int)x.d, (int)x.e, (int)x.f, (int)x.g); };
            Get["/LinkAniDBTvDBEpisode/{a}/{b}/{c}"] = x => { return LinkAniDBTvDBEpisode((int)x.a, (int)x.b, (int)x.c); };
            Get["/MoveSeries/{a}/{b}/{c}"] = x => { return MoveSeries((int)x.a, (int)x.b, (int)x.c); };
            Get["/OnlineAnimeTitleSearch/{a}"] = x => { return OnlineAnimeTitleSearch(x.a); };
            Get["/PostTraktCommentShow/{a}/{b}/{c}/{d}"] = x => { return PostTraktCommentShow(x.a, x.b, (bool)x.c, x.d); };
            Get["/RandomFileRenamePreview/{a}/{b}"] = x => { return RandomFileRenamePreview((int)x.a, (int)x.b); };
            Get["/RecreateAllGroups"] = x => { return RecreateAllGroups(); };
            Get["/ReevaluateDuplicateFiles"] = x => { return ReevaluateDuplicateFiles(); };
            Get["/RefreshAllMediaInfo"] = x => { return RefreshAllMediaInfo(); };
            Get["/RehashFile/{a}"] = x => { return RehashFile((int)x.a); };
            Get["/RemoveAssociationOnFile/{a}/{b}"] = x => { return RemoveAssociationOnFile((int)x.a, (int)x.b); };
            Get["/RemoveDefaultSeriesForGroup/{a}"] = x => { return RemoveDefaultSeriesForGroup((int)x.a); };
            Get["/RemoveIgnoreAnime/{a}"] = x => { return RemoveIgnoreAnime((int)x.a); };
            Get["/RemoveLinkAniDBMAL/{a}/{b}/{c}"] = x => { return RemoveLinkAniDBMAL((int)x.a, (int)x.b, (int)x.c); };
            Get["/RemoveLinkAniDBOther/{a}/{b}"] = x => { return RemoveLinkAniDBOther((int)x.a, (int)x.b); };
            Get["/RemoveLinkAniDBTrakt/{a}/{b}/{c}/{d}/{e}/{f}"] = x => { return RemoveLinkAniDBTrakt((int)x.a, (int)x.b, (int)x.c, x.d, (int)x.e, (int)x.f); };
            Get["/RemoveLinkAniDBTraktForAnime/{a}"] = x => { return RemoveLinkAniDBTraktForAnime((int)x.a); };
            Get["/RemoveLinkAniDBTvDB/{a}/{b}/{c}/{d}/{e}/{f}"] = x => { return RemoveLinkAniDBTvDB((int)x.a, (int)x.b, (int)x.c, (int)x.d, (int)x.e, (int)x.f); };
            Get["/RemoveLinkAniDBTvDBEpisode/{a}"] = x => { return RemoveLinkAniDBTvDBEpisode((int)x.a); };
            Get["/RemoveLinkAniDBTvDBForAnime/{a}"] = x => { return RemoveLinkAniDBTvDBForAnime((int)x.a); };
            Get["/RemoveMissingFiles/{a}"] = x => { return RemoveMissingFiles((int)x.a); };
            Get["/RenameAllGroups"] = x => { return RenameAllGroups(); };
            Get["/RenameFile/{a}/{b}"] = x => { return RenameFile((int)x.a,x.b); };
            Get["/RenameFilePreview/{a}/{b}"] = x => { return RenameFilePreview((int)x.a,x.b); };
            Get["/RescanFile/{a}"] = x => { return RescanFile((int)x.a); };
            Get["/RescanManuallyLinkedFiles"] = x => { return RescanManuallyLinkedFiles(); };
            Get["/RescanUnlinkedFiles"] = x => { return RescanUnlinkedFiles(); };
            Get["/RevokeTraktCrossRefWebCache/{a}"] = x => { return RevokeTraktCrossRefWebCache((int)x.a); };
            Get["/RevokeTVDBCrossRefWebCache/{a}"] = x => { return RevokeTVDBCrossRefWebCache((int)x.a); };
            Get["/RunImport"] = x => { return RunImport(); };
            Get["/ScanDropFolders"] = x => { return ScanDropFolders(); };
            Get["/ScanFolder/{a}"] = x => { return ScanFolder((int)x.a); };
            Get["/SearchForFiles/{a}/{b}/{c}"] = x => { return SearchForFiles((int)x.a,x.b, (int)x.c); };
            Get["/SearchMAL/{a}"] = x => { return SearchMAL(x.a); };
            Get["/SearchTheMovieDB/{a}"] = x => { return SearchTheMovieDB(x.a); };
            Get["/SearchTheTvDB/{a}"] = x => { return SearchTheTvDB(x.a); };
            Get["/SearchTrakt/{a}"] = x => { return SearchTrakt(x.a); };
            Get["/SetCommandProcessorGeneralPaused/{a}"] = x => { return SetCommandProcessorGeneralPaused((bool)x.a); };
            Get["/SetCommandProcessorHasherPaused/{a}"] = x => { return SetCommandProcessorHasherPaused((bool)x.a); };
            Get["/SetCommandProcessorImagesPaused/{a}"] = x => { return SetCommandProcessorImagesPaused((bool)x.a); };
            Get["/SetDefaultImage/{a}/{b}/{c}/{d}/{e}"] = x => { return SetDefaultImage((bool)x.a, (int)x.b, (int)x.c, (int)x.d, (int)x.e); };
            Get["/SetDefaultSeriesForGroup/{a}/{b}"] = x => { return SetDefaultSeriesForGroup((int)x.a, (int)x.b); };
            Get["/SetIgnoreStatusOnFile/{a}/{b}"] = x => { return SetIgnoreStatusOnFile((int)x.a, (bool)x.b); };
            Get["/SetVariationStatusOnFile/{a}/{b}"] = x => { return SetVariationStatusOnFile((int)x.a, (bool)x.b); };
            Get["/SetWatchedStatusOnSeries/{a}/{b}/{c}/{d}/{e}"] = x => { return SetWatchedStatusOnSeries((int)x.a, (bool)x.b, (int)x.c, (int)x.d, (int)x.e); };
            Get["/SyncHashes"] = x => { return SyncHashes(); };
            Get["/SyncMALDownload"] = x => { return SyncMALDownload(); };
            Get["/SyncMALUpload"] = x => { return SyncMALUpload(); };
            Get["/SyncMyList"] = x => { return SyncMyList(); };
            Get["/SyncTraktSeries/{a}"] = x => { return SyncTraktSeries((int)x.a); };
            Get["/SyncVotes"] = x => { return SyncVotes(); };
            Get["/TestAniDBConnection"] = x => { return TestAniDBConnection(); };
            Get["/TestMALLogin"] = x => { return TestMALLogin(); };
            Get["/ToggleWatchedStatusOnEpisode/{a}/{b}/{c}"] = x => { return ToggleWatchedStatusOnEpisode((int)x.a, (bool)x.b, (int)x.c); };
            Get["/ToggleWatchedStatusOnVideo/{a}/{b}/{c}"] = x => { return ToggleWatchedStatusOnVideo((int)x.a, (bool)x.b, (int)x.c); };
            Get["/TraktFriendRequestApprove/{a}/{b}"] = x => { return TraktFriendRequestApprove(x.a,x.b); };
            Get["/TraktFriendRequestDeny/{a}/{b}"] = x => { return TraktFriendRequestDeny(x.a,x.b); };
            Get["/UpdateAniDBFileData/{a}/{b}/{c}"] = x => { return UpdateAniDBFileData((bool)x.a, (bool)x.b, (bool)x.c); };
            Get["/UpdateAnimeData/{a}"] = x => { return UpdateAnimeData((int)x.a); };
            Get["/UpdateAnimeDisableExternalLinksFlag/{a}/{b}"] = x => { return UpdateAnimeDisableExternalLinksFlag((int)x.a, (int)x.b); };
            Get["/UpdateCalendarData"] = x => { return UpdateCalendarData(); };
            Get["/UpdateEpisodeData/{a}"] = x => { return UpdateEpisodeData((int)x.a); };
            Get["/UpdateFileData/{a}"] = x => { return UpdateFileData((int)x.a); };
            Get["/UpdateMovieDBData/{a}"] = x => { return UpdateMovieDBData((int)x.a); };
            Get["/UpdateTraktData/{a}"] = x => { return UpdateTraktData(x.a); };
            Get["/UpdateTvDBData/{a}"] = x => { return UpdateTvDBData((int)x.a); };
            Get["/UseMyTraktLinksWebCache"] = x => { return UseMyTraktLinksWebCache((int)x.a); };
            Get["/UseMyTvDBLinksWebCache"] = x => { return UseMyTvDBLinksWebCache((int)x.a); };
            Get["/VoteAnime/{a}/{b}/{c}"] = x => { return VoteAnime((int)x.a,(decimal)x.b, (int)x.c); };
            Get["/VoteAnimeRevoke/{a}"] = x => { return VoteAnimeRevoke((int)x.a); };

            //im unsure how to start this, probably need to be redone or not needed
            //some of them need to be POST so you can capture body request as object
            Get["/RemoveMissingMyListFiles/{a}"] = x => { return RemoveMissingMyListFiles(x.a); };
            Get["/RenameFiles/{a}/{b}"] = x => { return RenameFiles(x.a, x.b); };
            //Get["/GetSeriesForGroup/{a}"] = x => { return GetSeriesForGroup(x.a); };
            Get["/AssociateMultipleFiles/{a}/{b}/{c}/{d}"] = x => { return AssociateMultipleFiles(x.a, (int)x.a, (int)x.c, (bool)x.d); };
            Get["/EvaluateGroupFilter/{a}"] = x => { return EvaluateGroupFilter(x.a); };
            Get["/SaveBookmarkedAnime/{a}"] = x => { return SaveBookmarkedAnime(x.a); };
            Get["/SaveCustomTag/{a}"] = x => { return SaveCustomTag(x.a); };
            Get["/SaveCustomTagCrossRef/{a}"] = x => { return SaveCustomTagCrossRef(x.a); };
            Get["/SaveFFDPreset/{a}"] = x => { return SaveFFDPreset(x.a); };
            Get["/SaveGroup/{a}/{b}"] = x => { return SaveGroup(x.a, (int)x.b); };
            Get["/SaveGroupFilter/{a}"] = x => { return SaveGroupFilter(x.a); };
            Get["/SaveImportFolder/{a}"] = x => { return SaveImportFolder(x.a); };
            Get["/SavePlaylist/{a}"] = x => { return SavePlaylist(x.a); };
            Get["/SaveRenameScript/{a}"] = x => { return SaveRenameScript(x.a); };
            Get["/SaveSeries/{a}/{b}"] = x => { return SaveSeries(x.a, (int)x.b); };
            Get["/SaveServerSettings/{a}"] = x => { return SaveServerSettings(x.a); };
            Get["/SaveUser/{a}"] = x => { return SaveUser(x.a); };
        }

        JMMServiceImplementation _impl = new JMMServiceImplementation();
        Nancy.Response response;
        public static Nancy.Request request;

        private object Admin_GetRandomLinkForApproval(int type)
        {
            return _impl.Admin_GetRandomLinkForApproval(type);
        }

        private object ApproveTraktCrossRefWebCache(int id)
        {
            return _impl.ApproveTraktCrossRefWebCache(id);
        }

        private object ApproveTVDBCrossRefWebCache(int id)
        {
            return _impl.ApproveTVDBCrossRefWebCache(id);
        }

        private object AssociateSingleFile(int videoid, int animeid)
        {
            return _impl.AssociateSingleFile(videoid, animeid);
        }

        private object AssociateSingleFileWithMultipleEpisodes(int videoid, int animeid, int epstart, int epstop)
        {
            return _impl.AssociateSingleFileWithMultipleEpisodes(videoid, animeid, epstart, epstop);
        }

        private object AuthenticateUser(string usr, string pass)
        {
            return _impl.AuthenticateUser(usr, pass);
        }

        private object ChangePassword(int uid, string newpass)
        {
            return _impl.ChangePassword(uid, newpass);
        }

        private object CheckTraktLinkValidity(string slug, bool remove)
        {
            return _impl.CheckTraktLinkValidity(slug, remove);
        }

        private object ClearGeneralQueue()
        {
            _impl.ClearGeneralQueue();
            return "ok";
        }

        private object ClearHasherQueue()
        {
            _impl.ClearHasherQueue();
            return "ok";
        }

        private object ClearImagesQueue()
        {
            _impl.ClearImagesQueue();
            return "ok";
        }

        private object CreateSeriesFromAnime(int aid, int gid, int uid)
        {
            return _impl.CreateSeriesFromAnime(aid, gid, uid);
        }

        private object DeleteAnimeGroup(int agid, bool remove)
        {
            return _impl.DeleteAnimeGroup(agid, remove);
        }

        private object DeleteAnimeSeries(int asid, bool removefile, bool removeparent)
        {
            return _impl.DeleteAnimeSeries(asid, removefile, removeparent);
        }

        private object DeleteBookmarkedAnime(int id)
        {
            return _impl.DeleteBookmarkedAnime(id);
        }

        private object DeleteCustomTag(int tag)
        {
           return _impl.DeleteCustomTag(tag);
        }

        private object DeleteCustomTagCrossRef(int a, int b, int c)
        {
            //I started with random name values as its temporary
           return _impl.DeleteCustomTagCrossRef(a,b,c);
        }

        private object DeleteCustomTagCrossRefByID(int a)
        {
           return _impl.DeleteCustomTagCrossRefByID(a);
        }

        private object DeleteDuplicateFile(int a, int b)
        {
           return _impl.DeleteDuplicateFile(a, b);
        }

        private object DeleteFFDPreset(int a)
        {
           _impl.DeleteFFDPreset(a);
            return "ok";
        }

        private object DeleteFileFromMyList(int a)
        {
           _impl.DeleteFileFromMyList(a);
            return "ok";
        }

        private object DeleteGroupFilter(int a)
        {
           return _impl.DeleteGroupFilter(a);
        }

        private object DeleteImportFolder(int a)
        {
           return _impl.DeleteImportFolder(a);
        }

        private object DeletePlaylist(int a)
        {
           return _impl.DeletePlaylist(a);
        }

        private object DeleteRenameScript(int a)
        {
           return _impl.DeleteRenameScript(a);
        }

        private object DeleteUser(int a)
        {
           return _impl.DeleteUser(a);
        }

        private object DeleteVideoLocalAndFile(int a)
        {
            //return _impl.DeleteVideoLocalAndFile(a);
            // TODO check if still usable//
            return null;
        }

        private object EnableDisableImage(bool a, int b, int c)
        {
            return _impl.EnableDisableImage(a, b, c);
        }

        private object EnterTraktPIN(string a)
        {
           return _impl.EnterTraktPIN(a);
        }

        private object ForceAddFileToMyList(string a)
        {
           _impl.ForceAddFileToMyList(a);
            return "ok";
        }

        private object GetAdminMessages()
        {
           return _impl.GetAdminMessages();
        }

        private object GetAllAnime()
        {
           return _impl.GetAllAnime();
        }

        private object GetAllAnimeDetailed()
        {
           return _impl.GetAllAnimeDetailed();
        }

        private object GetAllBookmarkedAnime()
        {
           return _impl.GetAllBookmarkedAnime();
        }

        private object GetAllChanges(DateTime a, int b)
        {
           return _impl.GetAllChanges(a, b);
        }

        private object GetAllCustomTags()
        {
           return _impl.GetAllCustomTags();
        }

        private object GetAllDuplicateFiles()
        {
           return _impl.GetAllDuplicateFiles();
        }

        private object GetAllEpisodesWithMultipleFiles(int a, bool b, bool c)
        {
            return _impl.GetAllEpisodesWithMultipleFiles(a, b, c);
        }

        private object GetAllGroupFilters()
        {
           return _impl.GetAllGroupFilters();
        }

        private object GetAllGroupFiltersExtended(int a)
        {
           return _impl.GetAllGroupFiltersExtended(a);
        }

        private object GetAllGroups(int a)
        {
           return _impl.GetAllGroups(a);
        }

        private object GetAllGroupsAboveGroupInclusive(int a, int b)
        {
           return _impl.GetAllGroupsAboveGroupInclusive(a, b);
        }

        private object GetAllGroupsAboveSeries(int a, int b)
        {
           return _impl.GetAllGroupsAboveSeries(a, b);
        }

        private object GetAllManuallyLinkedFiles(int a)
        {
           return _impl.GetAllManuallyLinkedFiles(a);
        }

        private object GetAllMovieDBFanart(int a)
        {
           return _impl.GetAllMovieDBFanart(a);
        }

        private object GetAllMovieDBPosters(int a)
        {
           return _impl.GetAllMovieDBPosters(a);
        }

        private object GetAllPlaylists()
        {
           return _impl.GetAllPlaylists();
        }

        private object GetAllRenameScripts()
        {
           return _impl.GetAllRenameScripts();
        }

        private object GetAllSeries(int a)
        {
           return _impl.GetAllSeries(a);
        }

        private object GetAllTagNames()
        {
           return _impl.GetAllTagNames();
        }

        private object GetAllTraktCrossRefs()
        {
           return _impl.GetAllTraktCrossRefs();
        }

        private object GetAllTraktEpisodes(int a)
        {
           return _impl.GetAllTraktEpisodes(a);
        }

        private object GetAllTraktEpisodesByTraktID(string a)
        {
           return _impl.GetAllTraktEpisodesByTraktID(a);
        }

        private object GetAllTraktFanart(int a)
        {
           return _impl.GetAllTraktFanart(a);
        }

        private object GetAllTraktPosters(int a)
        {
           return _impl.GetAllTraktPosters(a);
        }

        private object GetAllTvDBEpisodes(int a)
        {
           return _impl.GetAllTvDBEpisodes(a);
        }

        private object GetAllTvDBFanart(int a)
        {
           return _impl.GetAllTvDBFanart(a);
        }

        private object GetAllTvDBPosters(int a)
        {
           return _impl.GetAllTvDBPosters(a);
        }

        private object GetAllTvDBWideBanners(int a)
        {
           return _impl.GetAllTvDBWideBanners(a);
        }

        private object GetAllUniqueAudioLanguages()
        {
           return _impl.GetAllUniqueAudioLanguages();
        }

        private object GetAllUniqueSubtitleLanguages()
        {
           return _impl.GetAllUniqueSubtitleLanguages();
        }

        private object GetAllUniqueVideoQuality()
        {
           return _impl.GetAllUniqueVideoQuality();
        }

        private object GetAllUnwatchedEpisodes(int a, int b)
        {
           return _impl.GetAllUnwatchedEpisodes(a, b);
        }

        private object GetAllUsers()
        {
           return _impl.GetAllUsers();
        }

        private object GetAniDBEpisodesForAnime(int a)
        {
           return _impl.GetAniDBEpisodesForAnime(a);
        }

        private object GetAniDBRecommendations(int a)
        {
           return _impl.GetAniDBRecommendations(a);
        }

        private object GetAniDBSeiyuu(int a)
        {
           return _impl.GetAniDBSeiyuu(a);
        }

        private object GetAnime(int a)
        {
           return _impl.GetAnime(a);
        }

        private object GetAnimeDetailed(int a)
        {
           return _impl.GetAnimeDetailed(a);
        }

        private object GetAnimeForMonth(int a, int b, int c)
        {
           return _impl.GetAnimeForMonth(a, b, c);
        }

        private object GetAnimeGroupsForFilter(int a, int b, bool c)
        {
           return _impl.GetAnimeGroupsForFilter(a, b, c);
        }

        private object GetAnimeRatings(int a, int b, int c, int d)
        {
           return _impl.GetAnimeRatings(a,b,c,d);
        }

        private object GetAppVersions()
        {
           return _impl.GetAppVersions();
        }

        private object GetBookmarkedAnime(int a)
        {
           return _impl.GetBookmarkedAnime(a);
        }

        private object GetCharactersForAnime(int a)
        {
           return _impl.GetCharactersForAnime(a);
        }

        private object GetCharactersForSeiyuu(int a)
        {
           return _impl.GetCharactersForSeiyuu(a);
        }

        private object GetContinueWatchingFilter(int a, int b)
        {
           return _impl.GetContinueWatchingFilter(a,b);
        }

        private object GetCrossRefDetails(int a)
        {
           return _impl.GetCrossRefDetails(a);
        }

        private object GetCustomTag(int a)
        {
           return _impl.GetCustomTag(a);
        }

        private object GetEpisode(int a, int b)
        {
           return _impl.GetEpisode(a,b);
        }

        private object GetEpisodeByAniDBEpisodeID(int a, int b)
        {
           return _impl.GetEpisodeByAniDBEpisodeID(a,b);
        }

        private object GetEpisodesForFile(int a, int b)
        {
           return _impl.GetEpisodesForFile(a,b);
        }

        private object GetEpisodesForSeries(int a, int b)
        {
           return _impl.GetEpisodesForSeries(a,b);
        }

        private object GetEpisodesForSeriesOld(int a)
        {
           return _impl.GetEpisodesForSeriesOld(a);
        }

        private object GetEpisodesRecentlyAdded(int a, int b)
        {
           return _impl.GetEpisodesRecentlyAdded(a,b);
        }

        private object GetEpisodesRecentlyAddedSummary(int a, int b)
        {
           return _impl.GetEpisodesRecentlyAddedSummary(a,b);
        }

        private object GetEpisodesRecentlyWatched(int a, int b)
        {
           return _impl.GetEpisodesRecentlyWatched(a,b);
        }

        private object GetEpisodesToWatch_RecentlyWatched(int a, int b)
        {
           return _impl.GetEpisodesToWatch_RecentlyWatched(a,b);
        }

        private object GetFFDPreset(int a)
        {
           return _impl.GetFFDPreset(a);
        }

        private object GetFilesByGroup(int a,string b, int c)
        {
           return _impl.GetFilesByGroup(a,b,c);
        }

        private object GetFilesByGroupAndResolution(int a, string b, string c, string d, int e, int f)
        {
           return _impl.GetFilesByGroupAndResolution(a,b,c,d,e,f);
        }

        private object GetFilesForEpisode(int a, int b)
        {
           return _impl.GetFilesForEpisode(a,b);
        }

        private object GetGroup(int a, int b)
        {
           return _impl.GetGroup(a,b);
        }

        private object GetGroupFileSummary(int a)
        {
           return _impl.GetGroupFileSummary(a);
        }

        private object GetGroupFilter(int a)
        {
           return _impl.GetGroupFilter(a);
        }

        private object GetGroupFilterChanges(DateTime a)
        {
           return _impl.GetGroupFilterChanges(a);
        }

        private object GetGroupFilterExtended(int a, int b)
        {
           return _impl.GetGroupFilterExtended(a,b);
        }

        private object GetGroupFilters(int a)
        {
           return _impl.GetGroupFilters(a);
        }

        private object GetGroupFiltersExtended(int a)
        {
           return _impl.GetGroupFiltersExtended(a);
        }

        private object GetGroupVideoQualitySummary(int a)
        {
           return _impl.GetGroupVideoQualitySummary(a);
        }

        private object GetHashCode()
        {
           return _impl.GetHashCode();
        }

        private object GetIgnoredAnime(int a)
        {
           return _impl.GetIgnoredAnime(a);
        }

        private object GetIgnoredFiles(int a)
        {
           return _impl.GetIgnoredFiles(a);
        }

        private object GetImportFolders()
        {
           return _impl.GetImportFolders();
        }

        private object GetLastWatchedEpisodeForSeries(int a, int b)
        {
           return _impl.GetLastWatchedEpisodeForSeries(a,b);
        }

        private object GetMALCrossRefWebCache(int a)
        {
           return _impl.GetMALCrossRefWebCache(a);
        }

        private object GetManuallyLinkedFiles(int a)
        {
           return _impl.GetManuallyLinkedFiles(a);
        }

        private object GetMiniCalendar(int a, int b)
        {
           return _impl.GetMiniCalendar(a,b);
        }

        private object GetMissingEpisodes(int a, bool b, bool c, int d)
        {
           return _impl.GetMissingEpisodes(a,b,c,d);
        }

        private object GetMyListFilesForRemoval(int a)
        {
           return _impl.GetMyListFilesForRemoval(a);
        }

        private object GetMyReleaseGroupsForAniDBEpisode(int a)
        {
           return _impl.GetMyReleaseGroupsForAniDBEpisode(a);
        }

        private object GetNextUnwatchedEpisode(int a, int b)
        {
           return _impl.GetNextUnwatchedEpisode(a,b);
        }

        private object GetNextUnwatchedEpisodeForGroup(int a, int b)
        {
           return _impl.GetNextUnwatchedEpisodeForGroup(a,b);
        }

        private object GetOtherAnimeCrossRef(int a, int b)
        {
           return _impl.GetOtherAnimeCrossRef(a,b);
        }

        private object GetOtherAnimeCrossRefWebCache(int a, int b)
        {
           return _impl.GetOtherAnimeCrossRefWebCache(a,b);
        }

        private object GetPlaylist(int a)
        {
           return _impl.GetPlaylist(a);
        }

        private object GetPreviousEpisodeForUnwatched(int a, int b)
        {
           return _impl.GetPreviousEpisodeForUnwatched(a,b);
        }

        private object GetRecommendations(int a, int b, int c)
        {
           return _impl.GetRecommendations(a,b,c);
        }

        private object GetRelatedAnimeLinks(int a, int b)
        {
           return _impl.GetRelatedAnimeLinks(a,b);
        }

        private object GetReleaseGroupsForAnime(int a)
        {
           return _impl.GetReleaseGroupsForAnime(a);
        }

        private object GetSeasonNumbersForSeries(int a)
        {
           return _impl.GetSeasonNumbersForSeries(a);
        }

        private object GetSeasonNumbersForTrakt(string a)
        {
           return _impl.GetSeasonNumbersForTrakt(a);
        }

        private object GetSeries(int a, int b)
        {
           return _impl.GetSeries(a, b);
        }

        private object GetSeriesExistingForAnime(int a)
        {
           return _impl.GetSeriesExistingForAnime(a);
        }

        private object GetSeriesForAnime(int a, int b)
        {
           return _impl.GetSeriesForAnime(a,b);
        }

        private object GetSeriesForGroupRecursive(int a, int b)
        {
           return _impl.GetSeriesForGroupRecursive(a,b);
        }

        private object GetSeriesRecentlyAdded(int a, int b)
        {
           return _impl.GetSeriesRecentlyAdded(a,b);
        }

        private object GetSeriesWithMissingEpisodes(int a, int b)
        {
           return _impl.GetSeriesWithMissingEpisodes(a,b);
        }

        private object GetSeriesWithoutAnyFiles(int a)
        {
           return _impl.GetSeriesWithoutAnyFiles(a);
        }

        private object GetServerSettings()
        {
           return _impl.GetServerSettings();
        }

        private object GetServerStatus()
        {
           return _impl.GetServerStatus();
        }

        private object GetSimilarAnimeLinks(int a, int b)
        {
           return _impl.GetSimilarAnimeLinks(a, b);
        }

        private object GetSubGroupsForGroup(int a, int b)
        {
           return _impl.GetSubGroupsForGroup(a,b);
        }

        private object GetTopLevelGroupForSeries(int a, int b)
        {
           return _impl.GetTopLevelGroupForSeries(a,b);
        }

        private object GetTraktCommentsForAnime(int a)
        {
           return _impl.GetTraktCommentsForAnime(a);
        }

        private object GetTraktCrossRefEpisode(int a)
        {
           return _impl.GetTraktCrossRefEpisode(a);
        }

        private object GetTraktCrossRefV2(int a)
        {
           return _impl.GetTraktCrossRefV2(a);
        }

        private object GetTraktCrossRefWebCache(int a, bool b)
        {
           return _impl.GetTraktCrossRefWebCache(a,b);
        }

        private object GetTVDBCrossRefEpisode(int a)
        {
           return _impl.GetTVDBCrossRefEpisode(a);
        }

        private object GetTVDBCrossRefV2(int a)
        {
           return _impl.GetTVDBCrossRefV2(a);
        }

        private object GetTVDBCrossRefWebCache(int a, bool b)
        {
           return _impl.GetTVDBCrossRefWebCache(a, b);
        }

        private object GetTvDBLanguages()
        {
           return _impl.GetTvDBLanguages();
        }

        private object GetUnrecognisedFiles(int a)
        {
           return _impl.GetUnrecognisedFiles(a);
        }

        private object GetUserVote(int a)
        {
           return _impl.GetUserVote(a);
        }

        private object GetVideoDetailed(int a, int b)
        {
           return _impl.GetVideoDetailed(a,b);
        }

        private object GetVideoLocalsForAnime(int a, int b)
        {
           return _impl.GetVideoLocalsForAnime(a,b);
        }

        private object GetVideoLocalsForEpisode(int a, int b)
        {
           return _impl.GetVideoLocalsForEpisode(a,b);
        }

        private object IgnoreAnime(int a, int b, int c)
        {
           _impl.IgnoreAnime(a,b,c);
            return "ok";
        }

        private object IncrementEpisodeStats(int a, int b, int c)
        {
           _impl.IncrementEpisodeStats(a,b,c);
            return "ok";
        }

        private object IsWebCacheAdmin()
        {
           return _impl.IsWebCacheAdmin();
        }

        private object LinkAniDBMAL(int a, int b, string c, int d, int e)
        {
           return _impl.LinkAniDBMAL(a,b,c,d,e);
        }

        private object LinkAniDBMALUpdated(int a, int b, string c, int d, int e, int f, int g)
        {
           return _impl.LinkAniDBMALUpdated(a,b,c,d,e,f,g);
        }

        private object LinkAniDBOther(int a, int b, int c)
        {
           return _impl.LinkAniDBOther(a,b,c);
        }

        private object LinkAniDBTrakt(int a, int b, int c, string d, int e, int f, int g)
        {
           return _impl.LinkAniDBTrakt(a,b,c, d,e,f,g);
        }

        private object LinkAniDBTvDB(int a, int b, int c, int d, int e, int f, int g)
        {
           return _impl.LinkAniDBTvDB(a,b,c,d,e,f,g);
        }

        private object LinkAniDBTvDBEpisode(int a, int b, int c)
        {
           return _impl.LinkAniDBTvDBEpisode(a,b,c);
        }

        private object MoveSeries(int a, int b, int c)
        {
           return _impl.MoveSeries(a,b,c);
        }

        private object OnlineAnimeTitleSearch(string a)
        {
           return _impl.OnlineAnimeTitleSearch(a);
        }

        private object PostTraktCommentShow(string a, string b, bool c, string d)
        {
           return _impl.PostTraktCommentShow(a,b,c,ref d);
        }

        private object RandomFileRenamePreview(int a, int b)
        {
           return _impl.RandomFileRenamePreview(a,b);
        }

        private object RecreateAllGroups()
        {
            new AnimeGroupCreator().RecreateAllGroups();
            return "ok";
        }

        private object ReevaluateDuplicateFiles()
        {
           _impl.ReevaluateDuplicateFiles();
            return "ok";
        }

        private object RefreshAllMediaInfo()
        {
           _impl.RefreshAllMediaInfo();
            return "ok";
        }

        private object RehashFile(int a)
        {
           _impl.RehashFile(a);
            return "ok";
        }

        private object RemoveAssociationOnFile(int a, int b)
        {
           return _impl.RemoveAssociationOnFile(a,b);
        }

        private object RemoveDefaultSeriesForGroup(int a)
        {
           _impl.RemoveDefaultSeriesForGroup(a);
            return "ok";
        }

        private object RemoveIgnoreAnime(int a)
        {
           _impl.RemoveIgnoreAnime(a);
            return "ok";
        }

        private object RemoveLinkAniDBMAL(int a, int b, int c)
        {
           return _impl.RemoveLinkAniDBMAL(a,b,c);
        }

        private object RemoveLinkAniDBOther(int a, int b)
        {
           return _impl.RemoveLinkAniDBOther(a,b);
        }

        private object RemoveLinkAniDBTrakt(int a, int b, int c, string d, int e, int f)
        {
           return _impl.RemoveLinkAniDBTrakt(a,b,c,d,e,f);
        }

        private object RemoveLinkAniDBTraktForAnime(int a)
        {
           return _impl.RemoveLinkAniDBTraktForAnime(a);
        }

        private object RemoveLinkAniDBTvDB(int a, int b, int c, int d, int e, int f)
        {
           return _impl.RemoveLinkAniDBTvDB(a,b,c,d,e,f);
        }

        private object RemoveLinkAniDBTvDBEpisode(int a)
        {
           return _impl.RemoveLinkAniDBTvDBEpisode(a);
        }

        private object RemoveLinkAniDBTvDBForAnime(int a)
        {
           return _impl.RemoveLinkAniDBTvDBForAnime(a);
        }

        private object RemoveMissingFiles(int a)
        {
           _impl.RemoveMissingFiles();
            return "ok";
        }

        private object RenameAllGroups()
        {
           return _impl.RenameAllGroups();
        }

        private object RenameFilePreview(int a, string b)
        {
           return _impl.RenameFilePreview(a,b);
        }

        private object RescanFile(int a)
        {
           return _impl.RescanFile(a);
        }

        private object RescanManuallyLinkedFiles()
        {
           _impl.RescanManuallyLinkedFiles();
            return "ok";
        }

        private object RescanUnlinkedFiles()
        {
           _impl.RescanUnlinkedFiles();
            return "ok";
        }

        private object RevokeTraktCrossRefWebCache(int a)
        {
           return _impl.RevokeTraktCrossRefWebCache(a);
        }

        private object RevokeTVDBCrossRefWebCache(int a)
        {
           return _impl.RevokeTVDBCrossRefWebCache(a);
        }

        private object RunImport()
        {
            _impl.RunImport();
            return "ok";
        }

        private object ScanDropFolders()
        {
           _impl.ScanDropFolders();
            return "ok";
        }

        private object ScanFolder(int a)
        {
           _impl.ScanFolder(a);
            return "ok";
        }

        private object SearchForFiles(int a, string b, int c)
        {
           return _impl.SearchForFiles(a,b,c);
        }

        private object SearchMAL(string a)
        {
           return _impl.SearchMAL(a);
        }

        private object SearchTheMovieDB(string a)
        {
           return _impl.SearchTheMovieDB(a);
        }

        private object SearchTheTvDB(string a)
        {
           return _impl.SearchTheTvDB(a);
        }

        private object SearchTrakt(string a)
        {
           return _impl.SearchTrakt(a);
        }

        private object SetCommandProcessorGeneralPaused(bool a)
        {
           _impl.SetCommandProcessorGeneralPaused(a);
            return "ok";
        }

        private object SetCommandProcessorHasherPaused(bool a)
        {
           _impl.SetCommandProcessorHasherPaused(a);
            return "ok";
        }

        private object SetCommandProcessorImagesPaused(bool a)
        {
           _impl.SetCommandProcessorImagesPaused(a);
            return "ok";
        }

        private object SetDefaultImage(bool a, int b, int c, int d, int e)
        {
           return _impl.SetDefaultImage(a,b,c,d,e);
        }

        private object SetDefaultSeriesForGroup(int a, int b)
        {
           _impl.SetDefaultSeriesForGroup(a, b);
            return "ok";
        }

        private object SetIgnoreStatusOnFile(int a, bool b)
        {
           return _impl.SetIgnoreStatusOnFile(a, b);
        }

        private object SetVariationStatusOnFile(int a, bool b)
        {
           return _impl.SetVariationStatusOnFile(a,b);
        }

        private object SetWatchedStatusOnSeries(int a, bool b, int c , int d, int e)
        {
           return _impl.SetWatchedStatusOnSeries(a,b,c,d,e);
        }

        private object SyncHashes()
        {
           _impl.SyncHashes();
            return "ok";
        }

        private object SyncMALDownload()
        {
            _impl.SyncMALDownload();
            return "ok";
        }

        private object SyncMALUpload()
        {
          _impl.SyncMALUpload();
            return "ok";
        }

        private object SyncMyList()
        {
           _impl.SyncMyList();
            return "ok";
        }

        private object SyncTraktSeries(int a)
        {
           return _impl.SyncTraktSeries(a);
        }

        private object SyncVotes()
        {
           _impl.SyncVotes();
            return "ok";
        }

        private object TestAniDBConnection()
        {
           return _impl.TestAniDBConnection();
        }

        private object TestMALLogin()
        {
           return _impl.TestMALLogin();
        }

        private object ToggleWatchedStatusOnEpisode(int a, bool b, int c)
        {
           return _impl.ToggleWatchedStatusOnEpisode(a,b,c);
        }

        private object ToggleWatchedStatusOnVideo(int a, bool b, int c)
        {
           return _impl.ToggleWatchedStatusOnVideo(a,b,c);
        }

        private object TraktFriendRequestApprove(string a, string b)
        {
           return _impl.TraktFriendRequestApprove(a, ref b);
        }

        private object TraktFriendRequestDeny(string a, string b)
        {
           return _impl.TraktFriendRequestDeny(a, ref b);
        }

        private object UpdateAniDBFileData(bool a, bool b, bool c)
        {
           return _impl.UpdateAniDBFileData(a,b,c);
        }

        private object UpdateAnimeData(int a)
        {
           return _impl.UpdateAnimeData(a);
        }

        private object UpdateAnimeDisableExternalLinksFlag(int a, int b)
        {
            _impl.UpdateAnimeDisableExternalLinksFlag(a,b);
            return "ok";
        }

        private object UpdateCalendarData()
        {
           return _impl.UpdateCalendarData();
        }

        private object UpdateEpisodeData(int a)
        {
           return _impl.UpdateEpisodeData(a);
        }

        private object UpdateFileData(int a)
        {
           return _impl.UpdateFileData(a);
        }

        private object UpdateMovieDBData(int a)
        {
           return _impl.UpdateMovieDBData(a);
        }

        private object UpdateTraktData(string a)
        {
           return _impl.UpdateTraktData(a);
        }

        private object UpdateTvDBData(int a)
        {
           return _impl.UpdateTvDBData(a);
        }

        private object UseMyTraktLinksWebCache(int a)
        {
           return _impl.UseMyTraktLinksWebCache(a);
        }

        private object UseMyTvDBLinksWebCache(int a)
        {
           return _impl.UseMyTvDBLinksWebCache(a);
        }

        private object VoteAnime(int a, decimal b, int c)
        {
           _impl.VoteAnime(a, b, c);
            return "ok";
        }

        private object VoteAnimeRevoke(int a)
        {
           _impl.VoteAnimeRevoke(a);
            return "ok"; 
        }

        //Not using simple parameters
        //private object GetSeriesForGroup(int a, List<Entities.AnimeSeries> b)
        //{
        //    return _impl.GetSeriesForGroup(a,b);
        //}

        private object SaveBookmarkedAnime(JMMContracts.Contract_BookmarkedAnime a)
        {
            return _impl.SaveBookmarkedAnime(a);
        }

        private object SaveCustomTag(JMMContracts.Contract_CustomTag a)
        {
            return _impl.SaveCustomTag(a);
        }

        private object SaveCustomTagCrossRef(JMMContracts.Contract_CrossRef_CustomTag a)
        {
            return _impl.SaveCustomTagCrossRef(a);
        }

        private object SaveFFDPreset(JMMContracts.Contract_FileFfdshowPreset a)
        {
             _impl.SaveFFDPreset(a);
            return "ok";
        }

        private object SaveGroup(JMMContracts.Contract_AnimeGroup_Save a, int b)
        {
            return _impl.SaveGroup(a,b);
        }

        private object SaveGroupFilter(JMMContracts.Contract_GroupFilter a)
        {
            return _impl.SaveGroupFilter(a);
        }

        private object SaveImportFolder(JMMContracts.Contract_ImportFolder a)
        {
            return _impl.SaveImportFolder(a);
        }

        private object SavePlaylist(JMMContracts.Contract_Playlist a)
        {
            return _impl.SavePlaylist(a);
        }

        private object SaveRenameScript(JMMContracts.Contract_RenameScript a)
        {
            return _impl.SaveRenameScript(a);
        }

        private object SaveSeries(JMMContracts.Contract_AnimeSeries_Save a, int b)
        {
            return _impl.SaveSeries(a,b);
        }

        private object SaveServerSettings(JMMContracts.Contract_ServerSettings a)
        {
            return _impl.SaveServerSettings(a);
        }

        private object SaveUser(JMMContracts.Contract_JMMUser a)
        {
            return _impl.SaveUser(a);
        }

        private object RenameFile(int a, string b)
        {
            return _impl.RenameFile(a, b);
        }

        private object RenameFiles(List<int> a, string b)
        {
            return _impl.RenameFiles(a,b);
        }

        private object RemoveMissingMyListFiles(List<JMMContracts.Contract_MissingFile> a)
        {
            _impl.RemoveMissingMyListFiles(a);
            return "ok";
        }

        private object AssociateMultipleFiles(List<int> a, int b, int c , bool d)
        {
            return _impl.AssociateMultipleFiles(a, b, c, d);
        }

        private object EvaluateGroupFilter(JMMContracts.Contract_GroupFilter a)
        {
            return _impl.EvaluateGroupFilter(a);
        }
    }
}
