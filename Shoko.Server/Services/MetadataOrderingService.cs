using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

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
/// <param name="coreSources">The core sources that keep orderings in their own tables.</param>
/// <param name="seriesService">Updates a Shoko series' stats when one of its episodes is hidden or shown.</param>
/// <param name="groupService">Updates a Shoko group's stats likewise.</param>
/// <param name="cleanup">Removes the image links of the orderings and groups that go.</param>
/// <param name="logger">The logger.</param>
public class MetadataOrderingService(
    Metadata_OrderingRepository orderingRepository,
    Metadata_Ordering_GroupRepository groupRepository,
    Metadata_Ordering_EntryRepository entryRepository,
    IOrderingRowState rowState,
    AnimeEpisodeRepository shokoEpisodeRepository,
    MetadataTextStore textStore,
    Lazy<IMetadataService> metadataService,
    Lazy<IEnumerable<ICoreOrderingSource>> coreSources,
    Lazy<AnimeSeriesService> seriesService,
    Lazy<AnimeGroupService> groupService,
    Lazy<MetadataEntityCleanup> cleanup,
    ILogger<MetadataOrderingService> logger
) : IMetadataOrderingService
{
    /// <summary>
    ///   The start of a plugin source's default ordering IDs, which a global
    ///   ordering's ID may not start with.
    /// </summary>
    internal const string DefaultIDPrefix = "default/";

    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly Lock _writeLock = new();

    #region Identity

    /// <summary>
    ///   The ID of a series' default ordering: the series' own ID for a
    ///   source the core keeps, and <c>default/</c> and the series' ID for a
    ///   plugin's source, or a hash of the ID when that would be too long.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The default ordering's ID, under the series' source.</returns>
    public static MetadataGuid DefaultOrderingID(MetadataGuid seriesID)
    {
        if (seriesID.Source.IsCore)
            return new(seriesID.Source, MetadataEntityType.Ordering, seriesID.ID);

        var id = DefaultIDPrefix + seriesID.ID;
        if (id.Length > MetadataGuid.MaxIDLength)
            id = DefaultIDPrefix + "#" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seriesID.ID)));
        return new(seriesID.Source, MetadataEntityType.Ordering, id);
    }

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
            .. CoreSourcesFor(series.ID.Source).SelectMany(source => source.GetOrderings(series)),
            .. Stored(orderingRepository.GetBySeries(series.ID)),
        ];
    }

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
        return Stored(orderingRepository.GetBySource(source).Where(row => GetSeries(row.SeriesGuid) is not null));
    }

    /// <inheritdoc />
    public IOrdering GetDefaultOrdering(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return DefaultFor(series);
    }

    /// <inheritdoc />
    public IReadOnlyList<IEpisodeOrderingInformation> GetEpisodeOrderings(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var series = episode.Series ?? GetSeries(episode.SeriesID);
        var places = new List<IEpisodeOrderingInformation> { DefaultPlaceFor(episode, series) };
        places.AddRange(CoreSourcesFor(episode.ID.Source).SelectMany(source => source.GetEpisodeOrderings(episode)));

        var orderingIDs = entryRepository.GetByEpisode(episode.ID)
            .Select(entry => (entry.Source, entry.OrderingID))
            .Distinct()
            .ToList();
        var orderings = orderingIDs
            .Select(ordering => orderingRepository.GetByProviderID(ordering.Source, ordering.OrderingID))
            .OfType<Metadata_Ordering>()
            .Where(row => row.SeriesGuid == episode.SeriesID);
        foreach (var ordering in Stored(orderings))
        {
            foreach (var group in ((StoredOrdering)ordering).Groups)
            {
                for (var position = 0; position < group.Places.Count; position++)
                {
                    if (group.Places[position].Episode.ID == episode.ID)
                        places.Add(new StoredEpisodeOrdering(group, group.Places[position].Episode, position));
                }
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
            GetSeries(ordering.SeriesGuid) is null)
            return null;

        return new StoredOrdering(ordering, this).Groups.FirstOrDefault(stored => stored.ID == groupID);
    }

    /// <summary>
    ///   Reads the groups of a stored ordering, with the episodes still
    ///   available in each, in order.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    /// <returns>The groups, in viewing order.</returns>
    internal IReadOnlyList<StoredOrderingGroup> ReadGroups(StoredOrdering ordering)
    {
        var row = ordering.Row;
        var entries = entryRepository.GetByOrderingID(row.Source, row.ProviderID)
            .GroupBy(entry => entry.GroupID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.Position).ToList(), StringComparer.Ordinal);
        var episodes = new Dictionary<MetadataGuid, IEpisode?>();
        var groups = groupRepository.GetByOrderingID(row.Source, row.ProviderID);
        var seasonNumbers = NumberGroups([.. groups.Select(group => group.IsSpecial)]);
        return
        [
            .. groups.Select((group, index) => new StoredOrderingGroup(
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

        IEpisode? Episode(MetadataGuid id)
        {
            if (!episodes.TryGetValue(id, out var episode))
                episodes[id] = episode = metadataService.Value.GetEpisode(id);
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
    ///   Looks up a stored ordering, or one a core source keeps, but not a
    ///   default one.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <returns>The ordering, or <c>null</c>.</returns>
    private IOrdering? FindOrdering(MetadataGuid orderingID)
    {
        if (orderingRepository.GetByProviderID(orderingID.Source, orderingID.ID) is { } row)
            return new StoredOrdering(row, this);

        return CoreSourcesFor(orderingID.Source)
            .Select(source => source.GetOrdering(orderingID))
            .FirstOrDefault(ordering => ordering is not null);
    }

    /// <summary>
    ///   Stored orderings as read back: global before local, oldest first.
    /// </summary>
    /// <param name="rows">The orderings' rows.</param>
    /// <returns>The orderings.</returns>
    private IReadOnlyList<IOrdering> Stored(IEnumerable<Metadata_Ordering> rows)
        => [.. rows
            .OrderBy(row => row.Source == MetadataSource.User)
            .ThenBy(row => row.CreatedAt)
            .ThenBy(row => row.Metadata_OrderingID)
            .Select(row => new StoredOrdering(row, this))];

    /// <summary>
    ///   The default ordering of a series, in its core source's own view
    ///   when the source has one.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The default ordering.</returns>
    private IOrdering DefaultFor(ISeries series)
        => CoreSourcesFor(series.ID.Source)
            .Select(source => source.GetDefaultOrdering(series, this))
            .FirstOrDefault(ordering => ordering is not null) ?? new DefaultOrdering(series, this);

    /// <summary>
    ///   An episode's place in the default ordering of its series, in its
    ///   core source's own view when the source has one.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="series">The episode's series, if it is available.</param>
    /// <returns>The place.</returns>
    private IEpisodeOrderingInformation DefaultPlaceFor(IEpisode episode, ISeries? series)
        => CoreSourcesFor(episode.ID.Source)
            .Select(source => source.GetDefaultEpisodeOrdering(episode, series, this))
            .FirstOrDefault(place => place is not null) ?? new DefaultEpisodeOrdering(episode, series, this);

    /// <summary>
    ///   The core sources that keep orderings of a source's series.
    /// </summary>
    /// <param name="source">The series' source.</param>
    /// <returns>The core sources, usually none.</returns>
    private IEnumerable<ICoreOrderingSource> CoreSourcesFor(MetadataSource source)
        => source.IsCore ? coreSources.Value.Where(core => core.Source == source) : [];

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

            groups.Add(new(
                group.ID.ID,
                CheckName(group.Name, nameof(ordering)),
                group.Overview,
                CheckEpisodes(group.Episodes, episodes, series, nameof(ordering)),
                group.IsSpecial
            ));
        }

        CheckSpecialGroups(groups, nameof(ordering));
        return Write(ordering.ID, series.ID, ordering.Type, ordering.Name, ordering.Overview, groups, false, nameof(ordering))!;
    }

    /// <inheritdoc />
    public bool RemoveOrdering(MetadataGuid orderingID)
    {
        MetadataEntries.CheckEntry(orderingID, MetadataEntityType.Ordering, nameof(orderingID));
        return Remove(orderingID);
    }

    /// <summary>
    ///   Every source keeping global orderings: the core sources with
    ///   orderings in tables of their own, and the sources with stored ones.
    /// </summary>
    /// <returns>The sources.</returns>
    internal IReadOnlyList<MetadataSource> GetGlobalOrderingSources()
        => coreSources.Value.Select(core => core.Source)
            .Concat(orderingRepository.GetAll().Select(row => row.Source).Where(source => source != MetadataSource.User))
            .Distinct()
            .ToList();

    /// <summary>
    ///   Whether a source keeps global orderings: a plugin source, or a core
    ///   source with orderings in tables of its own.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal bool KeepsGlobalOrderings(MetadataSource source)
        => !source.IsCore || CoreSourcesFor(source).Any();

    /// <summary>
    ///   Removes every global ordering of a source, with its groups and the
    ///   choices of it. The users' own orderings are kept.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="progress">Told how far the removal is, from 0 to 100.</param>
    /// <param name="token">Stops the removal between two orderings.</param>
    /// <returns>How many orderings were removed.</returns>
    /// <exception cref="ArgumentException">The source keeps no global orderings.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    internal int RemoveGlobalOrderings(MetadataSource source, IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!KeepsGlobalOrderings(source))
            throw new ArgumentException($"{source} keeps no global orderings.", nameof(source));

        if (source.IsCore)
            return CoreSourcesFor(source).Sum(core => core.RemoveAllOrderings(progress, token));

        var rows = orderingRepository.GetBySource(source).ToList();
        var items = new ItemProgress(progress, rows.Count);
        items.Report(0);
        var removed = 0;
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
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
                group.IsSpecial
            ));
        }

        CheckSpecialGroups(groups, nameof(ordering));
        var orderingID = new MetadataGuid(MetadataSource.User, MetadataEntityType.Ordering, NewLocalID());
        return Write(orderingID, series.ID, OrderingType.User, ordering.Name, ordering.Overview, groups, false, nameof(ordering))!;
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
                group.IsSpecial
            ));
        }

        CheckSpecialGroups(groups, nameof(ordering));
        return Write(orderingID, series.ID, OrderingType.User, ordering.Name, ordering.Overview, groups, true, nameof(ordering));
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
    private sealed record GroupToWrite(string ID, string Name, string? Description, IReadOnlyList<MetadataGuid> Episodes, bool IsSpecial);

    /// <summary>
    ///   Gives each group of a stored ordering its season number: <c>0</c>
    ///   for the special group, and the others their place among themselves,
    ///   from <c>1</c>.
    /// </summary>
    /// <param name="isSpecial">Whether each group is special, in viewing order.</param>
    /// <returns>The season numbers, in the same order.</returns>
    internal static int[] NumberGroups(IReadOnlyList<bool> isSpecial)
    {
        var numbers = new int[isSpecial.Count];
        var next = 1;
        for (var index = 0; index < numbers.Length; index++)
            numbers[index] = isSpecial[index] ? 0 : next++;
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
    private StoredOrdering? Write(
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
    private StoredOrdering? WriteLocked(
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

            if (orderingID.EntityType != MetadataEntityType.Ordering || FindOrdering(orderingID) is not { } ordering || ordering.SeriesID != series.ID)
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
            FindOrdering(orderingID) is { } ordering &&
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
            removed = [.. orderings.Select(row => row.ID), .. groups.Select(group => group.ID)];
            logger.LogDebug("Removed {OrderingCount} orderings of the removed series {Series}.", count, seriesID);
        }

        RemoveImageLinks(removed);
        return count;
    }

    #endregion
}
