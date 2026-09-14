using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Anilist.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Scheduling.Jobs.Anilist;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Service for managing links between AniDB and AniList anime. Mirrors
/// <see cref="TMDB.TmdbLinkingService"/> wherever the two providers overlap.
/// AniList has no seasons, no movies and no episode titles, so the episode
/// matching only has the airing schedule to work with. Episode numbers are
/// deliberately not used as evidence on their own, for the same reason they
/// aren't for TMDB: the two sources can and do number episodes differently.
/// A number that agrees with a schedule date match does raise that match.
/// </summary>
public class AnilistLinkingService : IAnilistLinkingService
{
    private readonly ILogger<AnilistLinkingService> _logger;

    private readonly IQueueScheduler _scheduler;

    private readonly ISettingsProvider _settingsProvider;

    private readonly AnimeSeriesRepository _animeSeries;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly AniDB_EpisodeRepository _anidbEpisodes;

    private readonly Anilist_AnimeRepository _anilistAnime;

    private readonly Anilist_EpisodeRepository _anilistEpisodes;

    private readonly CrossRef_AniDB_Anilist_AnimeRepository _xrefAnidbAnilistAnime;

    private readonly CrossRef_AniDB_Anilist_EpisodeRepository _xrefAnidbAnilistEpisodes;

    public AnilistLinkingService(
        ILogger<AnilistLinkingService> logger,
        IQueueScheduler scheduler,
        ISettingsProvider settingsProvider,
        AnimeSeriesRepository animeSeries,
        AniDB_AnimeRepository anidbAnime,
        AniDB_EpisodeRepository anidbEpisodes,
        Anilist_AnimeRepository anilistAnime,
        Anilist_EpisodeRepository anilistEpisodes,
        CrossRef_AniDB_Anilist_AnimeRepository xrefAnidbAnilistAnime,
        CrossRef_AniDB_Anilist_EpisodeRepository xrefAnidbAnilistEpisodes
    )
    {
        _logger = logger;
        _scheduler = scheduler;
        _settingsProvider = settingsProvider;
        _animeSeries = animeSeries;
        _anidbAnime = anidbAnime;
        _anidbEpisodes = anidbEpisodes;
        _anilistAnime = anilistAnime;
        _anilistEpisodes = anilistEpisodes;
        _xrefAnidbAnilistAnime = xrefAnidbAnilistAnime;
        _xrefAnidbAnilistEpisodes = xrefAnidbAnilistEpisodes;
    }

    #region Shared

    /// <inheritdoc/>
    public void RemoveAllLinks()
    {
        _logger.LogInformation("Removing all AniDB - AniList links.");

        var animeXrefs = _xrefAnidbAnilistAnime.GetAll();
        _logger.LogInformation("Removing {Count} AniList anime links.", animeXrefs.Count);
        if (animeXrefs.Count > 0)
            _xrefAnidbAnilistAnime.Delete(animeXrefs);

        var episodeXrefs = _xrefAnidbAnilistEpisodes.GetAll();
        _logger.LogInformation("Removing {Count} AniList episode links.", episodeXrefs.Count);
        if (episodeXrefs.Count > 0)
            _xrefAnidbAnilistEpisodes.Delete(episodeXrefs);

        _logger.LogInformation("Done removing AniDB - AniList links.");
    }

    private void ResetSeriesTitlesAndOverview(int anidbAnimeId)
    {
        if (_animeSeries.GetByAnimeID(anidbAnimeId) is not { } series)
            return;

        series.ResetAnimeTitles();
        series.ResetPreferredTitle();
        series.ResetPreferredOverview();
        series.ResetAnilistAirTimeOffset();
    }

    // Only the overview descends into the episode links. The titles come from
    // the linked anime alone.
    private void ResetSeriesOverview(int anidbAnimeId)
    {
        if (_animeSeries.GetByAnimeID(anidbAnimeId) is not { } series)
            return;

        series.ResetPreferredOverview();
    }

    /// <inheritdoc/>
    public void ResetAutoLinkingState(bool disabled = false)
    {
        var series = _animeSeries.GetAll();
        var count = series.Count;
        if (disabled)
            _logger.LogInformation("Disabling AniList auto-linking for {Count} Shoko series.", count);
        else
            _logger.LogInformation("Enabling AniList auto-linking for {Count} Shoko series.", count);

        var itemNo = 0;
        foreach (var seriesItem in series)
        {
            seriesItem.IsAnilistAutoMatchingDisabled = disabled;
            _animeSeries.Save(seriesItem, false, false);

            if (++itemNo % 100 == 0)
            {
                if (disabled)
                    _logger.LogInformation("Disabling AniList auto-linking for {Count} Shoko series. (Processed {Processed})", count, itemNo);
                else
                    _logger.LogInformation("Enabling AniList auto-linking for {Count} Shoko series. (Processed {Processed})", count, itemNo);
            }
        }
    }

    private void DisableAutoMatchingForSeries(int anidbAnimeId)
    {
        // Disable auto-matching when we remove an existing match for the series.
        if (_animeSeries.GetByAnimeID(anidbAnimeId) is not { IsAnilistAutoMatchingDisabled: false } series)
            return;

        series.IsAnilistAutoMatchingDisabled = true;
        _animeSeries.Save(series, false, false);
    }

    #endregion

    #region Anime Links

    /// <inheritdoc/>
    public async Task AddAnimeLink(int anidbAnimeId, int anilistAnimeId, bool additiveLink = true, MatchRating matchRating = MatchRating.UserVerified)
    {
        // Remove all existing links.
        if (!additiveLink)
            await RemoveAllAnimeLinksForAnidbAnime(anidbAnimeId).ConfigureAwait(false);

        // Add or update the link.
        _logger.LogInformation("Adding AniList anime link: AniDB (AnimeID={AnidbID}) → AniList anime (ID={AnilistID})", anidbAnimeId, anilistAnimeId);
        var xref = _xrefAnidbAnilistAnime.GetByAnidbAnimeAndAnilistAnimeIDs(anidbAnimeId, anilistAnimeId) ??
            new(anidbAnimeId, anilistAnimeId);
        xref.MatchRating = matchRating;
        _xrefAnidbAnilistAnime.Save(xref);
        await Task.Run(() => MatchAnidbToAnilistEpisodes(anidbAnimeId, anilistAnimeId, true, true)).ConfigureAwait(false);
        ResetSeriesTitlesAndOverview(anidbAnimeId);
    }

    /// <inheritdoc/>
    public async Task RemoveAnimeLink(int anidbAnimeId, int anilistAnimeId, bool purge = false)
    {
        var xref = _xrefAnidbAnilistAnime.GetByAnidbAnimeAndAnilistAnimeIDs(anidbAnimeId, anilistAnimeId);
        if (xref is null)
            return;

        DisableAutoMatchingForSeries(anidbAnimeId);
        await RemoveAnimeLink(xref, purge).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveAllAnimeLinksForAnidbAnime(int anidbAnimeId, bool purge = false)
    {
        var xrefs = _xrefAnidbAnilistAnime.GetByAnidbAnimeID(anidbAnimeId);
        _logger.LogInformation("Removing {Count} AniList anime links for AniDB anime. (AnimeID={AnimeID})", xrefs.Count, anidbAnimeId);
        if (xrefs.Count == 0)
            return;

        DisableAutoMatchingForSeries(anidbAnimeId);
        foreach (var xref in xrefs)
            await RemoveAnimeLink(xref, purge).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveAllAnimeLinksForAnilistAnime(int anilistAnimeId)
    {
        var xrefs = _xrefAnidbAnilistAnime.GetByAnilistAnimeID(anilistAnimeId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveAnimeLink(xref, false).ConfigureAwait(false);
    }

    private async Task RemoveAnimeLink(CrossRef_AniDB_Anilist_Anime xref, bool purge = false)
    {
        _logger.LogInformation("Removing AniList anime link: AniDB anime (AnimeID={AnidbID}) → AniList anime (ID={AnilistID})", xref.AnidbAnimeID, xref.AnilistAnimeID);
        _xrefAnidbAnilistAnime.Delete(xref);

        var xrefs = _xrefAnidbAnilistEpisodes.GetOnlyByAnidbAnimeAndAnilistAnimeIDs(xref.AnidbAnimeID, xref.AnilistAnimeID).ToList();
        // When removing the last anime link, also remove floating episode xrefs (AnilistAnimeID=0)
        // created by ResetAllEpisodeLinks. Only do this when no links remain so we don't
        // accidentally delete xrefs that still belong to surviving anime links.
        if (_xrefAnidbAnilistAnime.GetByAnidbAnimeID(xref.AnidbAnimeID).Count == 0)
            xrefs.AddRange(_xrefAnidbAnilistEpisodes.GetOnlyByAnidbAnimeAndAnilistAnimeIDs(xref.AnidbAnimeID, 0));
        _logger.LogInformation("Removing {XRefsCount} episode cross-references for AniDB anime (AnimeID={AnidbID}) and AniList anime (ID={AnilistID})", xrefs.Count, xref.AnidbAnimeID, xref.AnilistAnimeID);
        if (xrefs.Count > 0)
            _xrefAnidbAnilistEpisodes.Delete(xrefs);
        ResetSeriesTitlesAndOverview(xref.AnidbAnimeID);
        if (purge)
            await _scheduler.StartJob<PurgeAnilistAnimeJob>(c => c.AnilistAnimeID = xref.AnilistAnimeID).ConfigureAwait(false);
    }

    #endregion

    #region Episode Links

    /// <inheritdoc/>
    public void ResetAllEpisodeLinks(int anidbAnimeId, bool allowAuto)
    {
        var hasXrefs = _xrefAnidbAnilistAnime.GetByAnidbAnimeID(anidbAnimeId).Count > 0;
        if (hasXrefs)
        {
            var xrefs = _xrefAnidbAnilistEpisodes.GetByAnidbAnimeID(anidbAnimeId);
            var toSave = new List<CrossRef_AniDB_Anilist_Episode>();
            var toDelete = new List<CrossRef_AniDB_Anilist_Episode>();

            // Reset existing xrefs.
            var existingIDs = new HashSet<int>();
            foreach (var xref in xrefs)
            {
                if (existingIDs.Add(xref.AnidbEpisodeID))
                {
                    xref.AnilistAnimeID = 0;
                    xref.AnilistEpisodeID = 0;
                    xref.EpisodeNumber = 0;
                    xref.Ordering = 0;
                    xref.MatchRating = allowAuto ? MatchRating.None : MatchRating.UserVerified;
                    toSave.Add(xref);
                }
                else
                {
                    toDelete.Add(xref);
                }
            }

            // Add missing xrefs.
            var anidbEpisodesWithoutXrefs = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
                .Where(episode => !existingIDs.Contains(episode.EpisodeID) && episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special)
                .ToList();
            foreach (var anidbEpisode in anidbEpisodesWithoutXrefs)
                toSave.Add(new(anidbAnimeId, anidbEpisode.EpisodeID, 0, 0, 0, allowAuto ? MatchRating.None : MatchRating.UserVerified));

            // Save the changes.
            if (toSave.Count > 0)
                _xrefAnidbAnilistEpisodes.Save(toSave);
            if (toDelete.Count > 0)
                _xrefAnidbAnilistEpisodes.Delete(toDelete);
        }
        else
        {
            // Remove all episode cross-references if no anime is linked.
            var xrefs = _xrefAnidbAnilistEpisodes.GetByAnidbAnimeID(anidbAnimeId);
            if (xrefs.Count > 0)
                _xrefAnidbAnilistEpisodes.Delete(xrefs);
        }

        ResetSeriesOverview(anidbAnimeId);
    }

    /// <inheritdoc/>
    public bool SetEpisodeLink(int anidbEpisodeId, int anilistEpisodeId, bool additiveLink = true, int? index = null)
    {
        var anidbEpisode = _anidbEpisodes.GetByEpisodeID(anidbEpisodeId);
        if (anidbEpisode is null)
            return false;

        // Set an empty link.
        if (anilistEpisodeId is 0)
        {
            var xrefs = _xrefAnidbAnilistEpisodes.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisode.AnimeID, anidbEpisodeId, 0, 0, 0);
            toSave.AnilistAnimeID = 0;
            toSave.AnilistEpisodeID = 0;
            toSave.EpisodeNumber = 0;
            toSave.Ordering = 0;
            toSave.MatchRating = MatchRating.UserVerified;
            var toDelete = xrefs.Skip(1).ToList();
            _xrefAnidbAnilistEpisodes.Save(toSave);
            if (toDelete.Count > 0)
                _xrefAnidbAnilistEpisodes.Delete(toDelete);
            ResetSeriesOverview(anidbEpisode.AnimeID);

            return true;
        }

        var anilistEpisode = _anilistEpisodes.GetByAnilistEpisodeID(anilistEpisodeId);
        if (anilistEpisode is null)
            return false;

        // Add another link
        if (additiveLink)
        {
            var toSave = _xrefAnidbAnilistEpisodes.GetByAnidbEpisodeAndAnilistEpisodeIDs(anidbEpisodeId, anilistEpisodeId)
                ?? new(anidbEpisode.AnimeID, anidbEpisodeId, anilistEpisode.AnilistAnimeID, anilistEpisodeId, anilistEpisode.EpisodeNumber);
            var existingAnidbLinks = _xrefAnidbAnilistEpisodes.GetByAnidbEpisodeID(anidbEpisodeId).MaxBy(x => x.Ordering) is { } x1 ? x1.Ordering + 1 : 0;
            var existingAnilistLinks = _xrefAnidbAnilistEpisodes.GetByAnilistEpisodeID(anilistEpisodeId).MaxBy(x => x.Ordering) is { } x2 ? x2.Ordering + 1 : 0;
            if (toSave.CrossRef_AniDB_Anilist_EpisodeID == 0 && !index.HasValue)
                index = existingAnidbLinks > 0 ? existingAnidbLinks : existingAnilistLinks > 0 ? existingAnilistLinks : 0;
            if (index.HasValue)
                toSave.Ordering = index.Value;
            toSave.EpisodeNumber = anilistEpisode.EpisodeNumber;
            toSave.MatchRating = MatchRating.UserVerified;
            _xrefAnidbAnilistEpisodes.Save(toSave);
        }
        else
        {
            var xrefs = _xrefAnidbAnilistEpisodes.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisode.AnimeID, anidbEpisodeId, anilistEpisode.AnilistAnimeID, anilistEpisodeId, anilistEpisode.EpisodeNumber);
            toSave.AnilistAnimeID = anilistEpisode.AnilistAnimeID;
            toSave.AnilistEpisodeID = anilistEpisode.AnilistEpisodeID;
            toSave.EpisodeNumber = anilistEpisode.EpisodeNumber;
            if (!index.HasValue && anidbEpisode.EpisodeNumber is > 0 &&
                _anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber - 1).FirstOrDefault() is { } previousEpisode)
            {
                var previousXrefs = _xrefAnidbAnilistEpisodes.GetByAnidbEpisodeID(previousEpisode.EpisodeID);
                if (previousXrefs.Count is 1 && previousXrefs[0].AnilistEpisodeID == anilistEpisodeId)
                    index = previousXrefs[0].Ordering + 1;
            }
            toSave.Ordering = index ?? 0;
            toSave.MatchRating = MatchRating.UserVerified;
            var toDelete = xrefs.Skip(1).ToList();
            _xrefAnidbAnilistEpisodes.Save(toSave);
            if (toDelete.Count > 0)
                _xrefAnidbAnilistEpisodes.Delete(toDelete);
        }

        ResetSeriesOverview(anidbEpisode.AnimeID);
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAnilistEpisodeCrossReference> MatchAnidbToAnilistEpisodes(int anidbAnimeId, int anilistAnimeId, bool useExisting = false, bool saveToDatabase = false, bool? useExistingOtherAnime = null)
    {
        var anime = _anidbAnime.GetByAnimeID(anidbAnimeId);
        if (anime is null)
            return [];

        var anilistAnime = _anilistAnime.GetByAnilistAnimeID(anilistAnimeId);
        if (anilistAnime is null)
            return [];

        var startedAt = DateTime.Now;
        _logger.LogTrace("Mapping AniDB Anime {AnidbAnimeId} to AniList Anime {AnilistAnimeId} (Use Existing: {UseExisting}, Save To Database: {SaveToDatabase})", anidbAnimeId, anilistAnimeId, useExisting, saveToDatabase);

        // Mapping logic
        var toSkip = new HashSet<int>();
        var toAdd = new List<CrossRef_AniDB_Anilist_Episode>();
        var crossReferences = new List<CrossRef_AniDB_Anilist_Episode>();
        var secondPass = new List<AniDB_Episode>();
        var thirdPass = new List<AniDB_Episode>();
        var fourthPass = new List<AniDB_Episode>();
        var existing = _xrefAnidbAnilistEpisodes.GetAllByAnidbAnimeAndAnilistAnimeIDs(anidbAnimeId, anilistAnimeId)
            .GroupBy(xref => xref.AnidbEpisodeID)
            .ToDictionary(grouped => grouped.Key, grouped => grouped.ToList());
        var anidbEpisodes = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
            .Where(episode => episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToDictionary(episode => episode.EpisodeID);
        var anilistEpisodeDict = _anilistEpisodes.GetByAnilistAnimeID(anilistAnimeId)
            .ToDictionary(episode => episode.AnilistEpisodeID);
        // AniList has no seasons and no specials, so there's a single candidate pool.
        var anilistEpisodes = anilistEpisodeDict.Values
            .OrderBy(episode => episode.EpisodeNumber)
            .ToList();
        var considerExistingOtherLinks = useExistingOtherAnime ?? _settingsProvider.GetSettings().Anilist.ConsiderExistingOtherLinks;
        if (considerExistingOtherLinks)
        {
            var otherAnimeExisting = existing.Values.SelectMany(xref => xref).ExceptBy(anidbEpisodes.Keys.Append(0), xref => xref.AnidbEpisodeID).ToList();
            foreach (var link in otherAnimeExisting)
            {
                _logger.LogTrace("Skipping existing episode link: AniDB episode (EpisodeID={EpisodeID}, AnimeID={AnimeID}) → AniList episode (EpisodeID={AnilistID})", link.AnidbEpisodeID, link.AnidbAnimeID, link.AnilistEpisodeID);

                // Exclude the linked episodes from the auto-match candidates.
                var index = anilistEpisodes.FindIndex(episode => episode.AnilistEpisodeID == link.AnilistEpisodeID);
                if (index >= 0)
                    anilistEpisodes.RemoveAt(index);
            }
        }

        var matchContext = new EpisodeMatchContext(anime, anilistAnime, anilistEpisodes);

        // Runs a deferred pass: episodes whose match satisfies `accepts` are linked and removed from
        // the shared candidate pool; the rest are queued into `overflow` for the next pass.
        void RunDeferredPass(int passNumber, string passLabel, List<AniDB_Episode> episodes, Func<MatchRating, bool> accepts, List<AniDB_Episode> overflow)
        {
            var passCurrent = 0;
            foreach (var (episode, cachedCrossRef) in RankByConfidence(episodes, matchContext))
            {
                passCurrent++;
                _logger.LogTrace("Linking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {EpisodeID}, Progress: {Current}/{Total}, Pass: {PassNumber}/4)", episode.EpisodeType, episode.EpisodeNumber, episode.EpisodeID, passCurrent, episodes.Count, passNumber);
                var crossRef = ResolveRankedMatch(episode, cachedCrossRef, matchContext);
                if (accepts(crossRef.MatchRating))
                {
                    var index = anilistEpisodes.FindIndex(episode => episode.AnilistEpisodeID == crossRef.AnilistEpisodeID);
                    if (index != -1)
                        anilistEpisodes.RemoveAt(index);

                    crossReferences.Add(crossRef);
                    toAdd.Add(crossRef);
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, AniList ID: {AnilistEpisodeID}, Rating: {MatchRating}, Pass: {PassNumber}/4)", episode.EpisodeID, crossRef.AnilistEpisodeID, crossRef.MatchRating, passNumber);
                }
                else
                {
                    _logger.LogTrace("Skipping episode in the {PassLabel} pass. (AniDB ID: {AnidbEpisodeID}, AniList ID: {AnilistEpisodeID}, Rating: {MatchRating}, Pass: {PassNumber}/4)", passLabel, episode.EpisodeID, crossRef.AnilistEpisodeID, crossRef.MatchRating, passNumber);
                    overflow.Add(episode);
                }
            }
        }

        // Links every remaining normal episode that lands on the offset the date-matched anchors agree
        // on, and returns the episodes it couldn't place for the next pass.
        List<AniDB_Episode> RunOffsetPass(List<AniDB_Episode> episodes)
        {
            var offsets = crossReferences
                .Where(xref => xref.AnilistEpisodeID is not 0 && xref.MatchRating is MatchRating.UserVerified or MatchRating.DateAndNumberMatches or MatchRating.DateMatches)
                .Where(xref => anidbEpisodes.TryGetValue(xref.AnidbEpisodeID, out var anidbEpisode) && anidbEpisode.EpisodeType is EpisodeType.Episode && anilistEpisodeDict.ContainsKey(xref.AnilistEpisodeID))
                .Select(xref => anilistEpisodeDict[xref.AnilistEpisodeID].EpisodeNumber - anidbEpisodes[xref.AnidbEpisodeID].EpisodeNumber)
                .ToList();
            if (offsets.Count < 2 || offsets.Distinct().Count() != 1)
            {
                _logger.LogTrace("Skipping offset pass. (Anchors: {Anchors}, Distinct Offsets: {Offsets})", offsets.Count, offsets.Distinct().Count());
                return episodes;
            }

            var offset = offsets[0];
            var overflow = new List<AniDB_Episode>();
            foreach (var episode in episodes)
            {
                if (episode.EpisodeType is not EpisodeType.Episode || IsSpecialEpisode(episode, matchContext))
                {
                    overflow.Add(episode);
                    continue;
                }

                var index = anilistEpisodes.FindIndex(candidate => candidate.EpisodeNumber == episode.EpisodeNumber + offset);
                if (index == -1)
                {
                    overflow.Add(episode);
                    continue;
                }

                var candidate = anilistEpisodes[index];
                anilistEpisodes.RemoveAt(index);
                var crossRef = new CrossRef_AniDB_Anilist_Episode(anidbAnimeId, episode.EpisodeID, candidate.AnilistAnimeID, candidate.AnilistEpisodeID, candidate.EpisodeNumber, MatchRating.DateOffsetMatches);
                crossReferences.Add(crossRef);
                toAdd.Add(crossRef);
                _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, AniList ID: {AnilistEpisodeID}, Rating: {MatchRating}, Offset: {Offset})", episode.EpisodeID, crossRef.AnilistEpisodeID, crossRef.MatchRating, offset);
            }

            return overflow;
        }

        var current = 0;
        foreach (var (episode, cachedCrossRef) in RankByConfidence(anidbEpisodes.Values, matchContext))
        {
            current++;
            _logger.LogTrace("Checking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {AnidbEpisodeID}, Progress: {Current}/{Total}, Pass: 1/4)", episode.EpisodeType, episode.EpisodeNumber, episode.EpisodeID, current, anidbEpisodes.Count);
            var shouldAddNewLinks = true;
            if (useExisting && existing.TryGetValue(episode.EpisodeID, out var existingLinks) && existingLinks.Any(link => link.MatchRating is MatchRating.UserVerified or MatchRating.DateAndNumberMatches))
            {
                // Remove empty links if we have one or more empty links and at least one non-empty link.
                if (existingLinks.Any(a => a.AnilistEpisodeID is 0 && a.AnilistAnimeID is 0) && existingLinks.Any(a => a.AnilistEpisodeID is not 0 || a.AnilistAnimeID is not 0))
                    existingLinks = existingLinks
                        .Where(link => link.AnilistEpisodeID is not 0 || link.AnilistAnimeID is not 0)
                        .ToList();

                // Remove duplicates, if any.
                existingLinks = existingLinks.DistinctBy(link => (link.AnilistAnimeID, link.AnilistEpisodeID)).ToList();

                if (existingLinks.Count == 1 && existingLinks[0].AnilistEpisodeID is 0 && existingLinks[0].AnilistAnimeID is 0 && existingLinks[0].MatchRating is not MatchRating.UserVerified)
                    goto skipExistingLinks;

                // If hidden and no user verified links, then unset the auto link.
                shouldAddNewLinks = false;
                if ((episode.AnimeEpisode?.IsHidden ?? false) && !existingLinks.Any(link => link.MatchRating is MatchRating.UserVerified))
                {
                    _logger.LogTrace("Skipping hidden episode. (AniDB ID: {AnidbEpisodeID})", episode.EpisodeID);
                    var link = existingLinks[0];
                    if (link.AnilistEpisodeID is 0 && link.AnilistAnimeID is 0)
                    {
                        crossReferences.Add(link);
                        toSkip.Add(link.CrossRef_AniDB_Anilist_EpisodeID);
                    }
                    else
                    {
                        crossReferences.Add(new(anidbAnimeId, episode.EpisodeID, 0, 0, 0, MatchRating.None));
                    }
                    continue;
                }

                // Else return all existing links.
                foreach (var link in existingLinks)
                {
                    _logger.LogTrace("Skipping existing link for episode. (AniDB ID: {AnidbEpisodeID}, AniList ID: {AnilistEpisodeID}, Rating: {MatchRating})", episode.EpisodeID, link.AnilistEpisodeID, link.MatchRating);
                    crossReferences.Add(link);
                    toSkip.Add(link.CrossRef_AniDB_Anilist_EpisodeID);

                    // Exclude the linked episodes from the auto-match candidates.
                    var index = anilistEpisodes.FindIndex(episode => episode.AnilistEpisodeID == link.AnilistEpisodeID);
                    if (index >= 0)
                        anilistEpisodes.RemoveAt(index);
                }
            }

            skipExistingLinks:;
            if (shouldAddNewLinks)
            {
                // If hidden then skip linking episode.
                if (episode.AnimeEpisode?.IsHidden ?? false)
                {
                    _logger.LogTrace("Skipping hidden episode. (AniDB ID: {AnidbEpisodeID})", episode.EpisodeID);
                    crossReferences.Add(new(anidbAnimeId, episode.EpisodeID, 0, 0, 0, MatchRating.None));
                    continue;
                }

                // Else try find a match.
                _logger.LogTrace("Linking episode. (AniDB ID: {AnidbEpisodeID}, Pass: 1/4)", episode.EpisodeID);
                var crossRef = ResolveRankedMatch(episode, cachedCrossRef, matchContext);
                if (crossRef.MatchRating is MatchRating.DateAndNumberMatches)
                {
                    var index = anilistEpisodes.FindIndex(episode => episode.AnilistEpisodeID == crossRef.AnilistEpisodeID);
                    if (index != -1)
                        anilistEpisodes.RemoveAt(index);

                    crossReferences.Add(crossRef);
                    toAdd.Add(crossRef);
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, AniList ID: {AnilistEpisodeID}, Rating: {MatchRating}, Pass: 1/4)", episode.EpisodeID, crossRef.AnilistEpisodeID, crossRef.MatchRating);
                }
                else
                {
                    _logger.LogTrace("Skipping episode in the first pass. (AniDB ID: {AnidbEpisodeID}, AniList ID: {AnilistEpisodeID}, Rating: {MatchRating}, Pass: 1/4)", episode.EpisodeID, crossRef.AnilistEpisodeID, crossRef.MatchRating);
                    secondPass.Add(episode);
                }
            }
        }

        // Run a second pass on the episodes that weren't date and number matches in the first pass.
        if (secondPass.Count > 0)
            RunDeferredPass(2, "second", secondPass, rating => rating is MatchRating.DateMatches, thirdPass);

        // Between the date passes and the fallbacks, use the date-matched episodes as anchors: if they
        // all agree on one episode-number offset, the episodes without a schedule entry are placed on
        // that offset instead of being guessed from air-date proximity or position.
        if (thirdPass.Count > 0)
            thirdPass = RunOffsetPass(thirdPass);

        // Run a third pass on the episodes that weren't schedule date matches in the first two passes,
        // accepting the nearest-air-date fallback.
        if (thirdPass.Count > 0)
            RunDeferredPass(3, "third", thirdPass, rating => rating is not MatchRating.FirstAvailable and not MatchRating.None, fourthPass);

        // Run a fourth pass on the remaining episodes. Every match is accepted here (including a
        // MatchRating.None miss), so nothing ever overflows past this pass.
        if (fourthPass.Count > 0)
            RunDeferredPass(4, "fourth", fourthPass, _ => true, []);

        ReconcileEpisodeOrderInversions(anidbEpisodes, anilistEpisodeDict, toAdd);

        if (!saveToDatabase)
        {
            _logger.LogDebug(
                "Found {a} anidb/anilist episode links for anime {AnimeTitle} in {Delta}. (Anime={AnimeId}, AniList={AnilistId})",
                crossReferences.Count,
                anime.Title,
                DateTime.Now - startedAt,
                anidbAnimeId,
                anilistAnimeId
            );
            return crossReferences;
        }

        // Remove the current anidb episodes that does not overlap with the anime.
        var toRemove = existing.Values
            .SelectMany(list => list)
            .Where(xref => (anidbEpisodes.ContainsKey(xref.AnidbEpisodeID) && !toSkip.Contains(xref.CrossRef_AniDB_Anilist_EpisodeID)) || (xref.AnilistAnimeID == anilistAnimeId && !anilistEpisodeDict.ContainsKey(xref.AnilistEpisodeID)))
            .ToList();

        _logger.LogDebug(
            "Added/removed/skipped {a}/{r}/{s} anidb/anilist episode cross-references for anime {AnimeTitle} in {Delta} (Anime={AnimeId}, AniList={AnilistId})",
            toAdd.Count,
            toRemove.Count,
            existing.Count - toRemove.Count,
            anime.Title,
            DateTime.Now - startedAt,
            anidbAnimeId,
            anilistAnimeId);
        if (toAdd.Count > 0)
            _xrefAnidbAnilistEpisodes.Save(toAdd);
        if (toRemove.Count > 0)
            _xrefAnidbAnilistEpisodes.Delete(toRemove);
        ResetSeriesOverview(anidbAnimeId);

        return crossReferences;
    }

    // Shared state for RankByConfidence/ResolveRankedMatch. Episodes is the live candidate pool,
    // not a snapshot, since the passes consume it as links are made.
    private readonly record struct EpisodeMatchContext(
        AniDB_Anime Anime,
        Anilist_Anime AnilistAnime,
        List<Anilist_Episode> Episodes);

    // Ranks episodes by their best candidate match without consuming it, so passes can process the
    // strongest matches first and let weaker duplicate claims fall back to their next-best candidate
    // instead of losing a shared AniList episode purely by AniDB episode order.
    private List<(AniDB_Episode Episode, CrossRef_AniDB_Anilist_Episode CrossRef)> RankByConfidence(
        IEnumerable<AniDB_Episode> episodes,
        EpisodeMatchContext context) =>
        episodes
            .Select(ep =>
            {
                var crossRef = TryFindAnidbAndAnilistMatch(context.Anime, ep, context.Episodes, IsSpecialEpisode(ep, context), out var confidence);
                return (Episode: ep, CrossRef: crossRef, Confidence: confidence);
            })
            .OrderByDescending(ranked => ranked.Confidence)
            .Select(ranked => (ranked.Episode, ranked.CrossRef))
            .ToList();

    // Re-finds a match for an episode, reusing the cached match from RankByConfidence when its chosen
    // AniList candidate hasn't since been claimed by an earlier (stronger) episode in the same pass.
    private CrossRef_AniDB_Anilist_Episode ResolveRankedMatch(
        AniDB_Episode episode,
        CrossRef_AniDB_Anilist_Episode cached,
        EpisodeMatchContext context)
        => cached.AnilistEpisodeID != 0 && context.Episodes.Any(candidate => candidate.AnilistEpisodeID == cached.AnilistEpisodeID)
            ? cached
            : TryFindAnidbAndAnilistMatch(context.Anime, episode, context.Episodes, IsSpecialEpisode(episode, context), out _);

    private static bool IsSpecialEpisode(AniDB_Episode episode, EpisodeMatchContext context) =>
        episode.EpisodeType is EpisodeType.Special || context.Anime.AnimeType is not AnimeType.TV and not AnimeType.Web;

    // AniList episodes carry no titles, so only the airing schedule can establish a link. An agreeing
    // episode number raises a date match but never makes one. The out confidence score lets callers
    // rank multiple AniDB episodes contending for the same AniList episode within a pass.
    private CrossRef_AniDB_Anilist_Episode TryFindAnidbAndAnilistMatch(
        AniDB_Anime anime,
        AniDB_Episode anidbEpisode,
        IReadOnlyList<Anilist_Episode> anilistEpisodes,
        bool isSpecial,
        out double confidence)
    {
        confidence = 0;

        var anidbDate = anidbEpisode.GetAirDateAsDate()?.ToDateOnly();
        if (anidbDate is not null && anidbDate > DateTime.UtcNow.AddDays(1).ToDateOnly())
        {
            _logger.LogTrace("Skipping future episode {EpisodeID}", anidbEpisode.EpisodeID);
            return new(anime.AnimeID, anidbEpisode.EpisodeID, 0, 0, 0, MatchRating.None);
        }

        var airdateProbability = anilistEpisodes
            .Select(episode => (episode, probability: EpisodeMatchingUtility.CalculateAirDateProbability(anidbDate, episode.AirDate)))
            .Where(result => result.probability != 0)
            .OrderByDescending(result => result.probability)
            .ThenBy(result => result.episode.EpisodeNumber)
            .ToList();
        // No candidate within the strict ±2-day window (e.g. a delayed/compressed episode) — fall back to
        // the closest air date available instead of dropping straight to a blind positional guess.
        var nearestAirdate = airdateProbability.Count > 0 || anidbDate is null
            ? new List<(Anilist_Episode episode, int distance)>()
            : (from episode in anilistEpisodes
               let distance = EpisodeMatchingUtility.CalculateAirDateDistance(anidbDate, episode.AirDate)
               where distance is not null
               select (episode, distance: distance.Value))
                .OrderBy(result => result.distance)
                .ThenBy(result => result.episode.EpisodeNumber)
                .ToList();
        return SelectBestEpisodeMatch(anidbEpisode, anilistEpisodes, isSpecial, airdateProbability, nearestAirdate, out confidence);
    }

    // Picks the strongest candidate out of the air-date evidence gathered by the caller, tiered from
    // a schedule date match corroborated by the episode number, through a bare date match, down to a
    // positional fallback. Without a schedule every episode lands on the positional fallback, which is
    // a deliberately weak rating until a user verifies it.
    private static CrossRef_AniDB_Anilist_Episode SelectBestEpisodeMatch(
        AniDB_Episode anidbEpisode,
        IReadOnlyList<Anilist_Episode> anilistEpisodes,
        bool isSpecial,
        List<(Anilist_Episode episode, double probability)> airdateProbability,
        List<(Anilist_Episode episode, int distance)> nearestAirdate,
        out double confidence)
    {
        if (TryAirDateMatch(anidbEpisode, airdateProbability, out var crossRef, out confidence))
        {
            // Prefer a date-window candidate whose number also agrees, and rate it higher. Specials never
            // share the AniList numbering, so they only ever get the bare date match.
            if (!isSpecial && anidbEpisode.EpisodeType is EpisodeType.Episode &&
                airdateProbability.FirstOrDefault(result => result.episode.EpisodeNumber == anidbEpisode.EpisodeNumber) is { episode: { } numberMatch } corroborated)
            {
                confidence = 1 + corroborated.probability;
                return new(anidbEpisode.AnimeID, anidbEpisode.EpisodeID, numberMatch.AnilistAnimeID, numberMatch.AnilistEpisodeID, numberMatch.EpisodeNumber, MatchRating.DateAndNumberMatches);
            }

            return crossRef;
        }

        // Unlike the strict ±2-day airdateProbability match above, this fallback widens to 120 days —
        // for a special, that's little more than a coin flip against whatever AniList episode happens
        // to be nearest, so it's gated the same way the positional FirstAvailable fallback below is.
        if (!isSpecial && TryNearestAirDateMatch(anidbEpisode, nearestAirdate, out crossRef, out confidence))
            return crossRef;

        confidence = 0;

        // And finally, just pick the first available episode if it's not a special.
        if (!isSpecial && anilistEpisodes.Count > 0)
            return new(anidbEpisode.AnimeID, anidbEpisode.EpisodeID, anilistEpisodes[0].AnilistAnimeID, anilistEpisodes[0].AnilistEpisodeID, anilistEpisodes[0].EpisodeNumber, MatchRating.FirstAvailable);

        // And if all above failed, then return an empty link.
        return new(anidbEpisode.AnimeID, anidbEpisode.EpisodeID, 0, 0, 0, MatchRating.None);
    }

    private static bool TryAirDateMatch(
        AniDB_Episode anidbEpisode,
        List<(Anilist_Episode episode, double probability)> airdateProbability,
        [NotNullWhen(true)] out CrossRef_AniDB_Anilist_Episode? crossRef,
        out double confidence)
    {
        crossRef = null;
        confidence = 0;
        if (airdateProbability.Count == 0)
            return false;

        var (anilistEpisode, probability) = airdateProbability[0];
        confidence = probability;
        crossRef = new(anidbEpisode.AnimeID, anidbEpisode.EpisodeID, anilistEpisode.AnilistAnimeID, anilistEpisode.AnilistEpisodeID, anilistEpisode.EpisodeNumber, MatchRating.DateMatches);
        return true;
    }

    // Last-resort tier: no candidate within the strict ±2-day window. Picks the
    // closest remaining episode by air date instead of grabbing whatever's positionally next.
    // Confidence is kept low (and decays with distance) so a genuine close match elsewhere always outranks it.
    private static bool TryNearestAirDateMatch(
        AniDB_Episode anidbEpisode,
        List<(Anilist_Episode episode, int distance)> nearestAirdate,
        [NotNullWhen(true)] out CrossRef_AniDB_Anilist_Episode? crossRef,
        out double confidence)
    {
        crossRef = null;
        confidence = 0;
        if (nearestAirdate.Count == 0)
            return false;

        var (anilistEpisode, distance) = nearestAirdate[0];
        if (distance > EpisodeMatchingUtility.MaxFallbackDifferenceInDays)
            return false;

        confidence = 0.5 / (1 + distance);
        crossRef = new(anidbEpisode.AnimeID, anidbEpisode.EpisodeID, anilistEpisode.AnilistAnimeID, anilistEpisode.AnilistEpisodeID, anilistEpisode.EpisodeNumber, MatchRating.DateKindaMatches);
        return true;
    }

    // Ratings with no number evidence and no anchoring — the only ones a coincidental air-date mismatch can put out of order.
    private static readonly HashSet<MatchRating> _weakOrderRatings = [MatchRating.DateMatches, MatchRating.DateKindaMatches, MatchRating.FirstAvailable];

    // Swaps adjacent weak AniList matches into AniDB order; strong matches stay in the ordering (true adjacency) but are never swapped.
    internal static void ReconcileEpisodeOrderInversions(
        IReadOnlyDictionary<int, AniDB_Episode> anidbEpisodes,
        IReadOnlyDictionary<int, Anilist_Episode> anilistEpisodeDict,
        IReadOnlyList<CrossRef_AniDB_Anilist_Episode> toAdd)
    {
        var groups = toAdd
            .Where(xref => xref.AnilistEpisodeID != 0 && anidbEpisodes.ContainsKey(xref.AnidbEpisodeID))
            .GroupBy(xref => anidbEpisodes[xref.AnidbEpisodeID].EpisodeType);

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(xref => anidbEpisodes[xref.AnidbEpisodeID].EpisodeNumber).ToList();

            // Repeat until a full pass makes no swaps, to fully untangle runs of 3+ reversed weak matches.
            while (BubbleSwapPass(ordered, anilistEpisodeDict))
            {
                // Intentionally empty: BubbleSwapPass has already applied this pass's swaps in place.
            }
        }
    }

    // A single left-to-right adjacent-swap pass over the ordered group; returns true if anything moved.
    private static bool BubbleSwapPass(List<CrossRef_AniDB_Anilist_Episode> ordered, IReadOnlyDictionary<int, Anilist_Episode> anilistEpisodeDict)
    {
        var swapped = false;
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var a = ordered[i];
            var b = ordered[i + 1];
            if (!ShouldSwap(a, b, anilistEpisodeDict))
                continue;

            (a.AnilistEpisodeID, b.AnilistEpisodeID) = (b.AnilistEpisodeID, a.AnilistEpisodeID);
            (a.AnilistAnimeID, b.AnilistAnimeID) = (b.AnilistAnimeID, a.AnilistAnimeID);
            (a.EpisodeNumber, b.EpisodeNumber) = (b.EpisodeNumber, a.EpisodeNumber);
            (a.MatchRating, b.MatchRating) = (b.MatchRating, a.MatchRating);
            swapped = true;
        }

        return swapped;
    }

    private static bool ShouldSwap(CrossRef_AniDB_Anilist_Episode a, CrossRef_AniDB_Anilist_Episode b, IReadOnlyDictionary<int, Anilist_Episode> anilistEpisodeDict)
    {
        if (!_weakOrderRatings.Contains(a.MatchRating) || !_weakOrderRatings.Contains(b.MatchRating))
            return false;
        if (!anilistEpisodeDict.TryGetValue(a.AnilistEpisodeID, out var anilistA) || !anilistEpisodeDict.TryGetValue(b.AnilistEpisodeID, out var anilistB))
            return false;

        return anilistA.EpisodeNumber.CompareTo(anilistB.EpisodeNumber) > 0;
    }

    #endregion
}
