using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Repositories.Direct.TMDB.Text;
using Shoko.Server.Services;

// ReSharper disable InconsistentNaming

#pragma warning disable CA2211
namespace Shoko.Server.Repositories;

public class RepoFactory
{
    private readonly ILogger<RepoFactory> _logger;
    private readonly SystemService _systemService;
    private readonly ICachedRepository[] _cachedRepositories;

    public static AniDB_Anime_CharacterRepository AniDB_Anime_Character = null!;
    public static AniDB_Anime_Character_CreatorRepository AniDB_Anime_Character_Creator = null!;
    public static AniDB_Anime_RelationRepository AniDB_Anime_Relation = null!;
    public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar = null!;
    public static AniDB_Anime_StaffRepository AniDB_Anime_Staff = null!;
    public static AniDB_Anime_TagRepository AniDB_Anime_Tag = null!;
    public static AniDB_Anime_TitleRepository AniDB_Anime_Title = null!;
    public static AniDB_AnimeRepository AniDB_Anime = null!;
    public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate = null!;
    public static AniDB_CharacterRepository AniDB_Character = null!;
    public static AniDB_CreatorRepository AniDB_Creator = null!;
    public static AniDB_Episode_TitleRepository AniDB_Episode_Title = null!;
    public static AniDB_EpisodeRepository AniDB_Episode = null!;
    public static AniDB_GroupStatusRepository AniDB_GroupStatus = null!;
    public static AniDB_MessageRepository AniDB_Message = null!;
    public static AniDB_NotifyQueueRepository AniDB_NotifyQueue = null!;
    public static AniDB_TagRepository AniDB_Tag = null!;
    public static AnimeEpisode_UserRepository AnimeEpisode_User = null!;
    public static AnimeEpisodeRepository AnimeEpisode = null!;
    public static AnimeGroup_UserRepository AnimeGroup_User = null!;
    public static AnimeGroupRepository AnimeGroup = null!;
    public static AnimeSeries_UserRepository AnimeSeries_User = null!;
    public static AnimeSeriesRepository AnimeSeries = null!;
    public static AuthTokensRepository AuthTokens = null!;
    public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL = null!;
    public static CrossRef_AniDB_TMDB_EpisodeRepository CrossRef_AniDB_TMDB_Episode = null!;
    public static CrossRef_AniDB_TMDB_MovieRepository CrossRef_AniDB_TMDB_Movie = null!;
    public static CrossRef_AniDB_TMDB_ShowRepository CrossRef_AniDB_TMDB_Show = null!;
    public static CrossRef_CustomTagRepository CrossRef_CustomTag = null!;
    public static CrossRef_File_EpisodeRepository CrossRef_File_Episode = null!;
    public static CustomTagRepository CustomTag = null!;
    public static FileNameHashRepository FileNameHash = null!;
    public static FilterPresetRepository FilterPreset = null!;
    public static JMMUserRepository JMMUser = null!;
    public static PlaylistRepository Playlist = null!;
    public static ScanFileRepository ScanFile = null!;
    public static ScanRepository Scan = null!;
    public static ScheduledUpdateRepository ScheduledUpdate = null!;
    public static ShokoImage_EntityRepository ShokoImage_Entity = null!;
    public static ShokoImageRepository ShokoImage = null!;
    public static ShokoManagedFolderRepository ShokoManagedFolder = null!;
    public static StoredReleaseInfoRepository StoredReleaseInfo = null!;
    public static StoredRelocationPresetRepository StoredRelocationPreset = null!;
    public static StoredReleaseInfo_MatchAttemptRepository StoredReleaseInfo_MatchAttempt = null!;
    public static TMDB_AlternateOrdering_EpisodeRepository TMDB_AlternateOrdering_Episode = null!;
    public static TMDB_AlternateOrdering_SeasonRepository TMDB_AlternateOrdering_Season = null!;
    public static TMDB_AlternateOrderingRepository TMDB_AlternateOrdering = null!;
    public static TMDB_Collection_MovieRepository TMDB_Collection_Movie = null!;
    public static TMDB_CollectionRepository TMDB_Collection = null!;
    public static TMDB_Company_EntityRepository TMDB_Company_Entity = null!;
    public static TMDB_CompanyRepository TMDB_Company = null!;
    public static TMDB_Episode_CastRepository TMDB_Episode_Cast = null!;
    public static TMDB_Episode_CrewRepository TMDB_Episode_Crew = null!;
    public static TMDB_EpisodeRepository TMDB_Episode = null!;
    public static TMDB_Movie_CastRepository TMDB_Movie_Cast = null!;
    public static TMDB_Movie_CrewRepository TMDB_Movie_Crew = null!;
    public static TMDB_MovieRepository TMDB_Movie = null!;
    public static TMDB_NetworkRepository TMDB_Network = null!;
    public static TMDB_OverviewRepository TMDB_Overview = null!;
    public static TMDB_PersonRepository TMDB_Person = null!;
    public static TMDB_SeasonRepository TMDB_Season = null!;
    public static TMDB_Show_NetworkRepository TMDB_Show_Network = null!;
    public static TMDB_ShowRepository TMDB_Show = null!;
    public static TMDB_TitleRepository TMDB_Title = null!;
    public static VersionsRepository Versions = null!;
    public static VideoLocalRepository VideoLocal = null!;
    public static VideoLocal_HashDigestRepository VideoLocalHashDigest = null!;
    public static VideoLocal_PlaceRepository VideoLocalPlace = null!;
    public static VideoLocal_UserRepository VideoLocalUser = null!;

    public RepoFactory(
        ILogger<RepoFactory> logger,
        SystemService systemService,
        IEnumerable<ICachedRepository> repositories,
        AniDB_Anime_CharacterRepository anidbAnimeCharacter,
        AniDB_Anime_Character_CreatorRepository anidbAnimeCharacterCreator,
        AniDB_Anime_RelationRepository anidbAnimeRelation,
        AniDB_Anime_SimilarRepository anidbAnimeSimilar,
        AniDB_Anime_StaffRepository anidbAnimeStaff,
        AniDB_Anime_TagRepository anidbAnimeTag,
        AniDB_Anime_TitleRepository anidbAnimeTitle,
        AniDB_AnimeRepository anidbAnime,
        AniDB_AnimeUpdateRepository anidbAnimeUpdate,
        AniDB_CharacterRepository anidbCharacter,
        AniDB_CreatorRepository anidbCreator,
        AniDB_Episode_TitleRepository anidbEpisodeTitle,
        AniDB_EpisodeRepository anidbEpisode,
        AniDB_GroupStatusRepository anidbGroupStatus,
        AniDB_MessageRepository anidbMessage,
        AniDB_NotifyQueueRepository anidbNotifyQueue,
        AniDB_TagRepository anidbTag,
        AnimeEpisode_UserRepository animeEpisodeUser,
        AnimeEpisodeRepository animeEpisode,
        AnimeGroup_UserRepository animeGroupUser,
        AnimeGroupRepository animeGroup,
        AnimeSeries_UserRepository animeSeriesUser,
        AnimeSeriesRepository animeSeries,
        AuthTokensRepository authTokens,
        CrossRef_AniDB_MALRepository crossRefAniDBMal,
        CrossRef_AniDB_TMDB_EpisodeRepository crossRefAniDBTmdbEpisode,
        CrossRef_AniDB_TMDB_MovieRepository crossRefAniDBTmdbMovie,
        CrossRef_AniDB_TMDB_ShowRepository crossRefAniDBTmdbShow,
        CrossRef_CustomTagRepository crossRefCustomTag,
        CrossRef_File_EpisodeRepository crossRefFileEpisode,
        CustomTagRepository customTag,
        FileNameHashRepository fileNameHash,
        FilterPresetRepository filterPreset,
        JMMUserRepository jmmUser,
        PlaylistRepository playlist,
        ScanFileRepository scanFile,
        ScanRepository scan,
        ScheduledUpdateRepository scheduledUpdate,
        ShokoImage_EntityRepository shokoImageEntity,
        ShokoImageRepository shokoImage,
        ShokoManagedFolderRepository shokoManagedFolder,
        StoredRelocationPresetRepository storedRelocationPreset,
        StoredReleaseInfoRepository storedReleaseInfo,
        StoredReleaseInfo_MatchAttemptRepository storedReleaseInfoMatchAttempt,
        TMDB_AlternateOrdering_EpisodeRepository tmdbAlternateOrderingEpisode,
        TMDB_AlternateOrdering_SeasonRepository tmdbAlternateOrderingSeason,
        TMDB_AlternateOrderingRepository tmdbAlternateOrdering,
        TMDB_Collection_MovieRepository tmdbCollectionMovie,
        TMDB_CollectionRepository tmdbCollection,
        TMDB_Company_EntityRepository tmdbCompanyEntity,
        TMDB_CompanyRepository tmdbCompany,
        TMDB_Episode_CastRepository tmdbEpisodeCast,
        TMDB_Episode_CrewRepository tmdbEpisodeCrew,
        TMDB_EpisodeRepository tmdbEpisode,
        TMDB_Movie_CastRepository tmdbMovieCast,
        TMDB_Movie_CrewRepository tmdbMovieCrew,
        TMDB_MovieRepository tmdbMovie,
        TMDB_NetworkRepository tmdbNetwork,
        TMDB_OverviewRepository tmdbOverview,
        TMDB_PersonRepository tmdbPerson,
        TMDB_SeasonRepository tmdbSeason,
        TMDB_Show_NetworkRepository tmdbShowNetwork,
        TMDB_ShowRepository tmdbShow,
        TMDB_TitleRepository tmdbTitle,
        VersionsRepository versions,
        VideoLocal_HashDigestRepository videoLocalHashDigest,
        VideoLocal_PlaceRepository videoLocalPlace,
        VideoLocal_UserRepository videoLocalUser,
        VideoLocalRepository videoLocal
    )
    {
        _logger = logger;
        _systemService = systemService;
        _cachedRepositories = repositories.ToArray();
        AniDB_Anime = anidbAnime;
        AniDB_Anime_Character = anidbAnimeCharacter;
        AniDB_Anime_Character_Creator = anidbAnimeCharacterCreator;
        AniDB_Anime_Relation = anidbAnimeRelation;
        AniDB_Anime_Similar = anidbAnimeSimilar;
        AniDB_Anime_Staff = anidbAnimeStaff;
        AniDB_Anime_Tag = anidbAnimeTag;
        AniDB_Anime_Title = anidbAnimeTitle;
        AniDB_AnimeUpdate = anidbAnimeUpdate;
        AniDB_Character = anidbCharacter;
        AniDB_Creator = anidbCreator;
        AniDB_Episode = anidbEpisode;
        AniDB_Episode_Title = anidbEpisodeTitle;
        AniDB_GroupStatus = anidbGroupStatus;
        AniDB_Message = anidbMessage;
        AniDB_NotifyQueue = anidbNotifyQueue;
        AniDB_Tag = anidbTag;
        AnimeEpisode = animeEpisode;
        AnimeEpisode_User = animeEpisodeUser;
        AnimeGroup = animeGroup;
        AnimeGroup_User = animeGroupUser;
        AnimeSeries = animeSeries;
        AnimeSeries_User = animeSeriesUser;
        AuthTokens = authTokens;
        CrossRef_AniDB_MAL = crossRefAniDBMal;
        CrossRef_AniDB_TMDB_Episode = crossRefAniDBTmdbEpisode;
        CrossRef_AniDB_TMDB_Movie = crossRefAniDBTmdbMovie;
        CrossRef_AniDB_TMDB_Show = crossRefAniDBTmdbShow;
        CrossRef_CustomTag = crossRefCustomTag;
        CrossRef_File_Episode = crossRefFileEpisode;
        CustomTag = customTag;
        FileNameHash = fileNameHash;
        FilterPreset = filterPreset;
        JMMUser = jmmUser;
        Playlist = playlist;
        Scan = scan;
        ScanFile = scanFile;
        ScheduledUpdate = scheduledUpdate;
        ShokoImage = shokoImage;
        ShokoImage_Entity = shokoImageEntity;
        ShokoManagedFolder = shokoManagedFolder;
        StoredReleaseInfo = storedReleaseInfo;
        StoredRelocationPreset = storedRelocationPreset;
        StoredReleaseInfo_MatchAttempt = storedReleaseInfoMatchAttempt;
        TMDB_AlternateOrdering = tmdbAlternateOrdering;
        TMDB_AlternateOrdering_Episode = tmdbAlternateOrderingEpisode;
        TMDB_AlternateOrdering_Season = tmdbAlternateOrderingSeason;
        TMDB_Collection = tmdbCollection;
        TMDB_Collection_Movie = tmdbCollectionMovie;
        TMDB_Company = tmdbCompany;
        TMDB_Company_Entity = tmdbCompanyEntity;
        TMDB_Episode = tmdbEpisode;
        TMDB_Episode_Cast = tmdbEpisodeCast;
        TMDB_Episode_Crew = tmdbEpisodeCrew;
        TMDB_Movie = tmdbMovie;
        TMDB_Movie_Cast = tmdbMovieCast;
        TMDB_Movie_Crew = tmdbMovieCrew;
        TMDB_Network = tmdbNetwork;
        TMDB_Overview = tmdbOverview;
        TMDB_Person = tmdbPerson;
        TMDB_Season = tmdbSeason;
        TMDB_Show = tmdbShow;
        TMDB_Show_Network = tmdbShowNetwork;
        TMDB_Title = tmdbTitle;
        Versions = versions;
        VideoLocal = videoLocal;
        VideoLocalHashDigest = videoLocalHashDigest;
        VideoLocalPlace = videoLocalPlace;
        VideoLocalUser = videoLocalUser;
    }

    public void Init(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;
        try
        {
            foreach (var repo in _cachedRepositories)
            {
                repo.Populate(cancellationToken: cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "There was an error starting the Database Factory - Caching: {Ex}", exception);
            throw;
        }
    }

    public void PostInit()
    {
        // Update Contracts if necessary
        try
        {
            _systemService.StartupMessage = "RepoFactory.PostInit()";
            foreach (var repo in _cachedRepositories)
            {
                _systemService.StartupMessage = $"Database - Validating - {repo.GetType().Name.Replace("Repository", "")} Database Regeneration...";
                repo.RegenerateDb();
            }

            foreach (var repo in _cachedRepositories)
            {
                repo.PostProcess();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error starting the Database Factory - Regenerating: {Ex}", e);
            throw;
        }
    }
}
