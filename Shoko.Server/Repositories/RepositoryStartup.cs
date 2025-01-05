using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Repositories.Direct.TMDB.Text;

namespace Shoko.Server.Repositories;

public static class RepositoryStartup
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<RepoFactory>();
        services.AddSingleton<DatabaseFactory>();
        services.AddDirectRepository<AniDB_AnimeUpdateRepository>();

        services.AddDirectRepository<AniDB_Anime_RelationRepository>();
        services.AddDirectRepository<AniDB_Anime_SimilarRepository>();
        services.AddDirectRepository<AniDB_Anime_StaffRepository>();
        services.AddDirectRepository<AniDB_FileUpdateRepository>();
        services.AddDirectRepository<AniDB_GroupStatusRepository>();
        services.AddDirectRepository<BookmarkedAnimeRepository>();
        services.AddDirectRepository<FileNameHashRepository>();
        services.AddDirectRepository<IgnoreAnimeRepository>();
        services.AddDirectRepository<PlaylistRepository>();
        services.AddDirectRepository<RenamerConfigRepository>();
        services.AddDirectRepository<ScanFileRepository>();
        services.AddDirectRepository<ScanRepository>();
        services.AddDirectRepository<ScheduledUpdateRepository>();
        services.AddDirectRepository<TMDB_AlternateOrdering_EpisodeRepository>();
        services.AddDirectRepository<TMDB_AlternateOrdering_SeasonRepository>();
        services.AddDirectRepository<TMDB_AlternateOrderingRepository>();
        services.AddDirectRepository<TMDB_Collection_MovieRepository>();
        services.AddDirectRepository<TMDB_CollectionRepository>();
        services.AddDirectRepository<TMDB_Company_EntityRepository>();
        services.AddDirectRepository<TMDB_CompanyRepository>();
        services.AddDirectRepository<TMDB_Episode_CastRepository>();
        services.AddDirectRepository<TMDB_Episode_CrewRepository>();
        services.AddDirectRepository<TMDB_Movie_CastRepository>();
        services.AddDirectRepository<TMDB_Movie_CrewRepository>();
        services.AddDirectRepository<TMDB_NetworkRepository>();
        services.AddDirectRepository<TMDB_OverviewRepository>();
        services.AddDirectRepository<TMDB_PersonRepository>();
        services.AddDirectRepository<TMDB_Show_NetworkRepository>();
        services.AddDirectRepository<TMDB_TitleRepository>();
        services.AddDirectRepository<Trakt_EpisodeRepository>();
        services.AddDirectRepository<Trakt_SeasonRepository>();
        services.AddDirectRepository<Trakt_ShowRepository>();
        services.AddDirectRepository<VersionsRepository>();
        services.AddDirectRepository<AniDB_MessageRepository>();
        services.AddDirectRepository<AniDB_NotifyQueueRepository>();

        services.AddCachedRepository<AniDB_AnimeRepository>();
        services.AddCachedRepository<AniDB_Anime_CharacterRepository>();
        services.AddCachedRepository<AniDB_Anime_Character_CreatorRepository>();
        services.AddCachedRepository<AniDB_Anime_PreferredImageRepository>();
        services.AddCachedRepository<AniDB_Anime_TagRepository>();
        services.AddCachedRepository<AniDB_Anime_TitleRepository>();
        services.AddCachedRepository<AniDB_CharacterRepository>();
        services.AddCachedRepository<AniDB_EpisodeRepository>();
        services.AddCachedRepository<AniDB_Episode_PreferredImageRepository>();
        services.AddCachedRepository<AniDB_Episode_TitleRepository>();
        services.AddCachedRepository<AniDB_FileRepository>();
        services.AddCachedRepository<AniDB_ReleaseGroupRepository>();
        services.AddCachedRepository<AniDB_CreatorRepository>();
        services.AddCachedRepository<AniDB_TagRepository>();
        services.AddCachedRepository<AniDB_VoteRepository>();
        services.AddCachedRepository<AnimeEpisodeRepository>();
        services.AddCachedRepository<AnimeEpisode_UserRepository>();
        services.AddCachedRepository<AnimeGroupRepository>();
        services.AddCachedRepository<AnimeGroup_UserRepository>();
        services.AddCachedRepository<AnimeSeriesRepository>();
        services.AddCachedRepository<AnimeSeries_UserRepository>();
        services.AddCachedRepository<AuthTokensRepository>();
        services.AddCachedRepository<CrossRef_AniDB_MALRepository>();
        services.AddCachedRepository<CrossRef_AniDB_TMDB_EpisodeRepository>();
        services.AddCachedRepository<CrossRef_AniDB_TMDB_MovieRepository>();
        services.AddCachedRepository<CrossRef_AniDB_TMDB_ShowRepository>();
        services.AddCachedRepository<CrossRef_AniDB_TraktV2Repository>();
        services.AddCachedRepository<CrossRef_CustomTagRepository>();
        services.AddCachedRepository<CrossRef_File_EpisodeRepository>();
        services.AddCachedRepository<CrossRef_Languages_AniDB_FileRepository>();
        services.AddCachedRepository<CrossRef_Subtitles_AniDB_FileRepository>();
        services.AddCachedRepository<CustomTagRepository>();
        services.AddCachedRepository<FilterPresetRepository>();
        services.AddCachedRepository<ImportFolderRepository>();
        services.AddCachedRepository<JMMUserRepository>();
        services.AddCachedRepository<TMDB_EpisodeRepository>();
        services.AddCachedRepository<TMDB_ImageRepository>();
        services.AddCachedRepository<TMDB_MovieRepository>();
        services.AddCachedRepository<TMDB_SeasonRepository>();
        services.AddCachedRepository<TMDB_ShowRepository>();
        services.AddCachedRepository<VideoLocalRepository>();
        services.AddCachedRepository<VideoLocal_PlaceRepository>();
        services.AddCachedRepository<VideoLocal_UserRepository>();

        return services;
    }

    private static void AddDirectRepository<Repo>(this IServiceCollection services) where Repo : class, IDirectRepository
    {
        services.AddSingleton<IDirectRepository, Repo>();
        services.AddSingleton(s => (Repo)s.GetServices(typeof(IDirectRepository)).FirstOrDefault(a => a?.GetType() == typeof(Repo)));
    }

    private static void AddCachedRepository<Repo>(this IServiceCollection services) where Repo : class, ICachedRepository
    {
        services.AddSingleton<ICachedRepository, Repo>();
        services.AddSingleton(typeof(Repo), s => (Repo)s.GetServices(typeof(ICachedRepository)).FirstOrDefault(a => a?.GetType() == typeof(Repo)));
    }
}
