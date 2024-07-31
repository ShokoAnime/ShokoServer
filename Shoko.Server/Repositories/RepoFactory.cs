using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Utils;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;

// ReSharper disable InconsistentNaming

#pragma warning disable CA2211
namespace Shoko.Server.Repositories;

public class RepoFactory
{
    private readonly ILogger<RepoFactory> _logger;
    private readonly ICachedRepository[] _cachedRepositories;

    public static AniDB_Anime_CharacterRepository AniDB_Anime_Character;
    public static AniDB_Anime_PreferredImageRepository AniDB_Anime_PreferredImage;
    public static AniDB_Anime_RelationRepository AniDB_Anime_Relation;
    public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar;
    public static AniDB_Anime_StaffRepository AniDB_Anime_Staff;
    public static AniDB_Anime_TagRepository AniDB_Anime_Tag;
    public static AniDB_Anime_TitleRepository AniDB_Anime_Title;
    public static AniDB_AnimeRepository AniDB_Anime;
    public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate;
    public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu;
    public static AniDB_CharacterRepository AniDB_Character;
    public static AniDB_Episode_PreferredImageRepository AniDB_Episode_PreferredImage;
    public static AniDB_Episode_TitleRepository AniDB_Episode_Title;
    public static AniDB_EpisodeRepository AniDB_Episode;
    public static AniDB_FileRepository AniDB_File;
    public static AniDB_FileUpdateRepository AniDB_FileUpdate;
    public static AniDB_GroupStatusRepository AniDB_GroupStatus;
    public static AniDB_MessageRepository AniDB_Message;
    public static AniDB_NotifyQueueRepository AniDB_NotifyQueue;
    public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup;
    public static AniDB_SeiyuuRepository AniDB_Seiyuu;
    public static AniDB_TagRepository AniDB_Tag;
    public static AniDB_VoteRepository AniDB_Vote;
    public static AnimeCharacterRepository AnimeCharacter;
    public static AnimeEpisode_UserRepository AnimeEpisode_User;
    public static AnimeEpisodeRepository AnimeEpisode;
    public static AnimeGroup_UserRepository AnimeGroup_User;
    public static AnimeGroupRepository AnimeGroup;
    public static AnimeSeries_UserRepository AnimeSeries_User;
    public static AnimeSeriesRepository AnimeSeries;
    public static AnimeStaffRepository AnimeStaff;
    public static AuthTokensRepository AuthTokens;
    public static BookmarkedAnimeRepository BookmarkedAnime;
    public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL;
    public static CrossRef_AniDB_TMDB_EpisodeRepository CrossRef_AniDB_TMDB_Episode;
    public static CrossRef_AniDB_TMDB_MovieRepository CrossRef_AniDB_TMDB_Movie;
    public static CrossRef_AniDB_TMDB_ShowRepository CrossRef_AniDB_TMDB_Show;
    public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2;
    public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CrossRef_AniDB_TvDB_Episode_Override;
    public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode;
    public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB;
    public static CrossRef_Anime_StaffRepository CrossRef_Anime_Staff;
    public static CrossRef_CustomTagRepository CrossRef_CustomTag;
    public static CrossRef_File_EpisodeRepository CrossRef_File_Episode;
    public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File;
    public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File;
    public static CustomTagRepository CustomTag;
    public static FileNameHashRepository FileNameHash;
    public static FilterPresetRepository FilterPreset;
    public static IgnoreAnimeRepository IgnoreAnime;
    public static ImportFolderRepository ImportFolder;
    public static JMMUserRepository JMMUser;
    public static PlaylistRepository Playlist;
    public static RenameScriptRepository RenameScript;
    public static ScanFileRepository ScanFile;
    public static ScanRepository Scan;
    public static ScheduledUpdateRepository ScheduledUpdate;
    public static TMDB_AlternateOrdering_EpisodeRepository TMDB_AlternateOrdering_Episode;
    public static TMDB_AlternateOrdering_SeasonRepository TMDB_AlternateOrdering_Season;
    public static TMDB_AlternateOrderingRepository TMDB_AlternateOrdering;
    public static TMDB_Collection_MovieRepository TMDB_Collection_Movie;
    public static TMDB_CollectionRepository TMDB_Collection;
    public static TMDB_Company_EntityRepository TMDB_Company_Entity;
    public static TMDB_CompanyRepository TMDB_Company;
    public static TMDB_Episode_CastRepository TMDB_Episode_Cast;
    public static TMDB_Episode_CrewRepository TMDB_Episode_Crew;
    public static TMDB_EpisodeRepository TMDB_Episode;
    public static TMDB_ImageRepository TMDB_Image;
    public static TMDB_Movie_CastRepository TMDB_Movie_Cast;
    public static TMDB_Movie_CrewRepository TMDB_Movie_Crew;
    public static TMDB_MovieRepository TMDB_Movie;
    public static TMDB_NetworkRepository TMDB_Network;
    public static TMDB_OverviewRepository TMDB_Overview;
    public static TMDB_PersonRepository TMDB_Person;
    public static TMDB_SeasonRepository TMDB_Season;
    public static TMDB_Show_NetworkRepository TMDB_Show_Network;
    public static TMDB_ShowRepository TMDB_Show;
    public static TMDB_TitleRepository TMDB_Title;
    public static Trakt_EpisodeRepository Trakt_Episode;
    public static Trakt_SeasonRepository Trakt_Season;
    public static Trakt_ShowRepository Trakt_Show;
    public static TvDB_EpisodeRepository TvDB_Episode;
    public static TvDB_ImageFanartRepository TvDB_ImageFanart;
    public static TvDB_ImagePosterRepository TvDB_ImagePoster;
    public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner;
    public static TvDB_SeriesRepository TvDB_Series;
    public static VersionsRepository Versions;
    public static VideoLocal_PlaceRepository VideoLocalPlace;
    public static VideoLocal_UserRepository VideoLocalUser;
    public static VideoLocalRepository VideoLocal;

    public RepoFactory(
        ILogger<RepoFactory> logger,
        IEnumerable<ICachedRepository> repositories,
        AniDB_Anime_CharacterRepository anidbAnimeCharacter,
        AniDB_Anime_PreferredImageRepository anidbAnimePreferredImage,
        AniDB_Anime_RelationRepository anidbAnimeRelation,
        AniDB_Anime_SimilarRepository anidbAnimeSimilar,
        AniDB_Anime_StaffRepository anidbAnimeStaff,
        AniDB_Anime_TagRepository anidbAnimeTag,
        AniDB_Anime_TitleRepository anidbAnimeTitle,
        AniDB_AnimeRepository anidbAnime,
        AniDB_AnimeUpdateRepository anidbAnimeUpdate,
        AniDB_Character_SeiyuuRepository anidbCharacterSeiyuu,
        AniDB_CharacterRepository anidbCharacter,
        AniDB_Episode_PreferredImageRepository anidbEpisodePreferredImage,
        AniDB_Episode_TitleRepository anidbEpisodeTitle,
        AniDB_EpisodeRepository anidbEpisode,
        AniDB_FileRepository anidbFile,
        AniDB_FileUpdateRepository anidbFileUpdate,
        AniDB_GroupStatusRepository anidbGroupStatus,
        AniDB_MessageRepository anidbMessage,
        AniDB_NotifyQueueRepository anidbNotifyQueue,
        AniDB_ReleaseGroupRepository anidbReleaseGroup,
        AniDB_SeiyuuRepository anidbSeiyuu,
        AniDB_TagRepository anidbTag,
        AniDB_VoteRepository anidbVote,
        AnimeCharacterRepository animeCharacter,
        AnimeEpisode_UserRepository animeEpisodeUser,
        AnimeEpisodeRepository animeEpisode,
        AnimeGroup_UserRepository animeGroupUser,
        AnimeGroupRepository animeGroup,
        AnimeSeries_UserRepository animeSeriesUser,
        AnimeSeriesRepository animeSeries,
        AnimeStaffRepository animeStaff,
        AuthTokensRepository authTokens,
        BookmarkedAnimeRepository bookmarkedAnime,
        CrossRef_AniDB_MALRepository crossRefAniDBMal,
        CrossRef_AniDB_TMDB_EpisodeRepository crossRefAniDBTmdbEpisode,
        CrossRef_AniDB_TMDB_MovieRepository crossRefAniDBTmdbMovie,
        CrossRef_AniDB_TMDB_ShowRepository crossRefAniDBTmdbShow,
        CrossRef_AniDB_TraktV2Repository crossRefAniDBTraktV2,
        CrossRef_AniDB_TvDB_Episode_OverrideRepository crossRefAniDBTvDBEpisodeOverride,
        CrossRef_AniDB_TvDB_EpisodeRepository crossRefAniDBTvDBEpisode,
        CrossRef_AniDB_TvDBRepository crossRefAniDBTvDB,
        CrossRef_Anime_StaffRepository crossRefAnimeStaff,
        CrossRef_CustomTagRepository crossRefCustomTag,
        CrossRef_File_EpisodeRepository crossRefFileEpisode,
        CrossRef_Languages_AniDB_FileRepository crossRefLanguagesAniDBFile,
        CrossRef_Subtitles_AniDB_FileRepository crossRefSubtitlesAniDBFile,
        CustomTagRepository customTag,
        FileNameHashRepository fileNameHash,
        FilterPresetRepository filterPreset,
        IgnoreAnimeRepository ignoreAnime,
        ImportFolderRepository importFolder,
        JMMUserRepository jmmUser,
        PlaylistRepository playlist,
        RenameScriptRepository renameScript,
        ScanFileRepository scanFile,
        ScanRepository scan,
        ScheduledUpdateRepository scheduledUpdate,
        Trakt_EpisodeRepository traktEpisode,
        Trakt_SeasonRepository traktSeason,
        Trakt_ShowRepository traktShow,
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
        TMDB_ImageRepository tmdbImage,
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
        TvDB_EpisodeRepository tvDBEpisode,
        TvDB_ImageFanartRepository tvDBImageFanart,
        TvDB_ImagePosterRepository tvDBImagePoster,
        TvDB_ImageWideBannerRepository tvDBImageWideBanner,
        TvDB_SeriesRepository tvDBSeries,
        VersionsRepository versions,
        VideoLocal_PlaceRepository videoLocalPlace,
        VideoLocal_UserRepository videoLocalUser,
        VideoLocalRepository videoLocal
    )
    {
        _logger = logger;
        _cachedRepositories = repositories.ToArray();
        AniDB_Anime = anidbAnime;
        AniDB_Anime_Character = anidbAnimeCharacter;
        AniDB_Anime_PreferredImage = anidbAnimePreferredImage;
        AniDB_Anime_Relation = anidbAnimeRelation;
        AniDB_Anime_Similar = anidbAnimeSimilar;
        AniDB_Anime_Staff = anidbAnimeStaff;
        AniDB_Anime_Tag = anidbAnimeTag;
        AniDB_Anime_Title = anidbAnimeTitle;
        AniDB_AnimeUpdate = anidbAnimeUpdate;
        AniDB_Character = anidbCharacter;
        AniDB_Character_Seiyuu = anidbCharacterSeiyuu;
        AniDB_Episode = anidbEpisode;
        AniDB_Episode_PreferredImage = anidbEpisodePreferredImage;
        AniDB_Episode_Title = anidbEpisodeTitle;
        AniDB_File = anidbFile;
        AniDB_FileUpdate = anidbFileUpdate;
        AniDB_GroupStatus = anidbGroupStatus;
        AniDB_Message = anidbMessage;
        AniDB_NotifyQueue = anidbNotifyQueue;
        AniDB_ReleaseGroup = anidbReleaseGroup;
        AniDB_Seiyuu = anidbSeiyuu;
        AniDB_Tag = anidbTag;
        AniDB_Vote = anidbVote;
        AnimeCharacter = animeCharacter;
        AnimeEpisode = animeEpisode;
        AnimeEpisode_User = animeEpisodeUser;
        AnimeGroup = animeGroup;
        AnimeGroup_User = animeGroupUser;
        AnimeSeries = animeSeries;
        AnimeSeries_User = animeSeriesUser;
        AnimeStaff = animeStaff;
        AuthTokens = authTokens;
        BookmarkedAnime = bookmarkedAnime;
        CrossRef_AniDB_MAL = crossRefAniDBMal;
        CrossRef_AniDB_TMDB_Episode = crossRefAniDBTmdbEpisode;
        CrossRef_AniDB_TMDB_Movie = crossRefAniDBTmdbMovie;
        CrossRef_AniDB_TMDB_Show = crossRefAniDBTmdbShow;
        CrossRef_AniDB_TraktV2 = crossRefAniDBTraktV2;
        CrossRef_AniDB_TvDB = crossRefAniDBTvDB;
        CrossRef_AniDB_TvDB_Episode = crossRefAniDBTvDBEpisode;
        CrossRef_AniDB_TvDB_Episode_Override = crossRefAniDBTvDBEpisodeOverride;
        CrossRef_Anime_Staff = crossRefAnimeStaff;
        CrossRef_CustomTag = crossRefCustomTag;
        CrossRef_File_Episode = crossRefFileEpisode;
        CrossRef_Languages_AniDB_File = crossRefLanguagesAniDBFile;
        CrossRef_Subtitles_AniDB_File = crossRefSubtitlesAniDBFile;
        CustomTag = customTag;
        FileNameHash = fileNameHash;
        FilterPreset = filterPreset;
        IgnoreAnime = ignoreAnime;
        ImportFolder = importFolder;
        JMMUser = jmmUser;
        Playlist = playlist;
        RenameScript = renameScript;
        Scan = scan;
        ScanFile = scanFile;
        ScheduledUpdate = scheduledUpdate;
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
        TMDB_Image = tmdbImage;
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
        Trakt_Episode = traktEpisode;
        Trakt_Season = traktSeason;
        Trakt_Show = traktShow;
        TvDB_Episode = tvDBEpisode;
        TvDB_ImageFanart = tvDBImageFanart;
        TvDB_ImagePoster = tvDBImagePoster;
        TvDB_ImageWideBanner = tvDBImageWideBanner;
        TvDB_Series = tvDBSeries;
        Versions = versions;
        VideoLocal = videoLocal;
        VideoLocalPlace = videoLocalPlace;
        VideoLocalUser = videoLocalUser;
    }

    public void Init()
    {
        try
        {
            foreach (var repo in _cachedRepositories)
            {
                repo.Populate();
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
            _logger.LogInformation("Starting Server: RepoFactory.PostInit()");
            foreach (var repo in _cachedRepositories)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, repo.GetType().Name.Replace("Repository", ""), " Database Regeneration");
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
