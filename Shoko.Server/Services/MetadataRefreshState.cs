using System;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.TMDB;

namespace Shoko.Server.Services;

/// <summary>
///   When each provider's series, movies and collections were last
///   refreshed, which the refresh job reads to skip an entry that is still
///   fresh.
/// </summary>
public interface IMetadataRefreshState
{
    /// <summary>
    ///   When an entry was last refreshed without failing.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <returns>The time, or <see langword="null"/> when it never was.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    DateTime? GetLastRefreshedAt(MetadataGuid entry);

    /// <summary>
    ///   Record that an entry was refreshed without failing.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <param name="refreshedAt">When the refresh finished.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    void RecordRefresh(MetadataGuid entry, DateTime refreshedAt);

    /// <summary>
    ///   Forget when an entry was refreshed, such as once it is purged.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <returns>Whether there was anything to forget.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    bool Forget(MetadataGuid entry);
}

/// <summary>
///   Keeps when each plugin source's entries were last refreshed in the
///   <c>Metadata_Refresh</c> table, and reads TMDB's from its own tables.
/// </summary>
/// <remarks>
///   A TMDB entry was last refreshed when its row was last updated, which is
///   when a refresh last found something new; one never updated since its
///   creation counts as never refreshed. Nothing is recorded or forgotten for
///   TMDB, whose provider keeps those times itself.
/// </remarks>
/// <param name="repository">The table.</param>
/// <param name="tmdbShows">TMDB's shows.</param>
/// <param name="tmdbMovies">TMDB's movies.</param>
/// <param name="tmdbCollections">TMDB's collections.</param>
public class MetadataRefreshState(
    Metadata_RefreshRepository repository,
    TMDB_ShowRepository tmdbShows,
    TMDB_MovieRepository tmdbMovies,
    TMDB_CollectionRepository tmdbCollections
) : IMetadataRefreshState
{
    #region Refresh State

    /// <summary>
    ///   How long a refreshed entry stays fresh: a refresh that is not forced
    ///   skips an entry refreshed more recently than this.
    /// </summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public DateTime? GetLastRefreshedAt(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Source == MetadataSource.TMDB)
            return GetTmdbLastUpdatedAt(entry);

        return repository.GetByEntry(entry)?.LastRefreshedAt;
    }

    /// <inheritdoc />
    public void RecordRefresh(MetadataGuid entry, DateTime refreshedAt)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Source.IsCore)
            return;

        var row = repository.GetByEntry(entry) ?? new Metadata_Refresh
        {
            Source = entry.Source,
            EntityType = entry.EntityType,
            ProviderID = entry.ID,
        };
        row.LastRefreshedAt = refreshedAt;
        repository.Save(row);
    }

    /// <inheritdoc />
    public bool Forget(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Source.IsCore || repository.GetByEntry(entry) is not { } row)
            return false;

        repository.Delete(row);
        return true;
    }

    /// <summary>
    ///   When TMDB's tables say a show, movie or collection was last updated.
    /// </summary>
    /// <param name="entry">The TMDB entry.</param>
    /// <returns>The time, or <see langword="null"/> when it is not stored or was never updated after it was added.</returns>
    private DateTime? GetTmdbLastUpdatedAt(MetadataGuid entry)
    {
        if (!entry.TryGetNumericID<int>(out var tmdbID) || tmdbID <= 0)
            return null;

        var (createdAt, lastUpdatedAt) = entry.EntityType switch
        {
            _ when entry.EntityType == MetadataEntityType.Series && tmdbShows.GetByTmdbShowID(tmdbID) is { } show => (show.CreatedAt, show.LastUpdatedAt),
            _ when entry.EntityType == MetadataEntityType.Movie && tmdbMovies.GetByTmdbMovieID(tmdbID) is { } movie => (movie.CreatedAt, movie.LastUpdatedAt),
            _ when entry.EntityType == MetadataEntityType.Collection && tmdbCollections.GetByTmdbCollectionID(tmdbID) is { } collection =>
                (collection.CreatedAt, collection.LastUpdatedAt),
            _ => (default(DateTime), default(DateTime)),
        };
        return createdAt == lastUpdatedAt ? null : lastUpdatedAt;
    }

    #endregion
}
