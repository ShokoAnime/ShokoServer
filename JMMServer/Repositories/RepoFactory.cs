using System;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
// ReSharper disable InconsistentNaming

namespace JMMServer.Repositories
{
    public static class RepoFactory
    {
        //Cached Ones
        public static VideoLocalRepository VideoLocal { get; } = VideoLocalRepository.Create();
        public static VideoLocal_UserRepository VideoLocalUser { get; } = VideoLocal_UserRepository.Create();
        public static VideoLocal_PlaceRepository VideoLocalPlace { get; } = VideoLocal_PlaceRepository.Create();
        public static ImportFolderRepository ImportFolder { get; } = ImportFolderRepository.Create();
        public static JMMUserRepository JMMUser { get;  } = JMMUserRepository.Create();
        public static GroupFilterRepository GroupFilter { get; } = GroupFilterRepository.Create();
        public static CloudAccountRepository CloudAccount { get; } = CloudAccountRepository.Create();
        public static CustomTagRepository CustomTag { get; } = CustomTagRepository.Create();
        public static CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; } = CrossRef_File_EpisodeRepository.Create();
        public static CrossRef_CustomTagRepository CrossRef_CustomTag { get; } = CrossRef_CustomTagRepository.Create();
        public static AnimeSeriesRepository AnimeSeries { get; } = AnimeSeriesRepository.Create();
        public static AnimeSeries_UserRepository AnimeSeries_User { get; } = AnimeSeries_UserRepository.Create();
        public static AnimeGroupRepository AnimeGroup { get; } = AnimeGroupRepository.Create();
        public static AnimeGroup_UserRepository AnimeGroup_User { get; } = AnimeGroup_UserRepository.Create();
        public static AnimeEpisodeRepository AnimeEpisode { get; } = AnimeEpisodeRepository.Create();
        public static AnimeEpisode_UserRepository AnimeEpisode_User { get; } = AnimeEpisode_UserRepository.Create();
        public static AniDB_TagRepository AniDB_Tag { get; } = AniDB_TagRepository.Create();
        public static AniDB_FileRepository AniDB_File { get; } = AniDB_FileRepository.Create();
        public static AniDB_EpisodeRepository AniDB_Episode { get; } = AniDB_EpisodeRepository.Create();
        public static AniDB_AnimeRepository AniDB_Anime { get; } = AniDB_AnimeRepository.Create();
        public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; } = AniDB_Anime_TitleRepository.Create();
        public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; } = AniDB_Anime_TagRepository.Create();

        //Direct Ones

        public static VersionsRepository Versions { get;  } = VersionsRepository.Create();
        public static TvDB_SeriesRepository TvDB_Series { get; } = TvDB_SeriesRepository.Create();
        public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; } = TvDB_ImageWideBannerRepository.Create();
        public static TvDB_ImagePosterRepository TvDB_ImagePoster { get; } = TvDB_ImagePosterRepository.Create();
        public static TvDB_ImageFanartRepository TvDB_ImageFanart { get; } = TvDB_ImageFanartRepository.Create();
        public static TvDB_EpisodeRepository TvDB_Episode { get; } = TvDB_EpisodeRepository.Create();
        public static Trakt_ShowRepository Trakt_Show { get; } = Trakt_ShowRepository.Create();
        public static Trakt_SeasonRepository Trakt_Season { get; } = Trakt_SeasonRepository.Create();
        public static Trakt_ImagePosterRepository Trakt_ImagePoster { get; } = Trakt_ImagePosterRepository.Create();
        public static Trakt_ImageFanartRepository Trakt_ImageFanart { get; } = Trakt_ImageFanartRepository.Create();
        public static Trakt_FriendRepository Trakt_Friend { get; } = Trakt_FriendRepository.Create();
        public static Trakt_EpisodeRepository Trakt_Episode { get; } = Trakt_EpisodeRepository.Create();
        public static ScheduledUpdateRepository ScheduledUpdate { get; } = ScheduledUpdateRepository.Create();
        public static RenameScriptRepository RenameScript { get; } = RenameScriptRepository.Create();
        public static PlaylistRepository Playlist { get; } = PlaylistRepository.Create();
        public static MovieDB_PosterRepository MovieDB_Poster { get; } = MovieDB_PosterRepository.Create();
        public static MovieDB_FanartRepository MovieDB_Fanart { get; } = MovieDB_FanartRepository.Create();
        public static MovieDb_MovieRepository MovieDb_Movie { get; } = MovieDb_MovieRepository.Create();
        public static LanguageRepository Language { get; } = LanguageRepository.Create();
        public static IgnoreAnimeRepository IgnoreAnime { get; } = IgnoreAnimeRepository.Create();
        public static FileNameHashRepository FileNameHash { get; } = FileNameHashRepository.Create();
        public static FileFfdshowPresetRepository FileFfdshowPreset { get; } = FileFfdshowPresetRepository.Create();
        public static DuplicateFileRepository DuplicateFile { get; } = DuplicateFileRepository.Create();
        public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; } = CrossRef_Subtitles_AniDB_FileRepository.Create();
        public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; } = CrossRef_Languages_AniDB_FileRepository.Create();
        public static CrossRef_AniDB_TvDBV2Repository CrossRef_AniDB_TvDBV2 { get; } = CrossRef_AniDB_TvDBV2Repository.Create();
        public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode { get; } = CrossRef_AniDB_TvDB_EpisodeRepository.Create();
        public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2 { get; } = CrossRef_AniDB_TraktV2Repository.Create();
        public static CrossRef_AniDB_OtherRepository CrossRef_AniDB_Other { get; } = CrossRef_AniDB_OtherRepository.Create();
        public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL { get; } = CrossRef_AniDB_MALRepository.Create();
        public static CommandRequestRepository CommandRequest { get; } = CommandRequestRepository.Create();
        public static BookmarkedAnimeRepository BookmarkedAnime { get; } = BookmarkedAnimeRepository.Create();
        public static AuthTokensRepository AuthTokens { get; } = AuthTokensRepository.Create();
        public static AniDB_VoteRepository AniDB_Vote { get; } = AniDB_VoteRepository.Create();
        public static AniDB_SeiyuuRepository AniDB_Seiyuu { get; } = AniDB_SeiyuuRepository.Create();
        public static AniDB_ReviewRepository AniDB_Review { get; } = AniDB_ReviewRepository.Create();
        public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; } = AniDB_ReleaseGroupRepository.Create();
        public static AniDB_RecommendationRepository AniDB_Recommendation { get; }  = AniDB_RecommendationRepository.Create();
        public static AniDB_MylistStatsRepository AniDB_MylistStats { get; } = AniDB_MylistStatsRepository.Create();
        public static AniDB_GroupStatusRepository AniDB_GroupStatus { get; } = AniDB_GroupStatusRepository.Create();
        public static AniDB_CharacterRepository AniDB_Character { get; } = AniDB_CharacterRepository.Create();
        public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; } = AniDB_Character_SeiyuuRepository.Create();
        public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; } = AniDB_Anime_SimilarRepository.Create();
        public static AniDB_Anime_ReviewRepository AniDB_Anime_Review { get; } = AniDB_Anime_ReviewRepository.Create();
        public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; } = AniDB_Anime_RelationRepository.Create();
        public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; } = AniDB_Anime_DefaultImageRepository.Create();
        public static AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; } = AniDB_Anime_CharacterRepository.Create();

        public static ScanRepository Scan { get; } = ScanRepository.Create();
        public static ScanFileRepository ScanFile { get; } = ScanFileRepository.Create();

        //AdHoc Repo
        public static AdhocRepository Adhoc { get; } = AdhocRepository.Create();


        /************** Might need to be DEPRECATED **************/
        public static CrossRef_AniDB_Trakt_EpisodeRepository CrossRef_AniDB_Trakt_Episode { get; } = CrossRef_AniDB_Trakt_EpisodeRepository.Create();


        /************** DEPRECATED **************/
        /* We need to delete them at some point */

        public static GroupFilterConditionRepository GroupFilterCondition { get; } = GroupFilterConditionRepository.Create();
        public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB { get; } = CrossRef_AniDB_TvDBRepository.Create();
        public static CrossRef_AniDB_TraktRepository CrossRef_AniDB_Trakt { get; } = CrossRef_AniDB_TraktRepository.Create();


        public static void Init()
        {
            JMMUser.Populate(a => a.JMMUserID);
            CloudAccount.Populate(a=>a.CloudID);
            ImportFolder.Populate(a=>a.ImportFolderID);            
            AniDB_Anime.Populate(a=>a.AniDB_AnimeID);
            AniDB_Episode.Populate(a=>a.AniDB_EpisodeID);
            AniDB_File.Populate(a=>a.AniDB_FileID);
            AniDB_Anime_Title.Populate(a=>a.AniDB_Anime_TitleID);
            AniDB_Anime_Tag.Populate(a=>a.AniDB_Anime_TagID);
            AniDB_Tag.Populate(a=>a.AniDB_TagID);
            CustomTag.Populate(a=>a.CustomTagID);
            CrossRef_CustomTag.Populate(a=>a.CrossRef_CustomTagID);
            CrossRef_File_Episode.Populate(a=>a.CrossRef_File_EpisodeID);
            VideoLocalPlace.Populate(a=>a.VideoLocal_Place_ID);
            VideoLocal.Populate(a=>a.VideoLocalID);
            VideoLocalUser.Populate(a => a.VideoLocal_UserID);
            GroupFilter.Populate(a => a.GroupFilterID);
            AnimeEpisode.Populate(a=>a.AnimeEpisodeID);
            AnimeEpisode_User.Populate(a=>a.AnimeEpisode_UserID);
            AnimeSeries.Populate(a=>a.AnimeSeriesID);
            AnimeSeries_User.Populate(a=>a.AnimeSeries_UserID);
            AnimeGroup.Populate(a=>a.AnimeGroupID);
            AnimeGroup_User.Populate(a=>a.AnimeGroup_UserID);
            GroupFilter.PostProcess();
            CleanUpMemory();
        }

        public static void CleanUpMemory()
        {
            AniDB_Anime.GetAll().ForEach(a => a.CollectContractMemory());
            VideoLocal.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeEpisode.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeEpisode_User.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeSeries.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeSeries_User.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeGroup.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeGroup_User.GetAll().ForEach(a => a.CollectContractMemory());
            GC.Collect();
        }

    }
}
