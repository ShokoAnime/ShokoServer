using System;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Models.Enums;

[Flags]
public enum AnimeGroupSortMethod
{
    SortName = 0,
    IsFave = 1
}

public enum AnimeSeason
{
    Winter,
    Spring,
    Summer,
    Fall
}

/// <summary>
/// Available data sources to chose from.
/// </summary>
[Flags]
public enum DataSourceType
{
    /// <summary>
    /// No source.
    /// </summary>
    None = 0,

    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 1,

    /// <summary>
    /// The Tv Database (TvDB).
    /// </summary>
    TvDB = 2,

    /// <summary>
    /// The Movie Database (TMDB).
    /// </summary>
    TMDB = 4,

    /// <summary>
    /// Trakt.
    /// </summary>
    Trakt = 8,

    /// <summary>
    /// My Anime List (MAL).
    /// </summary>
    MAL = 16,

    /// <summary>
    /// AniList (AL).
    /// </summary>
    AniList = 32,

    /// <summary>
    /// Animeshon.
    /// </summary>
    Animeshon = 64,

    /// <summary>
    /// Kitsu.
    /// </summary>
    Kitsu = 128,

    /// <summary>
    /// Shoko.
    /// </summary>
    Shoko = 1024,

    /// <summary>
    /// User.
    /// </summary>
    User = 2048,
}

public enum FileQualityFilterOperationType
{
    EQUALS,
    LESS_EQ,
    GREATER_EQ,
    IN,
    NOTIN
}
public enum FileQualityFilterType
{
    RESOLUTION,
    SOURCE,
    VERSION,
    AUDIOSTREAMCOUNT,
    VIDEOCODEC,
    AUDIOCODEC,
    CHAPTER,
    SUBGROUP,
    SUBSTREAMCOUNT
}

public enum FileSearchCriteria
{
    Name = 1,
    Size = 2,
    LastOneHundred = 3,
    ED2KHash = 4
}

public enum GroupFilterBaseCondition
{
    Include = 1,
    Exclude = 2
}

public enum GroupFilterConditionType
{
    CompletedSeries = 1,
    MissingEpisodes = 2,
    HasUnwatchedEpisodes = 3,
    // AllEpisodesWatched = 4,
    UserVoted = 5,
    Tag = 6,
    AirDate = 7,
    //Studio = 8,
    AssignedTvDBInfo = 9,
    //ReleaseGroup = 11,
    AnimeType = 12,
    VideoQuality = 13,
    Favourite = 14,
    AnimeGroup = 15,
    AniDBRating = 16,
    UserRating = 17,
    SeriesCreatedDate = 18,
    EpisodeAddedDate = 19,
    EpisodeWatchedDate = 20,
    FinishedAiring = 21,
    MissingEpisodesCollecting = 22,
    AudioLanguage = 23,
    SubtitleLanguage = 24,
    AssignedTvDBOrMovieDBInfo = 25,
    AssignedMovieDBInfo = 26,
    UserVotedAny = 27,
    HasWatchedEpisodes = 28,
    AssignedMALInfo = 29,
    EpisodeCount = 30,
    CustomTags = 31,
    LatestEpisodeAirDate = 32,

    Year = 34,
    Season = 35,
    AssignedTraktInfo = 36
}

public enum GroupFilterOperator
{
    Include = 1,
    Exclude = 2,
    GreaterThan = 3,
    LessThan = 4,
    Equals = 5,
    NotEquals = 6,
    In = 7,
    NotIn = 8,
    LastXDays = 9,
    InAllEpisodes = 10,
    NotInAllEpisodes = 11
}

public enum GroupFilterSortDirection
{
    Asc = 1,
    Desc = 2
}

public enum GroupFilterSorting
{
    SeriesAddedDate = 1,
    EpisodeAddedDate = 2,
    EpisodeAirDate = 3,
    EpisodeWatchedDate = 4,
    GroupName = 5,
    Year = 6,
    SeriesCount = 7,
    UnwatchedEpisodeCount = 8,
    MissingEpisodeCount = 9,
    UserRating = 10,
    AniDBRating = 11,
    SortName = 12,
    GroupFilterName = 13,
}

[Flags]
public enum GroupFilterType
{
    None = 0,
    UserDefined = 1,
    ContinueWatching = 2,
    All = 4,
    Directory = 8,
    Tag = 16,
    Year = 32,
    Season = 64,
}

public enum CL_ImageEntityType
{
    None = 0, // The lack of a type. Should generally not be used, except as a null/default check
    AniDB_Cover = 1, // use AnimeID
    AniDB_Character = 2, // use CharID
    AniDB_Creator = 3, // use CreatorID
    TvDB_Banner = 4, // use TvDB Banner ID
    TvDB_Cover = 5, // use TvDB Cover ID
    TvDB_Episode = 6, // use TvDB Episode ID
    TvDB_FanArt = 7, // use TvDB FanArt ID
    MovieDB_FanArt = 8,
    MovieDB_Poster = 9,
    Trakt_Poster = 10, // We don't download or load Trakt, but the enum is staying
    Trakt_Fanart = 11, // to allow for deletion
    Trakt_Episode = 12,
    Trakt_Friend = 13,
    Character = 14,
    Staff = 15,
    Static = 16, // This is for things that are served directly from Shoko, such as the 404, error, etc images
    UserAvatar = 17,
}

public enum CL_ImageSizeType
{
    Poster = 1,
    Fanart = 2,
    WideBanner = 3
}

public enum RatingCollectionState
{
    All = 0,
    InMyCollection = 1,
    AllEpisodesInMyCollection = 2,
    NotInMyCollection = 3
}

public enum RatingVotedState
{
    All = 0,
    Voted = 1,
    NotVoted = 2
}

public enum RatingWatchedState
{
    All = 0,
    AllEpisodesWatched = 1,
    NotWatched = 2
}

public enum ScanFileStatus
{
    Waiting = 0,
    ProcessedOK = 1,
    ErrorFileNotFound = 2,
    ErrorInvalidSize = 3,
    ErrorInvalidHash = 4,
    ErrorMissingHash = 5,
    ErrorIOError = 6,
}

public enum ScanStatus
{
    Standby = 0,
    Running = 1,
    Finish = 2,
}

public enum ScheduledUpdateFrequency
{
    [Display(Name = "Never")]
    Never = 1,
    [Display(Name = "Every 6 hours")]
    HoursSix = 2,
    [Display(Name = "Every 12 hours")]
    HoursTwelve = 3,
    [Display(Name = "Every 24 hours")]
    Daily = 4,
    [Display(Name = "Once a week")]
    WeekOne = 5,
    [Display(Name = "Once a month")]
    MonthOne = 6,
}

public enum StatCountType
{
    Watched = 1,
    Played = 2,
    Stopped = 3,
}
