using System;
using System.Globalization;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Looks entries up by a source's numeric ID, for the sources whose IDs are
///   whole numbers, through the <see cref="MetadataGuid"/> lookups of
///   <see cref="IMetadataService"/>.
/// </summary>
public static class MetadataServiceExtensions
{
    #region Lookup

    /// <summary>
    ///   Looks up any entry by its source, kind and numeric ID, as
    ///   <see cref="IMetadataService.GetEntry(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.AniDB"/>.</param>
    /// <param name="entityType">The kind of entry, e.g. <see cref="MetadataEntityType.Series"/>.</param>
    /// <param name="id">The ID the source gave the entry.</param>
    /// <returns>The entry, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/>, <paramref name="source"/> or
    ///   <paramref name="entityType"/> is <see langword="null"/>.
    /// </exception>
    public static IMetadata? GetEntry(this IMetadataService service, MetadataSource source, MetadataEntityType entityType, int id)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetEntry(ToGuid(source, entityType, id));
    }

    /// <summary>
    ///   Looks up an entry by its source, kind and numeric ID, as
    ///   <see cref="IMetadataService.GetEntry{TMetadata}(MetadataGuid)"/>
    ///   does, when it is a <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <typeparam name="TMetadata">The type wanted, e.g. <see cref="ISeries"/>.</typeparam>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.TMDB"/>.</param>
    /// <param name="entityType">The kind of entry, e.g. <see cref="MetadataEntityType.Studio"/>.</param>
    /// <param name="id">The ID the source gave the entry.</param>
    /// <returns>
    ///   The entry, or <see langword="null"/> when nothing holds it or it is
    ///   not a <typeparamref name="TMetadata"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/>, <paramref name="source"/> or
    ///   <paramref name="entityType"/> is <see langword="null"/>.
    /// </exception>
    public static TMetadata? GetEntry<TMetadata>(this IMetadataService service, MetadataSource source, MetadataEntityType entityType, int id)
        where TMetadata : class, IMetadata
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetEntry<TMetadata>(ToGuid(source, entityType, id));
    }

    /// <summary>
    ///   Looks up a series by its source and numeric ID, as
    ///   <see cref="IMetadataService.GetSeries(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.AniDB"/>.</param>
    /// <param name="id">The ID the source gave the series.</param>
    /// <returns>The series, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static ISeries? GetSeries(this IMetadataService service, MetadataSource source, int id)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetSeries(ToGuid(source, MetadataEntityType.Series, id));
    }

    /// <summary>
    ///   Looks up a season by its source and numeric ID, as
    ///   <see cref="IMetadataService.GetSeason(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.TMDB"/>.</param>
    /// <param name="id">The ID the source gave the season.</param>
    /// <returns>The season, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static ISeason? GetSeason(this IMetadataService service, MetadataSource source, int id)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetSeason(ToGuid(source, MetadataEntityType.Season, id));
    }

    /// <summary>
    ///   Looks up an episode by its source and numeric ID, as
    ///   <see cref="IMetadataService.GetEpisode(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.AniDB"/>.</param>
    /// <param name="id">The ID the source gave the episode.</param>
    /// <returns>The episode, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IEpisode? GetEpisode(this IMetadataService service, MetadataSource source, int id)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetEpisode(ToGuid(source, MetadataEntityType.Episode, id));
    }

    /// <summary>
    ///   Looks up a movie by its source and numeric ID, as
    ///   <see cref="IMetadataService.GetMovie(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.TMDB"/>.</param>
    /// <param name="id">The ID the source gave the movie.</param>
    /// <returns>The movie, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IMovie? GetMovie(this IMetadataService service, MetadataSource source, int id)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetMovie(ToGuid(source, MetadataEntityType.Movie, id));
    }

    /// <summary>
    ///   Looks up a collection by its source and numeric ID, as
    ///   <see cref="IMetadataService.GetCollection(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="service">The metadata service.</param>
    /// <param name="source">The source, e.g. <see cref="MetadataSource.TMDB"/>.</param>
    /// <param name="id">The ID the source gave the collection.</param>
    /// <returns>The collection, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="service"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static ICollection? GetCollection(this IMetadataService service, MetadataSource source, int id)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.GetCollection(ToGuid(source, MetadataEntityType.Collection, id));
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Names an entry by its numeric ID, written the invariant way.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <param name="id">The ID the source gave the entry.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="source"/> or <paramref name="entityType"/> is
    ///   <see langword="null"/>.
    /// </exception>
    private static MetadataGuid ToGuid(MetadataSource source, MetadataEntityType entityType, int id)
        => new(source, entityType, id.ToString(CultureInfo.InvariantCulture));

    #endregion
}
