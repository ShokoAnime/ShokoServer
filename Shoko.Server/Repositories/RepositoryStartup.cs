using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;

namespace Shoko.Server.Repositories;

public static class RepositoryStartup
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<RepoFactory>();
        services.AddSingleton<DatabaseFactory>();
        services.AddDirectRepository<AniDB_AnimeUpdateRepository>();

        services.AddDirectRepository<AniDB_Anime_RelationRepository>();
        services.AddDirectRepository<AniDB_Anime_StaffRepository>();
        services.AddDirectRepository<FileNameHashRepository>();
        services.AddDirectRepository<ScheduledUpdateRepository>();
        services.AddDirectRepository<TMDB_AlternateOrdering_EpisodeRepository>();
        services.AddDirectRepository<TMDB_AlternateOrdering_SeasonRepository>();
        services.AddDirectRepository<TMDB_AlternateOrderingRepository>();
        services.AddDirectRepository<TMDB_Collection_MovieRepository>();
        services.AddDirectRepository<TMDB_Company_EntityRepository>();
        services.AddDirectRepository<TMDB_CompanyRepository>();
        services.AddDirectRepository<TMDB_Episode_CastRepository>();
        services.AddDirectRepository<TMDB_Episode_CrewRepository>();
        services.AddDirectRepository<TMDB_Movie_CastRepository>();
        services.AddDirectRepository<TMDB_Movie_CrewRepository>();
        services.AddDirectRepository<TMDB_NetworkRepository>();
        services.AddDirectRepository<TMDB_PersonRepository>();
        services.AddDirectRepository<TMDB_Show_NetworkRepository>();
        services.AddDirectRepository<VersionsRepository>();
        services.AddDirectRepository<AniDB_MessageRepository>();
        services.AddDirectRepository<AniDB_NotifyQueueRepository>();

        // The texts load first, as other repositories read them to build
        // their indexes, such as the names of the AniDB tags.
        services.AddCachedRepository<TextCache>();
        services.AddCachedRepository<AniDB_AnimeRepository>();
        services.AddCachedRepository<AniDB_Anime_CharacterRepository>();
        services.AddCachedRepository<AniDB_Anime_Character_CreatorRepository>();
        services.AddCachedRepository<AniDB_Anime_SimilarRepository>();
        services.AddCachedRepository<AniDB_Anime_TagRepository>();
        services.AddCachedRepository<AniDB_CharacterRepository>();
        services.AddCachedRepository<AniDB_EpisodeRepository>();
        services.AddCachedRepository<AniDB_ResourceRepository>();
        services.AddCachedRepository<AniDB_CreatorRepository>();
        services.AddCachedRepository<AniDB_TagRepository>();
        services.AddCachedRepository<AniDB_GroupStatusRepository>();
        services.AddCachedRepository<AiringChannelRepository>();
        services.AddCachedRepository<AiringScheduleRepository>();
        services.AddCachedRepository<AiringScheduleSweepStateRepository>();
        services.AddCachedRepository<EpisodeAiringRepository>();
        services.AddCachedRepository<AnimeEpisodeRepository>();
        services.AddCachedRepository<AnimeEpisode_UserRepository>();
        services.AddCachedRepository<AnimeGroupRepository>();
        services.AddCachedRepository<AnimeGroup_UserRepository>();
        services.AddCachedRepository<AnimeSeriesRepository>();
        services.AddCachedRepository<AnimeSeries_UserRepository>();
        services.AddCachedRepository<AuthTokensRepository>();
        services.AddCachedRepository<ScheduledActionRepository>();
        services.AddCachedRepository<CrossRef_AniDB_MALRepository>();
        services.AddSingleton<CrossRef_AniDB_TMDB_EpisodeRepository>();
        services.AddSingleton<CrossRef_AniDB_TMDB_MovieRepository>();
        services.AddSingleton<CrossRef_AniDB_TMDB_ShowRepository>();
        services.AddCachedRepository<CrossRef_AniDB_Metadata_SeriesRepository>();
        services.AddCachedRepository<CrossRef_AniDB_Metadata_MovieRepository>();
        services.AddCachedRepository<CrossRef_AniDB_Metadata_EpisodeRepository>();
        services.AddCachedRepository<CrossRef_CustomTagRepository>();
        services.AddCachedRepository<CrossRef_File_EpisodeRepository>();
        services.AddCachedRepository<CustomTagRepository>();
        services.AddCachedRepository<StoredReleaseInfoRepository>();
        services.AddCachedRepository<StoredReleaseInfo_MatchAttemptRepository>();
        services.AddCachedRepository<StoredRelocationPresetRepository>();
        services.AddCachedRepository<FilterPresetRepository>();
        services.AddCachedRepository<ShokoManagedFolderRepository>();
        services.AddCachedRepository<ShokoImageRepository>();
        services.AddCachedRepository<ShokoImage_EntityRepository>();
        services.AddCachedRepository<JMMUserRepository>();
        services.AddCachedRepository<Metadata_CreatorRepository>();
        services.AddCachedRepository<Metadata_CharacterRepository>();
        services.AddCachedRepository<Metadata_CastRepository>();
        services.AddCachedRepository<Metadata_CrewRepository>();
        services.AddCachedRepository<Metadata_TagRepository>();
        services.AddCachedRepository<Metadata_Tag_EntryRepository>();
        services.AddCachedRepository<Metadata_StudioRepository>();
        services.AddCachedRepository<Metadata_Studio_EntryRepository>();
        services.AddCachedRepository<Metadata_NetworkRepository>();
        services.AddCachedRepository<Metadata_Network_EntryRepository>();
        services.AddCachedRepository<Metadata_RelationRepository>();
        services.AddCachedRepository<Metadata_SuggestionRepository>();
        services.AddCachedRepository<Metadata_SeriesRepository>();
        services.AddCachedRepository<Metadata_SeasonRepository>();
        services.AddCachedRepository<Metadata_EpisodeRepository>();
        services.AddCachedRepository<Metadata_MovieRepository>();
        services.AddCachedRepository<Metadata_CollectionRepository>();
        services.AddCachedRepository<Metadata_Collection_MemberRepository>();
        services.AddCachedRepository<Metadata_ContentRatingRepository>();
        services.AddCachedRepository<Metadata_RefreshRepository>();
        services.AddCachedRepository<Metadata_OrderingRepository>();
        services.AddCachedRepository<Metadata_Ordering_GroupRepository>();
        services.AddCachedRepository<Metadata_Ordering_EntryRepository>();
        services.AddSingleton<MetadataRowWriter>();
        services.AddCachedRepository<TMDB_CollectionRepository>();
        services.AddCachedRepository<TMDB_EpisodeRepository>();
        services.AddCachedRepository<TMDB_MovieRepository>();
        services.AddCachedRepository<TMDB_SeasonRepository>();
        services.AddCachedRepository<TMDB_ShowRepository>();
        services.AddCachedRepository<TMDB_SuggestionRepository>();
        services.AddCachedRepository<VideoLocalRepository>();
        services.AddCachedRepository<VideoLocal_PlaceRepository>();
        services.AddCachedRepository<VideoLocal_UserRepository>();
        services.AddCachedRepository<VideoLocal_HashDigestRepository>();

        return services;
    }

    private static void AddDirectRepository<Repo>(this IServiceCollection services) where Repo : class, IDirectRepository
    {
        services.AddSingleton<IDirectRepository, Repo>();
        services.AddSingleton(s => (Repo)s.GetServices(typeof(IDirectRepository)).FirstOrDefault(a => a?.GetType() == typeof(Repo))!);
    }

    private static void AddCachedRepository<Repo>(this IServiceCollection services) where Repo : class, ICachedRepository
    {
        services.AddSingleton<ICachedRepository, Repo>();
        services.AddSingleton(typeof(Repo), s => (Repo)s.GetServices(typeof(ICachedRepository)).FirstOrDefault(a => a?.GetType() == typeof(Repo))!);
    }
}
