using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Reads one source's links out of an entry's metadata cross-references,
///   and one source's entries out of a Shoko entry's linked entries.
/// </summary>
public static class MetadataCrossReferenceExtensions
{
    #region Series

    /// <summary>
    ///   The series-level links of one source in the series's
    ///   <see cref="ISeries.MetadataSeriesCrossReferences"/>, in the order the series lists them.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesCrossReferences(this ISeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return FromSource(series.MetadataSeriesCrossReferences, source);
    }

    /// <summary>
    ///   The series-level links of one source in the series's
    ///   <see cref="ISeries.MetadataSeriesCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the series lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeriesCrossReference<TProvider>> GetSeriesCrossReferences<TProvider>(this ISeries series, MetadataSource source)
        where TProvider : ISeries
        => [.. series.GetSeriesCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The season-level links of one source in the series's
    ///   <see cref="ISeries.MetadataSeasonCrossReferences"/>, in the order the series lists them.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeasonCrossReference> GetSeasonCrossReferences(this ISeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return FromSource(series.MetadataSeasonCrossReferences, source);
    }

    /// <summary>
    ///   The season-level links of one source in the series's
    ///   <see cref="ISeries.MetadataSeasonCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the series lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeasonCrossReference<TProvider>> GetSeasonCrossReferences<TProvider>(this ISeries series, MetadataSource source)
        where TProvider : ISeason
        => [.. series.GetSeasonCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The episode-level links of one source in the series's
    ///   <see cref="ISeries.MetadataEpisodeCrossReferences"/>, in the order the series lists them.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferences(this ISeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return FromSource(series.MetadataEpisodeCrossReferences, source);
    }

    /// <summary>
    ///   The episode-level links of one source in the series's
    ///   <see cref="ISeries.MetadataEpisodeCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the series lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference<TProvider>> GetEpisodeCrossReferences<TProvider>(this ISeries series, MetadataSource source)
        where TProvider : IEpisode
        => [.. series.GetEpisodeCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The film links of one source in the series's
    ///   <see cref="ISeries.MetadataMovieCrossReferences"/>, in the order the series lists them.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(this ISeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return FromSource(series.MetadataMovieCrossReferences, source);
    }

    /// <summary>
    ///   The film links of one source in the series's
    ///   <see cref="ISeries.MetadataMovieCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the series lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="series">The series.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference<TProvider>> GetMovieCrossReferences<TProvider>(this ISeries series, MetadataSource source)
        where TProvider : IMovie
        => [.. series.GetMovieCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    #endregion

    #region Season

    /// <summary>
    ///   The season-level links of one source in the season's
    ///   <see cref="ISeason.MetadataSeasonCrossReferences"/>, in the order the season lists them.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeasonCrossReference> GetSeasonCrossReferences(this ISeason season, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(season);
        return FromSource(season.MetadataSeasonCrossReferences, source);
    }

    /// <summary>
    ///   The season-level links of one source in the season's
    ///   <see cref="ISeason.MetadataSeasonCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the season lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="season">The season.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeasonCrossReference<TProvider>> GetSeasonCrossReferences<TProvider>(this ISeason season, MetadataSource source)
        where TProvider : ISeason
        => [.. season.GetSeasonCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The episode-level links of one source in the season's
    ///   <see cref="ISeason.MetadataEpisodeCrossReferences"/>, in the order the season lists them.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferences(this ISeason season, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(season);
        return FromSource(season.MetadataEpisodeCrossReferences, source);
    }

    /// <summary>
    ///   The episode-level links of one source in the season's
    ///   <see cref="ISeason.MetadataEpisodeCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the season lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="season">The season.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference<TProvider>> GetEpisodeCrossReferences<TProvider>(this ISeason season, MetadataSource source)
        where TProvider : IEpisode
        => [.. season.GetEpisodeCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The film links of one source in the season's
    ///   <see cref="ISeason.MetadataMovieCrossReferences"/>, in the order the season lists them.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(this ISeason season, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(season);
        return FromSource(season.MetadataMovieCrossReferences, source);
    }

    /// <summary>
    ///   The film links of one source in the season's
    ///   <see cref="ISeason.MetadataMovieCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the season lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="season">The season.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference<TProvider>> GetMovieCrossReferences<TProvider>(this ISeason season, MetadataSource source)
        where TProvider : IMovie
        => [.. season.GetMovieCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    #endregion

    #region Episode

    /// <summary>
    ///   The episode-level links of one source in the episode's
    ///   <see cref="IEpisode.MetadataEpisodeCrossReferences"/>, in the order the episode lists them.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferences(this IEpisode episode, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return FromSource(episode.MetadataEpisodeCrossReferences, source);
    }

    /// <summary>
    ///   The episode-level links of one source in the episode's
    ///   <see cref="IEpisode.MetadataEpisodeCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the episode lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference<TProvider>> GetEpisodeCrossReferences<TProvider>(this IEpisode episode, MetadataSource source)
        where TProvider : IEpisode
        => [.. episode.GetEpisodeCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The series-level links of one source in the episode's
    ///   <see cref="IEpisode.MetadataSeriesCrossReferences"/>, in the order the episode lists them.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesCrossReferences(this IEpisode episode, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return FromSource(episode.MetadataSeriesCrossReferences, source);
    }

    /// <summary>
    ///   The series-level links of one source in the episode's
    ///   <see cref="IEpisode.MetadataSeriesCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the episode lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeriesCrossReference<TProvider>> GetSeriesCrossReferences<TProvider>(this IEpisode episode, MetadataSource source)
        where TProvider : ISeries
        => [.. episode.GetSeriesCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The film links of one source in the episode's
    ///   <see cref="IEpisode.MetadataMovieCrossReferences"/>, in the order the episode lists them.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(this IEpisode episode, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return FromSource(episode.MetadataMovieCrossReferences, source);
    }

    /// <summary>
    ///   The film links of one source in the episode's
    ///   <see cref="IEpisode.MetadataMovieCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the episode lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference<TProvider>> GetMovieCrossReferences<TProvider>(this IEpisode episode, MetadataSource source)
        where TProvider : IMovie
        => [.. episode.GetMovieCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    #endregion

    #region Movie

    /// <summary>
    ///   The film links of one source in the movie's
    ///   <see cref="IMovie.MetadataMovieCrossReferences"/>, in the order the movie lists them.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="movie"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(this IMovie movie, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(movie);
        return FromSource(movie.MetadataMovieCrossReferences, source);
    }

    /// <summary>
    ///   The film links of one source in the movie's
    ///   <see cref="IMovie.MetadataMovieCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the movie lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="movie">The movie.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="movie"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference<TProvider>> GetMovieCrossReferences<TProvider>(this IMovie movie, MetadataSource source)
        where TProvider : IMovie
        => [.. movie.GetMovieCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    #endregion

    #region Video Cross-References

    /// <summary>
    ///   The series-level links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataSeriesCrossReferences"/>, in the order the file link lists them.
    /// </summary>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesCrossReferences(this IVideoCrossReference videoCrossReference, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(videoCrossReference);
        return FromSource(videoCrossReference.MetadataSeriesCrossReferences, source);
    }

    /// <summary>
    ///   The series-level links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataSeriesCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the file link lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeriesCrossReference<TProvider>> GetSeriesCrossReferences<TProvider>(
        this IVideoCrossReference videoCrossReference,
        MetadataSource source
    )
        where TProvider : ISeries
        => [.. videoCrossReference.GetSeriesCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The season-level links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataSeasonCrossReferences"/>, in the order the file link lists them.
    /// </summary>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeasonCrossReference> GetSeasonCrossReferences(this IVideoCrossReference videoCrossReference, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(videoCrossReference);
        return FromSource(videoCrossReference.MetadataSeasonCrossReferences, source);
    }

    /// <summary>
    ///   The season-level links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataSeasonCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the file link lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataSeasonCrossReference<TProvider>> GetSeasonCrossReferences<TProvider>(
        this IVideoCrossReference videoCrossReference,
        MetadataSource source
    )
        where TProvider : ISeason
        => [.. videoCrossReference.GetSeasonCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The episode-level links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataEpisodeCrossReferences"/>, in the order the file link lists them.
    /// </summary>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferences(this IVideoCrossReference videoCrossReference, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(videoCrossReference);
        return FromSource(videoCrossReference.MetadataEpisodeCrossReferences, source);
    }

    /// <summary>
    ///   The episode-level links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataEpisodeCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the file link lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataEpisodeCrossReference<TProvider>> GetEpisodeCrossReferences<TProvider>(
        this IVideoCrossReference videoCrossReference,
        MetadataSource source
    )
        where TProvider : IEpisode
        => [.. videoCrossReference.GetEpisodeCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    /// <summary>
    ///   The film links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataMovieCrossReferences"/>, in the order the file link lists them.
    /// </summary>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(this IVideoCrossReference videoCrossReference, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(videoCrossReference);
        return FromSource(videoCrossReference.MetadataMovieCrossReferences, source);
    }

    /// <summary>
    ///   The film links of one source in the file link's
    ///   <see cref="IVideoCrossReference.MetadataMovieCrossReferences"/> whose provider entry is a
    ///   <typeparamref name="TProvider"/>, in the order the file link lists them.
    ///   Resolves each link's provider entry to tell.
    /// </summary>
    /// <typeparam name="TProvider">The type the provider entry must have.</typeparam>
    /// <param name="videoCrossReference">The file link.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The typed links of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="videoCrossReference"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMetadataMovieCrossReference<TProvider>> GetMovieCrossReferences<TProvider>(
        this IVideoCrossReference videoCrossReference,
        MetadataSource source
    )
        where TProvider : IMovie
        => [.. videoCrossReference.GetMovieCrossReferences(source).Select(Typed<TProvider>).Where(xref => xref.Provider is TProvider)];

    #endregion

    #region Shoko Series Linked Entries

    /// <summary>
    ///   The series of one source in the Shoko series's
    ///   <see cref="IShokoSeries.LinkedSeries"/>, in the order the Shoko series lists them.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source whose series to keep.</param>
    /// <returns>The series of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<ISeries> GetLinkedSeries(this IShokoSeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return EntriesFromSource(series.LinkedSeries, source);
    }

    /// <summary>
    ///   The series of one source in the Shoko series's
    ///   <see cref="IShokoSeries.LinkedSeries"/> that are a
    ///   <typeparamref name="TSeries"/>, in the order the Shoko series lists them.
    /// </summary>
    /// <typeparam name="TSeries">The type the entries must have.</typeparam>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source whose series to keep.</param>
    /// <returns>The typed series of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TSeries> GetLinkedSeries<TSeries>(this IShokoSeries series, MetadataSource source) where TSeries : ISeries
        => [.. series.GetLinkedSeries(source).OfType<TSeries>()];

    /// <summary>
    ///   The seasons of one source in the Shoko series's
    ///   <see cref="IShokoSeries.LinkedSeasons"/>, in the order the Shoko series lists them.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source whose seasons to keep.</param>
    /// <returns>The seasons of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<ISeason> GetLinkedSeasons(this IShokoSeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return EntriesFromSource(series.LinkedSeasons, source);
    }

    /// <summary>
    ///   The seasons of one source in the Shoko series's
    ///   <see cref="IShokoSeries.LinkedSeasons"/> that are a
    ///   <typeparamref name="TSeason"/>, in the order the Shoko series lists them.
    /// </summary>
    /// <typeparam name="TSeason">The type the entries must have.</typeparam>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source whose seasons to keep.</param>
    /// <returns>The typed seasons of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TSeason> GetLinkedSeasons<TSeason>(this IShokoSeries series, MetadataSource source) where TSeason : ISeason
        => [.. series.GetLinkedSeasons(source).OfType<TSeason>()];

    /// <summary>
    ///   The movies of one source in the Shoko series's
    ///   <see cref="IShokoSeries.LinkedMovies"/>, in the order the Shoko series lists them.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source whose movies to keep.</param>
    /// <returns>The movies of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMovie> GetLinkedMovies(this IShokoSeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);
        return EntriesFromSource(series.LinkedMovies, source);
    }

    /// <summary>
    ///   The movies of one source in the Shoko series's
    ///   <see cref="IShokoSeries.LinkedMovies"/> that are a
    ///   <typeparamref name="TMovie"/>, in the order the Shoko series lists them.
    /// </summary>
    /// <typeparam name="TMovie">The type the entries must have.</typeparam>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source whose movies to keep.</param>
    /// <returns>The typed movies of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TMovie> GetLinkedMovies<TMovie>(this IShokoSeries series, MetadataSource source) where TMovie : IMovie
        => [.. series.GetLinkedMovies(source).OfType<TMovie>()];

    #endregion

    #region Shoko Season Linked Entries

    /// <summary>
    ///   The seasons of one source in the Shoko season's
    ///   <see cref="IShokoSeason.LinkedSeasons"/>, in the order the Shoko season lists them.
    /// </summary>
    /// <param name="season">The Shoko season.</param>
    /// <param name="source">The source whose seasons to keep.</param>
    /// <returns>The seasons of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<ISeason> GetLinkedSeasons(this IShokoSeason season, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(season);
        return EntriesFromSource(season.LinkedSeasons, source);
    }

    /// <summary>
    ///   The seasons of one source in the Shoko season's
    ///   <see cref="IShokoSeason.LinkedSeasons"/> that are a
    ///   <typeparamref name="TSeason"/>, in the order the Shoko season lists them.
    /// </summary>
    /// <typeparam name="TSeason">The type the entries must have.</typeparam>
    /// <param name="season">The Shoko season.</param>
    /// <param name="source">The source whose seasons to keep.</param>
    /// <returns>The typed seasons of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TSeason> GetLinkedSeasons<TSeason>(this IShokoSeason season, MetadataSource source) where TSeason : ISeason
        => [.. season.GetLinkedSeasons(source).OfType<TSeason>()];

    /// <summary>
    ///   The movies of one source in the Shoko season's
    ///   <see cref="IShokoSeason.LinkedMovies"/>, in the order the Shoko season lists them.
    /// </summary>
    /// <param name="season">The Shoko season.</param>
    /// <param name="source">The source whose movies to keep.</param>
    /// <returns>The movies of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMovie> GetLinkedMovies(this IShokoSeason season, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(season);
        return EntriesFromSource(season.LinkedMovies, source);
    }

    /// <summary>
    ///   The movies of one source in the Shoko season's
    ///   <see cref="IShokoSeason.LinkedMovies"/> that are a
    ///   <typeparamref name="TMovie"/>, in the order the Shoko season lists them.
    /// </summary>
    /// <typeparam name="TMovie">The type the entries must have.</typeparam>
    /// <param name="season">The Shoko season.</param>
    /// <param name="source">The source whose movies to keep.</param>
    /// <returns>The typed movies of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TMovie> GetLinkedMovies<TMovie>(this IShokoSeason season, MetadataSource source) where TMovie : IMovie
        => [.. season.GetLinkedMovies(source).OfType<TMovie>()];

    #endregion

    #region Shoko Episode Linked Entries

    /// <summary>
    ///   The episodes of one source in the Shoko episode's
    ///   <see cref="IShokoEpisode.LinkedEpisodes"/>, in the order the Shoko episode lists them.
    /// </summary>
    /// <param name="episode">The Shoko episode.</param>
    /// <param name="source">The source whose episodes to keep.</param>
    /// <returns>The episodes of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IEpisode> GetLinkedEpisodes(this IShokoEpisode episode, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return EntriesFromSource(episode.LinkedEpisodes, source);
    }

    /// <summary>
    ///   The episodes of one source in the Shoko episode's
    ///   <see cref="IShokoEpisode.LinkedEpisodes"/> that are a
    ///   <typeparamref name="TEpisode"/>, in the order the Shoko episode lists them.
    /// </summary>
    /// <typeparam name="TEpisode">The type the entries must have.</typeparam>
    /// <param name="episode">The Shoko episode.</param>
    /// <param name="source">The source whose episodes to keep.</param>
    /// <returns>The typed episodes of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TEpisode> GetLinkedEpisodes<TEpisode>(this IShokoEpisode episode, MetadataSource source) where TEpisode : IEpisode
        => [.. episode.GetLinkedEpisodes(source).OfType<TEpisode>()];

    /// <summary>
    ///   The movies of one source in the Shoko episode's
    ///   <see cref="IShokoEpisode.LinkedMovies"/>, in the order the Shoko episode lists them.
    /// </summary>
    /// <param name="episode">The Shoko episode.</param>
    /// <param name="source">The source whose movies to keep.</param>
    /// <returns>The movies of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IMovie> GetLinkedMovies(this IShokoEpisode episode, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return EntriesFromSource(episode.LinkedMovies, source);
    }

    /// <summary>
    ///   The movies of one source in the Shoko episode's
    ///   <see cref="IShokoEpisode.LinkedMovies"/> that are a
    ///   <typeparamref name="TMovie"/>, in the order the Shoko episode lists them.
    /// </summary>
    /// <typeparam name="TMovie">The type the entries must have.</typeparam>
    /// <param name="episode">The Shoko episode.</param>
    /// <param name="source">The source whose movies to keep.</param>
    /// <returns>The typed movies of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TMovie> GetLinkedMovies<TMovie>(this IShokoEpisode episode, MetadataSource source) where TMovie : IMovie
        => [.. episode.GetLinkedMovies(source).OfType<TMovie>()];

    #endregion

    #region Helpers

    /// <summary>
    ///   Keeps the links of one source.
    /// </summary>
    /// <typeparam name="TCrossReference">The level of the links.</typeparam>
    /// <param name="links">The links.</param>
    /// <param name="source">The source whose links to keep.</param>
    /// <returns>The links of that source, in order.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    private static List<TCrossReference> FromSource<TCrossReference>(IReadOnlyList<TCrossReference> links, MetadataSource source)
        where TCrossReference : IMetadataCrossReference
    {
        ArgumentNullException.ThrowIfNull(source);
        return [.. links.Where(link => link.Source == source)];
    }

    /// <summary>
    ///   Keeps the entries of one source.
    /// </summary>
    /// <typeparam name="TEntry">The kind of the entries.</typeparam>
    /// <param name="entries">The entries.</param>
    /// <param name="source">The source whose entries to keep.</param>
    /// <returns>The entries of that source, in order.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    private static List<TEntry> EntriesFromSource<TEntry>(IReadOnlyList<TEntry> entries, MetadataSource source) where TEntry : IMetadata
    {
        ArgumentNullException.ThrowIfNull(source);
        return [.. entries.Where(entry => entry.ID.Source == source)];
    }

    /// <summary>
    ///   The series-level link with its provider entry typed: the link
    ///   itself when it already is, or else the link behind an adapter.
    /// </summary>
    /// <typeparam name="TProvider">The provider entry's type.</typeparam>
    /// <param name="link">The link.</param>
    /// <returns>The typed link.</returns>
    private static IMetadataSeriesCrossReference<TProvider> Typed<TProvider>(IMetadataSeriesCrossReference link) where TProvider : ISeries
        => link as IMetadataSeriesCrossReference<TProvider> ?? new MetadataSeriesCrossReferenceAdapter<TProvider>(link);

    /// <summary>
    ///   The season-level link with its provider entry typed: the link
    ///   itself when it already is, or else the link behind an adapter.
    /// </summary>
    /// <typeparam name="TProvider">The provider entry's type.</typeparam>
    /// <param name="link">The link.</param>
    /// <returns>The typed link.</returns>
    private static IMetadataSeasonCrossReference<TProvider> Typed<TProvider>(IMetadataSeasonCrossReference link) where TProvider : ISeason
        => link as IMetadataSeasonCrossReference<TProvider> ?? new MetadataSeasonCrossReferenceAdapter<TProvider>(link);

    /// <summary>
    ///   The episode-level link with its provider entry typed: the link
    ///   itself when it already is, or else the link behind an adapter.
    /// </summary>
    /// <typeparam name="TProvider">The provider entry's type.</typeparam>
    /// <param name="link">The link.</param>
    /// <returns>The typed link.</returns>
    private static IMetadataEpisodeCrossReference<TProvider> Typed<TProvider>(IMetadataEpisodeCrossReference link) where TProvider : IEpisode
        => link as IMetadataEpisodeCrossReference<TProvider> ?? new MetadataEpisodeCrossReferenceAdapter<TProvider>(link);

    /// <summary>
    ///   The film link with its provider entry typed: the link
    ///   itself when it already is, or else the link behind an adapter.
    /// </summary>
    /// <typeparam name="TProvider">The provider entry's type.</typeparam>
    /// <param name="link">The link.</param>
    /// <returns>The typed link.</returns>
    private static IMetadataMovieCrossReference<TProvider> Typed<TProvider>(IMetadataMovieCrossReference link) where TProvider : IMovie
        => link as IMetadataMovieCrossReference<TProvider> ?? new MetadataMovieCrossReferenceAdapter<TProvider>(link);

    #endregion
}
