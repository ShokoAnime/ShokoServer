using System;
using System.Collections.Generic;
using System.Runtime;
using System.Threading.Tasks;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories
{
    public static class RepoFactory
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static readonly List<ICachedRepository> CachedRepositories = new List<ICachedRepository>();
        //Cached Ones
        // DECLARE THESE IN ORDER OF DEPENDENCY
        public static JMMUserRepository JMMUser { get; } = JMMUserRepository.Create();
        public static AuthTokensRepository AuthTokens { get; } = AuthTokensRepository.Create();
        public static CloudAccountRepository CloudAccount { get; } = CloudAccountRepository.Create();
        public static ImportFolderRepository ImportFolder { get; } = ImportFolderRepository.Create();
        public static AniDB_AnimeRepository AniDB_Anime { get; } = AniDB_AnimeRepository.Create();
        public static AniDB_Episode_TitleRepository AniDB_Episode_Title { get; } = AniDB_Episode_TitleRepository.Create();
        public static AniDB_EpisodeRepository AniDB_Episode { get; } = AniDB_EpisodeRepository.Create();
        public static AniDB_FileRepository AniDB_File { get; } = AniDB_FileRepository.Create();
        public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; } = AniDB_Anime_TitleRepository.Create();
        public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; } = AniDB_Anime_TagRepository.Create();
        public static AniDB_TagRepository AniDB_Tag { get; } = AniDB_TagRepository.Create();
        public static CustomTagRepository CustomTag { get; } = CustomTagRepository.Create();
        public static CrossRef_CustomTagRepository CrossRef_CustomTag { get; } = CrossRef_CustomTagRepository.Create();
        public static CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; } = CrossRef_File_EpisodeRepository.Create();
        public static VideoLocal_PlaceRepository VideoLocalPlace { get; } = VideoLocal_PlaceRepository.Create();
        public static VideoLocalRepository VideoLocal { get; } = VideoLocalRepository.Create();
        public static VideoLocal_UserRepository VideoLocalUser { get; } = VideoLocal_UserRepository.Create();
        public static AnimeEpisodeRepository AnimeEpisode { get; } = AnimeEpisodeRepository.Create();
        public static AnimeEpisode_UserRepository AnimeEpisode_User { get; } = AnimeEpisode_UserRepository.Create();
        public static AnimeSeriesRepository AnimeSeries { get; } = AnimeSeriesRepository.Create();
        public static AnimeSeries_UserRepository AnimeSeries_User { get; } = AnimeSeries_UserRepository.Create();
        public static AnimeGroupRepository AnimeGroup { get; } = AnimeGroupRepository.Create();
        public static AnimeGroup_UserRepository AnimeGroup_User { get; } = AnimeGroup_UserRepository.Create();
        public static AniDB_VoteRepository AniDB_Vote { get; } = AniDB_VoteRepository.Create();
        public static TvDB_EpisodeRepository TvDB_Episode { get; } = TvDB_EpisodeRepository.Create();
        public static TvDB_SeriesRepository TvDB_Series { get; } = TvDB_SeriesRepository.Create();
        public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB { get; } = CrossRef_AniDB_TvDBRepository.Create();
        public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode { get; } = CrossRef_AniDB_TvDB_EpisodeRepository.Create();
        public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CrossRef_AniDB_TvDB_Episode_Override { get; } = CrossRef_AniDB_TvDB_Episode_OverrideRepository.Create();
        public static TvDB_ImagePosterRepository TvDB_ImagePoster { get; } = TvDB_ImagePosterRepository.Create();
        public static TvDB_ImageFanartRepository TvDB_ImageFanart { get; } = TvDB_ImageFanartRepository.Create();
        public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; } = TvDB_ImageWideBannerRepository.Create();
        public static AnimeCharacterRepository AnimeCharacter { get; } = AnimeCharacterRepository.Create();
        public static AnimeStaffRepository AnimeStaff { get; } = AnimeStaffRepository.Create();
        public static CrossRef_Anime_StaffRepository CrossRef_Anime_Staff { get; } = CrossRef_Anime_StaffRepository.Create();
        public static GroupFilterRepository GroupFilter { get; } = GroupFilterRepository.Create();

        //Direct Ones

        public static VersionsRepository Versions { get; } = VersionsRepository.Create();

        public static CommandRequestRepository CommandRequest { get; } = CommandRequestRepository.Create();
        public static Trakt_ShowRepository Trakt_Show { get; } = Trakt_ShowRepository.Create();
        public static Trakt_SeasonRepository Trakt_Season { get; } = Trakt_SeasonRepository.Create();
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
        public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate { get; } = AniDB_AnimeUpdateRepository.Create();

        public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; } =
            CrossRef_Subtitles_AniDB_FileRepository.Create();

        public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; } =
            CrossRef_Languages_AniDB_FileRepository.Create();

        public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2 { get; } =
            CrossRef_AniDB_TraktV2Repository.Create();

        public static CrossRef_AniDB_OtherRepository CrossRef_AniDB_Other { get; } =
            CrossRef_AniDB_OtherRepository.Create();

        public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL { get; } = CrossRef_AniDB_MALRepository.Create();
        public static BookmarkedAnimeRepository BookmarkedAnime { get; } = BookmarkedAnimeRepository.Create();
        public static AniDB_SeiyuuRepository AniDB_Seiyuu { get; } = AniDB_SeiyuuRepository.Create();
        public static AniDB_ReviewRepository AniDB_Review { get; } = AniDB_ReviewRepository.Create();
        public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; } = AniDB_ReleaseGroupRepository.Create();

        public static AniDB_RecommendationRepository AniDB_Recommendation { get; } =
            AniDB_RecommendationRepository.Create();

        public static AniDB_MylistStatsRepository AniDB_MylistStats { get; } = AniDB_MylistStatsRepository.Create();
        public static AniDB_GroupStatusRepository AniDB_GroupStatus { get; } = AniDB_GroupStatusRepository.Create();
        public static AniDB_CharacterRepository AniDB_Character { get; } = AniDB_CharacterRepository.Create();

        public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; } =
            AniDB_Character_SeiyuuRepository.Create();

        public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; } =
            AniDB_Anime_SimilarRepository.Create();

        public static AniDB_Anime_ReviewRepository AniDB_Anime_Review { get; } = AniDB_Anime_ReviewRepository.Create();

        public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; } =
            AniDB_Anime_RelationRepository.Create();

        public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; } =
            AniDB_Anime_DefaultImageRepository.Create();

        public static AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; } =
            AniDB_Anime_CharacterRepository.Create();

        public static ScanRepository Scan { get; } = ScanRepository.Create();
        public static ScanFileRepository ScanFile { get; } = ScanFileRepository.Create();

        //AdHoc Repo
        public static AdhocRepository Adhoc { get; } = AdhocRepository.Create();


        /************** Might need to be DEPRECATED **************/
        public static CrossRef_AniDB_Trakt_EpisodeRepository CrossRef_AniDB_Trakt_Episode { get; } =
            CrossRef_AniDB_Trakt_EpisodeRepository.Create();


        /************** DEPRECATED **************/
        /* We need to delete them at some point */

        public static GroupFilterConditionRepository GroupFilterCondition { get; } =
            GroupFilterConditionRepository.Create();

        public static void PostInit()
        {
            // Update Contracts if necessary
            try
            {
                logger.Info("Starting Server: RepoFactory.PostInit()");
                CachedRepositories.ForEach(repo =>
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Resources.Database_Validating, repo.GetType().Name.Replace("Repository", ""), " DbRegen");
                    repo.RegenerateDb();
                });
                CachedRepositories.ForEach(repo => repo.PostProcess());
            }
            catch (Exception e)
            {
                logger.Error($"There was an error starting the Database Factory - Regenerating: {e}");
                throw;
            }
            CleanUpMemory();
        }

        public static void Init()
        {
            try
            {
                foreach (var repo in CachedRepositories)
                {
                    Task task = Task.Run(() => repo.Populate());

                    // don't wait longer than 3 minutes
                    if (!task.Wait(180000)) throw new TimeoutException($"{repo.GetType()} took too long to cache.");
                }
            }
            catch (Exception exception)
            {
                logger.Error($"There was an error starting the Database Factory - Caching: {exception}");
                throw;
            }
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

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }
    }
}
