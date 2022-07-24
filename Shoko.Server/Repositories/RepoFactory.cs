using System;
using System.Collections.Generic;
using System.Runtime;
using System.Threading.Tasks;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Settings;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories
{
    public static class RepoFactory
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static readonly List<ICachedRepository> CachedRepositories = new List<ICachedRepository>();

        // Declare in order of dependency. Direct Repos don't have dependencies, so do them first
        //Direct Ones
        public static VersionsRepository Versions { get; } = new VersionsRepository();
        public static CommandRequestRepository CommandRequest { get; } = new CommandRequestRepository();
        public static Trakt_ShowRepository Trakt_Show { get; } = new Trakt_ShowRepository();
        public static Trakt_SeasonRepository Trakt_Season { get; } = new Trakt_SeasonRepository();
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
        public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate { get; } = new AniDB_AnimeUpdateRepository();
        public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; } = new CrossRef_Subtitles_AniDB_FileRepository();
        public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; } = new CrossRef_Languages_AniDB_FileRepository();
        public static CrossRef_AniDB_OtherRepository CrossRef_AniDB_Other { get; } = new CrossRef_AniDB_OtherRepository();
        public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL { get; } = new CrossRef_AniDB_MALRepository();
        public static BookmarkedAnimeRepository BookmarkedAnime { get; } = new BookmarkedAnimeRepository();
        public static AniDB_SeiyuuRepository AniDB_Seiyuu { get; } = new AniDB_SeiyuuRepository();
        public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; } = new AniDB_ReleaseGroupRepository();
        public static AniDB_RecommendationRepository AniDB_Recommendation { get; } = new AniDB_RecommendationRepository();
        public static AniDB_MylistStatsRepository AniDB_MylistStats { get; } = new AniDB_MylistStatsRepository();
        public static AniDB_GroupStatusRepository AniDB_GroupStatus { get; } = new AniDB_GroupStatusRepository();
        public static AniDB_CharacterRepository AniDB_Character { get; } = new AniDB_CharacterRepository();
        public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; } = new AniDB_Character_SeiyuuRepository();
        public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; } = new AniDB_Anime_SimilarRepository();
        public static AniDB_Anime_ReviewRepository AniDB_Anime_Review { get; } = new AniDB_Anime_ReviewRepository();
        public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; } = new AniDB_Anime_RelationRepository();
        public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; } = new AniDB_Anime_DefaultImageRepository();
        public static AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; } = new AniDB_Anime_CharacterRepository();
        public static AniDB_Anime_StaffRepository AniDB_Anime_Staff { get; } = new AniDB_Anime_StaffRepository();
        public static ScanRepository Scan { get; } = new ScanRepository();
        public static ScanFileRepository ScanFile { get; } = new ScanFileRepository();
        public static AdhocRepository Adhoc { get; } = new AdhocRepository();
        
        //Cached Ones
        // DECLARE THESE IN ORDER OF DEPENDENCY
        public static JMMUserRepository JMMUser { get; } = new JMMUserRepository();
        public static AuthTokensRepository AuthTokens { get; } = new AuthTokensRepository();
        public static ImportFolderRepository ImportFolder { get; } = new ImportFolderRepository();
        public static AniDB_AnimeRepository AniDB_Anime { get; } = new AniDB_AnimeRepository();
        public static AniDB_Episode_TitleRepository AniDB_Episode_Title { get; } = new AniDB_Episode_TitleRepository();
        public static AniDB_EpisodeRepository AniDB_Episode { get; } = new AniDB_EpisodeRepository();
        public static AniDB_FileRepository AniDB_File { get; } = new AniDB_FileRepository();
        public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; } = new AniDB_Anime_TitleRepository();
        public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; } = new AniDB_Anime_TagRepository();
        public static AniDB_TagRepository AniDB_Tag { get; } = new AniDB_TagRepository();
        public static CustomTagRepository CustomTag { get; } = new CustomTagRepository();
        public static CrossRef_CustomTagRepository CrossRef_CustomTag { get; } = new CrossRef_CustomTagRepository();
        public static CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; } = new CrossRef_File_EpisodeRepository();
        public static VideoLocal_PlaceRepository VideoLocalPlace { get; } = new VideoLocal_PlaceRepository();
        public static VideoLocalRepository VideoLocal { get; } = new VideoLocalRepository();
        public static VideoLocal_UserRepository VideoLocalUser { get; } = new VideoLocal_UserRepository();
        public static AnimeEpisodeRepository AnimeEpisode { get; } = new AnimeEpisodeRepository();
        public static AnimeEpisode_UserRepository AnimeEpisode_User { get; } = new AnimeEpisode_UserRepository();
        public static AnimeSeriesRepository AnimeSeries { get; } = new AnimeSeriesRepository();
        public static AnimeSeries_UserRepository AnimeSeries_User { get; } = new AnimeSeries_UserRepository();
        public static AnimeGroupRepository AnimeGroup { get; } = new AnimeGroupRepository();
        public static AnimeGroup_UserRepository AnimeGroup_User { get; } = new AnimeGroup_UserRepository();
        public static AniDB_VoteRepository AniDB_Vote { get; } = new AniDB_VoteRepository();
        public static TvDB_EpisodeRepository TvDB_Episode { get; } = new TvDB_EpisodeRepository();
        public static TvDB_SeriesRepository TvDB_Series { get; } = new TvDB_SeriesRepository();
        public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB { get; } = new CrossRef_AniDB_TvDBRepository();
        public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode { get; } = new CrossRef_AniDB_TvDB_EpisodeRepository();
        public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CrossRef_AniDB_TvDB_Episode_Override { get; } = new CrossRef_AniDB_TvDB_Episode_OverrideRepository();
        public static TvDB_ImagePosterRepository TvDB_ImagePoster { get; } = new TvDB_ImagePosterRepository();
        public static TvDB_ImageFanartRepository TvDB_ImageFanart { get; } = new TvDB_ImageFanartRepository();
        public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; } = new TvDB_ImageWideBannerRepository();
        public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2 { get; } = new CrossRef_AniDB_TraktV2Repository();
        public static AnimeCharacterRepository AnimeCharacter { get; } = new AnimeCharacterRepository();
        public static AnimeStaffRepository AnimeStaff { get; } = new AnimeStaffRepository();
        public static CrossRef_Anime_StaffRepository CrossRef_Anime_Staff { get; } = new CrossRef_Anime_StaffRepository();
        public static GroupFilterRepository GroupFilter { get; } = new GroupFilterRepository();


        /************** Might need to be DEPRECATED **************/
        public static CrossRef_AniDB_Trakt_EpisodeRepository CrossRef_AniDB_Trakt_Episode { get; } = new CrossRef_AniDB_Trakt_EpisodeRepository();


        /************** DEPRECATED **************/
        /* We need to delete them at some point */

        public static GroupFilterConditionRepository GroupFilterCondition { get; } = new GroupFilterConditionRepository();

        public static void PostInit()
        {
            // Update Contracts if necessary
            try
            {
                logger.Info("Starting Server: RepoFactory.PostInit()");
                CachedRepositories.ForEach(repo =>
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
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
                    if (!task.Wait(ServerSettings.Instance.CachingDatabaseTimeout * 1000)) throw new TimeoutException($"{repo.GetType()} took too long to cache.");
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
            AnimeSeries.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeSeries_User.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeGroup.GetAll().ForEach(a => a.CollectContractMemory());
            AnimeGroup_User.GetAll().ForEach(a => a.CollectContractMemory());

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }
    }
}