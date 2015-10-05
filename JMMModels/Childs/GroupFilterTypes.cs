namespace JMMModels.Childs
{
    public enum GroupFilterConditionType
    {
        CompletedSeries = 1,
        MissingEpisodes = 2,
        HasUnwatchedEpisodes = 3,
        AllEpisodesWatched = 4,
        UserVoted = 5,
        Category = 6,
        AirDate = 7,
        Studio = 8,
        AssignedTvDBInfo = 9,
        ReleaseGroup = 11,
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
        CustomTags = 31
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
        SortName = 12
    }

    public enum GroupFilterSortDirection
    {
        Asc = 1,
        Desc = 2
    }

    public enum GroupFilterBaseCondition
    {
        Include = 1,
        Exclude = 2
    }

}
