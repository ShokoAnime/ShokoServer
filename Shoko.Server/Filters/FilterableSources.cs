using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters;

/// <summary>
/// What a series has from each metadata source, behind the source-agnostic
/// filter members, shared by the series and the group filterable.
/// </summary>
/// <remarks>
/// Links are read from the cached cross-reference tables, and tags and genres
/// off the linked entries as <see cref="ITag"/>s, so every source is read the
/// same way. TMDB's entries are taken straight from its own tables, which is
/// cheaper than resolving them through the metadata service.
/// </remarks>
internal static class FilterableSources
{
    #region Links

    /// <summary>
    /// The sources a series is linked to at the series or movie level.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The sources.</returns>
    public static IEnumerable<MetadataSource> LinkedSources(AnimeSeries series)
        => Linked(RepoFactory.CrossRef_AniDB_Metadata_Series.GetByAnidbAnimeID(series.AniDB_ID))
            .Concat<CrossRef_AniDB_Metadata>(Linked(RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByAnidbAnimeID(series.AniDB_ID)))
            .Select(xref => xref.Source)
            .Distinct();

    /// <summary>
    /// The sources a series is deliberately linked to nothing on, at the
    /// series or movie level.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The sources.</returns>
    public static IEnumerable<MetadataSource> UnlinkedSources(AnimeSeries series)
        => RepoFactory.CrossRef_AniDB_Metadata_Series.GetByAnidbAnimeID(series.AniDB_ID)
            .Concat<CrossRef_AniDB_Metadata>(RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByAnidbAnimeID(series.AniDB_ID))
            .Where(xref => xref.IsUnlinked)
            .Select(xref => xref.Source)
            .Distinct();

    /// <summary>
    /// The sources automatic linking is turned off for on a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The sources.</returns>
    public static IEnumerable<MetadataSource> AutoLinkingDisabledSources(AnimeSeries series)
        => series.DisabledAutoMatchKeys.Select(key => MetadataSource.TryParse(key, out var source) ? source : null).OfType<MetadataSource>().Distinct();

    /// <summary>
    /// The number of a series' series and movie-level links to a source that
    /// were or were not verified by a user. A link made to nothing counts, as
    /// it does for the episode links.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <param name="userVerified">Whether to count the verified links or the automatic ones.</param>
    /// <returns>The count.</returns>
    public static int Links(AnimeSeries series, MetadataSource source, bool userVerified)
        => RepoFactory.CrossRef_AniDB_Metadata_Series.GetByAnidbAnimeID(series.AniDB_ID, source)
            .Concat<CrossRef_AniDB_Metadata>(RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByAnidbAnimeID(series.AniDB_ID, source))
            .Count(xref => xref.MatchRating is MatchRating.UserVerified == userVerified);

    /// <summary>
    /// The number of a series' episode links to a source, films included,
    /// that were or were not verified by a user. A link made to nothing
    /// counts, as TMDB always counted it.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <param name="userVerified">Whether to count the verified links or the automatic ones.</param>
    /// <returns>The count.</returns>
    public static int EpisodeLinks(AnimeSeries series, MetadataSource source, bool userVerified)
        => EpisodeLevelRows(series, source).Count(xref => xref.MatchRating is MatchRating.UserVerified == userVerified);

    /// <summary>
    /// The number of a series' episodes with no link to a source.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    public static int MissingEpisodeLinks(AnimeSeries series, MetadataSource source)
    {
        var linked = Linked(EpisodeLevelRows(series, source))
            .Select(xref => xref is CrossRef_AniDB_Metadata_Episode episode ? episode.AnidbEpisodeID : ((CrossRef_AniDB_Metadata_Movie)xref).AnidbEpisodeID)
            .ToHashSet();
        return series.AnimeEpisodes.Count(episode => !linked.Contains(episode.AniDB_EpisodeID));
    }

    private static IEnumerable<CrossRef_AniDB_Metadata> EpisodeLevelRows(AnimeSeries series, MetadataSource source)
        => RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByAnidbAnimeID(series.AniDB_ID, source)
            .Concat<CrossRef_AniDB_Metadata>(RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByAnidbAnimeID(series.AniDB_ID, source));

    private static IEnumerable<T> Linked<T>(IEnumerable<T> links) where T : CrossRef_AniDB_Metadata
        => links.Where(xref => !xref.IsUnlinked);

    #endregion

    #region Tags

    /// <summary>
    /// The genres a source gives a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <param name="entityType">Only the linked series or only the linked movies, or <c>null</c> for both.</param>
    /// <returns>The genre names.</returns>
    public static IEnumerable<string> Genres(AnimeSeries series, MetadataSource source, MetadataEntityType? entityType = null)
        => TagNames(series, source, entityType, TagKind.Genre);

    /// <summary>
    /// The descriptive tags and keywords a source gives a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <param name="entityType">Only the linked series or only the linked movies, or <c>null</c> for both.</param>
    /// <returns>The tag names.</returns>
    public static IEnumerable<string> Tags(AnimeSeries series, MetadataSource source, MetadataEntityType? entityType = null)
        => TagNames(series, source, entityType, TagKind.Tag, TagKind.Keyword);

    private static IEnumerable<string> TagNames(AnimeSeries series, MetadataSource source, MetadataEntityType? entityType, params TagKind[] kinds)
        => TaggedEntries(series, source, entityType)
            .SelectMany(entry => entry.Tags)
            .Where(tag => kinds.Contains(tag.Kind))
            .Select(tag => tag.Name);

    /// <summary>
    /// The entries a series is linked to on a source that carry tags: AniDB's
    /// anime, TMDB's shows and movies read from TMDB's tables, and any other
    /// source's series and movies resolved only when a link exists.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <param name="entityType">Only the linked series or only the linked movies, or <c>null</c> for both.</param>
    /// <returns>The entries.</returns>
    private static IEnumerable<IWithTags> TaggedEntries(AnimeSeries series, MetadataSource source, MetadataEntityType? entityType)
    {
        var withSeries = entityType is null || entityType == MetadataEntityType.Series;
        var withMovies = entityType is null || entityType == MetadataEntityType.Movie;
        if (source == MetadataSource.AniDB)
            return withSeries && series.AniDB_Anime is ISeries anime ? [anime] : [];

        if (source == MetadataSource.TMDB)
            return [.. withSeries ? series.TmdbShows : [], .. withMovies ? series.TmdbMovies : []];

        if (!LinkedSources(series).Contains(source))
            return [];

        return [
            .. withSeries ? series.LinkedSeries.Where(entry => entry.Source == source) : [],
            .. withMovies ? series.LinkedMovies.Where(entry => entry.Source == source) : [],
        ];
    }

    #endregion

    #region Suggestions

    /// <summary>
    /// The series a series is linked to on the sources the suggestion counts
    /// do not read from their own tables: everything but AniDB and TMDB.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The linked entries.</returns>
    public static IEnumerable<ISeries> OtherLinkedSeries(AnimeSeries series)
        => LinkedSources(series)
            .Where(source => source != MetadataSource.AniDB && source != MetadataSource.TMDB)
            .SelectMany(source => LinkedEntries(series, source));

    /// <summary>
    /// The series a series is linked to on one source, resolved only when a
    /// link exists, since resolving reads the core's metadata stores.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <returns>The linked entries.</returns>
    private static IEnumerable<ISeries> LinkedEntries(AnimeSeries series, MetadataSource source)
        => LinkedSources(series).Contains(source)
            ? series.LinkedSeries.Where(entry => entry.Source == source)
            : [];

    #endregion

    #region Helpers

    /// <summary>
    /// A set compared without regard to case, as the filters expect.
    /// </summary>
    /// <param name="names">The names.</param>
    /// <returns>The set.</returns>
    public static IReadOnlySet<string> ToNameSet(this IEnumerable<string> names)
        => names.ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    #endregion
}
