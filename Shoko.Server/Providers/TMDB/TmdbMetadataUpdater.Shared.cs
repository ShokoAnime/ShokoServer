using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;
using TMDbLib.Objects.General;

using TitleLanguage = Shoko.Abstractions.Metadata.Enums.TitleLanguage;

#pragma warning disable CS0618
// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

public partial class TmdbMetadataUpdater
{
    #region Suggestions

    /// <summary>
    ///   Stores what TMDB suggests for an entry, both its recommendations and
    ///   its similar titles. Both ride along on the entry's own request, so
    ///   this costs no extra calls.
    /// </summary>
    /// <param name="entityType">Whether this is a show or a movie.</param>
    /// <param name="entityID">The TMDB id of the entry.</param>
    /// <param name="lists">Each of TMDB's two lists, with the ids it holds.</param>
    /// <returns>Whether anything changed.</returns>
    private bool UpdateSuggestions(MetadataEntityType entityType, int entityID, IEnumerable<(SuggestionKind Kind, IEnumerable<int> SuggestedIDs)> lists)
    {
        var existing = _tmdbSuggestion.GetByTmdbEntityID(entityType, entityID)
            .ToDictionary(suggestion => (suggestion.Kind, suggestion.SuggestedTmdbEntityID));
        var toSave = new List<TMDB_Suggestion>();
        var toKeep = new HashSet<(SuggestionKind, int)>();
        foreach (var (kind, suggestedIDs) in lists)
        {
            var ordering = 0;
            foreach (var suggestedID in suggestedIDs)
            {
                // An entry cannot suggest itself, and a duplicate would fight over the same row.
                if (suggestedID == entityID || !toKeep.Add((kind, suggestedID)))
                    continue;

                var order = ordering++;
                if (existing.TryGetValue((kind, suggestedID), out var suggestion))
                {
                    if (suggestion.Ordering == order)
                        continue;

                    suggestion.Ordering = order;
                    toSave.Add(suggestion);
                    continue;
                }

                toSave.Add(new()
                {
                    TmdbEntityType = entityType,
                    TmdbEntityID = entityID,
                    SuggestedTmdbEntityID = suggestedID,
                    Kind = kind,
                    Ordering = order,
                });
            }
        }

        var toRemove = existing
            .Where(pair => !toKeep.Contains(pair.Key))
            .Select(pair => pair.Value)
            .ToList();
        if (toSave.Count is 0 && toRemove.Count is 0)
            return false;

        _tmdbSuggestion.Save(toSave);
        _tmdbSuggestion.Delete(toRemove);
        return true;
    }

    /// <summary>
    ///   Removes the suggestions a purged entry made, and the ones pointing at
    ///   it from entries we still have.
    /// </summary>
    /// <param name="entityType">Whether this is a show or a movie.</param>
    /// <param name="entityID">The TMDB id of the entry.</param>
    private void PurgeSuggestions(MetadataEntityType entityType, int entityID)
    {
        _tmdbSuggestion.Delete(_tmdbSuggestion.GetByTmdbEntityID(entityType, entityID));
        _tmdbSuggestion.Delete(_tmdbSuggestion.GetBySuggestedTmdbEntityID(entityType, entityID));
    }

    #endregion

    #region Titles & Overviews

    /// <summary>
    /// Updates the titles and overviews for the <paramref name="tmdbEntity"/>
    /// using the translation data available in the <paramref name="translations"/>.
    /// </summary>
    /// <param name="tmdbEntity">The local TMDB Entity to update titles and overviews for.</param>
    /// <param name="translations">The translations container returned from the API.</param>
    /// <param name="preferredTitleLanguages">The preferred title languages to store. If not set then we will store all languages.</param>
    /// <param name="preferredOverviewLanguages">The preferred overview languages to store. If not set then we will store all languages.</param>
    /// <param name="includeTitles">Whether to reconcile titles too. <c>false</c> for an entity that has none to speak of, such as a person: you would not translate a given name or a family name, so TMDB carries none and nothing would read any we wrote.</param>
    /// <param name="aliases">A person's other names, stored as its titles when <paramref name="includeTitles"/> is <c>false</c>, or <c>null</c> to leave its titles alone.</param>
    /// <returns>A boolean indicating if any changes were made to the titles and/or overviews.</returns>
    private bool UpdateTitlesAndOverviews(IEntityMetadata tmdbEntity, TranslationsContainer? translations, HashSet<TitleLanguage>? preferredTitleLanguages, HashSet<TitleLanguage>? preferredOverviewLanguages, bool includeTitles = true, IReadOnlyList<string>? aliases = null)
    {
        var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbEntity, translations, preferredTitleLanguages, preferredOverviewLanguages, includeTitles, aliases);
        return titlesUpdated || overviewsUpdated;
    }

    /// <summary>
    /// Updates the titles and overviews for the <paramref name="tmdbEntity"/>
    /// using the translation data available in the <paramref name="translations"/>.
    /// </summary>
    /// <remarks>
    ///   The English title and overview stay on the entity's row. A listed
    ///   American English translation equal to them is not stored again; the
    ///   entity's flags say it was listed, and a gap in the stored positions
    ///   says where. A generic episode title with the episode's own number is
    ///   not stored either. Translations already stored keep their place, and
    ///   new ones follow in TMDB's order. The alternative titles TMDB marks
    ///   as transcribed follow, at most one per transcription language.
    /// </remarks>
    /// <param name="tmdbEntity">The local TMDB Entity to update titles and overviews for.</param>
    /// <param name="translations">The translations container returned from the API.</param>
    /// <param name="preferredTitleLanguages">The preferred title languages to store. If not set then we will store all languages.</param>
    /// <param name="preferredOverviewLanguages">The preferred overview languages to store. If not set then we will store all languages.</param>
    /// <param name="includeTitles">Whether to reconcile titles too. <c>false</c> for an entity that has none to speak of, such as a person: you would not translate a given name or a family name, so TMDB carries none and nothing would read any we wrote.</param>
    /// <param name="aliases">A person's other names, stored as its titles when <paramref name="includeTitles"/> is <c>false</c>, or <c>null</c> to leave its titles alone.</param>
    /// <param name="alternativeTitles">The alternative titles returned from the API, for a show or a movie, or <c>null</c>.</param>
    /// <returns>A tuple indicating if any changes were made to the titles and/or overviews.</returns>
    private (bool titlesUpdated, bool overviewsUpdated) UpdateTitlesAndOverviewsWithTuple(IEntityMetadata tmdbEntity, TranslationsContainer? translations, HashSet<TitleLanguage>? preferredTitleLanguages, HashSet<TitleLanguage>? preferredOverviewLanguages, bool includeTitles = true, IReadOnlyList<string>? aliases = null, IReadOnlyList<AlternativeTitle>? alternativeTitles = null)
    {
        var entityID = new MetadataGuid(MetadataSource.TMDB, tmdbEntity.Type, tmdbEntity.Id.ToString());
        var listedTitles = new List<TmdbTextListing.ListedText>();
        var listedOverviews = new List<TmdbTextListing.ListedText>();
        foreach (var translation in translations?.Translations ?? [new() { EnglishName = string.Empty, Iso_3166_1 = "US", Iso_639_1 = "en", Data = new() { Name = string.Empty, Overview = string.Empty } }])
        {
            if (translation.Iso_639_1 is null || translation.Iso_3166_1 is null)
                continue;

            var languageCode = translation.Iso_639_1.ToLowerInvariant();
            var countryCode = translation.Iso_3166_1.ToUpperInvariant();

            var alwaysInclude = false;
            var currentTitle = translation.Data?.Name ?? string.Empty;
            if (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode == tmdbEntity.OriginalLanguageCode)
            {
                currentTitle = tmdbEntity.OriginalTitle ?? string.Empty;
                alwaysInclude = true;
            }
            else if (languageCode == "en" && countryCode == "US")
            {
                currentTitle = tmdbEntity.EnglishTitle ?? string.Empty;
                alwaysInclude = true;
            }

            var shouldInclude = alwaysInclude || preferredTitleLanguages is null || preferredTitleLanguages.Contains(languageCode.GetTitleLanguage()) || preferredTitleLanguages.Contains(languageCode.GetTitleLanguage(countryCode));
            if (includeTitles && shouldInclude && !string.IsNullOrEmpty(currentTitle) && !(
                // Make sure the "translation" is not just the English Title or
                (languageCode != "en" && languageCode != "US" && !string.IsNullOrEmpty(tmdbEntity.EnglishTitle) && string.Equals(tmdbEntity.EnglishTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase)) ||
                // the Original Title.
                (!string.IsNullOrEmpty(tmdbEntity.OriginalLanguageCode) && languageCode != tmdbEntity.OriginalLanguageCode && !string.IsNullOrEmpty(tmdbEntity.OriginalTitle) && string.Equals(tmdbEntity.OriginalTitle, currentTitle, StringComparison.InvariantCultureIgnoreCase))
            ))
                listedTitles.Add(new(languageCode, countryCode, currentTitle));

            alwaysInclude = false;
            var currentOverview = translation.Data?.Overview ?? string.Empty;
            if (languageCode == "en" && countryCode == "US")
            {
                alwaysInclude = true;
                currentOverview = tmdbEntity.EnglishOverview ?? translation.Data?.Overview ?? string.Empty;
            }

            shouldInclude = alwaysInclude || preferredOverviewLanguages is null || preferredOverviewLanguages.Contains(languageCode.GetTitleLanguage()) || preferredOverviewLanguages.Contains(languageCode.GetTitleLanguage(countryCode));
            if (shouldInclude && !string.IsNullOrEmpty(currentOverview))
                listedOverviews.Add(new(languageCode, countryCode, currentOverview));
        }

        if (includeTitles)
            listedTitles.AddRange(TmdbTextListing.TranscribedTitles(alternativeTitles, tmdbEntity.OriginalLanguageCode)
                .Where(title => preferredTitleLanguages is null || preferredTitleLanguages.Contains(TmdbTextListing.Language(title.LanguageCode, title.CountryCode))));

        IReadOnlyList<ITitle>? titles = null;
        int? titleGap = null;
        var titleFlagChanged = false;
        if (includeTitles)
        {
            var (listed, gap) = TmdbTextListing.Plan(
                _textStore.GetTitles(entityID, MetadataSource.TMDB),
                tmdbEntity.EnglishTitleListed,
                listedTitles,
                tmdbEntity.EnglishTitle,
                (tmdbEntity as TMDB_Episode)?.EpisodeNumber
            );
            titleGap = gap;
            titles = [.. listed.Select(TmdbTextListing.ToTitle)];
            titleFlagChanged = tmdbEntity.EnglishTitleListed != titleGap.HasValue;
            tmdbEntity.EnglishTitleListed = titleGap.HasValue;
        }
        else if (aliases is not null)
        {
            titles =
            [
                .. aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Select(alias => new TitleStub
                {
                    Source = MetadataSource.TMDB,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = alias,
                    Type = TitleType.Synonym,
                }),
            ];
        }

        var (listedOverviewsInOrder, overviewGap) = TmdbTextListing.Plan(
            _textStore.GetOverviews(entityID, MetadataSource.TMDB),
            tmdbEntity.EnglishOverviewListed,
            listedOverviews,
            tmdbEntity.EnglishOverview
        );
        IReadOnlyList<IText> overviews = [.. listedOverviewsInOrder.Select(TmdbTextListing.ToOverview)];
        var overviewFlagChanged = tmdbEntity.EnglishOverviewListed != overviewGap.HasValue;
        tmdbEntity.EnglishOverviewListed = overviewGap.HasValue;

        var changed = _textStore.WriteListedTexts([new(entityID, titles, titleGap, overviews, overviewGap)]);
        var titlesUpdated = titleFlagChanged || changed.Contains((entityID, TextKind.Title));
        var overviewsUpdated = overviewFlagChanged || changed.Contains((entityID, TextKind.Overview));
        _logger.LogDebug(
            "Stored {TitleCount} titles and {OverviewCount} overviews for {type} {EntityTitle} ({EntityType}={EntityId}); titles changed: {TitlesUpdated}, overviews changed: {OverviewsUpdated}",
            titles?.Count,
            overviews.Count,
            tmdbEntity.Type.ToString().ToLowerInvariant(),
            tmdbEntity.OriginalTitle ?? tmdbEntity.EnglishTitle ?? $"<untitled {tmdbEntity.Type.ToString().ToLowerInvariant()}>",
            tmdbEntity.Type.ToString(),
            tmdbEntity.Id,
            titlesUpdated,
            overviewsUpdated);

        return (titlesUpdated, overviewsUpdated);
    }

    /// <summary>
    ///   Removes every title and overview of a TMDB entity, with every pick of
    ///   them.
    /// </summary>
    /// <param name="foreignType">The kind of entity.</param>
    /// <param name="foreignId">The TMDB ID.</param>
    private void PurgeTitlesAndOverviews(MetadataEntityType foreignType, int foreignId)
    {
        var removed = _textStore.RemoveEntry(new(MetadataSource.TMDB, foreignType, foreignId.ToString()));
        _logger.LogDebug(
            "Removed {Count} titles and overviews for {type} with id {EntityId}",
            removed,
            foreignType.ToString().ToLowerInvariant(),
            foreignId);
    }

    #endregion

    #region Companies

    private async Task<bool> UpdateCompanies(IEntityMetadata tmdbEntity, List<ProductionCompany> companies)
    {
        var existingXrefs = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(tmdbEntity.Type, tmdbEntity.Id)
            .GroupBy(xref => xref.TmdbCompanyID)
            .ToDictionary(xref => xref.Key, groupBy => groupBy.ToList());
        var xrefsToAdd = 0;
        var xrefsToSkip = new HashSet<int>();
        var xrefsToSave = new List<TMDB_Company_Entity>();
        var indexCounter = 0;
        foreach (var company in companies)
        {
            var currentIndex = indexCounter++;
            if (existingXrefs.TryGetValue(company.Id, out var existingXrefList))
            {
                var existingXref = existingXrefList[0];
                if (existingXref.Ordering != currentIndex || existingXref.ReleasedAt != tmdbEntity.ReleasedAt)
                {
                    existingXref.Ordering = currentIndex;
                    existingXref.ReleasedAt = tmdbEntity.ReleasedAt;
                    xrefsToSave.Add(existingXref);
                }
                xrefsToSkip.Add(existingXref.TMDB_Company_EntityID);
            }
            else
            {
                xrefsToAdd++;
                xrefsToSave.Add(new(company.Id, tmdbEntity.Type, tmdbEntity.Id, currentIndex, tmdbEntity.ReleasedAt));
            }

            await UpdateCompany(company);
        }
        var xrefsToRemove = existingXrefs.Values
            .SelectMany(xrefs => xrefs)
            .ExceptBy(xrefsToSkip, o => o.TMDB_Company_EntityID)
            .ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {oa}/{ou}/{or}/{os} company cross-references for {type} {EntityTitle} ({EntityType}={EntityId})",
            xrefsToAdd,
            xrefsToSave.Count - xrefsToAdd,
            xrefsToRemove.Count,
            xrefsToSkip.Count + xrefsToAdd - xrefsToSave.Count,
            tmdbEntity.Type.ToString().ToLowerInvariant(),
            tmdbEntity.OriginalTitle,
            tmdbEntity.Type.ToString(),
            tmdbEntity.Id);

        _xrefTmdbCompanyEntity.Save(xrefsToSave);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }


        return false;
    }

    private async Task UpdateCompany(ProductionCompany company)
    {
        var tmdbCompany = _tmdbCompany.GetByTmdbCompanyID(company.Id) ?? new(company.Id);
        var updated = tmdbCompany.Populate(company);
        if (updated)
        {
            _logger.LogDebug("Updating studio. (Company={CompanyId})", company.Id);
            _tmdbCompany.Save(tmdbCompany);
        }

        var settings = _settingsProvider.GetSettings();
        if (!string.IsNullOrEmpty(company.LogoPath))
            await _imageService.DownloadImageByType(company.LogoPath, ImageEntityType.Primary, tmdbCompany, isDesired: settings.Image.GetMetadataSourceSettings(MetadataSource.TMDB).AutoDownloadStudioImages);
    }

    private void PurgeCompany(int companyId)
    {
        var tmdbCompany = _tmdbCompany.GetByTmdbCompanyID(companyId);
        if (tmdbCompany is not null)
        {
            _logger.LogDebug("Removing studio. (Company={CompanyId})", companyId);
            _tmdbCompany.Delete(tmdbCompany);
        }

        _imageService.PurgeImages(tmdbCompany ?? new() { TmdbCompanyID = companyId });

        var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(companyId);
        if (xrefs.Count > 0)
        {
            _logger.LogDebug("Removing {count} cross-references for studio. (Company={CompanyId})", xrefs.Count, companyId);
            _xrefTmdbCompanyEntity.Delete(xrefs);
        }
    }

    #endregion

    #region Purge (Leftovers)

    /// <summary>
    ///   Removes what TMDB's tables still hold for shows, movies and
    ///   collections that have no row of their own and that nothing links
    ///   to, such as rows an interrupted purge left behind.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many shows, movies and collections were cleared.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<int> PurgeLeftovers(CancellationToken cancellationToken = default)
    {
        var removed = 0;
        var shows = new HashSet<int>([
            .. _tmdbSeasons.GetAll().Select(season => season.TmdbShowID),
            .. _tmdbEpisodes.GetAll().Select(episode => episode.TmdbShowID),
            .. _tmdbEpisodeCast.GetAllTmdbShowIDs(),
            .. _tmdbEpisodeCrew.GetAllTmdbShowIDs(),
            .. _xrefTmdbCompanyEntity.GetAllTmdbEntityIDs(MetadataEntityType.Series),
            .. _xrefTmdbShowNetwork.GetAll().Select(xref => xref.TmdbShowID),
            .. _tmdbAlternateOrdering.GetAllTmdbShowIDs(),
            .. _tmdbAlternateOrderingSeasons.GetAllTmdbShowIDs(),
            .. _tmdbAlternateOrderingEpisodes.GetAllTmdbShowIDs(),
        ]);
        foreach (var showId in shows.Where(IsLeftoverShow))
        {
            var entry = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, showId.ToString());
            using var entryLock = await _entryLocks.Acquire(entry, cancellationToken).ConfigureAwait(false);
            using var imagesLock = await _entryLocks.AcquireImages(entry, cancellationToken).ConfigureAwait(false);
            if (!IsLeftoverShow(showId))
                continue;

            _logger.LogInformation("Removing what is left of TMDB show {ShowId}, which has no row of its own.", showId);
            await PurgeShow(showId).ConfigureAwait(false);
            removed++;
        }

        var movies = new HashSet<int>([
            .. _tmdbMovieCast.GetAllTmdbMovieIDs(),
            .. _tmdbMovieCrew.GetAllTmdbMovieIDs(),
            .. _xrefTmdbCompanyEntity.GetAllTmdbEntityIDs(MetadataEntityType.Movie),
            .. _xrefTmdbCollectionMovies.GetAll().Select(xref => xref.TmdbMovieID),
        ]);
        foreach (var movieId in movies.Where(IsLeftoverMovie))
        {
            var entry = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, movieId.ToString());
            using var entryLock = await _entryLocks.Acquire(entry, cancellationToken).ConfigureAwait(false);
            using var imagesLock = await _entryLocks.AcquireImages(entry, cancellationToken).ConfigureAwait(false);
            if (!IsLeftoverMovie(movieId))
                continue;

            _logger.LogInformation("Removing what is left of TMDB movie {MovieId}, which has no row of its own.", movieId);
            await PurgeMovie(movieId).ConfigureAwait(false);
            removed++;
        }

        var collections = _xrefTmdbCollectionMovies.GetAll().Select(xref => xref.TmdbCollectionID).ToHashSet();
        foreach (var collectionId in collections.Where(IsLeftoverCollection))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (await GetLockForEntity(MetadataEntityType.Collection, collectionId, "metadata", "Purge").ConfigureAwait(false))
            {
                if (!IsLeftoverCollection(collectionId))
                    continue;

                _logger.LogInformation("Removing what is left of TMDB collection {CollectionId}, which has no row of its own.", collectionId);
                PurgeCollectionUnderLock(collectionId);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    ///   Whether a show ID found in TMDB's child tables has no show row and no
    ///   link.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns><see langword="true"/> when only leftovers name it.</returns>
    private bool IsLeftoverShow(int showId)
        => _tmdbShows.GetByTmdbShowID(showId) is null && _xrefAnidbTmdbShows.GetByTmdbShowID(showId).Count is 0;

    /// <summary>
    ///   Whether a movie ID found in TMDB's child tables has no movie row and
    ///   no link.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns><see langword="true"/> when only leftovers name it.</returns>
    private bool IsLeftoverMovie(int movieId)
        => _tmdbMovies.GetByTmdbMovieID(movieId) is null && _xrefAnidbTmdbMovies.GetByTmdbMovieID(movieId).Count is 0;

    /// <summary>
    ///   Whether a collection ID found in the collection membership rows has
    ///   no collection row and no linked movie.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <returns><see langword="true"/> when only leftovers name it.</returns>
    private bool IsLeftoverCollection(int collectionId)
        => _tmdbCollections.GetByTmdbCollectionID(collectionId) is null && !IsCollectionInUse(collectionId);

    #endregion
}
