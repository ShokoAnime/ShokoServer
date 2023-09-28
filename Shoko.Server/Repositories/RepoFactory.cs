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

namespace Shoko.Server.Repositories;

public static class RepoFactory
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public static readonly List<ICachedRepository> CachedRepositories = new();

    #region Direct Repositories

    public static AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; } = new();
    public static AniDB_Anime_PreferredImageRepository AniDB_Anime_PreferredImage { get; } = new();
    public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; } = new();
    public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; } = new();
    public static AniDB_Anime_StaffRepository AniDB_Anime_Staff { get; } = new();
    public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate { get; } = new();
    public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; } = new();
    public static AniDB_CharacterRepository AniDB_Character { get; } = new();
    public static AniDB_FileUpdateRepository AniDB_FileUpdate { get; } = new();
    public static AniDB_GroupStatusRepository AniDB_GroupStatus { get; } = new();
    public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; } = new();
    public static AniDB_SeiyuuRepository AniDB_Seiyuu { get; } = new();
    public static BookmarkedAnimeRepository BookmarkedAnime { get; } = new();
    public static CommandRequestRepository CommandRequest { get; } = new();
    public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL { get; } = new();
    public static CrossRef_AniDB_TMDB_EpisodeRepository CrossRef_AniDB_TMDB_Episode { get; } = new();
    public static CrossRef_AniDB_TMDB_MovieRepository CrossRef_AniDB_TMDB_Movie { get; } = new();
    public static CrossRef_AniDB_TMDB_ShowRepository CrossRef_AniDB_TMDB_Show { get; } = new();
    public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; } = new();
    public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; } = new();
    public static DuplicateFileRepository DuplicateFile { get; } = new();
    public static FileNameHashRepository FileNameHash { get; } = new();
    public static IgnoreAnimeRepository IgnoreAnime { get; } = new();
    public static MovieDb_MovieRepository MovieDb_Movie { get; } = new();
    public static PlaylistRepository Playlist { get; } = new();
    public static RenameScriptRepository RenameScript { get; } = new();
    public static ScanFileRepository ScanFile { get; } = new();
    public static ScanRepository Scan { get; } = new();
    public static ScheduledUpdateRepository ScheduledUpdate { get; } = new();
    public static TMDB_AlternateOrdering_EpisodeRepository TMDB_AlternateOrdering_Episode { get; } = new();
    public static TMDB_AlternateOrdering_SeasonRepository TMDB_AlternateOrdering_Season { get; } = new();
    public static TMDB_AlternateOrderingRepository TMDB_AlternateOrdering { get; } = new();
    public static TMDB_Collection_MovieRepository TMDB_Collection_Movie { get; } = new();
    public static TMDB_CollectionRepository TMDB_Collection { get; } = new();
    public static TMDB_Company_EntityRepository TMDB_Company_Entity { get; } = new();
    public static TMDB_CompanyRepository TMDB_Company { get; } = new();
    public static TMDB_Episode_CastRepository TMDB_Episode_Cast { get; } = new();
    public static TMDB_Episode_CrewRepository TMDB_Episode_Crew { get; } = new();
    public static TMDB_EpisodeRepository TMDB_Episode { get; } = new();
    public static TMDB_ImageRepository TMDB_Image { get; } = new();
    public static TMDB_Movie_CastRepository TMDB_Movie_Cast { get; } = new();
    public static TMDB_Movie_CrewRepository TMDB_Movie_Crew { get; } = new();
    public static TMDB_MovieRepository TMDB_Movie { get; } = new();
    public static TMDB_NetworkRepository TMDB_Network { get; } = new();
    public static TMDB_OverviewRepository TMDB_Overview { get; } = new();
    public static TMDB_PersonRepository TMDB_Person { get; } = new();
    public static TMDB_SeasonRepository TMDB_Season { get; } = new();
    public static TMDB_Show_NetworkRepository TMDB_Show_Network { get; } = new();
    public static TMDB_ShowRepository TMDB_Show { get; } = new();
    public static TMDB_TitleRepository TMDB_Title { get; } = new();
    public static Trakt_EpisodeRepository Trakt_Episode { get; } = new();
    public static Trakt_SeasonRepository Trakt_Season { get; } = new();
    public static Trakt_ShowRepository Trakt_Show { get; } = new();
    public static VersionsRepository Versions { get; } = new();

    #endregion

    #region Cached Repositories
    // Declared in order of dependency.

    public static JMMUserRepository JMMUser { get; } = new();
    public static AuthTokensRepository AuthTokens { get; } = new();
    public static ImportFolderRepository ImportFolder { get; } = new();
    public static AniDB_AnimeRepository AniDB_Anime { get; } = new();
    public static AniDB_Episode_TitleRepository AniDB_Episode_Title { get; } = new();
    public static AniDB_EpisodeRepository AniDB_Episode { get; } = new();
    public static AniDB_FileRepository AniDB_File { get; } = new();
    public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; } = new();
    public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; } = new();
    public static AniDB_TagRepository AniDB_Tag { get; } = new();
    public static CustomTagRepository CustomTag { get; } = new();
    public static CrossRef_CustomTagRepository CrossRef_CustomTag { get; } = new();
    public static CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; } = new();
    public static VideoLocal_PlaceRepository VideoLocalPlace { get; } = new();
    public static VideoLocalRepository VideoLocal { get; } = new();
    public static VideoLocal_UserRepository VideoLocalUser { get; } = new();
    public static AnimeEpisodeRepository AnimeEpisode { get; } = new();
    public static AnimeEpisode_UserRepository AnimeEpisode_User { get; } = new();
    public static AnimeSeriesRepository AnimeSeries { get; } = new();
    public static AnimeSeries_UserRepository AnimeSeries_User { get; } = new();
    public static AnimeGroupRepository AnimeGroup { get; } = new();
    public static AnimeGroup_UserRepository AnimeGroup_User { get; } = new();
    public static AniDB_VoteRepository AniDB_Vote { get; } = new();
    public static TvDB_EpisodeRepository TvDB_Episode { get; } = new();
    public static TvDB_SeriesRepository TvDB_Series { get; } = new();
    public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB { get; } = new();
    public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode { get; } = new();
    public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CrossRef_AniDB_TvDB_Episode_Override { get; } = new();
    public static TvDB_ImagePosterRepository TvDB_ImagePoster { get; } = new();
    public static TvDB_ImageFanartRepository TvDB_ImageFanart { get; } = new();
    public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; } = new();
    public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2 { get; } = new();
    public static AnimeCharacterRepository AnimeCharacter { get; } = new();
    public static AnimeStaffRepository AnimeStaff { get; } = new();
    public static CrossRef_Anime_StaffRepository CrossRef_Anime_Staff { get; } = new();
    public static GroupFilterRepository GroupFilter { get; } = new();

    #endregion

    /************** DEPRECATED **************/
    /* We need to delete them at some point */

    public static GroupFilterConditionRepository GroupFilterCondition { get; } = new();

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

    public static async Task Init()
    {
        try
        {
            foreach (var repo in CachedRepositories)
            {
                repo.Populate();
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
