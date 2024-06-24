using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories;

public class RepoFactory
{
    private readonly ILogger<RepoFactory> logger;
    private readonly ICachedRepository[] CachedRepositories;

    public static VersionsRepository Versions;
    public static Trakt_ShowRepository Trakt_Show;
    public static Trakt_SeasonRepository Trakt_Season;
    public static Trakt_EpisodeRepository Trakt_Episode;
    public static ScheduledUpdateRepository ScheduledUpdate;
    public static RenamerInstanceRepository RenamerInstance;
    public static PlaylistRepository Playlist;
    public static MovieDB_PosterRepository MovieDB_Poster;
    public static MovieDB_FanartRepository MovieDB_Fanart;
    public static MovieDb_MovieRepository MovieDb_Movie;
    public static IgnoreAnimeRepository IgnoreAnime;
    public static FileNameHashRepository FileNameHash;
    public static AniDB_AnimeUpdateRepository AniDB_AnimeUpdate;
    public static AniDB_FileUpdateRepository AniDB_FileUpdate;
    public static CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File;
    public static CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File;
    public static CrossRef_AniDB_OtherRepository CrossRef_AniDB_Other;
    public static CrossRef_AniDB_MALRepository CrossRef_AniDB_MAL;
    public static BookmarkedAnimeRepository BookmarkedAnime;
    public static AniDB_SeiyuuRepository AniDB_Seiyuu;
    public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup;
    public static AniDB_GroupStatusRepository AniDB_GroupStatus;
    public static AniDB_CharacterRepository AniDB_Character;
    public static AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu;
    public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar;
    public static AniDB_Anime_RelationRepository AniDB_Anime_Relation;
    public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage;
    public static AniDB_Anime_CharacterRepository AniDB_Anime_Character;
    public static AniDB_Anime_StaffRepository AniDB_Anime_Staff;
    public static ScanRepository Scan;
    public static ScanFileRepository ScanFile;

    public static JMMUserRepository JMMUser;
    public static AuthTokensRepository AuthTokens;
    public static ImportFolderRepository ImportFolder;
    public static AniDB_AnimeRepository AniDB_Anime;
    public static AniDB_Episode_TitleRepository AniDB_Episode_Title;
    public static AniDB_EpisodeRepository AniDB_Episode;
    public static AniDB_FileRepository AniDB_File;
    public static AniDB_Anime_TitleRepository AniDB_Anime_Title;
    public static AniDB_Anime_TagRepository AniDB_Anime_Tag;
    public static AniDB_TagRepository AniDB_Tag;
    public static CustomTagRepository CustomTag;
    public static CrossRef_CustomTagRepository CrossRef_CustomTag;
    public static CrossRef_File_EpisodeRepository CrossRef_File_Episode;
    public static VideoLocal_PlaceRepository VideoLocalPlace;
    public static VideoLocalRepository VideoLocal;
    public static VideoLocal_UserRepository VideoLocalUser;
    public static AnimeEpisodeRepository AnimeEpisode;
    public static AnimeEpisode_UserRepository AnimeEpisode_User;
    public static AnimeSeriesRepository AnimeSeries;
    public static AnimeSeries_UserRepository AnimeSeries_User;
    public static AnimeGroupRepository AnimeGroup;
    public static AnimeGroup_UserRepository AnimeGroup_User;
    public static AniDB_VoteRepository AniDB_Vote;
    public static TvDB_EpisodeRepository TvDB_Episode;
    public static TvDB_SeriesRepository TvDB_Series;
    public static CrossRef_AniDB_TvDBRepository CrossRef_AniDB_TvDB;
    public static CrossRef_AniDB_TvDB_EpisodeRepository CrossRef_AniDB_TvDB_Episode;
    public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CrossRef_AniDB_TvDB_Episode_Override;
    public static TvDB_ImagePosterRepository TvDB_ImagePoster;
    public static TvDB_ImageFanartRepository TvDB_ImageFanart;
    public static TvDB_ImageWideBannerRepository TvDB_ImageWideBanner;
    public static CrossRef_AniDB_TraktV2Repository CrossRef_AniDB_TraktV2;
    public static AnimeCharacterRepository AnimeCharacter;
    public static AnimeStaffRepository AnimeStaff;
    public static CrossRef_Anime_StaffRepository CrossRef_Anime_Staff;
    public static FilterPresetRepository FilterPreset;

    public RepoFactory(ILogger<RepoFactory> logger, IEnumerable<ICachedRepository> repositories, VersionsRepository versions, Trakt_ShowRepository traktShow,
        Trakt_SeasonRepository traktSeason, Trakt_EpisodeRepository traktEpisode, ScheduledUpdateRepository scheduledUpdate,
        RenamerInstanceRepository renamerInstance, PlaylistRepository playlist, MovieDB_PosterRepository movieDBPoster, MovieDB_FanartRepository movieDBFanart,
        MovieDb_MovieRepository movieDbMovie, IgnoreAnimeRepository ignoreAnime, FileNameHashRepository fileNameHash,
        AniDB_AnimeUpdateRepository aniDBAnimeUpdate, AniDB_FileUpdateRepository aniDBFileUpdate,
        CrossRef_Subtitles_AniDB_FileRepository crossRefSubtitlesAniDBFile, CrossRef_Languages_AniDB_FileRepository crossRefLanguagesAniDBFile,
        CrossRef_AniDB_OtherRepository crossRefAniDBOther, CrossRef_AniDB_MALRepository crossRefAniDBMal, BookmarkedAnimeRepository bookmarkedAnime,
        AniDB_SeiyuuRepository aniDBSeiyuu, AniDB_ReleaseGroupRepository aniDBReleaseGroup, AniDB_GroupStatusRepository aniDBGroupStatus,
        AniDB_CharacterRepository aniDBCharacter, AniDB_Character_SeiyuuRepository aniDBCharacterSeiyuu, AniDB_Anime_SimilarRepository aniDBAnimeSimilar,
        AniDB_Anime_RelationRepository aniDBAnimeRelation, AniDB_Anime_DefaultImageRepository aniDBAnimeDefaultImage,
        AniDB_Anime_CharacterRepository aniDBAnimeCharacter, AniDB_Anime_StaffRepository aniDBAnimeStaff, ScanRepository scan, ScanFileRepository scanFile,
        JMMUserRepository jmmUser, AuthTokensRepository authTokens, ImportFolderRepository importFolder, AniDB_AnimeRepository aniDBAnime,
        AniDB_Episode_TitleRepository aniDBEpisodeTitle, AniDB_EpisodeRepository aniDBEpisode, AniDB_FileRepository aniDBFile,
        AniDB_Anime_TitleRepository aniDBAnimeTitle, AniDB_Anime_TagRepository aniDBAnimeTag, AniDB_TagRepository aniDBTag, CustomTagRepository customTag,
        CrossRef_CustomTagRepository crossRefCustomTag, CrossRef_File_EpisodeRepository crossRefFileEpisode, VideoLocal_PlaceRepository videoLocalPlace,
        VideoLocalRepository videoLocal, VideoLocal_UserRepository videoLocalUser, AnimeEpisodeRepository animeEpisode,
        AnimeEpisode_UserRepository animeEpisodeUser, AnimeSeriesRepository animeSeries, AnimeSeries_UserRepository animeSeriesUser,
        AnimeGroupRepository animeGroup, AnimeGroup_UserRepository animeGroupUser, AniDB_VoteRepository aniDBVote, TvDB_EpisodeRepository tvDBEpisode,
        TvDB_SeriesRepository tvDBSeries, CrossRef_AniDB_TvDBRepository crossRefAniDBTvDB, CrossRef_AniDB_TvDB_EpisodeRepository crossRefAniDBTvDBEpisode,
        CrossRef_AniDB_TvDB_Episode_OverrideRepository crossRefAniDBTvDBEpisodeOverride, TvDB_ImagePosterRepository tvDBImagePoster,
        TvDB_ImageFanartRepository tvDBImageFanart, TvDB_ImageWideBannerRepository tvDBImageWideBanner, CrossRef_AniDB_TraktV2Repository crossRefAniDBTraktV2,
        AnimeCharacterRepository animeCharacter, AnimeStaffRepository animeStaff, CrossRef_Anime_StaffRepository crossRefAnimeStaff,
        FilterPresetRepository filterPreset)
    {
        this.logger = logger;
        CachedRepositories = repositories.ToArray();
        Versions = versions;
        Trakt_Show = traktShow;
        Trakt_Season = traktSeason;
        Trakt_Episode = traktEpisode;
        ScheduledUpdate = scheduledUpdate;
        RenamerInstance = renamerInstance;
        Playlist = playlist;
        MovieDB_Poster = movieDBPoster;
        MovieDB_Fanart = movieDBFanart;
        MovieDb_Movie = movieDbMovie;
        IgnoreAnime = ignoreAnime;
        FileNameHash = fileNameHash;
        AniDB_AnimeUpdate = aniDBAnimeUpdate;
        AniDB_FileUpdate = aniDBFileUpdate;
        CrossRef_Subtitles_AniDB_File = crossRefSubtitlesAniDBFile;
        CrossRef_Languages_AniDB_File = crossRefLanguagesAniDBFile;
        CrossRef_AniDB_Other = crossRefAniDBOther;
        CrossRef_AniDB_MAL = crossRefAniDBMal;
        BookmarkedAnime = bookmarkedAnime;
        AniDB_Seiyuu = aniDBSeiyuu;
        AniDB_ReleaseGroup = aniDBReleaseGroup;
        AniDB_GroupStatus = aniDBGroupStatus;
        AniDB_Character = aniDBCharacter;
        AniDB_Character_Seiyuu = aniDBCharacterSeiyuu;
        AniDB_Anime_Similar = aniDBAnimeSimilar;
        AniDB_Anime_Relation = aniDBAnimeRelation;
        AniDB_Anime_DefaultImage = aniDBAnimeDefaultImage;
        AniDB_Anime_Character = aniDBAnimeCharacter;
        AniDB_Anime_Staff = aniDBAnimeStaff;
        Scan = scan;
        ScanFile = scanFile;
        JMMUser = jmmUser;
        AuthTokens = authTokens;
        ImportFolder = importFolder;
        AniDB_Anime = aniDBAnime;
        AniDB_Episode_Title = aniDBEpisodeTitle;
        AniDB_Episode = aniDBEpisode;
        AniDB_File = aniDBFile;
        AniDB_Anime_Title = aniDBAnimeTitle;
        AniDB_Anime_Tag = aniDBAnimeTag;
        AniDB_Tag = aniDBTag;
        CustomTag = customTag;
        CrossRef_CustomTag = crossRefCustomTag;
        CrossRef_File_Episode = crossRefFileEpisode;
        VideoLocalPlace = videoLocalPlace;
        VideoLocal = videoLocal;
        VideoLocalUser = videoLocalUser;
        AnimeEpisode = animeEpisode;
        AnimeEpisode_User = animeEpisodeUser;
        AnimeSeries = animeSeries;
        AnimeSeries_User = animeSeriesUser;
        AnimeGroup = animeGroup;
        AnimeGroup_User = animeGroupUser;
        AniDB_Vote = aniDBVote;
        TvDB_Episode = tvDBEpisode;
        TvDB_Series = tvDBSeries;
        CrossRef_AniDB_TvDB = crossRefAniDBTvDB;
        CrossRef_AniDB_TvDB_Episode = crossRefAniDBTvDBEpisode;
        CrossRef_AniDB_TvDB_Episode_Override = crossRefAniDBTvDBEpisodeOverride;
        TvDB_ImagePoster = tvDBImagePoster;
        TvDB_ImageFanart = tvDBImageFanart;
        TvDB_ImageWideBanner = tvDBImageWideBanner;
        CrossRef_AniDB_TraktV2 = crossRefAniDBTraktV2;
        AnimeCharacter = animeCharacter;
        AnimeStaff = animeStaff;
        CrossRef_Anime_Staff = crossRefAnimeStaff;
        FilterPreset = filterPreset;
    }

    public void Init()
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
            logger.LogError(exception, "There was an error starting the Database Factory - Caching: {Ex}", exception);
            throw;
        }
    }

    public void PostInit()
    {
        // Update Contracts if necessary
        try
        {
            logger.LogInformation("Starting Server: RepoFactory.PostInit()");
            foreach (var repo in CachedRepositories)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, repo.GetType().Name.Replace("Repository", ""), " DbRegen");
                repo.RegenerateDb();
            }

            foreach (var repo in CachedRepositories)
            {
                repo.PostProcess();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an error starting the Database Factory - Regenerating: {Ex}", e);
            throw;
        }
    }
}
