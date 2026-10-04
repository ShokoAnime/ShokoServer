using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering;

/// <summary>
///   Filterable information.
/// </summary>
public interface IFilterableInfo
{
    /// <summary>
    /// Preferred name of the filterable.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sort name of the filterable. An altered version of <see cref="Name"/>.
    /// </summary>
    string SortName { get; }

    /// <summary>
    /// Main name of the filterable. Will be the same as <see cref="Name"/> if
    /// not available for the filterable.
    /// </summary>
    string MainName { get; }

    /// <summary>
    /// Original name of the filterable. Will be the same as <see cref="Name"/>
    /// if not available for the filterable.
    /// </summary>
    string OriginalName { get; }

    /// <summary>
    /// All names for the group and series within the filterable.
    /// </summary>
    IReadOnlySet<string> Names { get; }

    /// <summary>
    /// Names filtered to commonly-used languages (English, Japanese, Romaji,
    /// Chinese, Korean, Unknown) plus the server's preferred language order.
    /// Use for fuzzy matching to avoid false positives from obscure-language synonyms.
    /// </summary>
    IReadOnlySet<string> PreferredNames { get; }

    /// <summary>
    /// Description of the filterable.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// All descriptions for the group and series within the filterable.
    /// </summary>
    IReadOnlySet<string> Descriptions { get; }

    /// <summary>
    /// All Shoko IDs for the series in the filterable.
    /// </summary>
    IReadOnlySet<string> SeriesIDs { get; }

    /// <summary>
    /// The group's ID. Will be the closest group's ID. So for groups it will be
    /// the group's ID, for series it will be the parent group's ID.
    /// </summary>
    int GroupID { get; }

    /// <summary>
    /// The top-level parent group's ID.
    /// </summary>
    int TopLevelGroupID { get; }

    /// <summary>
    /// All parent group IDs.
    /// </summary>
    IReadOnlySet<string> GroupIDs { get; }

    /// <summary>
    /// All AniDB IDs for the series in the filterable.
    /// </summary>
    IReadOnlySet<string> AnidbAnimeIDs { get; }

    /// <summary>
    /// The number of series within the filterable. Will always be one for
    /// series. Will never be below one for groups.
    /// </summary>
    int SeriesCount { get; }

    /// <summary>
    /// The number of groups directly within the filterable. Will always be zero
    /// for series.
    /// </summary>
    int GroupCount { get; }

    /// <summary>
    /// The total number of groups within the filterable. Will always be zero
    /// for series.
    /// </summary>
    int TotalGroupCount { get; }

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
    /// The seasons this aired in, by the rule on <see cref="Metadata.Containers.IWithYearlySeasons"/>
    /// </summary>
    IReadOnlySet<(int year, YearlySeason season)> Seasons { get; }

    /// <summary>
    /// Available image types.
    /// </summary>
    IReadOnlySet<ImageEntityType> AvailableImageTypes { get; }

    /// <summary>
    /// Preferred image types.
    /// </summary>
    IReadOnlySet<ImageEntityType> PreferredImageTypes { get; }

    /// <summary>
    /// The sources the filterable is linked to at the series or movie level.
    /// </summary>
    IReadOnlySet<MetadataSource> LinkedSources { get; }

    /// <summary>
    /// The sources the filterable is deliberately linked to nothing on at the
    /// series or movie level, which settles that it has no entry there.
    /// </summary>
    IReadOnlySet<MetadataSource> UnlinkedSources { get; }

    /// <summary>
    /// The sources automatic linking is turned off for.
    /// </summary>
    IReadOnlySet<MetadataSource> AutoLinkingDisabledSources { get; }

    /// <summary>
    /// Number of episode links to a source, films included, that no user
    /// verified. A link made to nothing counts, since it still records a
    /// decision about the episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    int GetAutomaticEpisodeLinks(MetadataSource source);

    /// <summary>
    /// Number of episode links to a source, films included, that a user
    /// verified. A link made to nothing counts, since it still records a
    /// decision about the episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    int GetUserVerifiedEpisodeLinks(MetadataSource source);

    /// <summary>
    /// Number of episodes with no link to an entry on a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    int GetMissingEpisodeLinks(MetadataSource source);

    /// <summary>
    /// Number of series and movie-level links to a source that no user
    /// verified, from the links behind <see cref="LinkedSources"/> and
    /// <see cref="UnlinkedSources"/>. A link made to nothing counts, since it
    /// still records a decision.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    int GetAutomaticLinks(MetadataSource source);

    /// <summary>
    /// Number of series and movie-level links to a source that a user
    /// verified, from the links behind <see cref="LinkedSources"/> and
    /// <see cref="UnlinkedSources"/>. A link made to nothing counts, since it
    /// still records a decision.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    int GetUserVerifiedLinks(MetadataSource source);

    /// <summary>
    /// The genres a source gives the filterable, from the entries it is
    /// linked to there.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">
    /// <see cref="MetadataEntityType.Series"/> or
    /// <see cref="MetadataEntityType.Movie"/> to read only the linked series
    /// or only the linked movies, or <c>null</c> for both.
    /// </param>
    /// <returns>The genre names, compared without regard to case.</returns>
    IReadOnlySet<string> GetGenres(MetadataSource source, MetadataEntityType? entityType = null);

    /// <summary>
    /// The descriptive tags and keywords a source gives the filterable, from
    /// the entries it is linked to there.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">
    /// <see cref="MetadataEntityType.Series"/> or
    /// <see cref="MetadataEntityType.Movie"/> to read only the linked series
    /// or only the linked movies, or <c>null</c> for both.
    /// </param>
    /// <returns>The tag names, compared without regard to case.</returns>
    IReadOnlySet<string> GetTags(MetadataSource source, MetadataEntityType? entityType = null);

    /// <summary>
    /// Number of suggestions the filterable makes through its links to a
    /// source. Counts what it suggests, not what suggests it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    int GetSuggestions(MetadataSource source);

    /// <summary>
    /// Number of suggestions the filterable makes through its AniDB links.
    /// Counts what it suggests, not what suggests it.
    /// </summary>
    int AnidbSuggestions { get; }

    /// <summary>
    /// Number of suggestions the filterable makes across every source.
    /// </summary>
    int TotalSuggestions { get; }

    /// <summary>
    /// Number of suggestions the filterable makes that point at a series in
    /// the collection. Most suggestions point somewhere else, so this is
    /// usually far below <see cref="TotalSuggestions"/>.
    /// </summary>
    int LocalSuggestions { get; }

    /// <summary>
    /// Has Finished airing
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Indicates the filterable, or any series within the filterable, is
    /// restricted.
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// First Air Date
    /// </summary>
    PartialDateOnly? AirDate { get; }

    /// <summary>
    /// Latest Air Date
    /// </summary>
    PartialDateOnly? LastAirDate { get; }

    /// <summary>
    /// When it was first added to the collection
    /// </summary>
    DateTime AddedDate { get; }

    /// <summary>
    /// When it was most recently added to the collection
    /// </summary>
    DateTime? LastAddedDate { get; }

    /// <summary>
    /// Highest Episode Count. Equivalent to <see cref="EpisodeCounts.Episodes"/>.
    /// </summary>
    int EpisodeCount { get; }

    /// <summary>
    /// Total Episode Count. Equivalent to the sum of all <see cref="EpisodeCounts"/> properties.
    /// </summary>
    int TotalEpisodeCount { get; }

    /// <summary>
    /// Number of hidden episodes.
    /// </summary>
    int HiddenEpisodes { get; }

    /// <summary>
    /// Episode counts broken down by type.
    /// </summary>
    EpisodeCounts EpisodeCounts { get; }

    /// <summary>
    /// Local episode counts broken down by type (what is downloaded).
    /// </summary>
    EpisodeCounts LocalEpisodeCounts { get; }

    /// <summary>
    /// Missing episode counts broken down by type (aired but not locally available).
    /// </summary>
    EpisodeCounts MissingEpisodeCounts { get; }

    /// <summary>
    /// Unaired episode counts broken down by type (not yet aired and not locally available).
    /// </summary>
    EpisodeCounts UnairedEpisodeCounts { get; }

    /// <summary>
    /// File source counts.
    /// </summary>
    FileSourceCounts FileSourceCounts { get; }

    /// <summary>
    /// Release provider name to file count mapping.
    /// Provider names are split by '+' before counting.
    /// </summary>
    IReadOnlyDictionary<string, int> ReleaseProviderCounts { get; }

    /// <summary>
    /// Lowest AniDB Rating on a scale of 1-10.
    /// </summary>
    double LowestAniDBRating { get; }

    /// <summary>
    /// Highest AniDB Rating on a scale of 1-10.
    /// </summary>
    double HighestAniDBRating { get; }

    /// <summary>
    /// Average AniDB Rating on a scale of 1-10.
    /// </summary>
    double AverageAniDBRating { get; }

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
    IReadOnlySet<AnimeType> AnimeTypes { get; }

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
    /// Managed Folder IDs
    /// </summary>
    IReadOnlySet<string> ManagedFolderIDs { get; }

    /// <summary>
    /// Managed Folder Names
    /// </summary>
    IReadOnlySet<string> ManagedFolderNames { get; }

    /// <summary>
    /// Relative paths within managed folders for the files in the filterable.
    /// </summary>
    IReadOnlySet<string> FilePaths { get; }

    /// <summary>
    /// Absolute paths of the files for the filterable.
    /// </summary>
    IReadOnlySet<string> AbsoluteFilePaths { get; }

    /// <summary>
    /// Absolute paths of the folders containing the files for the filterable.
    /// </summary>
    IReadOnlySet<string> ContainingFolderPaths { get; }

    /// <summary>
    /// Character IDs
    /// </summary>
    IReadOnlySet<string> CharacterIDs { get; }

    /// <summary>
    /// Character Appearance Types
    /// </summary>
    IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances { get; }

    /// <summary>
    /// Creator IDs
    /// </summary>
    IReadOnlySet<string> CreatorIDs { get; }

    /// <summary>
    /// Creator Roles
    /// </summary>
    IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles { get; }

    /// <summary>
    /// Release Group Names
    /// </summary>
    IReadOnlySet<string> ReleaseGroupNames { get; }

    /// <summary>
    /// Release Provider Names
    /// </summary>
    IReadOnlySet<string> ReleaseProviderNames { get; }
}
