using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Repositories.Direct.Metadata;
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
    public static AniDB_AnimeRepository AniDB_Anime = null!;
    public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate = null!;
    public static AniDB_CharacterRepository AniDB_Character = null!;
    public static AniDB_CreatorRepository AniDB_Creator = null!;
    public static AniDB_EpisodeRepository AniDB_Episode = null!;
    public static AniDB_GroupStatusRepository AniDB_GroupStatus = null!;
    public static AniDB_MessageRepository AniDB_Message = null!;
    public static AniDB_NotifyQueueRepository AniDB_NotifyQueue = null!;
    public static AniDB_ResourceRepository AniDB_Resource = null!;
    public static AniDB_TagRepository AniDB_Tag = null!;
    public static AiringChannelRepository AiringChannel = null!;
    public static AiringScheduleRepository AiringSchedule = null!;
    public static AiringScheduleSweepStateRepository AiringScheduleSweepState = null!;
    public static EpisodeAiringRepository EpisodeAiring = null!;
    public static AnimeEpisode_UserRepository AnimeEpisode_User = null!;
    public static AnimeEpisodeRepository AnimeEpisode = null!;
    public static AnimeGroup_UserRepository AnimeGroup_User = null!;
    public static AnimeGroupRepository AnimeGroup = null!;
    public static AnimeSeries_UserRepository AnimeSeries_User = null!;
    public static AnimeSeriesRepository AnimeSeries = null!;
    public static AuthTokensRepository AuthTokens = null!;
    public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL = null!;
    public static CrossRef_AniDB_Metadata_SeriesRepository CrossRef_AniDB_Metadata_Series = null!;
    public static CrossRef_AniDB_Metadata_MovieRepository CrossRef_AniDB_Metadata_Movie = null!;
    public static CrossRef_AniDB_Metadata_EpisodeRepository CrossRef_AniDB_Metadata_Episode = null!;
    public static CrossRef_CustomTagRepository CrossRef_CustomTag = null!;
    public static CrossRef_File_EpisodeRepository CrossRef_File_Episode = null!;
    public static CustomTagRepository CustomTag = null!;
    public static FileNameHashRepository FileNameHash = null!;
    public static FilterPresetRepository FilterPreset = null!;
    public static JMMUserRepository JMMUser = null!;
    public static Metadata_CreatorRepository Metadata_Creator = null!;
    public static Metadata_CharacterRepository Metadata_Character = null!;
    public static Metadata_CastRepository Metadata_Cast = null!;
    public static Metadata_CrewRepository Metadata_Crew = null!;
    public static Metadata_TagRepository Metadata_Tag = null!;
    public static Metadata_Tag_EntryRepository Metadata_Tag_Entry = null!;
    public static Metadata_StudioRepository Metadata_Studio = null!;
    public static Metadata_Studio_EntryRepository Metadata_Studio_Entry = null!;
    public static Metadata_NetworkRepository Metadata_Network = null!;
    public static Metadata_Network_EntryRepository Metadata_Network_Entry = null!;
    public static Metadata_RelationRepository Metadata_Relation = null!;
    public static Metadata_SuggestionRepository Metadata_Suggestion = null!;
    public static TextCache TextCache = null!;
    public static Metadata_SeriesRepository Metadata_Series = null!;
    public static Metadata_SeasonRepository Metadata_Season = null!;
    public static Metadata_EpisodeRepository Metadata_Episode = null!;
    public static Metadata_MovieRepository Metadata_Movie = null!;
    public static Metadata_CollectionRepository Metadata_Collection = null!;
    public static Metadata_Collection_MemberRepository Metadata_Collection_Member = null!;
    public static Metadata_ContentRatingRepository Metadata_ContentRating = null!;
    public static ScheduledUpdateRepository ScheduledUpdate = null!;
    public static ShokoImage_EntityRepository ShokoImage_Entity = null!;
    public static ShokoImageRepository ShokoImage = null!;
    public static ShokoManagedFolderRepository ShokoManagedFolder = null!;
    public static StoredReleaseInfoRepository StoredReleaseInfo = null!;
    public static StoredRelocationPresetRepository StoredRelocationPreset = null!;
    public static StoredReleaseInfo_MatchAttemptRepository StoredReleaseInfo_MatchAttempt = null!;
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
        AniDB_AnimeRepository anidbAnime,
        AniDB_AnimeUpdateRepository anidbAnimeUpdate,
        AniDB_CharacterRepository anidbCharacter,
        AniDB_CreatorRepository anidbCreator,
        AniDB_EpisodeRepository anidbEpisode,
        AniDB_GroupStatusRepository anidbGroupStatus,
        AniDB_MessageRepository anidbMessage,
        AniDB_NotifyQueueRepository anidbNotifyQueue,
        AniDB_ResourceRepository anidbResource,
        AniDB_TagRepository anidbTag,
        AiringChannelRepository airingChannel,
        AiringScheduleRepository airingSchedule,
        AiringScheduleSweepStateRepository airingScheduleSweepState,
        EpisodeAiringRepository episodeAiring,
        AnimeEpisode_UserRepository animeEpisodeUser,
        AnimeEpisodeRepository animeEpisode,
        AnimeGroup_UserRepository animeGroupUser,
        AnimeGroupRepository animeGroup,
        AnimeSeries_UserRepository animeSeriesUser,
        AnimeSeriesRepository animeSeries,
        AuthTokensRepository authTokens,
        CrossRef_AniDB_MALRepository crossRefAniDBMal,
        CrossRef_AniDB_Metadata_SeriesRepository crossRefAniDBMetadataSeries,
        CrossRef_AniDB_Metadata_MovieRepository crossRefAniDBMetadataMovie,
        CrossRef_AniDB_Metadata_EpisodeRepository crossRefAniDBMetadataEpisode,
        CrossRef_CustomTagRepository crossRefCustomTag,
        CrossRef_File_EpisodeRepository crossRefFileEpisode,
        CustomTagRepository customTag,
        FileNameHashRepository fileNameHash,
        FilterPresetRepository filterPreset,
        JMMUserRepository jmmUser,
        Metadata_CreatorRepository metadataCreator,
        Metadata_CharacterRepository metadataCharacter,
        Metadata_CastRepository metadataCast,
        Metadata_CrewRepository metadataCrew,
        Metadata_TagRepository metadataTag,
        Metadata_Tag_EntryRepository metadataTagEntry,
        Metadata_StudioRepository metadataStudio,
        Metadata_Studio_EntryRepository metadataStudioEntry,
        Metadata_NetworkRepository metadataNetwork,
        Metadata_Network_EntryRepository metadataNetworkEntry,
        Metadata_RelationRepository metadataRelation,
        Metadata_SuggestionRepository metadataSuggestion,
        TextCache textCache,
        Metadata_SeriesRepository metadataSeries,
        Metadata_SeasonRepository metadataSeason,
        Metadata_EpisodeRepository metadataEpisode,
        Metadata_MovieRepository metadataMovie,
        Metadata_CollectionRepository metadataCollection,
        Metadata_Collection_MemberRepository metadataCollectionMember,
        Metadata_ContentRatingRepository metadataContentRating,
        ScheduledUpdateRepository scheduledUpdate,
        ShokoImage_EntityRepository shokoImageEntity,
        ShokoImageRepository shokoImage,
        ShokoManagedFolderRepository shokoManagedFolder,
        StoredRelocationPresetRepository storedRelocationPreset,
        StoredReleaseInfoRepository storedReleaseInfo,
        StoredReleaseInfo_MatchAttemptRepository storedReleaseInfoMatchAttempt,
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
        AniDB_AnimeUpdate = anidbAnimeUpdate;
        AniDB_Character = anidbCharacter;
        AniDB_Creator = anidbCreator;
        AniDB_Episode = anidbEpisode;
        AniDB_GroupStatus = anidbGroupStatus;
        AniDB_Message = anidbMessage;
        AniDB_NotifyQueue = anidbNotifyQueue;
        AniDB_Resource = anidbResource;
        AniDB_Tag = anidbTag;
        AnimeEpisode = animeEpisode;
        AiringChannel = airingChannel;
        AiringSchedule = airingSchedule;
        AiringScheduleSweepState = airingScheduleSweepState;
        EpisodeAiring = episodeAiring;
        AnimeEpisode_User = animeEpisodeUser;
        AnimeGroup = animeGroup;
        AnimeGroup_User = animeGroupUser;
        AnimeSeries = animeSeries;
        AnimeSeries_User = animeSeriesUser;
        AuthTokens = authTokens;
        CrossRef_AniDB_MAL = crossRefAniDBMal;
        CrossRef_AniDB_Metadata_Series = crossRefAniDBMetadataSeries;
        CrossRef_AniDB_Metadata_Movie = crossRefAniDBMetadataMovie;
        CrossRef_AniDB_Metadata_Episode = crossRefAniDBMetadataEpisode;
        CrossRef_CustomTag = crossRefCustomTag;
        CrossRef_File_Episode = crossRefFileEpisode;
        CustomTag = customTag;
        FileNameHash = fileNameHash;
        FilterPreset = filterPreset;
        JMMUser = jmmUser;
        Metadata_Creator = metadataCreator;
        Metadata_Character = metadataCharacter;
        Metadata_Cast = metadataCast;
        Metadata_Crew = metadataCrew;
        Metadata_Tag = metadataTag;
        Metadata_Tag_Entry = metadataTagEntry;
        Metadata_Studio = metadataStudio;
        Metadata_Studio_Entry = metadataStudioEntry;
        Metadata_Network = metadataNetwork;
        Metadata_Network_Entry = metadataNetworkEntry;
        Metadata_Relation = metadataRelation;
        Metadata_Suggestion = metadataSuggestion;
        TextCache = textCache;
        Metadata_Series = metadataSeries;
        Metadata_Season = metadataSeason;
        Metadata_Episode = metadataEpisode;
        Metadata_Movie = metadataMovie;
        Metadata_Collection = metadataCollection;
        Metadata_Collection_Member = metadataCollectionMember;
        Metadata_ContentRating = metadataContentRating;
        ScheduledUpdate = scheduledUpdate;
        ShokoImage = shokoImage;
        ShokoImage_Entity = shokoImageEntity;
        ShokoManagedFolder = shokoManagedFolder;
        StoredReleaseInfo = storedReleaseInfo;
        StoredRelocationPreset = storedRelocationPreset;
        StoredReleaseInfo_MatchAttempt = storedReleaseInfoMatchAttempt;
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
