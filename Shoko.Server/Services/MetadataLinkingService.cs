using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Server;

namespace Shoko.Server.Services;

/// <summary>
///   Routes a link to whoever owns the source.
/// </summary>
/// <remarks>
///   Every source, the core's own included, is answered for by a provider, so
///   this holds only the routing, Shoko's per-anime veto on automatic
///   linking, and the applying of what an auto-linker takes. Writing a link
///   queues no refresh: whoever linked decides whether to fetch the entry.
/// </remarks>
public class MetadataLinkingService(
    ILogger<MetadataLinkingService> logger,
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataService metadataService,
    MetadataProviderScheduler providerScheduler,
    AnimeSeriesRepository seriesRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_EpisodeRepository anidbEpisodeRepository,
    IEnumerable<IMetadataLinkIDRule>? idRules = null,
    MetadataLinkChangeTracker? linkChanges = null
) : IMetadataLinkingService
{
    private readonly Dictionary<MetadataSource, IMetadataLinkIDRule> _idRules = (idRules ?? []).ToDictionary(rule => rule.Source);

    /// <summary>
    ///   Where the store reports the links it changed, and where this
    ///   service's calls begin their operations.
    /// </summary>
    private readonly MetadataLinkChangeTracker _linkChanges = linkChanges ?? new();

    /// <summary>
    ///   Guards subscribing to <see cref="_linkChanges"/>.
    /// </summary>
    private readonly Lock _linksChangedLock = new();

    /// <summary>
    ///   The handlers of <see cref="LinksChanged"/>.
    /// </summary>
    private EventHandler<MetadataLinksChangedEventArgs>? _linksChanged;

    /// <summary>
    ///   Whether the tracker is forwarded to <see cref="LinksChanged"/> yet.
    /// </summary>
    private bool _forwarding;

    /// <inheritdoc />
    public event EventHandler<MetadataLinksChangedEventArgs>? LinksChanged
    {
        add
        {
            lock (_linksChangedLock)
            {
                if (!_forwarding)
                {
                    _linkChanges.Changed += OnLinksChanged;
                    _forwarding = true;
                }

                _linksChanged += value;
            }
        }
        remove
        {
            lock (_linksChangedLock)
                _linksChanged -= value;
        }
    }

    /// <summary>
    ///   Hands what the tracker reported to each handler of
    ///   <see cref="LinksChanged"/>, so one failing does not keep it from the
    ///   rest.
    /// </summary>
    /// <param name="sender">The tracker.</param>
    /// <param name="eventArgs">What changed.</param>
    private void OnLinksChanged(object? sender, MetadataLinksChangedEventArgs eventArgs)
    {
        if (_linksChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<MetadataLinksChangedEventArgs>>())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A handler of LinksChanged failed.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<MetadataSource, IReadOnlySet<MetadataEntityType>> LinkableEntityTypes
    {
        get
        {
            Dictionary<MetadataSource, HashSet<MetadataEntityType>> linkable = [];

            foreach (var info in providerManager.GetAvailableProviders())
            {
                if (!info.Enabled)
                    continue;

                // Only types the provider accepts links for and has turned on: one it
                // may not answer for is not one it may be corrected on either.
                List<MetadataEntityType> accepted = [];
                if (info.Provider is IMetadataSeriesLinkingProvider series)
                    accepted.AddRange(series.LinkableEntityTypes.Where(info.EnabledEntityTypes.Contains));
                if (info.Provider is IMetadataMovieLinkingProvider && info.EnabledEntityTypes.Contains(MetadataEntityType.Movie))
                    accepted.Add(MetadataEntityType.Movie);

                // A source served by more than one provider takes what any of
                // them accepts.
                if (accepted.Count > 0)
                {
                    if (linkable.TryGetValue(info.Source, out var existing))
                        existing.UnionWith(accepted);
                    else
                        linkable[info.Source] = [.. accepted];
                }
            }

            return linkable.ToDictionary(pair => pair.Key, IReadOnlySet<MetadataEntityType> (pair) => pair.Value.ToFrozenSet());
        }
    }

    #region Search

    /// <inheritdoc />
    public Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSource source, MetadataSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Provider(source, MetadataEntityType.Series).SearchSeries(options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSource source, MetadataSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return MovieProvider(source).SearchMovies(options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MetadataSeriesSearchResult?> LookupSeries(MetadataGuid seriesID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesID);

        CheckRequest(seriesID.Source, MetadataEntityType.Series, seriesID, nameof(seriesID));
        var provider = Provider(seriesID.Source, MetadataEntityType.Series);
        if (providerManager.GetProviderInfo(provider) is not { SupportsLookup: true })
            throw new NotSupportedException($"{seriesID.Source} does not look series up by ID.");

        return provider.LookupSeries(seriesID, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MetadataMovieSearchResult?> LookupMovie(MetadataGuid movieID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movieID);

        CheckRequest(movieID.Source, MetadataEntityType.Movie, movieID, nameof(movieID));
        var provider = MovieProvider(movieID.Source);
        if (providerManager.GetProviderInfo(provider) is not { SupportsLookup: true })
            throw new NotSupportedException($"{movieID.Source} does not look films up by ID.");

        return provider.LookupMovie(movieID, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MetadataAutoLinkCandidate>> PreviewAutoLink(MetadataSource source, int anidbAnimeID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var candidates = await AutoLinker(source).FindAutoLinks(anidbAnimeID, cancellationToken).ConfigureAwait(false);
        var current = await CurrentLinks(source, anidbAnimeID, cancellationToken).ConfigureAwait(false);

        // Reviewed as a replacing search, the only kind run over a linked anime. Ordered by origin, both
        // kinds of hint as one group since the first hint left is the one taken.
        var reviewed = ReviewAutoLinks(source, anidbAnimeID, [.. candidates, .. current], replace: true);
        return [
            .. reviewed.OrderBy(candidate => candidate.Origin is MetadataAutoLinkOrigin.CrossSourceLink
                ? MetadataAutoLinkOrigin.AnidbResource
                : candidate.Origin),
        ];
    }

    /// <summary>
    ///   The links an anime has on a source, as candidates listed beside a
    ///   search for context.
    /// </summary>
    /// <remarks>
    ///   Each entry is described by the provider's lookup where it has one,
    ///   by the stored entry otherwise, and by its ID alone when neither can
    ///   say more.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links, series first, each rated as it is stored.</returns>
    private async Task<IReadOnlyList<MetadataAutoLinkCandidate>> CurrentLinks(MetadataSource source, int anidbAnimeID, CancellationToken cancellationToken)
    {
        IReadOnlyList<IMetadataCrossReference> links =
        [
            .. crossReferences.GetSeriesLinks(anidbAnimeID, source),
            .. crossReferences.GetMovieLinksForSeries(anidbAnimeID, source),
        ];
        var candidates = new List<MetadataAutoLinkCandidate>(links.Count);
        foreach (var link in links)
        {
            if (link.ProviderID is not { } providerID)
                continue;

            var stored = metadataService.GetEntry(providerID);
            candidates.Add(new()
            {
                Result = await Describe(providerID, stored, cancellationToken).ConfigureAwait(false),
                AnidbAnimeID = anidbAnimeID,
                AnidbEpisodeID = link is IMetadataMovieCrossReference movie ? movie.AnidbEpisodeID : null,
                MatchRating = link.MatchRating,
                LinkMatchRating = link.MatchRating,
                Origin = MetadataAutoLinkOrigin.CurrentLink,
                IsLocal = stored is not null,
            });
        }

        return candidates;
    }

    /// <summary>
    ///   A linked entry, the way a search would have offered it.
    /// </summary>
    /// <param name="entryID">The entry.</param>
    /// <param name="stored">The entry as stored, if it is.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The entry as a search result.</returns>
    private async Task<MetadataSearchResult> Describe(MetadataGuid entryID, IMetadata? stored, CancellationToken cancellationToken)
    {
        try
        {
            MetadataSearchResult? found = entryID.EntityType == MetadataEntityType.Movie
                ? await LookupMovie(entryID, cancellationToken).ConfigureAwait(false)
                : await LookupSeries(entryID, cancellationToken).ConfigureAwait(false);
            if (found is not null)
                return found;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or MetadataProviderUnavailableException)
        {
            logger.LogDebug(ex, "Unable to look up {Entry} to list it as a current link.", entryID);
        }

        return stored switch
        {
            IMovie movie => new MetadataMovieSearchResult
            {
                ID = entryID,
                Title = movie.Title,
                IsRestricted = movie.Restricted,
                ReleasedAt = movie.ReleaseDate is { } released ? new PartialDateOnly(DateOnly.FromDateTime(released)) : null,
            },
            ISeries series => new MetadataSeriesSearchResult
            {
                ID = entryID,
                Title = series.Title,
                IsRestricted = series.Restricted,
                FirstAiredAt = series.AirDate,
                Type = series.Type,
                EpisodeCount = series.EpisodeCounts.Episodes > 0 ? series.EpisodeCounts.Episodes : null,
            },
            _ when entryID.EntityType == MetadataEntityType.Movie => new MetadataMovieSearchResult { ID = entryID, Title = entryID.ID },
            _ => new MetadataSeriesSearchResult { ID = entryID, Title = entryID.ID },
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataAutoLinkHint> GetCrossSourceHints(MetadataSource source, int anidbAnimeID)
    {
        ArgumentNullException.ThrowIfNull(source);

        IReadOnlyList<(IMetadataCrossReference Link, int? AnidbEpisodeID)> links =
        [
            .. crossReferences.GetSeriesLinks(anidbAnimeID).Select(link => ((IMetadataCrossReference)link, (int?)null)),
            .. crossReferences.GetMovieLinksForSeries(anidbAnimeID).Select(link => ((IMetadataCrossReference)link, (int?)link.AnidbEpisodeID)),
            .. crossReferences.GetEpisodeLinksForSeries(anidbAnimeID).Select(link => ((IMetadataCrossReference)link, (int?)link.AnidbEpisodeID)),
        ];
        var hints = new Dictionary<(MetadataGuid ID, int? AnidbEpisodeID), List<MetadataGuid>>();
        var order = new List<(MetadataGuid ID, int? AnidbEpisodeID)>();
        foreach (var (link, anidbEpisodeID) in links)
        {
            if (link.Source == source || link.ProviderID is not { } linkedID)
                continue;

            var named = metadataService.GetEntry(linkedID) is IWithCrossSources entry ? entry.CrossSourceIDs : [];
            foreach (var id in named)
            {
                if (id.Source != source)
                    continue;

                // A film keeps the episode it was named for, and an episode
                // stands for its series wherever that is known.
                (MetadataGuid ID, int? AnidbEpisodeID) key;
                if (id.EntityType == MetadataEntityType.Series)
                    key = (id, null);
                else if (id.EntityType == MetadataEntityType.Movie)
                    key = (id, anidbEpisodeID);
                else if (id.EntityType == MetadataEntityType.Episode)
                    key = (metadataService.GetEpisode(id)?.SeriesID is { } seriesID && seriesID.Source == source ? seriesID : id, null);
                else
                    continue;

                if (!hints.TryGetValue(key, out var namedBy))
                {
                    hints[key] = namedBy = [];
                    order.Add(key);
                }

                if (!namedBy.Contains(linkedID))
                    namedBy.Add(linkedID);
            }
        }

        return [
            .. order.Select(key => new MetadataAutoLinkHint
            {
                ID = key.ID,
                AnidbAnimeID = anidbAnimeID,
                AnidbEpisodeID = key.AnidbEpisodeID,
                NamedBy = hints[key],
            }),
        ];
    }

    #endregion

    #region Whole works

    /// <inheritdoc />
    public async Task<bool> AddSeriesLink(MetadataSeriesLinkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linkChanges = _linkChanges.Begin();
        await WriteSeriesLink(request, cancellationToken).ConfigureAwait(false);

        // A series has the anime's episodes matched to it at once, whatever its source.
        if (request.EntityType == MetadataEntityType.Series && request.ProviderID is { } providerID)
            await TryMatchEpisodes(request.AnidbAnimeID, providerID, cancellationToken).ConfigureAwait(false);

        // The link is ours to keep; whatever sits behind it is written by the
        // provider when a refresh asks it to, which is the caller's to queue.
        return true;
    }

    /// <summary>
    ///   Writes a series-level link, without matching the anime's episodes to
    ///   it.
    /// </summary>
    /// <param name="request">What to link.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the link is written.</returns>
    /// <exception cref="ArgumentException">The entry is on another source, or of another kind.</exception>
    /// <exception cref="NotSupportedException">No enabled provider links series on the source.</exception>
    private async Task WriteSeriesLink(MetadataSeriesLinkRequest request, CancellationToken cancellationToken)
    {
        CheckRequest(request.Source, request.EntityType, request.ProviderID, nameof(request));
        var provider = Provider(request.Source, MetadataEntityType.Series);
        var providerID = request.ProviderID;
        var replaced = request.Additive
            ? []
            : crossReferences.GetSeriesLinks(request.AnidbAnimeID, request.Source)
                .Where(link => link.ProviderID != providerID)
                .Select(link => link.ProviderID)
                .ToHashSet();
        await crossReferences.MergeSeriesLinks(
            [
                new()
                {
                    Source = request.Source,
                    AnidbAnimeID = request.AnidbAnimeID,
                    ProviderID = providerID,
                    MatchRating = request.MatchRating,
                },
            ],
            options: new() { ReplaceExisting = !request.Additive, WrittenBy = WriterOf(provider) },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        // A replaced series takes its episode links with it, the same as a
        // removed one, so none are left pointing into a work no longer linked.
        if (replaced.Count > 0)
            await RemoveEpisodeLinksUnder(request.Source, request.AnidbAnimeID, replaced, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///   Matches an anime's episodes to a series just linked and saves them,
    ///   keeping the episode links already there, when the source's episodes
    ///   may be linked.
    /// </summary>
    /// <remarks>
    ///   A matching that cannot run is no reason to fail the link: the series
    ///   is matched again once its refresh writes it.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="providerSeriesID">The series linked.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the matches are saved.</returns>
    private async Task TryMatchEpisodes(int anidbAnimeID, MetadataGuid providerSeriesID, CancellationToken cancellationToken)
    {
        if (TryProvider(providerSeriesID.Source, MetadataEntityType.Episode) is null)
            return;

        try
        {
            await MatchEpisodes(anidbAnimeID, providerSeriesID, useExisting: true, save: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            logger.LogDebug(ex, "Unable to match the episodes of AniDB anime {AnimeID} against {SeriesID}.", anidbAnimeID, providerSeriesID);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveSeriesLink(MetadataSeriesLinkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linkChanges = _linkChanges.Begin();
        CheckRequest(request.Source, request.EntityType, request.ProviderID, nameof(request));
        if (request.EntityType == MetadataEntityType.Movie)
            return await RemoveWholeMovieLink(request, cancellationToken).ConfigureAwait(false);

        // Removed whether or not the source's provider is on, as the bulk removals
        // are, so a link can be undone after its provider is turned off.
        var removing = crossReferences.GetSeriesLinks(request.AnidbAnimeID, request.Source)
            .Where(link => link.ProviderID == request.ProviderID)
            .ToList();
        if (removing.Count is 0)
            return false;

        await crossReferences.MergeSeriesLinks([], removing, cancellationToken: cancellationToken).ConfigureAwait(false);

        // The removed series' episode links go too; those naming no series only
        // with the last series link, as until then they may belong to one left.
        var lastLink = crossReferences.GetSeriesLinks(request.AnidbAnimeID, request.Source).Count is 0;
        await RemoveEpisodeLinksUnder(request.Source, request.AnidbAnimeID, [request.ProviderID], lastLink, cancellationToken).ConfigureAwait(false);
        if (request.DisableAutoLinking)
            DisableAutoLinking(request.Source, request.AnidbAnimeID);
        if (request.Purge && request.ProviderID is { } providerID)
            await providerScheduler.SchedulePurge(providerID, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///   Break a film's claim on a whole anime: its series-level link and its
    ///   film links on the anime's episodes.
    /// </summary>
    /// <param name="request">What to unlink, naming a film.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> if any link was removed.
    /// </returns>
    private async Task<bool> RemoveWholeMovieLink(MetadataSeriesLinkRequest request, CancellationToken cancellationToken)
    {
        var seriesLinks = crossReferences.GetSeriesLinks(request.AnidbAnimeID, request.Source)
            .Where(link => link.ProviderID == request.ProviderID)
            .ToList();
        var movieLinks = crossReferences.GetMovieLinksForSeries(request.AnidbAnimeID, request.Source)
            .Where(link => link.ProviderID == request.ProviderID)
            .ToList();
        if (seriesLinks.Count is 0 && movieLinks.Count is 0)
            return false;

        if (seriesLinks.Count > 0)
            await crossReferences.MergeSeriesLinks([], seriesLinks, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (movieLinks.Count > 0)
            await crossReferences.MergeMovieLinks([], movieLinks, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (request.DisableAutoLinking)
            DisableAutoLinking(request.Source, request.AnidbAnimeID);
        if (request.Purge && request.ProviderID is { } providerID)
            await providerScheduler.SchedulePurge(providerID, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///   Remove an anime's episode links that point into the given provider
    ///   series.
    /// </summary>
    /// <param name="source">The source the links belong to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="providerSeriesIDs">The provider series whose episode links go.</param>
    /// <param name="includeUnparented">
    ///   Whether links naming no provider series go too.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the links are gone.</returns>
    private async Task RemoveEpisodeLinksUnder(
        MetadataSource source,
        int anidbAnimeID,
        IReadOnlyCollection<MetadataGuid?> providerSeriesIDs,
        bool includeUnparented,
        CancellationToken cancellationToken
    )
    {
        var removing = crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source)
            .Where(link => link.ProviderParentID is { } parentID ? providerSeriesIDs.Contains(parentID) : includeUnparented)
            .ToList();
        if (removing.Count > 0)
            await crossReferences.MergeEpisodeLinks([], removing, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddMovieLink(MetadataEpisodeLinkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linkChanges = _linkChanges.Begin();
        CheckRequest(request.Source, MetadataEntityType.Movie, request.ProviderID, nameof(request));
        var provider = MovieProvider(request.Source);
        await crossReferences.MergeMovieLinks(
            [
                new()
                {
                    Source = request.Source,
                    AnidbAnimeID = request.AnidbAnimeID,
                    AnidbEpisodeID = request.AnidbEpisodeID,
                    ProviderID = request.ProviderID,
                    MatchRating = request.MatchRating,
                },
            ],
            options: new() { ReplaceExisting = !request.Additive, WrittenBy = WriterOf(provider) },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMovieLink(MetadataEpisodeLinkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linkChanges = _linkChanges.Begin();
        CheckRequest(request.Source, MetadataEntityType.Movie, request.ProviderID, nameof(request));
        var removing = crossReferences.GetMovieLinks(request.AnidbEpisodeID, request.Source)
            .Where(link => link.ProviderID == request.ProviderID)
            .ToList();
        if (removing.Count is 0)
            return false;

        await crossReferences.MergeMovieLinks([], removing, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (request.DisableAutoLinking)
            DisableAutoLinking(request.Source, request.AnidbAnimeID);
        if (request.Purge && request.ProviderID is { } providerID)
            await providerScheduler.SchedulePurge(providerID, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public bool IsAutoLinkingDisabled(IShokoSeries series, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(series);

        return series.IsAutoLinkingDisabled(source);
    }

    /// <inheritdoc />
    public void SetAutoLinkingDisabled(IShokoSeries series, MetadataSource source, bool disabled)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series is not AnimeSeries animeSeries)
            throw new NotSupportedException($"'{series.GetType().Name}' does not carry auto-linking vetoes.");

        var key = source.ToString();
        var keys = animeSeries.DisabledAutoMatchKeys;
        if (keys.Contains(key) == disabled)
            return;

        var updated = keys.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        if (disabled)
            updated.Add(key);
        else
            updated.Remove(key);

        animeSeries.DisabledAutoMatchKeys = updated;
        seriesRepository.Save(animeSeries, false, false);
    }

    /// <inheritdoc />
    public async Task<bool> AutoLink(MetadataSource source, int anidbAnimeID, CancellationToken cancellationToken = default)
    {
        // Asked only to refuse a source nobody auto-links.
        _ = AutoLinker(source);
        return await providerScheduler.ScheduleSearch(source, anidbAnimeID, force: true, replace: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public int ResetAutoLinkingState(MetadataSource source, bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(source);

        var changed = 0;
        foreach (var series in seriesRepository.GetAll())
        {
            if (series.IsAutoLinkingDisabled(source) == disabled)
                continue;

            SetAutoLinkingDisabled(series, source, disabled);
            changed++;
        }

        return changed;
    }

    #endregion

    #region Auto-linking

    /// <summary>
    ///   Adds the core's own refusals to what an auto-linker handed back.
    /// </summary>
    /// <remarks>
    ///   A provider's refusal keeps its reason. A taken candidate is refused
    ///   when it names nothing linkable to the anime or a kind the admin
    ///   turned off, and one listed for context always is, as an existing
    ///   link. Hints are weighed by <see cref="ReviewHints"/>, over the
    ///   anime's first link unless the search replaces its links.
    /// </remarks>
    /// <param name="source">The source searched.</param>
    /// <param name="anidbAnimeID">The AniDB anime searched for.</param>
    /// <param name="candidates">The candidates, as the provider handed them back.</param>
    /// <param name="replace">
    ///   Whether the search replaces the anime's links, so a hint may be taken
    ///   over a current link too.
    /// </param>
    /// <returns>The same candidates in the same order, with the refusals set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="candidates"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<MetadataAutoLinkCandidate> ReviewAutoLinks(
        MetadataSource source,
        int anidbAnimeID,
        IReadOnlyList<MetadataAutoLinkCandidate> candidates,
        bool replace = false
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidates);

        var linkable = LinkableEntityTypes.TryGetValue(source, out var types) ? types : FrozenSet<MetadataEntityType>.Empty;
        List<MetadataAutoLinkCandidate> reviewed =
        [
            .. candidates.Select(candidate => candidate switch
            {
                { Origin: MetadataAutoLinkOrigin.CurrentLink or MetadataAutoLinkOrigin.PrequelLink, Rejection.Reason: MatchRejectionReason.ExistingLink } => candidate,
                { Origin: MetadataAutoLinkOrigin.CurrentLink or MetadataAutoLinkOrigin.PrequelLink } => candidate with { Rejection = ExistingLink(candidate) },
                { Origin: MetadataAutoLinkOrigin.AnidbResource or MetadataAutoLinkOrigin.CrossSourceLink, MatchRating: MatchRating.UserVerified } =>
                    candidate with { MatchRating = MatchRating.FirstAvailable },
                _ => candidate,
            }),
        ];
        reviewed = [.. reviewed.Select(candidate => candidate is { Rejection: null } && Refuse(source, anidbAnimeID, candidate, linkable) is { } rejection
            ? candidate with { Rejection = rejection }
            : candidate)];

        ReviewHints(reviewed, source, replace ? null : FirstLink(source, anidbAnimeID));
        return reviewed;
    }

    /// <summary>
    ///   Turns down the candidates named by the anime's AniDB resources or by
    ///   its links on other sources that are not taken, and the search's
    ///   picks one of them is taken instead of.
    /// </summary>
    /// <remarks>
    ///   A hint is taken when no link is given, no hint before it is taken,
    ///   and the search took nothing it competes with or only picks the engine
    ///   rates strictly below it (turned down as outranked). A hint the search
    ///   took too, one rated no higher than the best pick, or one beside picks
    ///   it does not compete with is not taken.
    /// </remarks>
    /// <param name="reviewed">The candidates, with the other refusals set, changed in place.</param>
    /// <param name="source">The source searched.</param>
    /// <param name="linked">
    ///   The anime's first link on the source, or <see langword="null"/> when
    ///   it has none or the search replaces its links.
    /// </param>
    internal static void ReviewHints(List<MetadataAutoLinkCandidate> reviewed, MetadataSource source, IMetadataCrossReference? linked)
    {
        MetadataAutoLinkCandidate? hintTaken = null;
        for (var index = 0; index < reviewed.Count; index++)
        {
            var candidate = reviewed[index];
            if (candidate is not { Origin: MetadataAutoLinkOrigin.AnidbResource or MetadataAutoLinkOrigin.CrossSourceLink, Rejection: null })
                continue;

            var picks = reviewed.Where(other => IsTakenBySearch(other) && Competes(candidate, other)).ToList();
            var best = picks.OrderBy(pick => MetadataMatchingEngine.Priority(pick.MatchRating)).FirstOrDefault();
            string? why = null;
            if (reviewed.Any(other => IsTakenBySearch(other) && other.ID == candidate.ID))
                why = "and the search took it too.";
            else if (hintTaken is not null)
                why = $"but {hintTaken.ID} (\"{hintTaken.Result.Title}\"), named before it, was taken, and only one hint is.";
            else if (best is not null && !Outranks(candidate.MatchRating, best.MatchRating))
                why = $"but the search took {best.ID} (\"{best.Result.Title}\"), rated {best.MatchRating}, and a hint only replaces a pick it is rated above.";
            else if (best is null && reviewed.FirstOrDefault(IsTakenBySearch) is { } elsewhere)
                why = $"but the search took {elsewhere.ID} (\"{elsewhere.Result.Title}\") for {PlaceOf(elsewhere)}, and a hint for {PlaceOf(candidate)} " +
                    "only replaces a pick for it or for the whole anime.";
            else if (linked is not null)
                why = linked.ProviderID switch
                {
                    null => $"but the anime is deliberately on no entry of {source.Name}, which a hint never overrides.",
                    { } id when id == candidate.ID => "and the anime is linked to it already.",
                    { } id => $"but the anime is linked to {id} already, which a hint never replaces.",
                };

            if (why is not null)
            {
                reviewed[index] = candidate with { Rejection = HintNotNeeded(candidate, why) };
                continue;
            }

            // The picks it outranks are turned down in its favour.
            foreach (var pick in picks)
                reviewed[reviewed.FindIndex(other => ReferenceEquals(other, pick))] = pick with
                {
                    Rejection = new()
                    {
                        Reason = MatchRejectionReason.Outranked,
                        Details = $"Rated {pick.MatchRating}, below {candidate.ID} (\"{candidate.Result.Title}\"), rated {candidate.MatchRating}, " +
                            $"which {NamedBy(candidate)} name and which was taken instead.",
                    },
                };
            hintTaken = candidate;
        }
    }

    /// <summary>
    ///   The first link an anime has on a source, a series link before a
    ///   film's, including one that deliberately names no entry.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The link, or <see langword="null"/> when the anime has none on the source.</returns>
    private IMetadataCrossReference? FirstLink(MetadataSource source, int anidbAnimeID)
        => crossReferences.GetSeriesLinks(anidbAnimeID, source).FirstOrDefault() ??
            (IMetadataCrossReference?)crossReferences.GetMovieLinksForSeries(anidbAnimeID, source).FirstOrDefault();

    /// <summary>
    ///   Whether one rating is strictly above another in the order the
    ///   matching engine picks candidates by.
    /// </summary>
    /// <param name="rating">The rating weighed.</param>
    /// <param name="other">The rating it is weighed against.</param>
    /// <returns><see langword="true"/> when <paramref name="rating"/> ranks higher.</returns>
    internal static bool Outranks(MatchRating rating, MatchRating other)
        => MetadataMatchingEngine.Priority(rating) < MetadataMatchingEngine.Priority(other);

    /// <summary>
    ///   Whether a hint competes with a pick of the search: a hint for the
    ///   whole anime with every pick, and a film's with the picks for its
    ///   episode and for the whole anime.
    /// </summary>
    /// <param name="hint">The hint.</param>
    /// <param name="pick">The search's pick.</param>
    /// <returns><see langword="true"/> when taking the hint turns the pick down.</returns>
    private static bool Competes(MetadataAutoLinkCandidate hint, MetadataAutoLinkCandidate pick)
        => hint.AnidbEpisodeID is null || pick.AnidbEpisodeID is null || pick.AnidbEpisodeID == hint.AnidbEpisodeID;

    /// <summary>
    ///   Where a candidate would be linked, as a phrase.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The whole anime, or one of its episodes.</returns>
    private static string PlaceOf(MetadataAutoLinkCandidate candidate)
        => candidate.AnidbEpisodeID is { } episodeID ? $"AniDB episode {episodeID}" : "the whole anime";

    /// <summary>
    ///   Whether a candidate is one the search found and nothing turned down.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns><see langword="true"/> when the search took it.</returns>
    private static bool IsTakenBySearch(MetadataAutoLinkCandidate candidate)
        => candidate is { Origin: MetadataAutoLinkOrigin.Search, Rejection: null };

    /// <summary>
    ///   Why a hint is not taken.
    /// </summary>
    /// <param name="candidate">The hint.</param>
    /// <param name="why">The rest of the sentence, saying what was taken or linked instead.</param>
    /// <returns>The rejection.</returns>
    private static MetadataAutoLinkRejection HintNotNeeded(MetadataAutoLinkCandidate candidate, string why)
        => new()
        {
            Reason = MatchRejectionReason.HintNotNeeded,
            Details = $"Named by {NamedBy(candidate)}, {why} Rated {candidate.MatchRating}.",
        };

    /// <summary>
    ///   What named a hint, as a phrase.
    /// </summary>
    /// <param name="candidate">The hint.</param>
    /// <returns>The anime's AniDB resources, or its links on other sources.</returns>
    private static string NamedBy(MetadataAutoLinkCandidate candidate)
        => candidate.Origin is MetadataAutoLinkOrigin.CrossSourceLink ? "the anime's links on other sources" : "the anime's AniDB resources";

    /// <summary>
    ///   Why a candidate listed for context is never linked by a search.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The rejection.</returns>
    private static MetadataAutoLinkRejection ExistingLink(MetadataAutoLinkCandidate candidate)
        => new()
        {
            Reason = MatchRejectionReason.ExistingLink,
            Details = candidate.Origin == MetadataAutoLinkOrigin.CurrentLink
                ? $"The anime's current link, rated {candidate.LinkMatchRating ?? candidate.MatchRating}. Listed for context; never linked again by the search."
                : $"Linked to a prequel{(candidate.PrequelAnidbAnimeID is { } prequelID ? $", AniDB anime {prequelID}" : string.Empty)}" +
                    $"{(candidate.LinkMatchRating is { } rating ? $", rated {rating}" : string.Empty)}. Listed for context; never linked by the search.",
        };

    /// <summary>
    ///   Links what an auto-linker took for an anime, and logs every
    ///   candidate with what became of it.
    /// </summary>
    /// <remarks>
    ///   A film is linked to the AniDB episode it names, or to the whole anime
    ///   when it names none, and a series has the anime's episodes matched to
    ///   it. When replacing, what is taken is written first and the old links
    ///   are only removed once something was, so nothing is lost when every
    ///   write fails. When nothing is taken, every link is left as it was.
    /// </remarks>
    /// <param name="source">The source searched.</param>
    /// <param name="anidbAnimeID">The AniDB anime searched for.</param>
    /// <param name="candidates">The candidates, as the provider handed them back.</param>
    /// <param name="replace">
    ///   Whether what is taken replaces every link the anime has on the
    ///   source, verified ones included, as a search a person asked for does.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The candidates linked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="candidates"/> is <see langword="null"/>.</exception>
    public async Task<IReadOnlyList<MetadataAutoLinkCandidate>> ApplyAutoLinks(
        MetadataSource source,
        int anidbAnimeID,
        IReadOnlyList<MetadataAutoLinkCandidate> candidates,
        bool replace,
        CancellationToken cancellationToken = default
    )
    {
        using var linkChanges = _linkChanges.Begin(replace ? MetadataLinkChangeReason.ForcedResearch : MetadataLinkChangeReason.AutoLink);
        var reviewed = ReviewAutoLinks(source, anidbAnimeID, candidates, replace);
        foreach (var candidate in reviewed)
        {
            if (candidate.Rejection is { } rejection)
                logger.LogDebug(
                    "Auto-link candidate {Entry} ({Title}) for AniDB anime {AnimeID}{Episode}, rated {Rating}: rejected, {Reason}{Details}",
                    candidate.ID, candidate.Result.Title, anidbAnimeID, EpisodeSuffix(candidate), candidate.MatchRating, rejection.Reason,
                    rejection.Details is { Length: > 0 } details ? $" ({details})" : string.Empty
                );
            else
                logger.LogDebug(
                    "Auto-link candidate {Entry} ({Title}) for AniDB anime {AnimeID}{Episode}, rated {Rating}: taken",
                    candidate.ID, candidate.Result.Title, anidbAnimeID, EpisodeSuffix(candidate), candidate.MatchRating
                );
        }

        var taken = reviewed.Where(candidate => candidate.Rejection is null).ToList();
        if (taken.Count is 0)
        {
            logger.LogDebug("Nothing was taken on {Source} for AniDB anime {AnimeID}, so its links are left as they were.", source, anidbAnimeID);
            return [];
        }

        // Read before anything is written. Episode links all go, since the series
        // taken are matched again once the old ones are gone.
        IReadOnlyList<IMetadataCrossReference> previous = replace
            ?
            [
                .. crossReferences.GetSeriesLinks(anidbAnimeID, source),
                .. crossReferences.GetMovieLinksForSeries(anidbAnimeID, source),
                .. crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source),
            ]
            : [];
        var linked = new List<MetadataAutoLinkCandidate>();
        foreach (var candidate in taken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (candidate.ID.EntityType == MetadataEntityType.Movie && candidate.AnidbEpisodeID is { } anidbEpisodeID)
                {
                    await AddMovieLink(new()
                    {
                        Source = source,
                        EntityType = MetadataEntityType.Movie,
                        ProviderID = candidate.ID,
                        AnidbAnimeID = anidbAnimeID,
                        AnidbEpisodeID = anidbEpisodeID,
                        MatchRating = candidate.MatchRating,
                    }, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    MetadataSeriesLinkRequest request = new()
                    {
                        Source = source,
                        EntityType = candidate.ID.EntityType,
                        ProviderID = candidate.ID,
                        AnidbAnimeID = anidbAnimeID,
                        MatchRating = candidate.MatchRating,
                    };
                    if (replace)
                        await WriteSeriesLink(request, cancellationToken).ConfigureAwait(false);
                    else
                        await AddSeriesLink(request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
            {
                // Turned off, or refused by the store, since the review.
                logger.LogWarning(ex, "Unable to auto-link AniDB anime {AnimeID}{Episode} to {Entry}.", anidbAnimeID, EpisodeSuffix(candidate), candidate.ID);
                continue;
            }

            logger.LogInformation(
                "Auto-linked AniDB anime {AnimeID}{Episode} to {Entry} ({Title}), rated {Rating}.",
                anidbAnimeID, EpisodeSuffix(candidate), candidate.ID, candidate.Result.Title, candidate.MatchRating
            );
            linked.Add(candidate);
        }

        if (replace && linked.Count > 0)
            await ReplacePreviousLinks(source, anidbAnimeID, previous, linked, cancellationToken).ConfigureAwait(false);

        return linked;

        static string EpisodeSuffix(MetadataAutoLinkCandidate candidate)
            => candidate.AnidbEpisodeID is { } episodeID ? $", episode {episodeID}" : string.Empty;
    }

    /// <summary>
    ///   Removes the links an anime had before a replacing search wrote what it
    ///   took, and matches its episodes to the series taken.
    /// </summary>
    /// <remarks>
    ///   A link to an entry taken again was updated in place by the write, so
    ///   it stays. Every episode link goes, and the series taken are matched
    ///   afresh.
    /// </remarks>
    /// <param name="source">The source searched.</param>
    /// <param name="anidbAnimeID">The AniDB anime searched for.</param>
    /// <param name="previous">The anime's links on the source before the writes.</param>
    /// <param name="linked">The candidates written.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the old links are gone.</returns>
    private async Task ReplacePreviousLinks(
        MetadataSource source,
        int anidbAnimeID,
        IReadOnlyList<IMetadataCrossReference> previous,
        IReadOnlyList<MetadataAutoLinkCandidate> linked,
        CancellationToken cancellationToken
    )
    {
        var wholeWorks = linked
            .Where(candidate => candidate.ID.EntityType != MetadataEntityType.Movie || candidate.AnidbEpisodeID is null)
            .Select(candidate => candidate.ID)
            .ToHashSet();
        var films = linked
            .Where(candidate => candidate.ID.EntityType == MetadataEntityType.Movie && candidate.AnidbEpisodeID is not null)
            .Select(candidate => (candidate.AnidbEpisodeID!.Value, candidate.ID))
            .ToHashSet();
        var removing = previous
            .Where(link => link switch
            {
                IMetadataSeriesCrossReference series => series.ProviderID is not { } id || !wholeWorks.Contains(id),
                IMetadataMovieCrossReference movie => movie.ProviderID is not { } id || !films.Contains((movie.AnidbEpisodeID, id)),
                _ => true,
            })
            .ToList();
        var removed = await Remove(removing, purge: false, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Removed {Count} links of AniDB anime {AnimeID} on {Source} that the search replaced.", removed, anidbAnimeID, source);

        foreach (var providerID in wholeWorks.Where(id => id.EntityType == MetadataEntityType.Series))
            await TryMatchEpisodes(anidbAnimeID, providerID, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///   Why the core turns down a candidate a provider took, if it does.
    /// </summary>
    /// <param name="source">The source searched.</param>
    /// <param name="anidbAnimeID">The AniDB anime searched for.</param>
    /// <param name="candidate">The candidate.</param>
    /// <param name="linkable">What may be linked from the source.</param>
    /// <returns>The refusal, or <see langword="null"/> when the candidate may be linked.</returns>
    private MetadataAutoLinkRejection? Refuse(MetadataSource source, int anidbAnimeID, MetadataAutoLinkCandidate candidate, IReadOnlySet<MetadataEntityType> linkable)
    {
        var entry = candidate.ID;
        if (entry.Source != source)
            return Invalid($"It is on {entry.Source.Name}, not {source.Name}.");
        if (entry.EntityType != MetadataEntityType.Series && entry.EntityType != MetadataEntityType.Movie)
            return Invalid($"A {entry.EntityType.Value} is neither a series nor a film.");
        if (_idRules.TryGetValue(source, out var rule) && !rule.IsValid(entry))
            return Invalid($"\"{entry.ID}\" is not an ID {source.Name} gives.");
        if (candidate.AnidbAnimeID != anidbAnimeID)
            return Invalid($"It is for AniDB anime {candidate.AnidbAnimeID}.");
        if (candidate.AnidbEpisodeID is { } anidbEpisodeID)
        {
            if (entry.EntityType != MetadataEntityType.Movie)
                return Invalid("Only a film stands for one episode.");
            if (anidbEpisodeRepository.GetByEpisodeID(anidbEpisodeID)?.AnimeID != anidbAnimeID)
                return Invalid($"AniDB episode {anidbEpisodeID} is not one of the anime's.");
        }

        if (!linkable.Contains(entry.EntityType))
            return new() { Reason = MatchRejectionReason.KindDisabled, Details = $"Linking a {entry.EntityType.Value} from {source.Name} is turned off." };

        // A film standing for one episode is written by the source's film
        // provider, and anything claiming the whole anime by its series one.
        if (candidate.AnidbEpisodeID is not null ? TryMovieProvider(source) is null : TryProvider(source, MetadataEntityType.Series) is null)
            return new()
            {
                Reason = MatchRejectionReason.KindDisabled,
                Details = candidate.AnidbEpisodeID is not null
                    ? $"Nothing links a film to one episode from {source.Name}."
                    : $"Nothing links a {entry.EntityType.Value} to a whole anime from {source.Name}.",
            };

        return null;

        static MetadataAutoLinkRejection Invalid(string details)
            => new() { Reason = MatchRejectionReason.InvalidID, Details = details };
    }

    #endregion

    #region Match ratings

    /// <inheritdoc />
    public async Task<IReadOnlyList<IMetadataCrossReference>> SetMatchRating(
        IEnumerable<IMetadataCrossReference> links,
        MatchRating matchRating = MatchRating.UserVerified,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(links);
        if (!Enum.IsDefined(matchRating))
            throw new ArgumentOutOfRangeException(nameof(matchRating), matchRating, "Not a match rating.");

        using var linkChanges = _linkChanges.Begin(MetadataLinkChangeReason.Verify);
        // Every link is found again before anything is written, so a stale
        // copy never writes back what it remembers.
        var found = new List<IMetadataCrossReference>();
        foreach (var link in links)
        {
            ArgumentNullException.ThrowIfNull(link, nameof(links));
            if (Stored(link) is { } stored && !found.Contains(stored))
                found.Add(stored);
        }

        // Only the rating changes. A link keeps its writer, so each writer's
        // links are written on their own.
        foreach (var group in found.Where(link => link.MatchRating != matchRating).GroupBy(link => (link.EntityType, link.WrittenBy)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new MetadataLinkUpdateOptions { WrittenBy = group.Key.WrittenBy };
            switch (group.Key.EntityType)
            {
                case var entityType when entityType == MetadataEntityType.Series:
                    await crossReferences.MergeSeriesLinks(
                        [.. group.Cast<IMetadataSeriesCrossReference>().Select(link => link.ToLinkData() with { MatchRating = matchRating })],
                        options: options,
                        cancellationToken: cancellationToken
                    ).ConfigureAwait(false);
                    break;
                case var entityType when entityType == MetadataEntityType.Movie:
                    await crossReferences.MergeMovieLinks(
                        [.. group.Cast<IMetadataMovieCrossReference>().Select(link => link.ToLinkData() with { MatchRating = matchRating })],
                        options: options,
                        cancellationToken: cancellationToken
                    ).ConfigureAwait(false);
                    break;
                default:
                    await crossReferences.MergeEpisodeLinks(
                        [.. group.Cast<IMetadataEpisodeCrossReference>().Select(link => link.ToLinkData() with { MatchRating = matchRating })],
                        options: options,
                        cancellationToken: cancellationToken
                    ).ConfigureAwait(false);
                    break;
            }
        }

        return [.. found.Select(link => Stored(link) ?? link)];
    }

    /// <summary>
    ///   Finds a link in the store again, by its level, source, AniDB entry
    ///   and provider entry.
    /// </summary>
    /// <param name="link">The link, as read back at some point.</param>
    /// <returns>
    ///   The stored link, or <see langword="null"/> when it is gone or of a
    ///   level that is not stored.
    /// </returns>
    private IMetadataCrossReference? Stored(IMetadataCrossReference link)
        => link switch
        {
            IMetadataSeriesCrossReference series => crossReferences.GetSeriesLinks(series.AnidbAnimeID, series.Source)
                .FirstOrDefault(stored => stored.ProviderID == series.ProviderID),
            IMetadataMovieCrossReference movie => crossReferences.GetMovieLinks(movie.AnidbEpisodeID, movie.Source)
                .FirstOrDefault(stored => stored.ProviderID == movie.ProviderID),
            IMetadataEpisodeCrossReference episode => crossReferences.GetEpisodeLinks(episode.AnidbEpisodeID, episode.Source)
                .FirstOrDefault(stored => stored.ProviderID == episode.ProviderID),
            _ => null,
        };

    #endregion

    #region Bulk removal

    /// <inheritdoc />
    public async Task<int> RemoveAllLinks(
        MetadataSource source,
        bool removeSeriesLinks = true,
        bool removeMovieLinks = true,
        bool purge = false,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        using var linkChanges = _linkChanges.Begin();
        var removing = new List<IMetadataCrossReference>();
        var seriesLevel = crossReferences.GetAllSeriesLinks(source);
        if (removeSeriesLinks)
        {
            removing.AddRange(seriesLevel.Where(link => link.ProviderID?.EntityType != MetadataEntityType.Movie));
            removing.AddRange(crossReferences.GetAllEpisodeLinks(source));
        }

        if (removeMovieLinks)
        {
            removing.AddRange(seriesLevel.Where(link => link.ProviderID?.EntityType == MetadataEntityType.Movie));
            removing.AddRange(crossReferences.GetAllMovieLinks(source));
        }

        return await Remove(removing, purge, cancellationToken, progress).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RemoveLinksForAnime(
        MetadataSource source,
        int anidbAnimeID,
        MetadataEntityType? entityType = null,
        bool purge = false,
        bool disableAutoLinking = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        using var linkChanges = _linkChanges.Begin();
        var removing = new List<IMetadataCrossReference>();
        var seriesLevel = crossReferences.GetSeriesLinks(anidbAnimeID, source);
        if (entityType is null || entityType == MetadataEntityType.Series)
        {
            // The episode links under the removed series go with them, and
            // so do the ones naming no series once no series link is left.
            var series = seriesLevel.Where(link => link.ProviderID?.EntityType != MetadataEntityType.Movie).ToList();
            var lastLink = entityType is null || series.Count == seriesLevel.Count;
            var removedSeries = series.Select(link => link.ProviderID).ToHashSet();
            removing.AddRange(series);
            if (entityType is not null)
                removing.AddRange(crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source)
                    .Where(link => link.ProviderParentID is { } parentID ? removedSeries.Contains(parentID) : lastLink && series.Count > 0));
        }

        if (entityType is null || entityType == MetadataEntityType.Movie)
        {
            removing.AddRange(seriesLevel.Where(link => link.ProviderID?.EntityType == MetadataEntityType.Movie));
            removing.AddRange(crossReferences.GetMovieLinksForSeries(anidbAnimeID, source));
        }

        if (entityType is null || entityType == MetadataEntityType.Episode)
            removing.AddRange(crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source));

        var removed = await Remove(removing, purge, cancellationToken).ConfigureAwait(false);
        if (disableAutoLinking && removed > 0)
            DisableAutoLinking(source, anidbAnimeID);

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> RemoveLinksForEpisode(
        MetadataSource source,
        int anidbEpisodeID,
        MetadataEntityType? entityType = null,
        bool purge = false,
        bool disableAutoLinking = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        using var linkChanges = _linkChanges.Begin();
        var removing = new List<IMetadataCrossReference>();
        if (entityType is null || entityType == MetadataEntityType.Movie)
            removing.AddRange(crossReferences.GetMovieLinks(anidbEpisodeID, source));
        if (entityType is null || entityType == MetadataEntityType.Episode)
            removing.AddRange(crossReferences.GetEpisodeLinks(anidbEpisodeID, source));

        var removed = await Remove(removing, purge, cancellationToken).ConfigureAwait(false);
        if (disableAutoLinking && removed > 0 &&
            (removing.FirstOrDefault()?.AnidbAnimeID ?? anidbEpisodeRepository.GetByEpisodeID(anidbEpisodeID)?.AnimeID) is { } animeID)
            DisableAutoLinking(source, animeID);

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> RemoveLinksTo(MetadataGuid providerID, bool purge = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerID);

        using var linkChanges = _linkChanges.Begin();
        var removing = new List<IMetadataCrossReference>(crossReferences.GetLinksTo(providerID));
        if (providerID.EntityType == MetadataEntityType.Series)
        {
            removing.AddRange(crossReferences.GetEpisodeLinksInto(providerID));

            // An anime left with no series link loses its episode links naming
            // none too, as removing its last series link one at a time does.
            removing.AddRange(crossReferences.GetUnparentedEpisodeLinksLeftBy(providerID, removing.Select(link => link.AnidbAnimeID)));
        }

        var removed = await Remove(removing, purge: false, cancellationToken).ConfigureAwait(false);
        if (purge && providerID.EntityType != MetadataEntityType.Episode)
            await providerScheduler.SchedulePurge(providerID, cancellationToken: cancellationToken).ConfigureAwait(false);

        return removed;
    }

    /// <summary>
    ///   Removes links of every level, and queues the purge of what they
    ///   pointed at when asked: the series or film a whole-work link names,
    ///   and the series an episode link points into.
    /// </summary>
    /// <param name="links">The links, as read back from the store.</param>
    /// <param name="purge">Whether to queue the purges.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <param name="progress">Told how far the removal is, from 0 to 100.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    private async Task<int> Remove(
        IReadOnlyCollection<IMetadataCrossReference> links,
        bool purge,
        CancellationToken cancellationToken,
        IProgress<decimal>? progress = null
    )
    {
        var distinct = links.Distinct().ToList();
        var series = distinct.OfType<IMetadataSeriesCrossReference>().ToList();
        var movies = distinct.OfType<IMetadataMovieCrossReference>().ToList();
        var episodes = distinct.OfType<IMetadataEpisodeCrossReference>().ToList();

        // Each level weighs by its links, and the purges as a third of them.
        var stages = new StagedProgress(progress, series.Count + 1, movies.Count + 1, episodes.Count + 1, purge ? (distinct.Count / 3) + 1 : 0);
        stages.Report(0);
        var removed = 0;
        if (series.Count > 0)
            removed += (await crossReferences.MergeSeriesLinks([], series, cancellationToken: cancellationToken).ConfigureAwait(false)).Count;
        stages.NextStage();
        if (movies.Count > 0)
            removed += (await crossReferences.MergeMovieLinks([], movies, cancellationToken: cancellationToken).ConfigureAwait(false)).Count;
        stages.NextStage();
        if (episodes.Count > 0)
            removed += (await crossReferences.MergeEpisodeLinks([], episodes, cancellationToken: cancellationToken).ConfigureAwait(false)).Count;
        stages.NextStage();

        if (!purge)
        {
            stages.Complete();
            return removed;
        }

        var entries = series.Select(link => link.ProviderID)
            .Concat(movies.Select(link => link.ProviderID))
            .Concat(episodes.Select(link => link.ProviderParentID))
            .OfType<MetadataGuid>()
            .Distinct()
            .ToList();
        var items = new ItemProgress(stages, entries.Count);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await providerScheduler.SchedulePurge(entry, cancellationToken: cancellationToken).ConfigureAwait(false);
            items.Increment();
        }

        stages.Complete();
        return removed;
    }

    /// <summary>
    ///   Tells a source to leave an anime alone, when the anime is in the
    ///   library.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    private void DisableAutoLinking(MetadataSource source, int anidbAnimeID)
    {
        if (seriesRepository.GetByAnimeID(anidbAnimeID) is { } series)
            SetAutoLinkingDisabled(series, source, true);
    }

    #endregion

    #region Episodes

    /// <inheritdoc />
    public async Task<bool> SetEpisodeLink(MetadataEpisodeLinkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var linkChanges = _linkChanges.Begin();
        CheckRequest(request.Source, MetadataEntityType.Episode, request.ProviderID, nameof(request));
        CheckRequest(request.Source, MetadataEntityType.Series, request.ProviderSeriesID, nameof(request));
        var provider = Provider(request.Source, MetadataEntityType.Episode);

        // The series and season come from the core's copy of the source, or the request's series for an
        // episode not held yet. A core source's link reads them live, so the store keeps none for it.
        var episode = request.ProviderID is { } providerID
            ? metadataService.GetEpisode(providerID)
            : null;
        var seriesID = request.ProviderID is null ? null : episode?.SeriesID ?? request.ProviderSeriesID;

        // A link to nothing replaces the episode's other links, and a link kept
        // beside the others replaces a link to nothing.
        var additive = request.Additive && request.ProviderID is not null;
        await crossReferences.MergeEpisodeLinks(
            [
                new()
                {
                    Source = request.Source,
                    AnidbAnimeID = request.AnidbAnimeID,
                    AnidbEpisodeID = request.AnidbEpisodeID,
                    ProviderID = request.ProviderID,
                    ProviderParentID = seriesID,
                    SeasonID = episode?.SeasonID,
                    SeasonNumber = episode?.SeasonNumber,
                    EpisodeNumber = episode?.EpisodeNumber,
                    MatchRating = request.MatchRating,
                },
            ],
            options: new() { ReplaceExisting = !additive, WrittenBy = WriterOf(provider) },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        if (additive)
        {
            var toNothing = crossReferences.GetEpisodeLinks(request.AnidbEpisodeID, request.Source)
                .Where(link => link.ProviderID is null)
                .ToList();
            if (toNothing.Count > 0)
                await crossReferences.MergeEpisodeLinks([], toNothing, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // A replacing write leaves this the episode's only link, so there is
        // only something to place when the others are kept.
        if (additive && request.Ordering is { } ordering)
        {
            var links = crossReferences.GetEpisodeLinks(request.AnidbEpisodeID, request.Source).ToList();
            var index = links.FindIndex(link => link.ProviderID == request.ProviderID);
            if (index is not -1)
            {
                var link = links[index];
                links.RemoveAt(index);
                links.Insert(Math.Clamp(ordering, 0, links.Count), link);
                await crossReferences.OrderLinks(links, cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }

    /// <inheritdoc />
    public Task<bool> SetEpisodeLink(
        MetadataSource source,
        int anidbEpisodeID,
        MetadataGuid? providerEpisodeID,
        bool additive = true,
        int? ordering = null,
        MetadataGuid? providerSeriesID = null,
        CancellationToken cancellationToken = default
    )
    {
        // The anime is looked up the once, here, so that no provider has to.
        if (anidbEpisodeRepository.GetByEpisodeID(anidbEpisodeID) is not { } anidbEpisode)
            return Task.FromResult(false);

        return SetEpisodeLink(new()
        {
            Source = source,
            EntityType = MetadataEntityType.Episode,
            ProviderID = providerEpisodeID,
            ProviderSeriesID = providerSeriesID,
            AnidbEpisodeID = anidbEpisodeID,
            AnidbAnimeID = anidbEpisode.AnimeID,
            Additive = additive,
            Ordering = ordering,
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ResetEpisodeLinks(MetadataSource source, int anidbAnimeID, bool allowAutoMatch, CancellationToken cancellationToken = default)
    {
        using var linkChanges = _linkChanges.Begin();

        // Clearing links works while the source's provider is off, as any
        // removal does; the refusals are then recorded as nobody's.
        var provider = TryProvider(source, MetadataEntityType.Episode);
        await crossReferences
            .RemoveLinksForSeries(source, anidbAnimeID, MetadataEntityType.Episode, cancellationToken)
            .ConfigureAwait(false);

        // A person's no is kept as a link to nothing on each episode, which matching
        // leaves alone; with no series linked there is nothing to match into.
        var hasSeries = crossReferences.GetSeriesLinks(anidbAnimeID, source).Any(link => link.ProviderID?.EntityType != MetadataEntityType.Movie);
        if (!allowAutoMatch && hasSeries)
        {
            var refusals = anidbEpisodeRepository.GetByAnimeID(anidbAnimeID)
                .Where(episode => episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special)
                .Select(episode => new MetadataEpisodeLinkData
                {
                    Source = source,
                    AnidbAnimeID = anidbAnimeID,
                    AnidbEpisodeID = episode.EpisodeID,
                    ProviderID = null,
                    MatchRating = MatchRating.UserVerified,
                })
                .ToList();
            if (refusals.Count > 0)
                await crossReferences.MergeEpisodeLinks(
                    refusals,
                    options: new() { ReplaceExisting = true, WrittenBy = provider is null ? null : WriterOf(provider) },
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IMetadataEpisodeCrossReference>> MatchEpisodes(
        int anidbAnimeID,
        MetadataGuid providerSeriesID,
        MetadataGuid? providerSeasonID = null,
        bool useExisting = false,
        bool save = false,
        bool? considerOtherLinks = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(providerSeriesID);
        var source = providerSeriesID.Source;
        CheckRequest(source, MetadataEntityType.Series, providerSeriesID, nameof(providerSeriesID));
        CheckRequest(source, MetadataEntityType.Season, providerSeasonID, nameof(providerSeasonID));
        var provider = Provider(source, MetadataEntityType.Episode);
        if (anidbAnimeRepository.GetByAnimeID(anidbAnimeID) is not { } anime)
            return [];

        // Only the levels that are ever linked, so a provider is not left to
        // decide what counts as an episode.
        var anidbEpisodes = anidbEpisodeRepository.GetByAnimeID(anidbAnimeID)
            .Where(episode => episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special)
            .Cast<IAnidbEpisode>()
            .ToList();
        var existing = useExisting ? crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source) : null;

        var matched = await provider
            .MatchEpisodes(anime, anidbEpisodes, providerSeriesID, providerSeasonID, existing, considerOtherLinks, cancellationToken)
            .ConfigureAwait(false);

        // A core source's link reads its season and numbers live, so the
        // preview carries none either, as the store would keep none.
        var links = matched
            .Select(match => match.ToLinkData(source))
            .Select(link => source.IsCore ? link with { SeasonID = null, SeasonNumber = null, EpisodeNumber = null } : link)
            .ToList();
        if (save)
        {
            // The result is the whole picture for each episode it names, the
            // kept links included, so it replaces what those episodes had.
            using var linkChanges = _linkChanges.Begin();
            await crossReferences.MergeEpisodeLinks(
                links,
                options: new() { ReplaceExisting = true, WrittenBy = WriterOf(provider) },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        // What the provider left alone is still part of the picture, so the
        // links already on record come back beside the ones it came up with.
        var kept = existing?.Where(link => !matched.Any(match => match.AnidbEpisode.AnidbID == link.AnidbEpisodeID)) ?? [];
        return
        [
            .. kept,
            .. links.Select(IMetadataEpisodeCrossReference (link, index) => new CrossRef_AniDB_Metadata_Episode
            {
                Source = link.Source,
                AnidbAnimeID = link.AnidbAnimeID,
                AnidbEpisodeID = link.AnidbEpisodeID,
                ProviderID = CrossRef_AniDB_Metadata.ToStored(link.ProviderID),
                ProviderParentID = CrossRef_AniDB_Metadata.ToStored(link.ProviderParentID),
                ProviderSeasonID = link.SeasonID?.ID,
                SeasonNumber = link.SeasonNumber,
                EpisodeNumber = link.EpisodeNumber,
                MatchRating = link.MatchRating,
                Ordering = matched[index].Ordering,
            }),
        ];
    }

    #endregion

    /// <summary>
    ///   The ID recorded on the links a provider's answers become.
    /// </summary>
    /// <param name="provider">The provider the links came from.</param>
    /// <returns>Its registered ID.</returns>
    private Guid? WriterOf(IMetadataProvider provider)
        => providerManager.GetProviderInfo(provider)?.ID;

    /// <summary>
    ///   The series provider that owns a source, if it takes links for this
    ///   level.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The level linked.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="NotSupportedException">No enabled provider links the level on the source.</exception>
    private IMetadataSeriesLinkingProvider Provider(MetadataSource source, MetadataEntityType entityType)
        => TryProvider(source, entityType) ?? throw Unsupported(source, entityType);

    /// <summary>
    ///   The series provider that owns a source, if it takes links for this
    ///   level and is on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The level linked.</param>
    /// <returns>The provider, or <c>null</c> when none is.</returns>
    private IMetadataSeriesLinkingProvider? TryProvider(MetadataSource source, MetadataEntityType entityType)
        => providerManager.GetAvailableProviders(entityType, source)
            .Select(info => info.Provider)
            .OfType<IMetadataSeriesLinkingProvider>()
            .FirstOrDefault(provider => provider.LinkableEntityTypes.Contains(entityType));

    /// <summary>
    ///   The movie provider that owns a source, films being claimed whole and
    ///   so having no level to check.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="NotSupportedException">No enabled provider links films on the source.</exception>
    private IMetadataMovieLinkingProvider MovieProvider(MetadataSource source)
        => TryMovieProvider(source) ?? throw Unsupported(source, MetadataEntityType.Movie);

    /// <summary>
    ///   The movie provider that owns a source, if one is on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The provider, or <c>null</c> when none is.</returns>
    private IMetadataMovieLinkingProvider? TryMovieProvider(MetadataSource source)
        => providerManager.GetAvailableProviders(MetadataEntityType.Movie, source)
            .Select(info => info.Provider)
            .OfType<IMetadataMovieLinkingProvider>()
            .FirstOrDefault();

    /// <summary>
    ///   The provider the settings name to work out what an anime is for a
    ///   source. It hands back what it finds, and this service links it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The auto-linker.</returns>
    /// <exception cref="NotSupportedException">No enabled provider is set to auto-link the source.</exception>
    private IMetadataAutoLinkingProvider AutoLinker(MetadataSource source)
        => providerManager.GetAvailableProviders()
            .Where(info => info.Source == source && info.IsAutoLinker)
            .Select(info => info.Provider)
            .OfType<IMetadataAutoLinkingProvider>()
            .FirstOrDefault()
            ?? throw new NotSupportedException($"No provider is set to work out what an anime is from {source}.");

    /// <summary>
    ///   Refuses a request whose provider entry is not on the source it names,
    ///   or not of the kind being linked.
    /// </summary>
    /// <param name="source">The source the request names.</param>
    /// <param name="entityType">The kind of entry being linked.</param>
    /// <param name="providerID">The provider entry, or <c>null</c> for none.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <exception cref="ArgumentException">The entry is on another source, or of another kind.</exception>
    private static void CheckRequest(MetadataSource source, MetadataEntityType entityType, MetadataGuid? providerID, string paramName)
    {
        if (providerID is not null && (providerID.Source != source || providerID.EntityType != entityType))
            throw new ArgumentException($"\"{providerID}\" is not a {entityType.Value} on {source.Value}.", paramName);
    }

    private static NotSupportedException Unsupported(MetadataSource source, MetadataEntityType entityType)
        => new($"Nothing can link a {entityType} from {source}.");
}
