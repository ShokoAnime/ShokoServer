using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps the stored orderings in the core's own tables, cached in memory,
///   and the ordering chosen for each series and the hidden episodes on the
///   series' and episodes' own rows, and makes every series' default ordering
///   from its seasons.
/// </summary>
/// <param name="orderingRepository">The stored orderings.</param>
/// <param name="groupRepository">Their groups.</param>
/// <param name="entryRepository">The episodes' places in the groups.</param>
/// <param name="rowState">The ordering chosen for each series and the hidden flags of every source's episodes but Shoko's.</param>
/// <param name="shokoEpisodeRepository">The Shoko episodes, whose hidden flag is set with their stats.</param>
/// <param name="textStore">Writes an ordering's rows in one transaction, removing the texts of the orderings that go, and tells the text manager.</param>
/// <param name="metadataService">Finds the series and episodes an ordering names.</param>
/// <param name="seriesService">Updates a Shoko series' stats when one of its episodes is hidden or shown.</param>
/// <param name="groupService">Updates a Shoko group's stats likewise.</param>
/// <param name="cleanup">Removes the image links of the orderings and groups that go.</param>
/// <param name="studioStore">Keeps the networks of the orderings, with stubs for the users' own.</param>
/// <param name="logger">The logger.</param>
public class MetadataOrderingService(
    Metadata_OrderingRepository orderingRepository,
    Metadata_Ordering_GroupRepository groupRepository,
    Metadata_Ordering_EntryRepository entryRepository,
    IOrderingRowState rowState,
    AnimeEpisodeRepository shokoEpisodeRepository,
    MetadataTextStore textStore,
    Lazy<IMetadataService> metadataService,
    Lazy<AnimeSeriesService> seriesService,
    Lazy<AnimeGroupService> groupService,
    Lazy<MetadataEntityCleanup> cleanup,
    MetadataStudioStore studioStore,
    ILogger<MetadataOrderingService> logger
) : IMetadataOrderingService, IDisposable
{
    /// <summary>
    ///   The start of a plugin source's default ordering IDs, which a global
    ///   ordering's ID may not start with.
    /// </summary>
    internal const string DefaultIDPrefix = IOrdering.DefaultIDPrefix;

    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly Lock _writeLock = new();

    /// <summary>
    ///   The places of each series' default ordering, dropped when the series
    ///   or one of its episodes is updated.
    /// </summary>
    private readonly ConcurrentDictionary<MetadataGuid, OrderingPlaces> _defaultPlacements = new();

    /// <summary>
    ///   Counts the drops of each series' kept places, so places built
    ///   during a drop are not kept. AniDB and Shoko series share one count.
    /// </summary>
    private readonly ConcurrentDictionary<MetadataGuid, int> _placementGenerations = new();

    /// <summary>
    ///   The key AniDB and Shoko series share in <see cref="_placementGenerations"/>.
    /// </summary>
    private static readonly MetadataGuid _anidbPlacementKey = new(MetadataSource.AniDB, MetadataEntityType.Series, "*");

    /// <summary>
    ///   Whether the service listens to the series and episode updates.
    /// </summary>
    private int _listening;

    #region Identity

    /// <summary>
    ///   The ID of a series' default ordering, see
    ///   <see cref="IOrdering.DefaultOrderingID"/>.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The default ordering's ID, under the series' source.</returns>
    public static MetadataGuid DefaultOrderingID(MetadataGuid seriesID)
        => IOrdering.DefaultOrderingID(seriesID);

    /// <summary>
    ///   The series a default ordering's ID names, when it can be read back.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <returns>The series, or <c>null</c> when the ID is not a readable default ordering ID.</returns>
    private static MetadataGuid? SeriesOfDefaultOrdering(MetadataGuid orderingID)
    {
        if (orderingID.Source.IsCore)
            return new(orderingID.Source, MetadataEntityType.Series, orderingID.ID);
        if (!orderingID.ID.StartsWith(DefaultIDPrefix, StringComparison.Ordinal) || orderingID.ID.StartsWith(DefaultIDPrefix + "#", StringComparison.Ordinal))
            return null;

        var seriesID = orderingID.ID[DefaultIDPrefix.Length..];
        return MetadataEntries.ToGuid(orderingID.Source, MetadataEntityType.Series, seriesID);
    }

    /// <summary>
    ///   A new ID for a user's ordering or group.
    /// </summary>
    /// <returns>The ID.</returns>
    private static string NewLocalID()
        => Guid.NewGuid().ToString("N");

    #endregion

    #region Reading

    /// <inheritdoc />
    public IReadOnlyList<IOrdering> GetOrderings(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return
        [
            DefaultFor(series),
            .. InReadingOrder(orderingRepository.GetBySeries(series.ID)).Select(row => StoredFor(row, series)),
        ];
    }

    /// <summary>
    ///   Every ordering of a series whose seasons and episodes are typed, the
    ///   default one first, read with them typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The orderings.</returns>
    internal IReadOnlyList<IOrdering<TSeries, TEpisode>> GetOrderings<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => [DefaultFor<TSeries, TEpisode>(series), .. OtherOrderings<TSeries, TEpisode>(series)];

    /// <inheritdoc />
    public IReadOnlyList<IOrdering> GetOrderings(MetadataGuid seriesID)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        return GetSeries(seriesID) is { } series ? GetOrderings(series) : [];
    }

    /// <inheritdoc />
    public IOrdering? GetOrdering(MetadataGuid orderingID)
    {
        ArgumentNullException.ThrowIfNull(orderingID);
        if (orderingID.EntityType != MetadataEntityType.Ordering)
            return null;
        if (FindOrdering(orderingID) is { } ordering)
            return GetSeries(ordering.SeriesID) is not null ? ordering : null;

        return SeriesOfDefaultOrdering(orderingID) is { } seriesID &&
            GetSeries(seriesID) is { } series &&
            DefaultOrderingID(series.ID) == orderingID
                ? DefaultFor(series)
                : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IOrdering> GetStoredOrderings(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return
        [
            .. InReadingOrder(orderingRepository.GetBySource(source))
                .Select(row => GetSeries(row.SeriesGuid) is { } series ? StoredFor(row, series) : null)
                .OfType<IOrdering>(),
        ];
    }

    /// <inheritdoc />
    public IOrdering GetDefaultOrdering(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return DefaultFor(series);
    }

    /// <summary>
    ///   The default ordering of a series whose seasons and episodes are
    ///   typed, read with them typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The default ordering.</returns>
    internal IOrdering<TSeries, TEpisode> GetDefaultOrdering<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => DefaultFor<TSeries, TEpisode>(series);

    /// <inheritdoc />
    public IReadOnlyList<IEpisodeOrderingInformation> GetEpisodeOrderings(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return episode switch
        {
            IShokoEpisode shoko => GetEpisodeOrderings<IShokoSeries, IShokoEpisode>(shoko),
            IAnidbEpisode anidb => GetEpisodeOrderings<IAnidbAnime, IAnidbEpisode>(anidb),
            IEpisode<ISeries, IEpisode> typed => GetEpisodeOrderings<ISeries, IEpisode>(typed),
            _ => [new DefaultEpisodeOrdering(episode, episode.Series ?? GetSeries(episode.SeriesID), this), .. OtherPlaces<ISeries, IEpisode>(episode)],
        };
    }

    /// <summary>
    ///   Every place an episode whose series is typed has in the series'
    ///   orderings, read with them typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="episode">The episode, an <see cref="IEpisode{TSeries,TEpisode}"/>.</param>
    /// <returns>The places, its place in the default ordering first.</returns>
    internal IReadOnlyList<IEpisodeOrderingInformation<TSeries, TEpisode>> GetEpisodeOrderings<TSeries, TEpisode>(TEpisode episode)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
    {
        var series = (episode.Series ?? GetSeries(episode.SeriesID)) as TSeries;
        return [new DefaultEpisodeOrdering<TSeries, TEpisode>(episode, series, this), .. OtherPlaces<TSeries, TEpisode>(episode)];
    }

    /// <summary>
    ///   The places an episode has in its series' orderings other than the
    ///   default one.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="episode">The episode.</param>
    /// <returns>The places, the core sources' first.</returns>
    private List<IEpisodeOrderingInformation<TSeries, TEpisode>> OtherPlaces<TSeries, TEpisode>(IEpisode episode)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
    {
        var places = new List<IEpisodeOrderingInformation<TSeries, TEpisode>>();

        var orderingIDs = entryRepository.GetByEpisode(episode.ID)
            .Select(entry => (entry.Source, entry.OrderingID))
            .Distinct()
            .ToList();
        var orderings = orderingIDs
            .Select(ordering => orderingRepository.GetByProviderID(ordering.Source, ordering.OrderingID))
            .OfType<Metadata_Ordering>()
            .Where(row => row.SeriesGuid == episode.SeriesID);
        foreach (var ordering in InReadingOrder(orderings).Select(row => new StoredOrdering<TSeries, TEpisode>(row, this)))
        {
            if (ordering.EpisodeByID(episode.ID) is not { } typed)
                continue;

            var airing = ordering.Placement.AiringOf(episode.ID);
            foreach (var place in ordering.Placement.PlacesOf(episode.ID))
            {
                if (ordering.Groups.FirstOrDefault(group => group.ID == place.GroupID) is { } group)
                    places.Add(new StoredEpisodeOrdering<TSeries, TEpisode>(group, typed, place.EpisodeNumber, place.IsSpecial ? airing : null));
            }
        }

        return places;
    }

    /// <summary>
    ///   Looks up one group of a stored ordering, as a season.
    /// </summary>
    /// <param name="groupID">The group.</param>
    /// <returns>The group, or <c>null</c> when no stored ordering has it.</returns>
    public ISeason? GetGroup(MetadataGuid groupID)
    {
        ArgumentNullException.ThrowIfNull(groupID);
        if (groupID.EntityType != MetadataEntityType.Season ||
            groupRepository.GetByProviderID(groupID.Source, groupID.ID) is not { } group ||
            orderingRepository.GetByProviderID(group.Source, group.OrderingID) is not { } ordering ||
            GetSeries(ordering.SeriesGuid) is not { } series)
            return null;

        return StoredFor(ordering, series).Seasons.FirstOrDefault(stored => stored.ID == groupID);
    }

    /// <summary>
    ///   Reads the groups of a stored ordering, with the episodes still
    ///   available in each, in order.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="ordering">The ordering.</param>
    /// <returns>The groups, in viewing order.</returns>
    internal IReadOnlyList<StoredOrderingGroup<TSeries, TEpisode>> ReadGroups<TSeries, TEpisode>(StoredOrdering<TSeries, TEpisode> ordering)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
    {
        var row = ordering.Row;
        var entries = entryRepository.GetByOrderingID(row.Source, row.ProviderID)
            .GroupBy(entry => entry.GroupID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.Position).ToList(), StringComparer.Ordinal);
        var episodes = new Dictionary<MetadataGuid, TEpisode?>();
        var groups = groupRepository.GetByOrderingID(row.Source, row.ProviderID);
        var seasonNumbers = NumberGroups([.. groups.Select(group => (group.IsSpecial, group.SeasonNumber))]);
        return
        [
            .. groups.Select((group, index) => new StoredOrderingGroup<TSeries, TEpisode>(
                ordering,
                group,
                seasonNumbers[index],
                [
                    .. (entries.TryGetValue(group.ProviderID, out var places) ? places : [])
                        .Select(entry => (Entry: entry, Episode: Episode(entry.EpisodeGuid)))
                        .Where(place => place.Episode is not null)
                        .Select(place => (place.Entry, place.Episode!)),
                ]
            )),
        ];

        TEpisode? Episode(MetadataGuid id)
        {
            if (!episodes.TryGetValue(id, out var episode))
                episodes[id] = episode = metadataService.Value.GetEpisode(id) as TEpisode;
            return episode;
        }
    }

    /// <summary>
    ///   Looks up a series on any source.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The series, or <c>null</c> when it is not available.</returns>
    internal ISeries? GetSeries(MetadataGuid seriesID)
        => metadataService.Value.GetSeries(seriesID);

    /// <summary>
    ///   Reads the networks of a stored ordering, each as its source serves
    ///   it when it can, so a user's ordering shows a source's network by
    ///   its name. A stub no source serves reads with an empty name.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <returns>The networks, in order.</returns>
    internal IReadOnlyList<INetwork> GetNetworks(MetadataGuid orderingID)
        => [.. studioStore.GetNetworks(orderingID).Select(network => metadataService.Value.GetEntry(network.ID) as INetwork ?? network)];

    /// <summary>
    ///   Looks up a stored ordering, but not a default one.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <param name="series">The series it should order, when known, to save looking it up.</param>
    /// <returns>The ordering, or <c>null</c>.</returns>
    private IOrdering? FindOrdering(MetadataGuid orderingID, ISeries? series = null)
        => orderingRepository.GetByProviderID(orderingID.Source, orderingID.ID) is { } row
            ? StoredFor(row, series is not null && series.ID == row.SeriesGuid ? series : GetSeries(row.SeriesGuid))
            : null;

    /// <summary>
    ///   Looks up a stored ordering, but not a default one, read with its
    ///   series and episodes typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="orderingID">The ordering.</param>
    /// <returns>The ordering, or <c>null</c>.</returns>
    private IOrdering<TSeries, TEpisode>? FindOrdering<TSeries, TEpisode>(MetadataGuid orderingID)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => orderingRepository.GetByProviderID(orderingID.Source, orderingID.ID) is { } row
            ? new StoredOrdering<TSeries, TEpisode>(row, this)
            : null;

    /// <summary>
    ///   The orderings of a series other than the default one: the stored
    ///   ones.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series.</param>
    /// <returns>The orderings.</returns>
    private IEnumerable<IOrdering<TSeries, TEpisode>> OtherOrderings<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        =>
        [
            .. InReadingOrder(orderingRepository.GetBySeries(series.ID)).Select(row => new StoredOrdering<TSeries, TEpisode>(row, this)),
        ];

    /// <summary>
    ///   Stored orderings' rows in the order they are read back: global
    ///   before local, oldest first.
    /// </summary>
    /// <param name="rows">The orderings' rows.</param>
    /// <returns>The rows, in order.</returns>
    private static IEnumerable<Metadata_Ordering> InReadingOrder(IEnumerable<Metadata_Ordering> rows)
        => rows
            .OrderBy(row => row.Source == MetadataSource.User)
            .ThenBy(row => row.CreatedAt)
            .ThenBy(row => row.Metadata_OrderingID);

    /// <summary>
    ///   A stored ordering, read with the types of the series it orders.
    /// </summary>
    /// <param name="row">The ordering's row.</param>
    /// <param name="series">The series it orders, if it is available.</param>
    /// <returns>The ordering.</returns>
    private IOrdering StoredFor(Metadata_Ordering row, ISeries? series) => series switch
    {
        IShokoSeries => new StoredOrdering<IShokoSeries, IShokoEpisode>(row, this),
        IAnidbAnime => new StoredOrdering<IAnidbAnime, IAnidbEpisode>(row, this),
        _ => new StoredOrdering<ISeries, IEpisode>(row, this),
    };

    /// <summary>
    ///   The default ordering of a series, read with its types when its
    ///   seasons and episodes are typed.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The default ordering.</returns>
    private IOrdering DefaultFor(ISeries series) => series switch
    {
        IShokoSeries shoko => DefaultFor<IShokoSeries, IShokoEpisode>(shoko),
        IAnidbAnime anime => DefaultFor<IAnidbAnime, IAnidbEpisode>(anime),
        ISeries<ISeries, IEpisode> typed => DefaultFor<ISeries, IEpisode>(typed),
        _ => new DefaultOrdering(series, this),
    };

    /// <summary>
    ///   The default ordering of a series whose seasons and episodes are
    ///   typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The default ordering.</returns>
    private DefaultOrdering<TSeries, TEpisode> DefaultFor<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => new(series, this);

    #endregion

    #region Default Placement

    /// <summary>
    ///   The places of a series' default ordering, its specials placed where
    ///   they air without leaving season 0. Kept per series until the series
    ///   or one of its episodes is updated.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The places, keyed by the series' season IDs.</returns>
    internal OrderingPlaces GetDefaultPlacement(ISeries series)
    {
        Listen();
        if (_defaultPlacements.TryGetValue(series.ID, out var cached))
            return cached;

        var generation = _placementGenerations.GetValueOrDefault(GenerationKey(series.ID));
        var places = DefaultOrderingPlacement.Build(series);
        if (_placementGenerations.GetValueOrDefault(GenerationKey(series.ID)) == generation)
            _defaultPlacements[series.ID] = places;
        return places;
    }

    /// <summary>
    ///   Starts listening to the series and episode updates, once.
    /// </summary>
    private void Listen()
    {
        if (Interlocked.Exchange(ref _listening, 1) is 1)
            return;

        ShokoEventHandler.Instance.SeriesUpdated += OnSeriesUpdated;
        ShokoEventHandler.Instance.EpisodeUpdated += OnEpisodeUpdated;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _listening, 0) is 0)
            return;

        ShokoEventHandler.Instance.SeriesUpdated -= OnSeriesUpdated;
        ShokoEventHandler.Instance.EpisodeUpdated -= OnEpisodeUpdated;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///   Drops the kept places of an updated series.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The update.</param>
    private void OnSeriesUpdated(object? sender, SeriesInfoUpdatedEventArgs eventArgs)
    {
        if (eventArgs.SeriesInfo?.ID is { } seriesID)
            ForgetDefaultPlacement(seriesID);
    }

    /// <summary>
    ///   Drops the kept places of the series of an updated episode.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The update.</param>
    private void OnEpisodeUpdated(object? sender, EpisodeInfoUpdatedEventArgs eventArgs)
    {
        if (eventArgs.EpisodeInfo?.SeriesID is { } seriesID)
            ForgetDefaultPlacement(seriesID);
    }

    /// <summary>
    ///   Drops the kept places of a series. A Shoko series reads its AniDB
    ///   anime's titles, so an AniDB or Shoko update drops them all.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    internal void ForgetDefaultPlacement(MetadataGuid seriesID)
    {
        _placementGenerations.AddOrUpdate(GenerationKey(seriesID), 1, (_, generation) => generation + 1);
        if (seriesID.Source != MetadataSource.AniDB && seriesID.Source != MetadataSource.Shoko)
        {
            _defaultPlacements.TryRemove(seriesID, out _);
            return;
        }

        foreach (var key in _defaultPlacements.Keys)
        {
            if (key.Source == MetadataSource.AniDB || key.Source == MetadataSource.Shoko)
                _defaultPlacements.TryRemove(key, out _);
        }
    }

    /// <summary>
    ///   The key a series' drops are counted under.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The series, or the key AniDB and Shoko series share.</returns>
    private static MetadataGuid GenerationKey(MetadataGuid seriesID)
        => seriesID.Source == MetadataSource.AniDB || seriesID.Source == MetadataSource.Shoko ? _anidbPlacementKey : seriesID;

    #endregion

    #region Global Orderings

    /// <inheritdoc />
    public IOrdering SaveOrdering(MetadataOrderingData ordering)
    {
        ArgumentNullException.ThrowIfNull(ordering);
        MetadataEntries.CheckEntry(ordering.ID, MetadataEntityType.Ordering, nameof(ordering));
        if (ordering.ID.ID.StartsWith(DefaultIDPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"\"{ordering.ID}\" starts with \"{DefaultIDPrefix}\", which the default orderings use.", nameof(ordering));
        if (ordering.Type is OrderingType.Default or OrderingType.User)
            throw new ArgumentException($"The type {ordering.Type} is kept by the core for its own orderings.", nameof(ordering));

        var source = ordering.ID.Source;
        var series = CheckSeries(ordering.SeriesID, ordering.Name, nameof(ordering));
        var episodes = EpisodesOf(series);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<GroupToWrite>();
        foreach (var group in ordering.Groups ?? [])
        {
            ArgumentNullException.ThrowIfNull(group, nameof(ordering));
            MetadataEntries.CheckReference(group.ID, source, MetadataEntityType.Season, nameof(ordering));
            if (!seen.Add(group.ID.ID))
                throw new ArgumentException($"The group \"{group.ID}\" is given twice.", nameof(ordering));

            if (group.SeasonNumber is { } seasonNumber && (seasonNumber < 1 || group.IsSpecial))
            {
                throw new ArgumentException(
                    $"The group \"{group.ID}\" may not be numbered {seasonNumber}: a group's own number is at least 1, and the special group takes none.",
                    nameof(ordering)
                );
            }

            groups.Add(new(
                group.ID.ID,
                CheckName(group.Name, nameof(ordering)),
                group.Overview,
                CheckEpisodes(group.Episodes, episodes, series, nameof(ordering)),
                group.IsSpecial,
                group.SeasonNumber
            ));
        }

        CheckSpecialGroups(groups, nameof(ordering));
        var networks = CheckNetworks(ordering.Networks ?? [], source, nameof(ordering));
        var stored = Write(ordering.ID, series.ID, ordering.Type, ordering.Name, ordering.Overview, groups, false, nameof(ordering))!;
        studioStore.SetNetworks(ordering.ID, networks);
        return stored;
    }

    /// <inheritdoc />
    public bool RemoveOrdering(MetadataGuid orderingID)
    {
        MetadataEntries.CheckEntry(orderingID, MetadataEntityType.Ordering, nameof(orderingID));
        return Remove(orderingID);
    }

    /// <summary>
    ///   Every source keeping global orderings: the sources with stored ones.
    /// </summary>
    /// <returns>The sources.</returns>
    internal IReadOnlyList<MetadataSource> GetGlobalOrderingSources()
        => [.. orderingRepository.GetAll().Select(row => row.Source).Where(source => source != MetadataSource.User).Distinct()];

    /// <summary>
    ///   Whether a source keeps global orderings: any source outside the core.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><c>true</c> when it does.</returns>
    internal bool KeepsGlobalOrderings(MetadataSource source)
        => !source.IsCore;

    /// <inheritdoc />
    public int RemoveOrderings(MetadataSource source, IProgress<decimal>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!KeepsGlobalOrderings(source))
            throw new ArgumentException($"{source} keeps no global orderings.", nameof(source));

        var rows = orderingRepository.GetBySource(source).ToList();
        var items = new ItemProgress(progress, rows.Count);
        items.Report(0);
        var removed = 0;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Remove(row.ID))
                removed++;

            items.Increment();
        }

        return removed;
    }

    #endregion

    #region Local Orderings

    /// <inheritdoc />
    public IOrdering CreateLocalOrdering(MetadataLocalOrderingData ordering)
    {
        ArgumentNullException.ThrowIfNull(ordering);
        var series = CheckSeries(ordering.SeriesID, ordering.Name, nameof(ordering));
        var episodes = EpisodesOf(series);
        var groups = new List<GroupToWrite>();
        foreach (var group in ordering.Groups ?? [])
        {
            ArgumentNullException.ThrowIfNull(group, nameof(ordering));
            if (group.ID is not null)
                throw new ArgumentException($"A new ordering's groups get their IDs from the core, but one names \"{group.ID}\".", nameof(ordering));

            groups.Add(new(
                NewLocalID(),
                CheckName(group.Name, nameof(ordering)),
                group.Overview,
                CheckEpisodes(group.Episodes, episodes, series, nameof(ordering)),
                group.IsSpecial,
                null
            ));
        }

        CheckSpecialGroups(groups, nameof(ordering));
        var networks = MetadataStudioStore.CheckUserOrderingNetworks(ordering.Networks ?? [], nameof(ordering));
        var orderingID = new MetadataGuid(MetadataSource.User, MetadataEntityType.Ordering, NewLocalID());
        var stored = Write(orderingID, series.ID, OrderingType.User, ordering.Name, ordering.Overview, groups, false, nameof(ordering))!;
        if (networks.Count > 0)
            studioStore.SetUserOrderingNetworks(orderingID, networks);
        return stored;
    }

    /// <inheritdoc />
    public IOrdering? UpdateLocalOrdering(MetadataGuid orderingID, MetadataLocalOrderingData ordering)
    {
        CheckLocal(orderingID, nameof(orderingID));
        ArgumentNullException.ThrowIfNull(ordering);
        if (orderingRepository.GetByProviderID(MetadataSource.User, orderingID.ID) is not { } stored)
            return null;
        if (ordering.SeriesID != stored.SeriesGuid)
            throw new ArgumentException($"The ordering \"{orderingID}\" orders \"{stored.SeriesGuid}\", not \"{ordering.SeriesID}\".", nameof(ordering));

        var series = CheckSeries(ordering.SeriesID, ordering.Name, nameof(ordering));
        var episodes = EpisodesOf(series);
        var existing = groupRepository.GetByOrderingID(MetadataSource.User, orderingID.ID).Select(group => group.ProviderID).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<GroupToWrite>();
        foreach (var group in ordering.Groups ?? [])
        {
            ArgumentNullException.ThrowIfNull(group, nameof(ordering));
            string groupID;
            if (group.ID is { } kept)
            {
                if (kept.Source != MetadataSource.User || kept.EntityType != MetadataEntityType.Season || !existing.Contains(kept.ID))
                    throw new ArgumentException($"The ordering \"{orderingID}\" has no group \"{kept}\".", nameof(ordering));
                if (!seen.Add(kept.ID))
                    throw new ArgumentException($"The group \"{kept}\" is given twice.", nameof(ordering));
                groupID = kept.ID;
            }
            else
            {
                groupID = NewLocalID();
            }

            groups.Add(new(
                groupID,
                CheckName(group.Name, nameof(ordering)),
                group.Overview,
                CheckEpisodes(group.Episodes, episodes, series, nameof(ordering)),
                group.IsSpecial,
                null
            ));
        }

        CheckSpecialGroups(groups, nameof(ordering));
        var networks = ordering.Networks is { } given ? MetadataStudioStore.CheckUserOrderingNetworks(given, nameof(ordering)) : null;
        var updated = Write(orderingID, series.ID, OrderingType.User, ordering.Name, ordering.Overview, groups, true, nameof(ordering));
        if (updated is not null && networks is not null)
            studioStore.SetUserOrderingNetworks(orderingID, networks);
        return updated;
    }

    /// <inheritdoc />
    public bool DeleteLocalOrdering(MetadataGuid orderingID)
    {
        CheckLocal(orderingID, nameof(orderingID));
        return Remove(orderingID);
    }

    #endregion

    #region Writing

    /// <summary>
    ///   One group to write, checked.
    /// </summary>
    /// <param name="ID">The source's ID for the group.</param>
    /// <param name="Name">The group's name.</param>
    /// <param name="Description">What the group is about, if anything.</param>
    /// <param name="Episodes">The group's episodes, in order.</param>
    /// <param name="IsSpecial">Whether the group holds the ordering's specials.</param>
    /// <param name="SeasonNumber">The season number the source gave the group, if any.</param>
    private sealed record GroupToWrite(string ID, string Name, string? Description, IReadOnlyList<MetadataGuid> Episodes, bool IsSpecial, int? SeasonNumber);

    /// <summary>
    ///   Gives each group of a stored ordering its season number: <c>0</c>
    ///   for the special group, the number the source gave a group when it
    ///   gave one, and else the group's place among the regular groups.
    /// </summary>
    /// <param name="groups">Whether each group is special, and the number its source gave it, in viewing order.</param>
    /// <returns>The season numbers, in the same order.</returns>
    internal static int[] NumberGroups(IReadOnlyList<(bool IsSpecial, int? SeasonNumber)> groups)
    {
        var numbers = new int[groups.Count];
        var next = 1;
        for (var index = 0; index < numbers.Length; index++)
        {
            if (groups[index].IsSpecial)
                continue;

            numbers[index] = groups[index].SeasonNumber ?? next;
            next++;
        }

        return numbers;
    }

    /// <summary>
    ///   Checks that at most one group of an ordering is special.
    /// </summary>
    /// <param name="groups">The groups, in order.</param>
    /// <param name="paramName">The argument the ordering came in through.</param>
    /// <exception cref="ArgumentException">More than one group is special.</exception>
    private static void CheckSpecialGroups(IReadOnlyList<GroupToWrite> groups, string paramName)
    {
        var special = groups.Where(group => group.IsSpecial).Select(group => $"\"{group.ID}\"").ToList();
        if (special.Count > 1)
            throw new ArgumentException($"An ordering has at most one special group, but {special.Count} are: {string.Join(", ", special)}.", paramName);
    }

    /// <summary>
    ///   Writes an ordering whole, replacing its groups and places, and
    ///   removes the image links of the groups that go. Nothing is written
    ///   when nothing changed. What another writer could change meanwhile is
    ///   checked again under the write lock.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <param name="seriesID">The series it orders.</param>
    /// <param name="type">What it follows.</param>
    /// <param name="name">Its name.</param>
    /// <param name="description">What it is about, if anything.</param>
    /// <param name="groups">Its groups, checked, in order.</param>
    /// <param name="mustExist">Only update the ordering, writing nothing when it is gone.</param>
    /// <param name="paramName">The argument the ordering came in through.</param>
    /// <returns>The stored ordering, or <c>null</c> when <paramref name="mustExist"/> is set and it is gone.</returns>
    /// <exception cref="ArgumentException">A group's ID is taken by another ordering of the source.</exception>
    private StoredOrdering<ISeries, IEpisode>? Write(
        MetadataGuid orderingID,
        MetadataGuid seriesID,
        OrderingType type,
        string name,
        string? description,
        IReadOnlyList<GroupToWrite> groups,
        bool mustExist,
        string paramName
    )
    {
        var removedGroups = new List<MetadataGuid>();
        var stored = WriteLocked(orderingID, seriesID, type, name, description, groups, mustExist, paramName, removedGroups);
        RemoveImageLinks(removedGroups);
        return stored;
    }

    /// <summary>
    ///   Writes an ordering whole under the write lock, for
    ///   <see cref="Write"/>.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <param name="seriesID">The series it orders.</param>
    /// <param name="type">What it follows.</param>
    /// <param name="name">Its name.</param>
    /// <param name="description">What it is about, if anything.</param>
    /// <param name="groups">Its groups, checked, in order.</param>
    /// <param name="mustExist">Only update the ordering, writing nothing when it is gone.</param>
    /// <param name="paramName">The argument the ordering came in through.</param>
    /// <param name="removedGroups">Gets the groups the write removed.</param>
    /// <returns>The stored ordering, or <c>null</c> when <paramref name="mustExist"/> is set and it is gone.</returns>
    /// <exception cref="ArgumentException">A group's ID is taken by another ordering of the source.</exception>
    private StoredOrdering<ISeries, IEpisode>? WriteLocked(
        MetadataGuid orderingID,
        MetadataGuid seriesID,
        OrderingType type,
        string name,
        string? description,
        IReadOnlyList<GroupToWrite> groups,
        bool mustExist,
        string paramName,
        List<MetadataGuid> removedGroups
    )
    {
        var source = orderingID.Source;
        lock (_writeLock)
        {
            var now = DateTime.Now;
            var stored = orderingRepository.GetByProviderID(source, orderingID.ID);
            if (mustExist && stored is null)
                return null;

            foreach (var group in groups)
            {
                if (groupRepository.GetByProviderID(source, group.ID) is not { } other || other.OrderingID == orderingID.ID)
                    continue;

                var groupID = new MetadataGuid(source, MetadataEntityType.Season, group.ID);
                throw new ArgumentException($"The group \"{groupID}\" is taken by the ordering \"{other.OrderingGuid}\".", paramName);
            }
            var row = MetadataRows.Copy(stored) ?? new Metadata_Ordering { CreatedAt = now };
            row.Source = source;
            row.ProviderID = orderingID.ID;
            row.SeriesSource = seriesID.Source;
            row.SeriesID = seriesID.ID;
            row.Type = type;
            row.Name = name;
            row.Description = string.IsNullOrEmpty(description) ? null : description;

            var (groupsSaving, groupsDeleting) = MetadataRows.Replace(
                groupRepository.GetByOrderingID(source, orderingID.ID),
                groups,
                group => group.ProviderID,
                group => group.ID,
                (groupRow, group, position) =>
                {
                    groupRow.Source = source;
                    groupRow.ProviderID = group.ID;
                    groupRow.OrderingID = orderingID.ID;
                    groupRow.Position = position;
                    groupRow.IsSpecial = group.IsSpecial;
                    groupRow.SeasonNumber = group.SeasonNumber;
                    groupRow.Name = group.Name;
                    groupRow.Description = string.IsNullOrEmpty(group.Description) ? null : group.Description;
                },
                (before, after) => before.SameAs(after)
            );
            var places = groups
                .SelectMany(group => group.Episodes.Select((episode, position) => (GroupID: group.ID, Episode: episode, Position: position)))
                .ToList();
            var (entriesSaving, entriesDeleting) = MetadataRows.Replace(
                entryRepository.GetByOrderingID(source, orderingID.ID),
                places,
                entry => (entry.GroupID, entry.EpisodeSource, entry.EpisodeID),
                place => (place.GroupID, place.Episode.Source, place.Episode.ID),
                (entryRow, place, _) =>
                {
                    entryRow.Source = source;
                    entryRow.OrderingID = orderingID.ID;
                    entryRow.GroupID = place.GroupID;
                    entryRow.Position = place.Position;
                    entryRow.EpisodeSource = place.Episode.Source;
                    entryRow.EpisodeID = place.Episode.ID;
                },
                (before, after) => before.SameAs(after)
            );

            var changed = stored is null || !stored.SameAs(row) ||
                groupsSaving.Count > 0 || groupsDeleting.Count > 0 ||
                entriesSaving.Count > 0 || entriesDeleting.Count > 0;
            if (!changed)
                return new(stored!, this);

            row.LastUpdatedAt = now;

            // The groups' texts are never removed here: a group's ID is a
            // season's under its source, which a stored season may share.
            textStore.WriteWithoutEntries(
                [],
                new MetadataRowChanges<Metadata_Ordering>(orderingRepository, [row], []),
                new MetadataRowChanges<Metadata_Ordering_Group>(groupRepository, groupsSaving, groupsDeleting),
                new MetadataRowChanges<Metadata_Ordering_Entry>(entryRepository, entriesSaving, entriesDeleting)
            );
            removedGroups.AddRange(groupsDeleting.Select(group => group.ID));
            logger.LogDebug("Stored the ordering {Ordering} of {Series} with {Count} groups.", orderingID, seriesID, groups.Count);
            return new(row, this);
        }
    }

    /// <summary>
    ///   Removes a stored ordering with its groups and places, the choice of
    ///   it for its series, and the image links of it and its groups.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <returns><c>true</c> if it was stored.</returns>
    private bool Remove(MetadataGuid orderingID)
    {
        List<MetadataGuid> removed;
        lock (_writeLock)
        {
            if (orderingRepository.GetByProviderID(orderingID.Source, orderingID.ID) is not { } row)
                return false;

            var groups = groupRepository.GetByOrderingID(row.Source, row.ProviderID);
            textStore.WriteWithoutEntries(
                [orderingID],
                new MetadataRowChanges<Metadata_Ordering>(orderingRepository, [], [row]),
                new MetadataRowChanges<Metadata_Ordering_Group>(groupRepository, [], [.. groups]),
                new MetadataRowChanges<Metadata_Ordering_Entry>(entryRepository, [], [.. entryRepository.GetByOrderingID(row.Source, row.ProviderID)])
            );
            ForgetChoiceLocked(row.SeriesGuid, orderingID);
            removed = [row.ID, .. groups.Select(group => group.ID)];
            logger.LogDebug("Removed the ordering {Ordering}.", orderingID);
        }

        RemoveNetworks([orderingID]);
        RemoveImageLinks(removed);
        return true;
    }

    /// <summary>
    ///   Removes the image links of orderings and groups that are gone, out
    ///   of the write lock.
    /// </summary>
    /// <param name="entries">The orderings and groups.</param>
    private void RemoveImageLinks(IReadOnlyList<MetadataGuid> entries)
    {
        if (entries.Count > 0)
            cleanup.Value.RemoveImageLinks(entries);
    }

    /// <summary>
    ///   Removes the networks of orderings that are gone, out of the write
    ///   lock.
    /// </summary>
    /// <param name="orderings">The orderings.</param>
    private void RemoveNetworks(IEnumerable<MetadataGuid> orderings)
    {
        foreach (var orderingID in orderings)
        {
            if (orderingID.Source == MetadataSource.User)
                studioStore.RemoveUserOrderingNetworks(orderingID);
            else if (!orderingID.Source.IsCore)
                studioStore.RemoveNetworks(orderingID);
        }
    }

    #endregion

    #region Validation

    /// <summary>
    ///   Checks the series an ordering is for and the ordering's name.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <param name="name">The ordering's name.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The series.</returns>
    /// <exception cref="ArgumentNullException">The series or the name is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The ID does not name a series, it is not available, or the name is blank.</exception>
    private ISeries CheckSeries(MetadataGuid seriesID, string name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(seriesID, paramName);
        CheckName(name, paramName);
        if (seriesID.EntityType != MetadataEntityType.Series)
            throw new ArgumentException($"\"{seriesID}\" does not name a series.", paramName);

        return GetSeries(seriesID) ?? throw new ArgumentException($"The series \"{seriesID}\" is not available.", paramName);
    }

    /// <summary>
    ///   Checks a name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentNullException">The name is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    private static string CheckName(string name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(name, paramName);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An ordering and each of its groups need a name.", paramName);
        return name;
    }

    /// <summary>
    ///   The IDs of a series' episodes.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The IDs.</returns>
    private static HashSet<MetadataGuid> EpisodesOf(ISeries series)
        => [.. series.Episodes.Select(episode => episode.ID)];

    /// <summary>
    ///   Checks a group's episodes.
    /// </summary>
    /// <param name="episodes">The group's episodes.</param>
    /// <param name="seriesEpisodes">The series' episodes.</param>
    /// <param name="series">The series.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The episodes, in order.</returns>
    /// <exception cref="ArgumentNullException">An episode is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An episode is not the series'.</exception>
    private static IReadOnlyList<MetadataGuid> CheckEpisodes(IReadOnlyList<MetadataGuid>? episodes, HashSet<MetadataGuid> seriesEpisodes, ISeries series, string paramName)
    {
        foreach (var episode in episodes ?? [])
        {
            ArgumentNullException.ThrowIfNull(episode, paramName);
            if (!seriesEpisodes.Contains(episode))
                throw new ArgumentException($"\"{episode}\" is not an episode of the series \"{series.ID}\".", paramName);
        }

        return episodes ?? [];
    }

    /// <summary>
    ///   Checks a global ordering's networks before anything is written.
    /// </summary>
    /// <param name="networks">The networks.</param>
    /// <param name="source">The ordering's source, which they must be on.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The networks, in order.</returns>
    /// <exception cref="ArgumentNullException">A network is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A network is on another source, names another kind or is not stored.</exception>
    private IReadOnlyList<MetadataGuid> CheckNetworks(IReadOnlyList<MetadataGuid> networks, MetadataSource source, string paramName)
    {
        foreach (var network in networks)
        {
            MetadataEntries.CheckReference(network, source, MetadataEntityType.Network, paramName);
            if (studioStore.GetNetwork(network) is null)
                throw new ArgumentException($"The network \"{network}\" is not stored.", paramName);
        }

        return networks;
    }

    /// <summary>
    ///   Checks that an ID names a user's ordering.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <exception cref="ArgumentNullException">The ID is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The ID is not under <c>user</c> or does not name an ordering.</exception>
    private static void CheckLocal(MetadataGuid orderingID, string paramName)
    {
        ArgumentNullException.ThrowIfNull(orderingID, paramName);
        if (orderingID.Source != MetadataSource.User || orderingID.EntityType != MetadataEntityType.Ordering)
            throw new ArgumentException($"\"{orderingID}\" does not name a user's ordering.", paramName);
    }

    #endregion

    #region Preferred Ordering

    /// <inheritdoc />
    public IOrdering GetPreferredOrdering(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return Chosen(series) ?? DefaultFor(series);
    }

    /// <summary>
    ///   The ordering chosen for a series whose seasons and episodes are
    ///   typed, or its default one, read with them typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The ordering.</returns>
    internal IOrdering<TSeries, TEpisode> GetPreferredOrdering<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => Chosen<TSeries, TEpisode>(series) ?? DefaultFor<TSeries, TEpisode>(series);

    /// <inheritdoc />
    public bool SetPreferredOrdering(MetadataGuid seriesID, MetadataGuid? orderingID)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (seriesID.EntityType != MetadataEntityType.Series)
            throw new ArgumentException($"\"{seriesID}\" does not name a series.", nameof(seriesID));
        var series = GetSeries(seriesID) ?? throw new ArgumentException($"The series \"{seriesID}\" is not available.", nameof(seriesID));
        if (!rowState.HasSeries(series.ID))
            throw new ArgumentException($"The series \"{seriesID}\" is not stored, so no ordering can be chosen for it.", nameof(seriesID));

        lock (_writeLock)
        {
            if (orderingID is null || orderingID == DefaultOrderingID(series.ID))
                return rowState.SetPreferredOrdering(series.ID, null);

            if (orderingID.EntityType != MetadataEntityType.Ordering || FindOrdering(orderingID, series) is not { } ordering || ordering.SeriesID != series.ID)
                throw new ArgumentException($"\"{orderingID}\" is not an ordering of the series \"{series.ID}\".", nameof(orderingID));

            return rowState.SetPreferredOrdering(series.ID, orderingID);
        }
    }

    /// <summary>
    ///   Whether an ordering is the one chosen for a series, falling back to
    ///   the default one when none is chosen or the chosen one is gone.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="orderingID">One of its orderings.</param>
    /// <returns><c>true</c> if it is the one in use.</returns>
    internal bool IsPreferred(ISeries series, MetadataGuid orderingID)
        => (Chosen(series)?.ID ?? DefaultOrderingID(series.ID)) == orderingID;

    /// <summary>
    ///   Whether an ordering is the one chosen for a series, without looking
    ///   whether it is still there.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <param name="orderingID">The ordering.</param>
    /// <returns><c>true</c> if it is the chosen one.</returns>
    internal bool IsChosen(MetadataGuid seriesID, MetadataGuid orderingID)
        => rowState.GetPreferredOrdering(seriesID) == orderingID;

    /// <summary>
    ///   Forgets the choice of an ordering that is gone, which only the
    ///   series it orders can have made.
    /// </summary>
    /// <param name="seriesID">The series the ordering ordered.</param>
    /// <param name="orderingID">The ordering.</param>
    /// <returns><c>true</c> if the series had chosen it.</returns>
    private bool ForgetChoiceLocked(MetadataGuid seriesID, MetadataGuid orderingID)
        => rowState.GetPreferredOrdering(seriesID) == orderingID && rowState.SetPreferredOrdering(seriesID, null);

    /// <summary>
    ///   The ordering chosen for a series, if one is and it is still there.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The ordering, or <c>null</c>.</returns>
    private IOrdering? Chosen(ISeries series)
        => rowState.GetPreferredOrdering(series.ID) is { } orderingID &&
            FindOrdering(orderingID, series) is { } ordering &&
            ordering.SeriesID == series.ID
                ? ordering
                : null;

    /// <summary>
    ///   The ordering chosen for a series, if one is and it is still there,
    ///   read with its series and episodes typed.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series.</param>
    /// <returns>The ordering, or <c>null</c>.</returns>
    private IOrdering<TSeries, TEpisode>? Chosen<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => rowState.GetPreferredOrdering(series.ID) is { } orderingID &&
            FindOrdering<TSeries, TEpisode>(orderingID) is { } ordering &&
            ordering.SeriesID == series.ID
                ? ordering
                : null;

    #endregion

    #region Hidden Episodes

    /// <inheritdoc />
    public bool IsEpisodeHidden(MetadataGuid episodeID)
    {
        CheckEpisode(episodeID);
        if (episodeID.Source == MetadataSource.Shoko)
            return episodeID.TryGetNumericID<int>(out var localID) && shokoEpisodeRepository.GetByID(localID) is { IsHidden: true };

        return rowState.IsHidden(episodeID);
    }

    /// <inheritdoc />
    public bool SetEpisodeHidden(MetadataGuid episodeID, bool hidden)
    {
        CheckEpisode(episodeID);
        if (episodeID.Source == MetadataSource.Shoko)
            return SetShokoEpisodeHidden(episodeID, hidden);

        // The flag goes with the episode's row, so an episode that is gone is
        // already shown.
        if (!rowState.HasEpisode(episodeID))
        {
            if (!hidden)
                return false;
            if (metadataService.Value.GetEpisode(episodeID) is null)
                throw new ArgumentException($"The episode \"{episodeID}\" is not available.", nameof(episodeID));
            throw new ArgumentException($"The episode \"{episodeID}\" is not stored, so it cannot be hidden.", nameof(episodeID));
        }

        lock (_writeLock)
            return rowState.SetHidden(episodeID, hidden);
    }

    /// <summary>
    ///   How many of some episodes a user hid.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The count.</returns>
    internal int CountHidden(IEnumerable<IEpisode> episodes)
        => episodes.Count(episode => IsEpisodeHidden(episode.ID));

    /// <summary>
    ///   Hides or shows a Shoko episode through its own flag, and updates its
    ///   series' and group's stats.
    /// </summary>
    /// <param name="episodeID">The Shoko episode.</param>
    /// <param name="hidden">Whether to hide it.</param>
    /// <returns><c>true</c> if the flag changed.</returns>
    /// <exception cref="ArgumentException">The Shoko episode does not exist.</exception>
    private bool SetShokoEpisodeHidden(MetadataGuid episodeID, bool hidden)
    {
        if (!episodeID.TryGetNumericID<int>(out var localID) || shokoEpisodeRepository.GetByID(localID) is not { } episode)
            throw new ArgumentException($"The Shoko episode \"{episodeID}\" does not exist.", nameof(episodeID));
        if (episode.IsHidden == hidden)
            return false;

        episode.IsHidden = hidden;
        shokoEpisodeRepository.Save(episode);
        if (episode.AnimeSeries is { } series)
        {
            seriesService.Value.UpdateStats(series, true, true);
            groupService.Value.UpdateStatsFromTopLevel(series.TopLevelAnimeGroup, true, true);
            ShokoEventHandler.Instance.OnEpisodeUpdated(series, episode, UpdateReason.Updated);
        }

        return true;
    }

    /// <summary>
    ///   Checks that an ID names an episode.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <exception cref="ArgumentNullException">The ID is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The ID names another kind.</exception>
    private static void CheckEpisode(MetadataGuid episodeID)
    {
        ArgumentNullException.ThrowIfNull(episodeID);
        if (episodeID.EntityType != MetadataEntityType.Episode)
            throw new ArgumentException($"\"{episodeID}\" does not name an episode.", nameof(episodeID));
    }

    #endregion

    #region Series Removal

    /// <summary>
    ///   Removes every stored ordering of a series that is gone, of any
    ///   source and the users' own, with the image links of them and their
    ///   groups. Every path that purges or deletes a series calls this, as
    ///   the orderings go with what they order. The choice of one and the
    ///   hidden flags of the series' episodes were kept on the rows that went.
    /// </summary>
    /// <param name="seriesID">The series that is gone, of any source.</param>
    /// <returns>How many orderings were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seriesID"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="seriesID"/> does not name a series.</exception>
    public int RemoveForSeries(MetadataGuid seriesID)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (seriesID.EntityType != MetadataEntityType.Series)
            throw new ArgumentException($"\"{seriesID}\" does not name a series.", nameof(seriesID));

        int count;
        List<MetadataGuid> removed;
        List<MetadataGuid> removedOrderings;
        lock (_writeLock)
        {
            var orderings = orderingRepository.GetBySeries(seriesID);
            if (orderings.Count is 0)
                return 0;

            var groups = orderings.SelectMany(row => groupRepository.GetByOrderingID(row.Source, row.ProviderID)).ToList();
            textStore.WriteWithoutEntries(
                [.. orderings.Select(row => row.ID)],
                new MetadataRowChanges<Metadata_Ordering>(orderingRepository, [], orderings),
                new MetadataRowChanges<Metadata_Ordering_Group>(groupRepository, [], groups),
                new MetadataRowChanges<Metadata_Ordering_Entry>(
                    entryRepository,
                    [],
                    [.. orderings.SelectMany(row => entryRepository.GetByOrderingID(row.Source, row.ProviderID))]
                )
            );
            count = orderings.Count;
            removedOrderings = [.. orderings.Select(row => row.ID)];
            removed = [.. removedOrderings, .. groups.Select(group => group.ID)];
            logger.LogDebug("Removed {OrderingCount} orderings of the removed series {Series}.", count, seriesID);
        }

        RemoveNetworks(removedOrderings);
        RemoveImageLinks(removed);
        return count;
    }

    #endregion
}
