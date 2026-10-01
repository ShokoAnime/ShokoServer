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
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.General;
using TMDbLib.Objects.People;

#pragma warning disable CS0618
// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

public partial class TmdbMetadataUpdater
{
    #region People

    /// <summary>
    ///   How long the images fetched with a person are kept for the image job.
    /// </summary>
    private static readonly TimeSpan _personImagesFreshFor = TimeSpan.FromHours(2);

    /// <summary>
    ///   The images fetched with each person, and when.
    /// </summary>
    private readonly ConcurrentDictionary<int, (DateTime FetchedAt, IReadOnlyList<ImageData> Images)> _personImages = new();

    /// <summary>
    ///   Fetches every person TMDB's credits name that its people table lacks.
    /// </summary>
    /// <returns>A task that completes once they are fetched.</returns>
    public async Task RepairMissingPeople()
    {
        var missingIds = new HashSet<int>();
        var updateCount = 0;
        var skippedCount = 0;
        var peopleIds = _tmdbPeople.GetAll().Select(person => person.TmdbPersonID).ToHashSet();
        foreach (var person in _tmdbEpisodeCast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in _tmdbEpisodeCrew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        foreach (var person in _tmdbMovieCast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in _tmdbMovieCrew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        _logger.LogDebug("Found {Count} unique missing TMDB People for Episode & Movie staff", missingIds.Count);
        await ProcessWithConcurrencyAsync(TmdbApiClient.MaxConcurrency, missingIds, async personId =>
        {
            var (_, updated) = await UpdatePerson(personId, forceRefresh: true);
            if (updated)
                Interlocked.Increment(ref updateCount);
            else
                Interlocked.Increment(ref skippedCount);
        });

        _logger.LogInformation("Updated missing TMDB People: Found/Updated/Skipped {Found}/{Updated}/{Skipped}",
            missingIds.Count, updateCount, skippedCount);
    }

    /// <summary>
    ///   Brings a person up to date from TMDB, unless it was within the last
    ///   hour and the update is not forced. A person TMDB no longer has is
    ///   removed with its credits.
    /// </summary>
    /// <param name="personId">The TMDB person ID.</param>
    /// <param name="forceRefresh">Whether to fetch the person however recently it was.</param>
    /// <param name="downloadImages">Whether to fetch the person's images too, for the image job.</param>
    /// <param name="currentShowId">The show being refreshed, which is not refreshed again should the person be removed.</param>
    /// <param name="currentMovieId">The movie being refreshed, which is not refreshed again should the person be removed.</param>
    /// <returns>Whether the person was added, and whether it changed.</returns>
    /// <exception cref="TmdbApiKeyUnavailableException">No API key is available.</exception>
    public async Task<(bool added, bool updated)> UpdatePerson(int personId, bool forceRefresh = false, bool downloadImages = false, int? currentShowId = null, int? currentMovieId = null)
    {
        using (await GetLockForEntity(MetadataEntityType.Creator, personId, "metadata & images", "Update").ConfigureAwait(false))
        {
            var tmdbPerson = _tmdbPeople.GetByTmdbPersonID(personId) ?? new(personId);
            if (!forceRefresh && tmdbPerson.TMDB_PersonID is not 0 && tmdbPerson.LastUpdatedAt > DateTime.Now.AddHours(-1))
            {
                _logger.LogDebug("Skipping update for staff member. (Person={PersonId})", personId);
                return (false, false);
            }

            _logger.LogDebug("Updating staff member. (Person={PersonId})", personId);
            var methods = PersonMethods.Translations | PersonMethods.ExternalIds;
            if (downloadImages)
                methods |= PersonMethods.Images;
            var newlyAdded = tmdbPerson.TMDB_PersonID is 0;
            Person? person;
            try
            {
                person = await _client.UseClient(c => c.GetPersonAsync(personId, methods), $"Get person {personId}");
            }
            catch (NotFoundException ex)
            {
                // Deleted or merged on TMDB.
                _logger.LogWarning(ex, "Staff member not found on TMDB (HTTP 404). Purging local records. (Person={PersonId})", personId);
                await PurgePersonInternal(personId, currentShowId: currentShowId, currentMovieId: currentMovieId);
                return (false, !newlyAdded);
            }
            // TMDbLib may return null without throwing.
            if (person is null)
            {
                _logger.LogWarning("Staff member returned null from TMDB API. Purging local records. (Person={PersonId})", personId);
                await PurgePersonInternal(personId, currentShowId: currentShowId, currentMovieId: currentMovieId);
                return (false, !newlyAdded);
            }

            var settings = _settingsProvider.GetSettings();
            var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

            var updated = tmdbPerson.Populate(person);
            // `Populate` only keeps the English biography, so the translations
            // and other names are stored as the person's texts.
            updated = UpdateTitlesAndOverviews(tmdbPerson, person.Translations, null, preferredOverviewLanguages, includeTitles: false, aliases: person.AlsoKnownAs ?? []) || updated;
            if (IsPersonLinkedToOtherEntities(personId) && tmdbPerson.LastOrphanedAt.HasValue)
            {
                tmdbPerson.LastOrphanedAt = null;
                updated = true;
            }

            if (updated)
            {
                tmdbPerson.LastUpdatedAt = DateTime.Now;
                _tmdbPeople.Save(tmdbPerson);
            }

            // The images come along with the person when they are wanted, and
            // are kept for the image job that follows the refresh.
            if (downloadImages && person.Images?.Profiles is { } profiles)
                _personImages[personId] = (DateTime.Now, profiles);

            return (newlyAdded, updated);
        }
    }

    /// <summary>
    ///   The profile images TMDB gave for a person when the person was last
    ///   fetched with its images, within the last two hours.
    /// </summary>
    /// <remarks>
    ///   TMDB is never asked for them on their own: a person's images are
    ///   only reconciled after a refresh fetched the person, as TMDB's own
    ///   image downloads did.
    /// </remarks>
    /// <param name="personId">The TMDB person ID.</param>
    /// <returns>The images, in TMDB's order, or <see langword="null"/> when none were fetched lately.</returns>
    public IReadOnlyList<ImageData>? GetFetchedPersonImages(int personId)
    {
        if (_personImages.TryGetValue(personId, out var cached) && DateTime.Now - cached.FetchedAt < _personImagesFreshFor)
            return cached.Images;

        _personImages.TryRemove(personId, out _);
        return null;
    }

    /// <summary>
    ///   Stamps the people nothing credits any more, and removes the ones
    ///   stamped before a cutoff with their credits, titles and image links.
    /// </summary>
    /// <param name="orphanedBefore">
    ///   Remove only the people orphaned before this time; left out, the ones
    ///   orphaned for longer than the metadata settings say.
    /// </param>
    /// <returns>How many people were removed.</returns>
    public async Task<int> PurgeOrphanedPeople(DateTime? orphanedBefore = null)
    {
        var linkedPersonIds = GetLinkedPersonIds();
        var people = _tmdbPeople.GetAll().Where(p => !linkedPersonIds.Contains(p.TmdbPersonID)).ToList();
        _logger.LogDebug("Checking {count} orphaned staff members if they should be purged.", people.Count);
        var removed = 0;
        await ProcessWithConcurrencyAsync(TmdbApiClient.MaxConcurrency, people, async person =>
        {
            if (await PurgePerson(person.TmdbPersonID, orphanedBefore: orphanedBefore).ConfigureAwait(false))
                Interlocked.Increment(ref removed);
        });
        return removed;
    }

    /// <summary>
    ///   Stamps a person nothing credits any more, or removes it once it was
    ///   stamped before a cutoff.
    /// </summary>
    /// <param name="personId">The TMDB person ID.</param>
    /// <param name="force">Whether to remove the person even though it is still credited.</param>
    /// <param name="orphanedBefore">
    ///   Remove the person only when it was orphaned before this time; left
    ///   out, when it was orphaned for longer than the metadata settings say.
    /// </param>
    /// <returns>Whether the person was removed.</returns>
    public async Task<bool> PurgePerson(int personId, bool force = false, DateTime? orphanedBefore = null)
    {
        using (await GetLockForEntity(MetadataEntityType.Creator, personId, "metadata & images", "Purge"))
        {
            if (!force && IsPersonLinkedToOtherEntities(personId))
                return false;

            if (_tmdbPeople.GetByTmdbPersonID(personId) is { } person)
            {
                if (!person.LastOrphanedAt.HasValue)
                {
                    person.LastOrphanedAt = DateTime.UtcNow;
                    _tmdbPeople.Save(person);
                    _logger.LogDebug("Marked staff member as orphaned. (Person={PersonId})", personId);
                    return false;
                }

                // TMDB keeps its stamps in UTC.
                var cutoff = (orphanedBefore ?? DateTime.Now.AddDays(-_settingsProvider.GetSettings().Metadata.PurgeOrphanedAfterDays)).ToUniversalTime();
                if (person.LastOrphanedAt.Value >= cutoff)
                {
                    _logger.LogDebug("Staff member has not been orphaned for long enough yet. Skipping. (Person={PersonId})", personId);
                    return false;
                }
            }

            await PurgePersonInternal(personId).ConfigureAwait(false);

            return true;
        }
    }

    /// <summary>
    ///   Removes a person with its credits and image links, and queues a
    ///   refresh of the other shows and movies that credited it.
    /// </summary>
    /// <param name="personId">The TMDB person ID.</param>
    /// <param name="currentShowId">The show being refreshed, which is left out.</param>
    /// <param name="currentMovieId">The movie being refreshed, which is left out.</param>
    /// <returns>A task that completes once the person is removed.</returns>
    internal async Task PurgePersonInternal(int personId, int? currentShowId = null, int? currentMovieId = null)
    {
        var person = _tmdbPeople.GetByTmdbPersonID(personId);
        if (person is not null)
        {
            _logger.LogDebug("Removing staff member. (Person={PersonId})", personId);
            _tmdbPeople.Delete(person);
        }

        _imageService.PurgeImages(person ?? new() { TmdbPersonID = personId });

        PurgeTitlesAndOverviews(MetadataEntityType.Creator, personId);

        var movieCast = _tmdbMovieCast.GetByTmdbPersonID(personId);
        if (movieCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie cast roles for staff member. (Person={PersonId})", movieCast.Count, personId);
            _tmdbMovieCast.Delete(movieCast);
        }

        var movieCrew = _tmdbMovieCrew.GetByTmdbPersonID(personId);
        if (movieCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} movie crew roles for staff member. (Person={PersonId})", movieCrew.Count, personId);
            _tmdbMovieCrew.Delete(movieCrew);
        }

        var episodeCast = _tmdbEpisodeCast.GetByTmdbPersonID(personId);
        if (episodeCast.Count > 0)
        {
            _logger.LogDebug("Removing {count} show cast roles for staff member. (Person={PersonId})", episodeCast.Count, personId);
            _tmdbEpisodeCast.Delete(episodeCast);
        }

        var episodeCrew = _tmdbEpisodeCrew.GetByTmdbPersonID(personId);
        if (episodeCrew.Count > 0)
        {
            _logger.LogDebug("Removing {count} show crew roles for staff member. (Person={PersonId})", episodeCrew.Count, personId);
            _tmdbEpisodeCrew.Delete(episodeCrew);
        }

        var showIds = new HashSet<int>([
            ..episodeCast.Select(x => x.TmdbShowID),
            ..episodeCrew.Select(x => x.TmdbShowID),
        ]);
        if (currentShowId.HasValue)
            showIds.Remove(currentShowId.Value);
        if (showIds.Count > 0)
        {
            _logger.LogDebug("Scheduling {count} shows to be updated. (Person={PersonId})", showIds.Count, personId);
            foreach (var showId in showIds)
                await _refreshService.Value.RefreshEntry(
                    new(MetadataSource.TMDB, MetadataEntityType.Series, showId.ToString()),
                    options: new() { DownloadCrewAndCast = true }
                ).ConfigureAwait(false);
        }

        var movieIds = new HashSet<int>([
            ..movieCast.Select(x => x.TmdbMovieID),
            ..movieCrew.Select(x => x.TmdbMovieID),
        ]);
        if (currentMovieId.HasValue)
            movieIds.Remove(currentMovieId.Value);
        if (movieIds.Count > 0)
        {
            _logger.LogDebug("Scheduling {count} movies to be updated. (Person={PersonId})", movieIds.Count, personId);
            foreach (var movieId in movieIds)
                await _refreshService.Value.RefreshEntry(
                    new(MetadataSource.TMDB, MetadataEntityType.Movie, movieId.ToString()),
                    options: new() { DownloadCrewAndCast = true }
                ).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the set of all person IDs currently referenced by at least one cast or crew record
    /// across movies and episodes. Used as the single source of truth when deciding which
    /// <see cref="TMDB_Person"/> records are safe to purge, avoiding the old pattern of issuing
    /// up to 4 DB queries per person to check linkage.
    /// </summary>
    private HashSet<int> GetLinkedPersonIds()
    {
        var ids = new HashSet<int>();
        ids.UnionWith(_tmdbMovieCast.GetAll().Select(x => x.TmdbPersonID));
        ids.UnionWith(_tmdbMovieCrew.GetAll().Select(x => x.TmdbPersonID));
        ids.UnionWith(_tmdbEpisodeCast.GetAll().Select(x => x.TmdbPersonID));
        ids.UnionWith(_tmdbEpisodeCrew.GetAll().Select(x => x.TmdbPersonID));
        return ids;
    }

    private bool IsPersonLinkedToOtherEntities(int tmdbPersonId)
    {
        var movieCastLinks = _tmdbMovieCast.GetByTmdbPersonID(tmdbPersonId);
        if (movieCastLinks.Any())
            return true;

        var movieCrewLinks = _tmdbMovieCrew.GetByTmdbPersonID(tmdbPersonId);
        if (movieCrewLinks.Any())
            return true;

        var episodeCastLinks = _tmdbEpisodeCast.GetByTmdbPersonID(tmdbPersonId);
        if (episodeCastLinks.Any())
            return true;

        var episodeCrewLinks = _tmdbEpisodeCrew.GetByTmdbPersonID(tmdbPersonId);
        if (episodeCrewLinks.Any())
            return true;

        return false;
    }

    #endregion
}
