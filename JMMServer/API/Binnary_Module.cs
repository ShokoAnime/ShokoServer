using Nancy;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace JMMServer.API
{
    public class Binnary_Module : NancyModule
    {
        public Binnary_Module() : base("/bin")
        {
            Get["/Admin_GetRandomLinkForApproval/{type}"] = x => { return Admin_GetRandomLinkForApproval((int)x.type); };
            Get["/ApproveTraktCrossRefWebCache/{id}"] = x => { return ApproveTraktCrossRefWebCache((int)x.id); };
            Get["/ApproveTVDBCrossRefWebCache/{id}"] = x => { return ApproveTVDBCrossRefWebCache((int)x.id); };
            Get["/AssociateMultipleFiles"] = x => { return AssociateMultipleFiles(); };
            Get["/AssociateSingleFile/{a}/{b}"] = x => { return AssociateSingleFile((int)x.a, (int)x.b); };
            Get["/AssociateSingleFileWithMultipleEpisodes/{a}/{b}/{c}/{d}"] = x => { return AssociateSingleFileWithMultipleEpisodes((int)x.a, (int)x.b,(int)x.c, (int)x.d); };
            Get["/AuthenticateUser/{a}/{b}"] = x => { return AuthenticateUser(x.a, x.b); };
            Get["/ChangePassword/{a}/{b}"] = x => { return ChangePassword((int)x.a, x.b); };

            Get["/CheckTraktLinkValidity"] = x => { return CheckTraktLinkValidity(); };
            Get["/ClearGeneralQueue"] = x => { return ClearGeneralQueue(); };
            Get["/ClearHasherQueue"] = x => { return ClearHasherQueue(); };
            Get["/ClearImagesQueue"] = x => { return ClearImagesQueue(); };
            Get["/CreateSeriesFromAnime"] = x => { return CreateSeriesFromAnime(); };
            Get["/DeleteAnimeGroup"] = x => { return DeleteAnimeGroup(); };
            Get["/DeleteAnimeSeries"] = x => { return DeleteAnimeSeries(); };
            Get["/DeleteBookmarkedAnime"] = x => { return DeleteBookmarkedAnime(); };
            Get["/DeleteCustomTag"] = x => { return DeleteCustomTag(); };
            Get["/DeleteCustomTagCrossRef"] = x => { return DeleteCustomTagCrossRef(); };
            Get["/DeleteCustomTagCrossRefByID"] = x => { return DeleteCustomTagCrossRefByID(); };
            Get["/DeleteDuplicateFile"] = x => { return DeleteDuplicateFile(); };
            Get["/DeleteFFDPreset"] = x => { return DeleteFFDPreset(); };
            Get["/DeleteFileFromMyList"] = x => { return DeleteFileFromMyList(); };
            Get["/DeleteGroupFilter"] = x => { return DeleteGroupFilter(); };
            Get["/DeleteImportFolder"] = x => { return DeleteImportFolder(); };
            Get["/DeletePlaylist"] = x => { return DeletePlaylist(); };
            Get["/DeleteRenameScript"] = x => { return DeleteRenameScript(); };
            Get["/DeleteUser"] = x => { return DeleteUser(); };
            Get["/DeleteVideoLocalAndFile"] = x => { return DeleteVideoLocalAndFile(); };
            Get["/EnableDisableImage"] = x => { return EnableDisableImage(); };
            Get["/EnterTraktPIN"] = x => { return EnterTraktPIN(); };
            Get["/EvaluateGroupFilter"] = x => { return EvaluateGroupFilter(); };
            Get["/ForceAddFileToMyList"] = x => { return ForceAddFileToMyList(); };
            Get["/GetAdminMessages"] = x => { return GetAdminMessages(); };
            Get["/GetAllAnime"] = x => { return GetAllAnime(); };
            Get["/GetAllAnimeDetailed"] = x => { return GetAllAnimeDetailed(); };
            Get["/GetAllBookmarkedAnime"] = x => { return GetAllBookmarkedAnime(); };
            Get["/GetAllChanges"] = x => { return GetAllChanges(); };
            Get["/GetAllCustomTags"] = x => { return GetAllCustomTags(); };
            Get["/GetAllDuplicateFiles"] = x => { return GetAllDuplicateFiles(); };
            Get["/GetAllEpisodesWithMultipleFiles"] = x => { return GetAllEpisodesWithMultipleFiles(); };
            Get["/GetAllGroupFilters"] = x => { return GetAllGroupFilters(); };
            Get["/GetAllGroupFiltersExtended"] = x => { return GetAllGroupFiltersExtended(); };
            Get["/GetAllGroups"] = x => { return GetAllGroups(); };
            Get["/GetAllGroupsAboveGroupInclusive"] = x => { return GetAllGroupsAboveGroupInclusive(); };
            Get["/GetAllGroupsAboveSeries"] = x => { return GetAllGroupsAboveSeries(); };
            Get["/GetAllManuallyLinkedFiles"] = x => { return GetAllManuallyLinkedFiles(); };
            Get["/GetAllMovieDBFanart"] = x => { return GetAllMovieDBFanart(); };
            Get["/GetAllMovieDBPosters"] = x => { return GetAllMovieDBPosters(); };
            Get["/GetAllPlaylists"] = x => { return GetAllPlaylists(); };
            Get["/GetAllRenameScripts"] = x => { return GetAllRenameScripts(); };
            Get["/GetAllSeries"] = x => { return GetAllSeries(); };
            Get["/GetAllTagNames"] = x => { return GetAllTagNames(); };
            Get["/GetAllTraktCrossRefs"] = x => { return GetAllTraktCrossRefs(); };
            Get["/GetAllTraktEpisodes"] = x => { return GetAllTraktEpisodes(); };
            Get["/GetAllTraktEpisodesByTraktID"] = x => { return GetAllTraktEpisodesByTraktID(); };
            Get["/GetAllTraktFanart"] = x => { return GetAllTraktFanart(); };
            Get["/GetAllTraktPosters"] = x => { return GetAllTraktPosters(); };
            Get["/GetAllTvDBEpisodes"] = x => { return GetAllTvDBEpisodes(); };
            Get["/GetAllTvDBFanart"] = x => { return GetAllTvDBFanart(); };
            Get["/GetAllTvDBPosters"] = x => { return GetAllTvDBPosters(); };
            Get["/GetAllTvDBWideBanners"] = x => { return GetAllTvDBWideBanners(); };
            Get["/GetAllUniqueAudioLanguages"] = x => { return GetAllUniqueAudioLanguages(); };
            Get["/GetAllUniqueSubtitleLanguages"] = x => { return GetAllUniqueSubtitleLanguages(); };
            Get["/GetAllUniqueVideoQuality"] = x => { return GetAllUniqueVideoQuality(); };
            Get["/GetAllUnwatchedEpisodes"] = x => { return GetAllUnwatchedEpisodes(); };
            Get["/GetAllUsers"] = x => { return GetAllUsers(); };
            Get["/GetAniDBEpisodesForAnime"] = x => { return GetAniDBEpisodesForAnime(); };
            Get["/GetAniDBRecommendations"] = x => { return GetAniDBRecommendations(); };
            Get["/GetAniDBSeiyuu"] = x => { return GetAniDBSeiyuu(); };
            Get["/GetAnime"] = x => { return GetAnime(); };
            Get["/GetAnimeDetailed"] = x => { return GetAnimeDetailed(); };
            Get["/GetAnimeForMonth"] = x => { return GetAnimeForMonth(); };
            Get["/GetAnimeGroupsForFilter"] = x => { return GetAnimeGroupsForFilter(); };
            Get["/GetAnimeRatings"] = x => { return GetAnimeRatings(); };
            Get["/GetAppVersions"] = x => { return GetAppVersions(); };
            Get["/GetBookmarkedAnime"] = x => { return GetBookmarkedAnime(); };
            Get["/GetCharactersForAnime"] = x => { return GetCharactersForAnime(); };
            Get["/GetCharactersForSeiyuu"] = x => { return GetCharactersForSeiyuu(); };
            Get["/GetContinueWatchingFilter"] = x => { return GetContinueWatchingFilter(); };
            Get["/GetCrossRefDetails"] = x => { return GetCrossRefDetails(); };
            Get["/GetCustomTag"] = x => { return GetCustomTag(); };
            Get["/GetEpisode"] = x => { return GetEpisode(); };
            Get["/GetEpisodeByAniDBEpisodeID"] = x => { return GetEpisodeByAniDBEpisodeID(); };
            Get["/GetEpisodesForFile"] = x => { return GetEpisodesForFile(); };
            Get["/GetEpisodesForSeries"] = x => { return GetEpisodesForSeries(); };
            Get["/GetEpisodesForSeriesOld"] = x => { return GetEpisodesForSeriesOld(); };
            Get["/GetEpisodesRecentlyAdded"] = x => { return GetEpisodesRecentlyAdded(); };
            Get["/GetEpisodesRecentlyAddedSummary"] = x => { return GetEpisodesRecentlyAddedSummary(); };
            Get["/GetEpisodesRecentlyWatched"] = x => { return GetEpisodesRecentlyWatched(); };
            Get["/GetEpisodesToWatch_RecentlyWatched"] = x => { return GetEpisodesToWatch_RecentlyWatched(); };
            Get["/GetFFDPreset"] = x => { return GetFFDPreset(); };
            Get["/GetFilesByGroup"] = x => { return GetFilesByGroup(); };
            Get["/GetFilesByGroupAndResolution"] = x => { return GetFilesByGroupAndResolution(); };
            Get["/GetFilesForEpisode"] = x => { return GetFilesForEpisode(); };
            Get["/GetGroup"] = x => { return GetGroup(); };
            Get["/GetGroupFileSummary"] = x => { return GetGroupFileSummary(); };
            Get["/GetGroupFilter"] = x => { return GetGroupFilter(); };
            Get["/GetGroupFilterChanges"] = x => { return GetGroupFilterChanges(); };
            Get["/GetGroupFilterExtended"] = x => { return GetGroupFilterExtended(); };
            Get["/GetGroupFilters"] = x => { return GetGroupFilters(); };
            Get["/GetGroupFiltersExtended"] = x => { return GetGroupFiltersExtended(); };
            Get["/GetGroupVideoQualitySummary"] = x => { return GetGroupVideoQualitySummary(); };
            Get["/GetHashCode"] = x => { return GetHashCode(); };
            Get["/GetIgnoredAnime"] = x => { return GetIgnoredAnime(); };
            Get["/GetIgnoredFiles"] = x => { return GetIgnoredFiles(); };
            Get["/GetImportFolders"] = x => { return GetImportFolders(); };
            Get["/GetLastWatchedEpisodeForSeries"] = x => { return GetLastWatchedEpisodeForSeries(); };
            Get["/GetMALCrossRefWebCache"] = x => { return GetMALCrossRefWebCache(); };
            Get["/GetManuallyLinkedFiles"] = x => { return GetManuallyLinkedFiles(); };
            Get["/GetMiniCalendar"] = x => { return GetMiniCalendar(); };
            Get["/GetMissingEpisodes"] = x => { return GetMissingEpisodes(); };
            Get["/GetMyListFilesForRemoval"] = x => { return GetMyListFilesForRemoval(); };
            Get["/GetMyReleaseGroupsForAniDBEpisode"] = x => { return GetMyReleaseGroupsForAniDBEpisode(); };
            Get["/GetNextUnwatchedEpisode"] = x => { return GetNextUnwatchedEpisode(); };
            Get["/GetNextUnwatchedEpisodeForGroup"] = x => { return GetNextUnwatchedEpisodeForGroup(); };
            Get["/GetOtherAnimeCrossRef"] = x => { return GetOtherAnimeCrossRef(); };
            Get["/GetOtherAnimeCrossRefWebCache"] = x => { return GetOtherAnimeCrossRefWebCache(); };
            Get["/GetPlaylist"] = x => { return GetPlaylist(); };
            Get["/GetPreviousEpisodeForUnwatched"] = x => { return GetPreviousEpisodeForUnwatched(); };
            Get["/GetRecommendations"] = x => { return GetRecommendations(); };
            Get["/GetRelatedAnimeLinks"] = x => { return GetRelatedAnimeLinks(); };
            Get["/GetReleaseGroupsForAnime"] = x => { return GetReleaseGroupsForAnime(); };
            Get["/GetSeasonNumbersForSeries"] = x => { return GetSeasonNumbersForSeries(); };
            Get["/GetSeasonNumbersForTrakt"] = x => { return GetSeasonNumbersForTrakt(); };
            Get["/GetSeries"] = x => { return GetSeries(); };
            Get["/GetSeriesExistingForAnime"] = x => { return GetSeriesExistingForAnime(); };
            Get["/GetSeriesForAnime"] = x => { return GetSeriesForAnime(); };
            Get["/GetSeriesForGroup"] = x => { return GetSeriesForGroup(); };
            Get["/GetSeriesForGroupRecursive"] = x => { return GetSeriesForGroupRecursive(); };
            Get["/GetSeriesRecentlyAdded"] = x => { return GetSeriesRecentlyAdded(); };
            Get["/GetSeriesWithMissingEpisodes"] = x => { return GetSeriesWithMissingEpisodes(); };
            Get["/GetSeriesWithoutAnyFiles"] = x => { return GetSeriesWithoutAnyFiles(); };
            Get["/GetServerSettings"] = x => { return GetServerSettings(); };
            Get["/GetServerStatus"] = x => { return GetServerStatus(); };
            Get["/GetSimilarAnimeLinks"] = x => { return GetSimilarAnimeLinks(); };
            Get["/GetSubGroupsForGroup"] = x => { return GetSubGroupsForGroup(); };
            Get["/GetTopLevelGroupForSeries"] = x => { return GetTopLevelGroupForSeries(); };
            Get["/GetTraktCommentsForAnime"] = x => { return GetTraktCommentsForAnime(); };
            Get["/GetTraktCrossRefEpisode"] = x => { return GetTraktCrossRefEpisode(); };
            Get["/GetTraktCrossRefV2"] = x => { return GetTraktCrossRefV2(); };
            Get["/GetTraktCrossRefWebCache"] = x => { return GetTraktCrossRefWebCache(); };
            Get["/GetTVDBCrossRefEpisode"] = x => { return GetTVDBCrossRefEpisode(); };
            Get["/GetTVDBCrossRefV2"] = x => { return GetTVDBCrossRefV2(); };
            Get["/GetTVDBCrossRefWebCache"] = x => { return GetTVDBCrossRefWebCache(); };
            Get["/GetTvDBLanguages"] = x => { return GetTvDBLanguages(); };
            Get["/GetUnrecognisedFiles"] = x => { return GetUnrecognisedFiles(); };
            Get["/GetUserVote"] = x => { return GetUserVote(); };
            Get["/GetVideoDetailed"] = x => { return GetVideoDetailed(); };
            Get["/GetVideoLocalsForAnime"] = x => { return GetVideoLocalsForAnime(); };
            Get["/GetVideoLocalsForEpisode"] = x => { return GetVideoLocalsForEpisode(); };
            Get["/IgnoreAnime"] = x => { return IgnoreAnime(); };
            Get["/IncrementEpisodeStats"] = x => { return IncrementEpisodeStats(); };
            Get["/IsWebCacheAdmin"] = x => { return IsWebCacheAdmin(); };
            Get["/LinkAniDBMAL"] = x => { return LinkAniDBMAL(); };
            Get["/LinkAniDBMALUpdated"] = x => { return LinkAniDBMALUpdated(); };
            Get["/LinkAniDBOther"] = x => { return LinkAniDBOther(); };
            Get["/LinkAniDBTrakt"] = x => { return LinkAniDBTrakt(); };
            Get["/LinkAniDBTvDB"] = x => { return LinkAniDBTvDB(); };
            Get["/LinkAniDBTvDBEpisode"] = x => { return LinkAniDBTvDBEpisode(); };
            Get["/MoveSeries"] = x => { return MoveSeries(); };
            Get["/OnlineAnimeTitleSearch"] = x => { return OnlineAnimeTitleSearch(); };
            Get["/PostTraktCommentShow"] = x => { return PostTraktCommentShow(); };
            Get["/RandomFileRenamePreview"] = x => { return RandomFileRenamePreview(); };
            Get["/RecreateAllGroups"] = x => { return RecreateAllGroups(); };
            Get["/ReevaluateDuplicateFiles"] = x => { return ReevaluateDuplicateFiles(); };
            Get["/RefreshAllMediaInfo"] = x => { return RefreshAllMediaInfo(); };
            Get["/RehashFile"] = x => { return RehashFile(); };
            Get["/RemoveAssociationOnFile"] = x => { return RemoveAssociationOnFile(); };
            Get["/RemoveDefaultSeriesForGroup"] = x => { return RemoveDefaultSeriesForGroup(); };
            Get["/RemoveIgnoreAnime"] = x => { return RemoveIgnoreAnime(); };
            Get["/RemoveLinkAniDBMAL"] = x => { return RemoveLinkAniDBMAL(); };
            Get["/RemoveLinkAniDBOther"] = x => { return RemoveLinkAniDBOther(); };
            Get["/RemoveLinkAniDBTrakt"] = x => { return RemoveLinkAniDBTrakt(); };
            Get["/RemoveLinkAniDBTraktForAnime"] = x => { return RemoveLinkAniDBTraktForAnime(); };
            Get["/RemoveLinkAniDBTvDB"] = x => { return RemoveLinkAniDBTvDB(); };
            Get["/RemoveLinkAniDBTvDBEpisode"] = x => { return RemoveLinkAniDBTvDBEpisode(); };
            Get["/RemoveLinkAniDBTvDBForAnime"] = x => { return RemoveLinkAniDBTvDBForAnime(); };
            Get["/RemoveMissingFiles"] = x => { return RemoveMissingFiles(); };
            Get["/RemoveMissingMyListFiles"] = x => { return RemoveMissingMyListFiles(); };
            Get["/RenameAllGroups"] = x => { return RenameAllGroups(); };
            Get["/RenameFile"] = x => { return RenameFile(); };
            Get["/RenameFilePreview"] = x => { return RenameFilePreview(); };
            Get["/RenameFiles"] = x => { return RenameFiles(); };
            Get["/RescanFile"] = x => { return RescanFile(); };
            Get["/RescanManuallyLinkedFiles"] = x => { return RescanManuallyLinkedFiles(); };
            Get["/RescanUnlinkedFiles"] = x => { return RescanUnlinkedFiles(); };
            Get["/RevokeTraktCrossRefWebCache"] = x => { return RevokeTraktCrossRefWebCache(); };
            Get["/RevokeTVDBCrossRefWebCache"] = x => { return RevokeTVDBCrossRefWebCache(); };
            Get["/RunImport"] = x => { return RunImport(); };
            Get["/SaveBookmarkedAnime"] = x => { return SaveBookmarkedAnime(); };
            Get["/SaveCustomTag"] = x => { return SaveCustomTag(); };
            Get["/SaveCustomTagCrossRef"] = x => { return SaveCustomTagCrossRef(); };
            Get["/SaveFFDPreset"] = x => { return SaveFFDPreset(); };
            Get["/SaveGroup"] = x => { return SaveGroup(); };
            Get["/SaveGroupFilter"] = x => { return SaveGroupFilter(); };
            Get["/SaveImportFolder"] = x => { return SaveImportFolder(); };
            Get["/SavePlaylist"] = x => { return SavePlaylist(); };
            Get["/SaveRenameScript"] = x => { return SaveRenameScript(); };
            Get["/SaveSeries"] = x => { return SaveSeries(); };
            Get["/SaveServerSettings"] = x => { return SaveServerSettings(); };
            Get["/SaveUser"] = x => { return SaveUser(); };
            Get["/ScanDropFolders"] = x => { return ScanDropFolders(); };
            Get["/ScanFolder"] = x => { return ScanFolder(); };
            Get["/SearchForFiles"] = x => { return SearchForFiles(); };
            Get["/SearchMAL"] = x => { return SearchMAL(); };
            Get["/SearchTheMovieDB"] = x => { return SearchTheMovieDB(); };
            Get["/SearchTheTvDB"] = x => { return SearchTheTvDB(); };
            Get["/SearchTrakt"] = x => { return SearchTrakt(); };
            Get["/SetCommandProcessorGeneralPaused"] = x => { return SetCommandProcessorGeneralPaused(); };
            Get["/SetCommandProcessorHasherPaused"] = x => { return SetCommandProcessorHasherPaused(); };
            Get["/SetCommandProcessorImagesPaused"] = x => { return SetCommandProcessorImagesPaused(); };
            Get["/SetDefaultImage"] = x => { return SetDefaultImage(); };
            Get["/SetDefaultSeriesForGroup"] = x => { return SetDefaultSeriesForGroup(); };
            Get["/SetIgnoreStatusOnFile"] = x => { return SetIgnoreStatusOnFile(); };
            Get["/SetVariationStatusOnFile"] = x => { return SetVariationStatusOnFile(); };
            Get["/SetWatchedStatusOnSeries"] = x => { return SetWatchedStatusOnSeries(); };
            Get["/SyncHashes"] = x => { return SyncHashes(); };
            Get["/SyncMALDownload"] = x => { return SyncMALDownload(); };
            Get["/SyncMALUpload"] = x => { return SyncMALUpload(); };
            Get["/SyncMyList"] = x => { return SyncMyList(); };
            Get["/SyncTraktSeries"] = x => { return SyncTraktSeries(); };
            Get["/SyncVotes"] = x => { return SyncVotes(); };
            Get["/TestAniDBConnection"] = x => { return TestAniDBConnection(); };
            Get["/TestMALLogin"] = x => { return TestMALLogin(); };
            Get["/ToggleWatchedStatusOnEpisode"] = x => { return ToggleWatchedStatusOnEpisode(); };
            Get["/ToggleWatchedStatusOnVideo"] = x => { return ToggleWatchedStatusOnVideo(); };
            Get["/ToString"] = x => { return ToString(); };
            Get["/TraktFriendRequestApprove"] = x => { return TraktFriendRequestApprove(); };
            Get["/TraktFriendRequestDeny"] = x => { return TraktFriendRequestDeny(); };
            Get["/UpdateAniDBFileData"] = x => { return UpdateAniDBFileData(); };
            Get["/UpdateAnimeData"] = x => { return UpdateAnimeData(); };
            Get["/UpdateAnimeDisableExternalLinksFlag"] = x => { return UpdateAnimeDisableExternalLinksFlag(); };
            Get["/UpdateCalendarData"] = x => { return UpdateCalendarData(); };
            Get["/UpdateEpisodeData"] = x => { return UpdateEpisodeData(); };
            Get["/UpdateFileData"] = x => { return UpdateFileData(); };
            Get["/UpdateMovieDBData"] = x => { return UpdateMovieDBData(); };
            Get["/UpdateTraktData"] = x => { return UpdateTraktData(); };
            Get["/UpdateTvDBData"] = x => { return UpdateTvDBData(); };
            Get["/UseMyTraktLinksWebCache"] = x => { return UseMyTraktLinksWebCache(); };
            Get["/UseMyTvDBLinksWebCache"] = x => { return UseMyTvDBLinksWebCache(); };
            Get["/VoteAnime"] = x => { return VoteAnime(); };
            Get["/VoteAnimeRevoke"] = x => { return VoteAnimeRevoke(); };
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
           return _impl.DeleteVideoLocalAndFile(a);
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
           _impl.RecreateAllGroups();
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
        private object GetSeriesForGroup(int a)
        {
            return _impl.GetSeriesForGroup(a);
        }

        private object SaveBookmarkedAnime(int a)
        {
            return _impl.SaveBookmarkedAnime(a);
        }

        private object SaveCustomTag(int a)
        {
            return _impl.SaveCustomTag(a);
        }

        private object SaveCustomTagCrossRef(int a)
        {
            return _impl.SaveCustomTagCrossRef(a);
        }

        private object SaveFFDPreset(int a)
        {
            return _impl.SaveFFDPreset(a);
        }

        private object SaveGroup(int a)
        {
            return _impl.SaveGroup(a);
        }

        private object SaveGroupFilter(int a)
        {
            return _impl.SaveGroupFilter(a);
        }

        private object SaveImportFolder(int a)
        {
            return _impl.SaveImportFolder(a);
        }

        private object SavePlaylist(int a)
        {
            return _impl.SavePlaylist(a);
        }

        private object SaveRenameScript(int a)
        {
            return _impl.SaveRenameScript(a);
        }

        private object SaveSeries(int a)
        {
            return _impl.SaveSeries(a);
        }

        private object SaveServerSettings(int a)
        {
            return _impl.SaveServerSettings(a);
        }

        private object SaveUser(int a)
        {
            return _impl.SaveUser(a);
        }

        private object RenameFile(int a, string b)
        {
            return _impl.RenameFile(a, b);
        }
        private object RenameFiles(int a)
        {
            return _impl.RenameFiles(a);
        }

        private object RemoveMissingMyListFiles(int a)
        {
            return _impl.RemoveMissingMyListFiles(a);
        }

        private object AssociateMultipleFiles()
        {
            _impl.AssociateMultipleFiles();
        }
        private object EvaluateGroupFilter()
        {
            return _impl.EvaluateGroupFilter();
        }
    }
}
