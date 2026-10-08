using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   Keeps TMDB's genres in the core's tag store, so a search hit that names
///   its genres by ID can be told their names.
/// </summary>
/// <remarks>
///   TMDB's genre lists are written into the tag store once per start, and
///   again, at most hourly, when a hit names a genre the store lacks. There is
///   no genre catalogue of the plugin's own: the tag store is it.
/// </remarks>
/// <param name="apiClient">The TMDB client.</param>
/// <param name="tagStore">The core's tag store.</param>
/// <param name="logger">The logger.</param>
public sealed class TmdbTagService(TmdbApiClient apiClient, IMetadataTagStore tagStore, ILogger<TmdbTagService> logger) : IDisposable
{
    #region Fields

    private readonly SemaphoreSlim _fillLock = new(1, 1);

    private volatile bool _filled;

    private DateTimeOffset _filledAt;

    #endregion

    #region Genres

    /// <summary>
    ///   Writes TMDB's show and movie genres into the tag store, unless that
    ///   was done since the plugin started.
    /// </summary>
    /// <param name="force">Whether to write them again anyway.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many genres were written.</returns>
    public async Task<int> FillGenres(bool force = false, CancellationToken cancellationToken = default)
    {
        if (_filled && !force)
            return 0;

        await _fillLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_filled && !force)
                return 0;

            var shows = await apiClient.GetShowGenres(cancellationToken).ConfigureAwait(false);
            var movies = await apiClient.GetMovieGenres(cancellationToken).ConfigureAwait(false);
            var genres = shows.Concat(movies)
                .Where(genre => genre.Id > 0 && !string.IsNullOrWhiteSpace(genre.Name))
                .DistinctBy(genre => genre.Id)
                .SelectMany(TmdbEntityMapper.GenreTags)
                .ToList();
            if (genres.Count > 0)
                tagStore.SaveTags(genres);

            _filled = true;
            _filledAt = apiClient.TimeProvider.GetUtcNow();
            logger.LogDebug("Stored {Count} TMDB genres in the tag store.", genres.Count);
            return genres.Count;
        }
        finally
        {
            _fillLock.Release();
        }
    }

    /// <summary>
    ///   A lookup of the names of the genres some search hits name, filling
    ///   the tag store first when it lacks one of them.
    /// </summary>
    /// <remarks>
    ///   A failure to fetch TMDB's lists leaves the hits with the genres the
    ///   store already knows.
    /// </remarks>
    /// <param name="genreIDs">The genre IDs the hits name.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The lookup, answering no names for a genre still not known.</returns>
    public async Task<Func<int, IReadOnlyList<string>>> GetGenreNames(IEnumerable<int> genreIDs, CancellationToken cancellationToken = default)
    {
        var wanted = genreIDs.Where(id => id > 0).ToHashSet();
        // A genre TMDB's lists lack is not asked for again for an hour.
        if (wanted.Any(id => GenreNames(id).Count is 0) && (!_filled || apiClient.TimeProvider.GetUtcNow() - _filledAt >= TimeSpan.FromHours(1)))
        {
            try
            {
                await FillGenres(force: _filled, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (TmdbApiClient.IsTransient(ex))
            {
                logger.LogDebug(ex, "Unable to fetch TMDB's genres; naming only the genres already stored.");
            }
        }

        return GenreNames;
    }

    /// <summary>
    ///   The names of a genre the tag store holds: its own, or those of its
    ///   parts when it joins two.
    /// </summary>
    /// <param name="genreID">The TMDB genre ID.</param>
    /// <returns>The names, or none when the store has no such genre.</returns>
    public IReadOnlyList<string> GenreNames(int genreID)
    {
        if (genreID <= 0)
            return [];

        if (tagStore.GetTag(TmdbIds.Genre(genreID)) is { } whole)
            return [whole.Name];

        var names = new List<string>();
        for (var part = 1; tagStore.GetTag(TmdbIds.GenrePart(genreID, part)) is { } tag; part++)
            names.Add(tag.Name);
        return names;
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
        => _fillLock.Dispose();
}
