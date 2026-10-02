using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.Movies;

using MovieCredits = TMDbLib.Objects.Movies.Credits;
using TitleLanguage = Shoko.Abstractions.Metadata.Enums.TitleLanguage;

#pragma warning disable CS0618
// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

public partial class TmdbMetadataUpdater
{
    #region Update (Movies)

    /// <summary>
    ///   Brings a movie up to date from TMDB, with its credits and collection
    ///   when the options ask for them.
    /// </summary>
    /// <remarks>
    ///   A movie that is not forced and not new is only fetched when TMDB
    ///   recorded a change to it since it was last updated, within the
    ///   changes window.
    /// </remarks>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <param name="options">What to fetch; a switch left out goes by the settings.</param>
    /// <param name="forceRefresh">Whether to fetch everything again, the people included, however recently it was fetched.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    /// <exception cref="TmdbApiKeyUnavailableException">No API key is available.</exception>
    public async Task<bool> UpdateMovie(int movieId, MetadataRefreshOptions options, bool forceRefresh)
    {
        var downloadImages = options.DownloadImages;
        var settings = _settingsProvider.GetSettings();
        var downloadCrewAndCast = options.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast;
        var downloadCollections = options.DownloadCollections ?? settings.TMDB.AutoDownloadCollections;
        if (movieId <= 0)
            return false;

        var tmdbMovie = _tmdbMovies.GetByTmdbMovieID(movieId) ?? new(movieId);
        var newlyAdded = tmdbMovie.TMDB_MovieID == 0;
        if (!forceRefresh && !newlyAdded)
        {
            bool hasChanged;
            try
            {
                hasChanged = await _client.HasMovieChangedSinceAsync(movieId, tmdbMovie.LastUpdatedAt).ConfigureAwait(false);
            }
            catch (Exception ex) when (TmdbApiClient.IsTmdbTransient(ex))
            {
                _logger.LogWarning(ex, "TMDB: Transient error checking changes for movie {MovieId}; proceeding with full refresh.", movieId);
                hasChanged = true;
            }
            if (!hasChanged)
            {
                _logger.LogInformation("Skipping update of movie {MovieID} as no changes were detected on TMDB since {LastUpdatedAt}.", movieId, tmdbMovie.LastUpdatedAt);
                return false;
            }
        }

        // Abort if we couldn't find the movie by id.
        var methods = MovieMethods.Translations | MovieMethods.AlternativeTitles | MovieMethods.ReleaseDates | MovieMethods.ExternalIds |
            MovieMethods.Keywords | MovieMethods.Recommendations | MovieMethods.Similar;
        if (downloadCrewAndCast)
            methods |= MovieMethods.Credits;
        var movie = await _client.UseClient(c => c.GetMovieAsync(movieId, "en-US", null, methods), $"Get movie {movieId}").ConfigureAwait(false);
        if (movie is null)
            return false;

        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();
        var contentRatingLanguages = settings.TMDB.DownloadAllContentRatings
            ? null
            : Languages.PreferredNamingLanguages.Select(a => a.Language)
                .Concat(Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language))
                .Except([TitleLanguage.Main, TitleLanguage.Unknown, TitleLanguage.None])
                .ToHashSet();
        var updated = tmdbMovie.Populate(movie, contentRatingLanguages);
        var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbMovie, movie.Translations, preferredTitleLanguages, preferredOverviewLanguages,
            alternativeTitles: movie.AlternativeTitles?.Titles);
        updated = titlesUpdated || overviewsUpdated || updated;
        updated = UpdateMovieExternalIDs(tmdbMovie, movie.ExternalIds!) || updated;
        updated = await UpdateCompanies(tmdbMovie, movie.ProductionCompanies!) || updated;
        updated = UpdateSuggestions(MetadataEntityType.Movie, tmdbMovie.TmdbMovieID,
        [
            (SuggestionKind.Recommended, movie.Recommendations?.Results?.Select(result => result.Id) ?? []),
            (SuggestionKind.Similar, movie.Similar?.Results?.Select(result => result.Id) ?? []),
        ]) || updated;
        if (downloadCrewAndCast)
            updated = await UpdateMovieCastAndCrew(tmdbMovie, movie.Credits!, forceRefresh, downloadImages) || updated;
        if (updated)
        {
            tmdbMovie.LastUpdatedAt = DateTime.Now;
            _tmdbMovies.Save(tmdbMovie);
        }

        if (downloadCollections)
            await UpdateMovieCollections(movie);

        if (newlyAdded || updated)
            ShokoEventHandler.Instance.OnMovieUpdated(tmdbMovie, newlyAdded ? UpdateReason.Added : UpdateReason.Updated);

        return updated;
    }

    private async Task<bool> UpdateMovieCastAndCrew(TMDB_Movie tmdbMovie, MovieCredits credits, bool forceRefresh, bool downloadImages)
    {
        var peopleToKeep = new HashSet<int>();

        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Movie_Cast>();
        var existingCastDict = _tmdbMovieCast.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        foreach (var cast in credits.Cast!)
        {
            var ordering = counter++;
            peopleToKeep.Add(cast.Id);
            castToKeep.Add(cast.CreditId!);

            var roleUpdated = false;
            if (!existingCastDict.TryGetValue(cast.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbMovieID = tmdbMovie.Id,
                    TmdbPersonID = cast.Id,
                    TmdbCreditID = cast.CreditId!,
                };
                castToAdd++;
                roleUpdated = true;
            }

            var characterName = cast.Character!.Replace(" (voice)", "");
            if (role.CharacterName != characterName)
            {
                role.CharacterName = characterName;
                roleUpdated = true;
            }

            if (role.Ordering != ordering)
            {
                role.Ordering = ordering;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                castToSave.Add(role);
            }
        }

        var crewToAdd = 0;
        var crewToKeep = new HashSet<string>();
        var crewToSave = new List<TMDB_Movie_Crew>();
        var existingCrewDict = _tmdbMovieCrew.GetByTmdbMovieID(tmdbMovie.Id)
            .ToDictionary(crew => crew.TmdbCreditID);
        foreach (var crew in credits.Crew!)
        {
            peopleToKeep.Add(crew.Id);
            crewToKeep.Add(crew.CreditId!);

            var roleUpdated = false;
            if (!existingCrewDict.TryGetValue(crew.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbMovieID = tmdbMovie.Id,
                    TmdbPersonID = crew.Id,
                    TmdbCreditID = crew.CreditId!,
                };
                crewToAdd++;
                roleUpdated = true;
            }

            if (role.Department != crew.Department)
            {
                role.Department = crew.Department!;
                roleUpdated = true;
            }

            if (role.Job != crew.Job)
            {
                role.Job = crew.Job!;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                crewToSave.Add(role);
            }
        }

        var castToRemove = existingCastDict.Values
            .ExceptBy(castToKeep, cast => cast.TmdbCreditID)
            .ToList();
        var crewToRemove = existingCrewDict.Values
            .ExceptBy(crewToKeep, crew => crew.TmdbCreditID)
            .ToList();

        _tmdbMovieCast.Save(castToSave);
        _tmdbMovieCrew.Save(crewToSave);
        _tmdbMovieCast.Delete(castToRemove);
        _tmdbMovieCrew.Delete(crewToRemove);

        _logger.LogDebug(
            "Added/updated/removed/skipped {aa}/{au}/{ar}/{as} cast and {ra}/{ru}/{rr}/{rs} crew  for movie {MovieTitle} (Movie={MovieId})",
            castToAdd,
            castToSave.Count - castToAdd,
            castToRemove.Count,
            existingCastDict.Count - (castToSave.Count - castToAdd),
            crewToAdd,
            crewToSave.Count - crewToAdd,
            crewToRemove.Count,
            existingCrewDict.Count - (crewToSave.Count - crewToAdd),
            tmdbMovie.EnglishTitle,
            tmdbMovie.Id
            );

        // Only add/remove staff if we're not doing a quick refresh.
        var peopleAdded = 0;
        var peopleUpdated = 0;
        var peoplePurged = 0;
        var peopleToPurge = existingCastDict.Values.Select(cast => cast.TmdbPersonID)
            .Concat(existingCrewDict.Values.Select(crew => crew.TmdbPersonID))
            .Except(peopleToKeep)
            .ToHashSet();
        var transientlyFailedMoviePeople = new ConcurrentBag<int>();
        await ProcessWithConcurrencyAsync(TmdbApiClient.MaxConcurrency, peopleToKeep, async personId =>
        {
            try
            {
                var (added, updated) = await UpdatePerson(personId, forceRefresh, downloadImages, currentMovieId: tmdbMovie.Id);
                if (added)
                    Interlocked.Increment(ref peopleAdded);
                else if (updated)
                    Interlocked.Increment(ref peopleUpdated);
            }
            catch (Exception ex) when (TmdbApiClient.IsTmdbTransient(ex))
            {
                // Keeps the person's cast and crew rows for the retry.
                transientlyFailedMoviePeople.Add(personId);
            }
            catch (Exception ex)
            {
                // Left for CleanupOrphanedCastCrew.
                _logger.LogWarning(ex, "TMDB: Unexpected error updating person {PersonId} for movie {MovieId}", personId, tmdbMovie.Id);
            }
        }, onDropped: transientlyFailedMoviePeople.Add);
        var transientlyFailedMovieSet = transientlyFailedMoviePeople.ToHashSet();
        if (transientlyFailedMovieSet.Count > 0)
            await Task.WhenAll(transientlyFailedMovieSet.Select(personId =>
                _scheduler.Enqueue<UpdateTmdbPersonJob>(j => { j.TmdbPersonID = personId; j.DownloadImages = downloadImages; j.TmdbMovieID = tmdbMovie.Id; })));
        CleanupOrphanedCastCrew(peopleToKeep, transientlyFailedMovieSet, missingPersonIds =>
        {
            var orphanedCast = _tmdbMovieCast.GetByTmdbMovieID(tmdbMovie.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            var orphanedCrew = _tmdbMovieCrew.GetByTmdbMovieID(tmdbMovie.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            if (orphanedCast.Count > 0 || orphanedCrew.Count > 0)
            {
                _logger.LogWarning("TMDB: Removed {CastCount} cast and {CrewCount} crew entries for {PersonCount} people that failed to fetch. (Movie={MovieId})",
                    orphanedCast.Count, orphanedCrew.Count, missingPersonIds.Count, tmdbMovie.Id);
                _tmdbMovieCast.Delete(orphanedCast);
                _tmdbMovieCrew.Delete(orphanedCrew);
            }
        });
        try
        {
            await ProcessWithConcurrencyAsync(TmdbApiClient.MaxConcurrency, peopleToPurge, async personId =>
            {
                if (await PurgePerson(personId))
                    Interlocked.Increment(ref peoplePurged);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB: Failed to purge one or more people during movie cast/crew update (Movie={MovieId})", tmdbMovie.Id);
        }

        _logger.LogDebug("Added/removed {a}/{u}/{r}/{s} staff for movie {MovieTitle} (Movie={MovieId})",
            peopleAdded,
            peopleUpdated,
            peoplePurged,
            peopleToKeep.Count + peopleToPurge.Count - peopleAdded - peopleUpdated - peoplePurged,
            tmdbMovie.EnglishTitle,
            tmdbMovie.Id
        );
        return castToSave.Count > 0 ||
            castToRemove.Count > 0 ||
            crewToSave.Count > 0 ||
            crewToRemove.Count > 0 ||
            peopleAdded > 0 ||
            peopleUpdated > 0 ||
            peoplePurged > 0;
    }

    #endregion

    #region Collections

    private async Task UpdateMovieCollections(Movie movie)
    {
        if (movie.BelongsToCollection?.Id is not { } collectionId)
        {
            await CleanupMovieCollection(movie.Id);
            return;
        }

        await UpdateCollection(collectionId, movie.Id, $" for movie {movie.Id} \"{movie.Title}\"").ConfigureAwait(false);
    }

    /// <summary>
    ///   Brings a collection up to date from TMDB on its own: its titles,
    ///   overviews and the order of the movies it holds, dropping the ones it
    ///   no longer holds.
    /// </summary>
    /// <remarks>
    ///   A collection TMDB no longer has is removed.
    /// </remarks>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    /// <exception cref="TmdbApiKeyUnavailableException">No API key is available.</exception>
    public Task<bool> UpdateCollection(int collectionId)
        => UpdateCollection(collectionId, null, string.Empty);

    /// <summary>
    ///   Brings a collection up to date from TMDB, adding a movie to it or
    ///   putting it in its place when one is given.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <param name="movieId">The movie being refreshed, or <see langword="null"/>.</param>
    /// <param name="context">What the collection is fetched for, for the log.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    private async Task<bool> UpdateCollection(int collectionId, int? movieId, string context)
    {
        var collection = await _client.UseClient(c => c.GetCollectionAsync(collectionId, CollectionMethods.Images | CollectionMethods.Translations), $"Get movie collection {collectionId}{context}").ConfigureAwait(false);
        if (collection is null)
        {
            await PurgeCollection(collectionId);
            return true;
        }

        using (await GetLockForEntity(MetadataEntityType.Collection, collection.Id, "metadata", "Update").ConfigureAwait(false))
        {
            var settings = _settingsProvider.GetSettings();
            var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
            var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

            var tmdbCollection = _tmdbCollections.GetByTmdbCollectionID(collectionId) ?? new(collectionId);
            var updated = tmdbCollection.Populate(collection);
            updated = UpdateTitlesAndOverviews(tmdbCollection, collection.Translations, preferredTitleLanguages, preferredOverviewLanguages) || updated;

            var xrefsToAdd = 0;
            var xrefsToSave = new List<TMDB_Collection_Movie>();
            var movieXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(collectionId);
            var xrefsToRemove = movieXRefs.Where(xref => !collection.Parts!.Any(part => xref.TmdbMovieID == part.Id)).ToList();
            // A collection refreshed on its own puts every movie it holds in
            // its place; one refreshed for a movie only places that movie.
            foreach (var xref in movieId is null ? movieXRefs.Except(xrefsToRemove) : [])
            {
                var index = collection.Parts!.FindIndex(part => part.Id == xref.TmdbMovieID);
                if (xref.Ordering == index + 1)
                    continue;

                xref.Ordering = index + 1;
                xrefsToSave.Add(xref);
            }

            if (movieId is { } id)
            {
                var movieXref = movieXRefs.FirstOrDefault(xref => xref.TmdbMovieID == id);
                var index = collection.Parts!.FindIndex(part => part.Id == id);
                if (index == -1)
                    index = collection.Parts.Count;
                if (movieXref is null)
                {
                    xrefsToAdd++;
                    xrefsToSave.Add(new(collectionId, id, index + 1));
                }
                else
                {
                    xrefsToRemove.Remove(movieXref);
                    if (movieXref.Ordering != index + 1)
                    {
                        movieXref.Ordering = index + 1;
                        xrefsToSave.Add(movieXref);
                    }
                }
            }

            _logger.LogDebug(
                "Added/updated/removed/skipped {ta}/{tu}/{tr}/{ts} movie cross-references for movie collection {CollectionTitle} (Id={CollectionId})",
                xrefsToAdd,
                xrefsToSave.Count - xrefsToAdd,
                xrefsToRemove.Count,
                movieXRefs.Count + xrefsToAdd - xrefsToRemove.Count - xrefsToSave.Count,
                tmdbCollection.EnglishTitle,
                tmdbCollection.Id);
            _xrefTmdbCollectionMovies.Save(xrefsToSave);
            _xrefTmdbCollectionMovies.Delete(xrefsToRemove);

            if (updated || xrefsToSave.Count > 0 || xrefsToRemove.Count > 0)
            {
                tmdbCollection.LastUpdatedAt = DateTime.Now;
                _tmdbCollections.Save(tmdbCollection);
                return true;
            }

            return false;
        }
    }

    #endregion

    #region Purge (Movies)

    /// <summary>
    ///   Removes a movie from TMDB's tables, with its credits, titles,
    ///   overviews, suggestions and image links, the companies only it used,
    ///   and its place in its collection, which goes too once it holds nothing.
    /// </summary>
    /// <remarks>
    ///   The people it credited are stamped as orphaned and removed once they
    ///   have been for long enough. The links to the movie are the core's, and
    ///   are gone before this is called. The movie's own row goes last, so an
    ///   interrupted purge is finished by the next one.
    /// </remarks>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns>A task that completes once the movie is removed.</returns>
    public async Task PurgeMovie(int movieId)
    {
        var movie = _tmdbMovies.GetByTmdbMovieID(movieId);

        PurgeMovieCompanies(movieId);

        await PurgeMovieCastAndCrew(movieId);

        await CleanupMovieCollection(movieId);

        PurgeSuggestions(MetadataEntityType.Movie, movieId);

        PurgeTitlesAndOverviews(MetadataEntityType.Movie, movieId);

        _imageService.PurgeImages(movie ?? new() { TmdbMovieID = movieId });

        if (movie is not null)
        {
            _logger.LogTrace("Removing movie {MovieName} (Movie={MovieID})", movie.OriginalTitle, movie.Id);
            _tmdbMovies.Delete(movie);
        }
    }

    private void PurgeMovieCompanies(int movieId)
    {
        var xrefsToRemove = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(MetadataEntityType.Movie, movieId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }
    }

    private async Task PurgeMovieCastAndCrew(int movieId)
    {
        var castMembers = _tmdbMovieCast.GetByTmdbMovieID(movieId);
        var crewMembers = _tmdbMovieCrew.GetByTmdbMovieID(movieId);

        _tmdbMovieCast.Delete(castMembers);
        _tmdbMovieCrew.Delete(crewMembers);

        var allPeopleSet = castMembers.Select(c => c.TmdbPersonID)
            .Concat(crewMembers.Select(c => c.TmdbPersonID))
            .Distinct()
            .ToHashSet();
        foreach (var personId in allPeopleSet)
            await PurgePerson(personId);
    }

    private async Task CleanupMovieCollection(int movieId)
    {
        var xref = _xrefTmdbCollectionMovies.GetByTmdbMovieID(movieId);
        if (xref is null)
            return;

        var allXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(xref.TmdbCollectionID);
        if (allXRefs.Count > 1)
            _xrefTmdbCollectionMovies.Delete(xref);
        else
            await PurgeCollection(xref.TmdbCollectionID);
    }

    /// <summary>
    ///   Removes a collection from TMDB's tables, with which movies it holds,
    ///   its titles, overviews and image links. The movies stay.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <returns>A task that completes once the collection is removed.</returns>
    public async Task PurgeCollection(int collectionId)
    {
        using (await GetLockForEntity(MetadataEntityType.Collection, collectionId, "metadata", "Update").ConfigureAwait(false))
            PurgeCollectionUnderLock(collectionId);
    }

    /// <summary>
    ///   Removes a collection from TMDB's tables, as
    ///   <see cref="PurgeCollection(int)"/> does, while its lock is held.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    private void PurgeCollectionUnderLock(int collectionId)
    {
        var collection = _tmdbCollections.GetByTmdbCollectionID(collectionId);
        var collectionXRefs = _xrefTmdbCollectionMovies.GetByTmdbCollectionID(collectionId);
        if (collectionXRefs.Count > 0)
        {
            _logger.LogTrace(
                "Removing {Count} cross-references for movie collection {CollectionName} (Collection={CollectionID})",
                collectionXRefs.Count, collection?.EnglishTitle ?? string.Empty,
                collectionId
            );
            _xrefTmdbCollectionMovies.Delete(collectionXRefs);
        }

        _imageService.PurgeImages(collection ?? new() { TmdbCollectionID = collectionId });

        PurgeTitlesAndOverviews(MetadataEntityType.Collection, collectionId);

        if (collection is not null)
        {
            _logger.LogTrace(
                "Removing movie collection {CollectionName} (Collection={CollectionID})",
                collection.EnglishTitle,
                collectionId
            );
            _tmdbCollections.Delete(collection);
        }
    }

    /// <summary>
    ///   Whether any movie a collection holds is linked to an anime, which
    ///   keeps the collection from being purged.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <returns><see langword="true"/> when a member is linked.</returns>
    public bool IsCollectionInUse(int collectionId)
        => _xrefTmdbCollectionMovies.GetByTmdbCollectionID(collectionId)
            .Any(xref => _xrefAnidbTmdbMovies.GetByTmdbMovieID(xref.TmdbMovieID).Count > 0);

    #endregion
}
