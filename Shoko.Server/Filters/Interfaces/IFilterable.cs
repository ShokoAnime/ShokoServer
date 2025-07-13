using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Server;

namespace Shoko.Server.Filters.Interfaces;

public interface IFilterable
{
    /// <summary>
    /// Name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// All Names for the group and series within
    /// </summary>
    IReadOnlySet<string> Names { get; }

    /// <summary>
    /// All AniDB IDs for the series
    /// </summary>
    IReadOnlySet<string> AniDBIDs { get; }

    /// <summary>
    /// Sorting Name
    /// </summary>
    string SortingName { get; }

    /// <summary>
    /// The number of series in a group
    /// </summary>
    int SeriesCount { get; }

    /// <summary>
    /// The number of series in a group with any vote set, be it temporary or
    /// permanent.
    /// </summary>
    int SeriesVoteCount { get; }

    /// <summary>
    /// The number of series in a group with a temporary vote set.
    /// </summary>
    int SeriesTemporaryVoteCount { get; }

    /// <summary>
    /// The number of series in a group with a permanent vote set.
    /// </summary>
    int SeriesPermanentVoteCount { get; }

    /// <summary>
    /// Number of Missing Episodes
    /// </summary>
    int MissingEpisodes { get; }

    /// <summary>
    /// Number of Missing Episodes from Groups that you have
    /// </summary>
    int MissingEpisodesCollecting { get; }

    /// <summary>
    /// Number of video files for the filterable.
    /// </summary>
    int VideoFiles { get; }

    /// <summary>
    /// All of the AniDB tag IDs.
    /// </summary>
    IReadOnlySet<string> AnidbTagIDs { get; }

    /// <summary>
    /// All of the AniDB tags.
    /// </summary>
    IReadOnlySet<string> AnidbTags { get; }

    /// <summary>
    /// All of the custom tag IDs.
    /// </summary>
    IReadOnlySet<string> CustomTagIDs { get; }

    /// <summary>
    /// All of the custom tags
    /// </summary>
    IReadOnlySet<string> CustomTags { get; }

    /// <summary>
    /// The years this aired in
    /// </summary>
    IReadOnlySet<int> Years { get; }

    /// <summary>
    /// The seasons this aired in
    /// </summary>
    IReadOnlySet<(int year, AnimeSeason season)> Seasons { get; }

    /// <summary>
    /// Available image types.
    /// </summary>
    IReadOnlySet<ImageEntityType> AvailableImageTypes { get; }

    /// <summary>
    /// Preferred image types.
    /// </summary>
    IReadOnlySet<ImageEntityType> PreferredImageTypes { get; }

    /// <summary>
    /// Has at least one TMDB Link
    /// </summary>
    bool HasTmdbLink { get; }

    /// <summary>
    /// Has automatic TMDB linking disabled.
    /// </summary>
    bool HasTmdbAutoLinkingDisabled { get; }

    /// <summary>
    /// Number of automatic TMDB episode links.
    /// </summary>
    int AutomaticTmdbEpisodeLinks { get; }

    /// <summary>
    /// Number of user verified TMDB episode links.
    /// </summary>
    int UserVerifiedTmdbEpisodeLinks { get; }

    /// <summary>
    /// Has at least one Trakt Link
    /// </summary>
    bool HasTraktLink { get; }

    /// <summary>
    /// Has automatic Trakt linking disabled.
    /// </summary>
    bool HasTraktAutoLinkingDisabled { get; }

    /// <summary>
    /// Has Finished airing
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// First Air Date
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// Latest Air Date
    /// </summary>
    DateTime? LastAirDate { get; }

    /// <summary>
    /// When it was first added to the collection
    /// </summary>
    DateTime AddedDate { get; }

    /// <summary>
    /// When it was most recently added to the collection
    /// </summary>
    DateTime LastAddedDate { get; }

    /// <summary>
    /// Highest Episode Count
    /// </summary>
    int EpisodeCount { get; }

    /// <summary>
    /// Total Episode Count
    /// </summary>
    int TotalEpisodeCount { get; }

    /// <summary>
    /// Lowest AniDB Rating (0-10)
    /// </summary>
    decimal LowestAniDBRating { get; }

    /// <summary>
    /// Highest AniDB Rating (0-10)
    /// </summary>
    decimal HighestAniDBRating { get; }

    /// <summary>
    /// Average AniDB Rating (0-10)
    /// </summary>
    decimal AverageAniDBRating { get; }

    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc.
    /// </summary>
    IReadOnlySet<string> VideoSources { get; }

    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc. (only sources that are in every file)
    /// </summary>
    IReadOnlySet<string> SharedVideoSources { get; }

    /// <summary>
    /// The anime types (movie, series, ova, etc)
    /// </summary>
    IReadOnlySet<string> AnimeTypes { get; }

    /// <summary>
    /// Audio Languages
    /// </summary>
    IReadOnlySet<string> AudioLanguages { get; }

    /// <summary>
    /// Audio Languages (only languages that are in every file)
    /// </summary>
    IReadOnlySet<string> SharedAudioLanguages { get; }

    /// <summary>
    /// Subtitle Languages
    /// </summary>
    IReadOnlySet<string> SubtitleLanguages { get; }

    /// <summary>
    /// Subtitle Languages (only languages that are in every file)
    /// </summary>
    IReadOnlySet<string> SharedSubtitleLanguages { get; }

    /// <summary>
    /// Resolutions
    /// </summary>
    IReadOnlySet<string> Resolutions { get; }

    /// <summary>
    /// Import Folder IDs
    /// </summary>
    IReadOnlySet<string> ImportFolderIDs { get; }

    /// <summary>
    /// Import Folder Names
    /// </summary>
    IReadOnlySet<string> ImportFolderNames { get; }

    /// <summary>
    /// Relative File Paths
    /// </summary>
    IReadOnlySet<string> FilePaths { get; }

    /// <summary>
    /// Character IDs
    /// </summary>
    IReadOnlySet<string> CharacterIDs { get; }

    /// <summary>
    /// Character Appearance Types
    /// </summary>
    IReadOnlyDictionary<CharacterAppearanceType, IReadOnlySet<string>> CharacterAppearances { get; }

    /// <summary>
    /// Creator IDs
    /// </summary>
    IReadOnlySet<string> CreatorIDs { get; }

    /// <summary>
    /// Creator Roles
    /// </summary>
    IReadOnlyDictionary<CreatorRoleType, IReadOnlySet<string>> CreatorRoles { get; }
}
