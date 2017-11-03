using System;

namespace Shoko.Models.Enums
{
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

    public enum AutoFileSubsequentType
    {
        PreviousGroup = 0,
        BestQuality = 1
    }

    public enum AutostartMethod
    {
        Registry = 1,
        TaskScheduler = 2
    }

    public enum AvailableEpisodeType
    {
        All = 1,
        Available = 2,
        NoFiles = 3
    }

    public enum DataSourceType
    {
        AniDB = 1,
        TheTvDB = 2
    }

    public enum EpisodeDisplayStyle
    {
        Always = 1,
        InExpanded = 2,
        Never = 3
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
        Season = 35
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
        UserDefined = 1,
        ContinueWatching = 2,
        All = 4,
        Directory = 8,
        Tag = 16,
        Year = 32,
        Season = 64,
    }

    public enum IgnoreAnimeType
    {
        RecWatch = 1,
        RecDownload = 2
    }

    public enum ImageDownloadEventType
    {
        Started = 1,
        Complete = 2
    }

    public enum ImageEntityType
    {
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
    }

    public enum ImageFormatEnum
    {
        bmp,
        jpeg,
        gif,
        tiff,
        png,
        unknown
    }

    public enum ImageSizeType
    {
        Poster = 1,
        Fanart = 2,
        WideBanner = 3
    }

    public enum ImportFolderType
    {
        HDD = 1, // files stored on a "permanent" hard drive
        NAS = 2, // file are stored on a "nas" hard drive
        Cloud = 3, // files stored in the cloud 
        DVD = 4, // files stored on a cd/dvd 
    }

    public enum PlaylistItemType
    {
        Episode = 1,
        AnimeSeries = 2
    }

    public enum PlaylistPlayOrder
    {
        Sequential = 1,
        Random = 2
    }

    public enum RandomObjectType
    {
        Series = 1,
        Episode = 2
    }

    public enum RandomSeriesEpisodeLevel
    {
        All = 1,
        GroupFilter = 2,
        Group = 3,
        Series = 4
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

    public enum RecentAdditionsType
    {
        Episode = 1,
        Series = 2
    }

    public enum RecommendationType
    {
        Watch = 1,
        Download = 2
    }

    public enum RenamingLanguage
    {
        Romaji = 1,
        English = 2
    }

    public enum ScanFileStatus
    {
        Waiting=0,
        ProcessedOK=1,
        ErrorFileNotFound=2,
        ErrorInvalidSize=3,
        ErrorInvalidHash=4,
        ErrorMissingHash=5,
        ErrorIOError=6
    }

    public enum ScanStatus
    {
        Standby=0,
        Running=1,
        Finish=2
    }

    public enum ScheduledUpdateFrequency
    {
        Never = 1,
        HoursSix = 2,
        HoursTwelve = 3,
        Daily = 4,
        WeekOne = 5,
        MonthOne = 6
    }

    public enum SeriesSearchType
    {
        TitleOnly = 0,
        Everything = 1
    }

    public enum SeriesWidgets
    {
        Categories = 1,
        Titles = 2,
        FileSummary = 3,
        TvDBLinks = 4,
        PlayNextEpisode = 5,
        Tags = 6,
        CustomTags = 7
    }

    public enum SortDirection
    {
        Ascending = 1,
        Descending = 2
    }

    public enum StaffRoleType
    {
        Seiyuu,
        Studio,
        Producer,
        Licensor,
        Director,
        Composer,
        OriginalAuthor, // original source author
        Writer, // this can anything involved in writing the show
        CharacterDesign,
        ThemeMusic
    }

    public enum StatCountType
    {
        Watched = 1,
        Played = 2,
        Stopped = 3
    }

    public enum WatchedStatus
    {
        All = 1,
        Unwatched = 2,
        Watched = 3
    }

    public enum WhatPeopleAreSayingType
    {
        TraktComment = 1,
        AniDBRecommendation = 2,
        AniDBMustSee = 3,
        AniDBForFans = 4,
    }
}