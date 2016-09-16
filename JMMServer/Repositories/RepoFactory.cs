using System;
using JMMServer.Entities;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
// ReSharper disable InconsistentNaming

namespace JMMServer.Repositories
{
    public static class RepoFactory
    {
        //Cached Ones
        public static VideoLocalRepository VideoLocal { get;  } = new VideoLocalRepository();
        public static VideoLocal_UserRepository VideoLocalUser { get; } = new VideoLocal_UserRepository();
        public static VideoLocal_PlaceRepository VideoLocalPlace { get;  } = new VideoLocal_PlaceRepository();
        public static ImportFolderRepository ImportFolder { get; } = new ImportFolderRepository();
        public static JMMUserRepository JMMUser { get;  } = new JMMUserRepository();
        public static GroupFilterRepository GroupFilter { get; } = new GroupFilterRepository();
        public static CustomTagRepository CustomTag { get; } = new CustomTagRepository();
        public static CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; } = new CrossRef_File_EpisodeRepository();
        public static CrossRef_CustomTagRepository CrossRef_CustomTag { get; } = new CrossRef_CustomTagRepository();
        public static AnimeSeriesRepository AnimeSeries { get; } = new AnimeSeriesRepository();
        public static AnimeSeries_UserRepository AnimeSeries_User { get; } = new AnimeSeries_UserRepository();
        public static AnimeGroupRepository AnimeGroup { get; } = new AnimeGroupRepository();
        public static AnimeGroup_UserRepository AnimeGroup_User { get; } = new AnimeGroup_UserRepository();
        public static AnimeEpisodeRepository AnimeEpisode { get; } = new AnimeEpisodeRepository();
        public static AnimeEpisode_UserRepository AnimeEpisode_User { get; } = new AnimeEpisode_UserRepository();
        public static AniDB_TagRepository AniDB_Tag { get; } = new AniDB_TagRepository();
        public static AniDB_FileRepository AniDB_File { get; } = new AniDB_FileRepository();
        public static AniDB_EpisodeRepository AniDB_Episode { get; } = new AniDB_EpisodeRepository();
        public static AniDB_AnimeRepository AniDB_Anime { get; } = new AniDB_AnimeRepository();
        public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; } = new AniDB_Anime_TitleRepository();
        public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; } = new AniDB_Anime_TagRepository();

        //Direct Ones

        public static VersionsRepository Versions { get;  } = new VersionsRepository();
        public static TvDB_SeriesRepository TvDB_Series { get; } = new TvDB_SeriesRepository();
        public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; } = new TvDB_ImageWideBannerRepository();
        public static TvDB_ImagePosterRepository TvDB_ImagePoster { get; } = new TvDB_ImagePosterRepository();
        public static TvDB_ImageFanartRepository TvDB_ImageFanart { get; } = new TvDB_ImageFanartRepository();
        public static TvDB_EpisodeRepository TvDB_Episode { get; } = new TvDB_EpisodeRepository();
        public static Trakt_ShowRepository Trakt_Show { get; } = new Trakt_ShowRepository();
        public static Trakt_SeasonRepository Trakt_Season { get; } = new Trakt_SeasonRepository();
        public static Trakt_ImagePosterRepository Trakt_ImagePoster { get; } = new Trakt_ImagePosterRepository();
        public static Trakt_ImageFanartRepository Trakt_ImageFanart { get; } = new Trakt_ImageFanartRepository();
        public static Trakt_FriendRepository Trakt_Friend { get; } = new Trakt_FriendRepository();
        public static Trakt_EpisodeRepository Trakt_Episode { get; } = new Trakt_EpisodeRepository();
        public static ScheduledUpdateRepository ScheduledUpdate { get; } = new ScheduledUpdateRepository();
        public static RenameScriptRepository RenameScript { get; } = new RenameScriptRepository();
        public static PlaylistRepository Playlist { get; } = new PlaylistRepository();
        public static MovieDB_PosterRepository MovieDB_Poster { get; } = new MovieDB_PosterRepository();
        public static MovieDB_FanartRepository MovieDB_Fanart { get; } = new MovieDB_FanartRepository();
        public static MovieDb_MovieRepository MovieDb_Movie { get; } = new MovieDb_MovieRepository();
        public static LanguageRepository Language { get; } = new LanguageRepository();
        public static IgnoreAnimeRepository IgnoreAnime { get; } = new IgnoreAnimeRepository();
        public static FileNameHashRepository FileNameHash { get; } = new FileNameHashRepository();
        public static FileFfdshowPresetRepository FileFfdshowPreset { get; } = new FileFfdshowPresetRepository();
        public static DuplicateFileRepository DuplicateFile { get; } = new DuplicateFileRepository();
        public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; } = new CrossRef_Subtitles_AniDB_FileRepository();
        public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; } = new CrossRef_Languages_AniDB_FileRepository();
        public static CrossRef_AniDB_TvDBV2Repository CrossRef_AniDB_TvDBV2 { get; } = new CrossRef_AniDB_TvDBV2Repository();
        public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode { get; } = new CrossRef_AniDB_TvDB_EpisodeRepository();
        public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2 { get; } = new CrossRef_AniDB_TraktV2Repository();
        public static CrossRef_AniDB_OtherRepository CrossRef_AniDB_Other { get; } = new CrossRef_AniDB_OtherRepository();
        public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL { get; } = new CrossRef_AniDB_MALRepository();
        public static CommandRequestRepository CommandRequest { get; } = new CommandRequestRepository();
        public static CloudAccountRepository CloudAccount { get; } = new CloudAccountRepository();
        public static BookmarkedAnimeRepository BookmarkedAnime { get; } = new BookmarkedAnimeRepository();
        public static AuthTokensRepository AuthTokens { get; } = new AuthTokensRepository();
        public static AniDB_VoteRepository AniDB_Vote { get; } = new AniDB_VoteRepository();
        public static AniDB_SeiyuuRepository AniDB_Seiyuu { get; } = new AniDB_SeiyuuRepository();
        public static AniDB_ReviewRepository AniDB_Review { get; } = new AniDB_ReviewRepository();
        public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; } = new AniDB_ReleaseGroupRepository();
        public static AniDB_RecommendationRepository AniDB_Recommendation { get; }  = new AniDB_RecommendationRepository();
        public static AniDB_MylistStatsRepository AniDB_MylistStats { get; } = new AniDB_MylistStatsRepository();
        public static AniDB_GroupStatusRepository AniDB_GroupStatus { get; } = new AniDB_GroupStatusRepository();
        public static AniDB_CharacterRepository AniDB_Character { get; } = new AniDB_CharacterRepository();
        public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; } = new AniDB_Character_SeiyuuRepository();
        public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; } = new AniDB_Anime_SimilarRepository();
        public static AniDB_Anime_ReviewRepository AniDB_Anime_Review { get; } = new AniDB_Anime_ReviewRepository();
        public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; } = new AniDB_Anime_RelationRepository();
        public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; } = new AniDB_Anime_DefaultImageRepository();
        public static AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; } = new AniDB_Anime_CharacterRepository();


        //AdHoc Repo
        public static AdhocRepository Adhoc { get; } = new AdhocRepository();


        /************** Might need to be DEPRECATED **************/
        public static CrossRef_AniDB_Trakt_EpisodeRepository CrossRef_AniDB_Trakt_Episode { get; } = new CrossRef_AniDB_Trakt_EpisodeRepository();


        /************** DEPRECATED **************/
        /* We need to delete them at some point */

        public static GroupFilterConditionRepository GroupFilterCondition { get; } = new GroupFilterConditionRepository();
        public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB { get; } = new CrossRef_AniDB_TvDBRepository();
        public static CrossRef_AniDB_TraktRepository CrossRef_AniDB_Trakt { get; } = new CrossRef_AniDB_TraktRepository();


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
