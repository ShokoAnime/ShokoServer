using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every plugin source's movies in the store's own table, cached in
///   memory, with their titles and descriptions in the text table.
/// </summary>
/// <param name="movieRepository">The movies.</param>
/// <param name="contentRatingRepository">The movies' content ratings.</param>
/// <param name="textStore">Keeps the titles and descriptions.</param>
/// <param name="cleanup">Removes what the other stores hold for a removed movie.</param>
public class MetadataMovieStore(
    Metadata_MovieRepository movieRepository,
    Metadata_ContentRatingRepository contentRatingRepository,
    MetadataTextStore textStore,
    MetadataEntityCleanup cleanup
) : IMetadataMovieStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    public IMovie? GetMovie(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Movie ? movieRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMovie> GetAllMovies(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return movieRepository.GetBySource(source);
    }

    #endregion

    #region Writing

    /// <inheritdoc />
    public int SaveMovie(MetadataMovieData movie)
    {
        ArgumentNullException.ThrowIfNull(movie);
        MetadataEntries.CheckEntry(movie.ID, MetadataEntityType.Movie, nameof(movie));

        var originalLanguageCode = MetadataEntries.CheckLanguageCode(movie.OriginalLanguageCode, nameof(movie));
        var resources = MetadataEntries.CheckResources(movie.Resources, nameof(movie));
        var crossSourceIDs = MetadataEntries.CheckCrossSourceIDs(movie.CrossSourceIDs, nameof(movie));
        var countries = MetadataEntries.CheckCountries(movie.ProductionCountries, nameof(movie));
        var contentRatings = MetadataContentRatings.Check(movie.ContentRatings, nameof(movie));
        lock (_writeLock)
        {
            var stored = movieRepository.GetByProviderID(movie.ID.Source, movie.ID.ID);
            var row = MetadataRows.Copy(stored) ?? new Metadata_Movie();
            row.Source = movie.ID.Source;
            row.ProviderID = movie.ID.ID;
            row.ReleasedAt = movie.ReleaseDate;
            row.IsRestricted = movie.Restricted;
            row.IsVideo = movie.Video;
            row.RuntimeSeconds = movie.Runtime is { } runtime ? (int)Math.Round(runtime.TotalSeconds) : null;
            row.OriginalLanguageCode = originalLanguageCode;
            // Ratings are stored with two decimals on every backend.
            row.Rating = Math.Round(movie.Rating, 2);
            row.RatingVotes = movie.RatingVotes;
            row.Resources = resources;
            row.CrossSourceIDs = crossSourceIDs;
            row.ExtraData = MetadataDefaultImages.Apply((row.ExtraData ?? new()) with { ProductionCountries = countries }, movie.DefaultImageResourceIDs).NullIfEmpty();
            var (ratingsSaving, ratingsDeleting) = MetadataContentRatings.Plan(contentRatingRepository, movie.ID, contentRatings);

            // Written, and reported as changed, only when new or when its
            // columns, texts or ratings changed.
            var reason = UpdateReason.None;
            textStore.WriteWithTexts([(movie.ID, movie.Titles ?? [], movie.Overviews ?? [])], [], changedTexts =>
            {
                reason = stored is null
                    ? UpdateReason.Added
                    : !stored.SameAs(row) || changedTexts.Contains(movie.ID) || ratingsSaving.Count + ratingsDeleting.Count > 0
                        ? UpdateReason.Updated
                        : UpdateReason.None;
                if (reason is UpdateReason.None)
                    return [];

                row.LastUpdatedAt = DateTime.Now;
                if (stored is null)
                    row.CreatedAt = row.LastUpdatedAt;
                return
                [
                    new MetadataRowChanges<Metadata_Movie>(movieRepository, [row], []),
                    new MetadataRowChanges<Metadata_ContentRating>(contentRatingRepository, ratingsSaving, ratingsDeleting),
                ];
            });

            if (reason is UpdateReason.None)
                return 0;

            MetadataStoredEntry.Events.OnMovieUpdated(row, reason);
            return 1;
        }
    }

    /// <inheritdoc />
    public int RemoveMovie(MetadataGuid id)
    {
        MetadataEntries.CheckEntry(id, MetadataEntityType.Movie, nameof(id));

        lock (_writeLock)
        {
            if (movieRepository.GetByProviderID(id.Source, id.ID) is not { } row)
                return 0;

            textStore.WriteWithTexts([], [id], _ =>
            [
                new MetadataRowChanges<Metadata_Movie>(movieRepository, [], [row]),
                new MetadataRowChanges<Metadata_ContentRating>(contentRatingRepository, [], contentRatingRepository.GetByEntry(id)),
            ]);
            MetadataStoredEntry.Events.OnMovieUpdated(row, UpdateReason.Removed);
        }

        // Outside the lock, since the other stores take their own.
        cleanup.Remove([id]);
        return 1;
    }

    #endregion

    #region Refresh State

    /// <summary>
    ///   Stamps when the core last refreshed a stored movie, on the movie's
    ///   own row, leaving the rest of it as it is.
    /// </summary>
    /// <param name="movieID">The movie.</param>
    /// <param name="refreshedAt">When the refresh finished, in local time.</param>
    /// <returns><c>true</c> if the movie is stored.</returns>
    internal bool SetLastRefreshedAt(MetadataGuid movieID, DateTime refreshedAt)
    {
        lock (_writeLock)
        {
            if (movieRepository.GetByProviderID(movieID.Source, movieID.ID) is not { } stored)
                return false;

            // A copy, so the cached row stays as it was until the write has committed.
            var row = MetadataRows.Copy(stored)!;
            row.LastRefreshedAt = refreshedAt;
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Movie>(movieRepository, [row], []));
            return true;
        }
    }

    #endregion
}
