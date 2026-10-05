using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Settings;

using File = Shoko.Server.API.v3.Models.Shoko.File;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The entries of any metadata source, read the same way for every source:
/// its series, seasons, episodes, movies and collections, the people, tags,
/// studios and networks on them, and the AniDB and Shoko entries linked to
/// them.
/// </summary>
/// <remarks>
/// <c>{source}</c> is a registered source's value, an alias or an old spelling,
/// ignoring case; any other answers <c>404</c>. <c>{id}</c> is the source's own
/// ID in one path segment, a <c>/</c> sent as <c>%2F</c>. Reads are open to
/// every user, entries linked to no series included; AniDB and Shoko entries a
/// user may not see are left out, and answer <c>404</c>.
/// </remarks>
[ApiController]
[Route("/api/v{version:apiVersion}/Metadata/{source:metadata-source}")]
[ApiV3]
[Authorize]
public partial class MetadataEntryController : BaseController
{
    #region Fields

    private readonly ILogger<MetadataEntryController> _logger;

    private readonly IMetadataService _metadataService;

    private readonly IMetadataRefreshService _refreshService;

    private readonly IMetadataPurgeService _purgeService;

    private readonly IMetadataOrderingService _orderingService;

    private readonly IMetadataTagStore _tagStore;

    private readonly IMetadataCollectionStore _collectionStore;

    private readonly IFuzzySearchService _fuzzySearch;

    private readonly IMetadataLinkingService _linkingService;

    private readonly IMetadataCrossReferenceStore _crossReferences;

    private readonly MetadataModelBuilder _models;

    private JMMUser? _viewer;

    #endregion

    #region Constructors

    /// <summary>
    /// Takes the services the routes read through.
    /// </summary>
    /// <param name="settingsProvider">The settings.</param>
    /// <param name="logger">Logs refreshes refused while a source is paused.</param>
    /// <param name="metadataService">Resolves entries and links.</param>
    /// <param name="refreshService">Waits out refreshes and queues new ones.</param>
    /// <param name="purgeService">Purges entries.</param>
    /// <param name="orderingService">Reads orderings.</param>
    /// <param name="tagStore">Reads a plugin source's tags.</param>
    /// <param name="collectionStore">Reads a plugin source's collection members.</param>
    /// <param name="fuzzySearch">Matches titles loosely.</param>
    /// <param name="linkingService">Changes how far links are trusted.</param>
    /// <param name="crossReferences">Finds the links to change.</param>
    /// <param name="models">Builds the models sent.</param>
    public MetadataEntryController(
        ISettingsProvider settingsProvider,
        ILogger<MetadataEntryController> logger,
        IMetadataService metadataService,
        IMetadataRefreshService refreshService,
        IMetadataPurgeService purgeService,
        IMetadataOrderingService orderingService,
        IMetadataTagStore tagStore,
        IMetadataCollectionStore collectionStore,
        IFuzzySearchService fuzzySearch,
        IMetadataLinkingService linkingService,
        IMetadataCrossReferenceStore crossReferences,
        MetadataModelBuilder models
    ) : base(settingsProvider)
    {
        _logger = logger;
        _metadataService = metadataService;
        _refreshService = refreshService;
        _purgeService = purgeService;
        _orderingService = orderingService;
        _tagStore = tagStore;
        _collectionStore = collectionStore;
        _fuzzySearch = fuzzySearch;
        _linkingService = linkingService;
        _crossReferences = crossReferences;
        _models = models;
    }

    #endregion

    #region Constants

    internal const string SeriesNotFound = "A series by the given source and `id` was not found.";

    internal const string SeasonNotFound = "A season by the given source and `id` was not found.";

    internal const string EpisodeNotFound = "An episode by the given source and `id` was not found.";

    internal const string MovieNotFound = "A movie by the given source and `id` was not found.";

    internal const string CollectionNotFound = "A collection by the given source and `id` was not found.";

    internal const string CreatorNotFound = "A creator by the given source and `id` was not found.";

    internal const string CharacterNotFound = "A character by the given source and `id` was not found.";

    internal const string TagNotFound = "A tag by the given source and `id` was not found.";

    internal const string StudioNotFound = "A studio by the given source and `id` was not found.";

    internal const string NetworkNotFound = "A network by the given source and `id` was not found.";

    internal const string EntryNotFound = "An entry by the given source, kind and `id` was not found.";

    internal const string CrossReferencesNotFound = "No link to an entry by the given source and `id` matched.";

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex EpisodeNumberSearchRegex();

    [GeneratedRegex(
        @"^(?:(?<special>specials?)(?:\s*(?<specialNumber>\d+))?|s(?<season>\d+)(?:\s*[e#](?<episode>\d+))?|[e#](?<episode>\d+))(?=\s|$)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex EpisodePrefixSearchRegex();

    #endregion

    #region Other Kinds

    /// <summary>
    /// Get an entry of any kind a source registered, in its minimal form.
    /// </summary>
    /// <remarks>
    /// The kinds with typed routes of their own, such as <c>Series</c>, are
    /// answered there instead. Users and filters are never answered here,
    /// nor are AniDB and Shoko entries the user may not see.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry, by its value or an alias.</param>
    /// <param name="id">The source's ID for the entry.</param>
    /// <param name="include">The extra details to include: <c>Titles</c>, <c>Overviews</c> and <c>Images</c>.</param>
    /// <returns>The entry.</returns>
    [HttpGet("{kind:metadata-entity-type}/{id}")]
    public ActionResult<MetadataEntry> GetEntry(
        [FromRoute] MetadataSource source,
        [FromRoute] MetadataEntityType kind,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(ModelBinders.CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null
    )
    {
        if (!IsOpenKind(kind) || ToGuid(source, kind, id) is not { } guid || _metadataService.GetEntry(guid) is not { } entry || !MaySee(entry))
            return NotFound(EntryNotFound);

        return _models.Entry(entry, include);
    }

    /// <summary>
    /// Whether the minimal routes may answer for a kind of entry: every kind
    /// but a Shoko user or filter, which are the users' own business.
    /// </summary>
    /// <param name="kind">The kind of entry.</param>
    /// <returns><c>true</c> when the kind may be answered.</returns>
    internal static bool IsOpenKind(MetadataEntityType kind)
        => kind != MetadataEntityType.User && kind != MetadataEntityType.Filter;

    #endregion

    #region Helpers | Lookup

    /// <summary>
    /// The identifier a route names, with a <c>/</c> sent as <c>%2F</c> put
    /// back.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="id">The ID as the route holds it.</param>
    /// <returns>The identifier, or <c>null</c> when the ID is not a valid one.</returns>
    internal static MetadataGuid? ToGuid(MetadataSource source, MetadataEntityType kind, string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        // Routing decodes everything but an escaped slash, which it keeps as
        // it came so the path's segments stay apart.
        id = id.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        try
        {
            return new(source, kind, id);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The identifier a bulk body names: a full identifier of the source and
    /// kind, or the source's own ID.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="text">The text.</param>
    /// <returns>The identifier, or <c>null</c> when the text names no entry of the source and kind.</returns>
    internal static MetadataGuid? FromBody(MetadataSource source, MetadataEntityType kind, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Contains("://", StringComparison.Ordinal))
            return MetadataGuid.TryParse(text, out var guid) && guid.Source == source && guid.EntityType == kind ? guid : null;

        try
        {
            return new(source, kind, text);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Look an entry up, after waiting out a refresh or purge of what it
    /// belongs to, so what is read is what the provider wrote.
    /// </summary>
    /// <typeparam name="TMetadata">The entry's type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="id">The ID as the route holds it.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The entry, or <c>null</c> when it is not stored.</returns>
    private async Task<TMetadata?> Get<TMetadata>(MetadataSource source, MetadataEntityType kind, string id, CancellationToken cancellationToken)
        where TMetadata : class, IMetadata
        => ToGuid(source, kind, id) is { } guid ? await Get<TMetadata>(guid, cancellationToken).ConfigureAwait(false) : null;

    /// <summary>
    /// Look an entry up by its identifier, after waiting out a refresh or
    /// purge of what it belongs to.
    /// </summary>
    /// <typeparam name="TMetadata">The entry's type.</typeparam>
    /// <remarks>
    /// A collection is read through <see cref="IMetadataService.GetCollection"/>,
    /// which queues the fetch of one that is not stored.
    /// </remarks>
    /// <param name="guid">The entry.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The entry, or <c>null</c> when it is not stored.</returns>
    private async Task<TMetadata?> Get<TMetadata>(MetadataGuid guid, CancellationToken cancellationToken)
        where TMetadata : class, IMetadata
    {
        var found = guid.EntityType == MetadataEntityType.Collection
            ? _metadataService.GetCollection(guid) as TMetadata
            : _metadataService.GetEntry<TMetadata>(guid);
        if (found is not { } entry || !MaySee(entry))
            return null;

        return await Fresh(entry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// An entry as it stands once a running refresh or purge of what it
    /// belongs to has ended.
    /// </summary>
    /// <typeparam name="TMetadata">The entry's type.</typeparam>
    /// <param name="entry">The entry, as read before.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The entry read again when there was something to wait for, or <c>null</c> when it is gone.</returns>
    private async Task<TMetadata?> Fresh<TMetadata>(TMetadata entry, CancellationToken cancellationToken)
        where TMetadata : class, IMetadata
    {
        if (RefreshedWith(entry) is not { } owner || !await _refreshService.WaitForRefresh(owner, cancellationToken).ConfigureAwait(false))
            return entry;

        return _metadataService.GetEntry<TMetadata>(entry.ID);
    }

    /// <summary>
    /// Whether the user may see an entry. Only AniDB and Shoko entries are
    /// kept from users, by the same tag restrictions the other routes apply.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns><c>true</c> when the entry may be shown.</returns>
    private bool MaySee(IMetadata entry)
        => MaySee(entry, () => _viewer ??= User);

    /// <summary>
    /// Whether a user may see an entry: an AniDB anime, season or episode, or
    /// a Shoko series, season or episode, when the anime or series behind it
    /// is not hidden from the user, and a video with no series or one the user
    /// may see. Every other entry may be seen.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="user">Gives the user, only asked for when the entry is one the user may be kept from.</param>
    /// <returns><c>true</c> when the entry may be shown.</returns>
    internal static bool MaySee(IMetadata entry, Func<IUser> user)
        => entry switch
        {
            IAnidbAnime anime => user().IsAllowedToSee(anime),
            ISeason<IAnidbAnime, IAnidbEpisode> season => user().IsAllowedToSee(season.Series),
            IAnidbEpisode episode => episode.Series is not { } anime || user().IsAllowedToSee(anime),
            IShokoSeries series => user().IsAllowedToSee(series),
            ISeason<IShokoSeries, IShokoEpisode> season => user().IsAllowedToSee(season.Series),
            IShokoEpisode episode => episode.Series is not { } series || user().IsAllowedToSee(series),
            IVideo video => video.Series is var linked && (linked.Count is 0 || linked.Any(series => user().IsAllowedToSee(series))),
            _ => true,
        };

    /// <summary>
    /// The series, movie or collection an entry is refreshed with.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>What is refreshed, or <c>null</c> when the entry is not refreshed on its own.</returns>
    private static MetadataGuid? RefreshedWith(IMetadata entry)
        => entry switch
        {
            ISeason season => season.SeriesID,
            IEpisode episode => episode.SeriesID,
            ISeries or IMovie or ICollection => entry.ID,
            _ => null,
        };

    /// <summary>
    /// The stored members of a collection.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <returns>The movies and series in it.</returns>
    private IReadOnlyList<IMetadata> Members(ICollection collection)
    {
        if (!collection.Source.IsCore)
            return [.. _collectionStore.GetMembers(collection.ID).Select(_metadataService.GetEntry).OfType<IMetadata>().Where(MaySee)];

        // The core's sources know which collection an entry is in, not the
        // other way round.
        return
        [
            .. _metadataService.GetAllMoviesForSource(collection.Source)
                .Where(movie => _metadataService.GetCollectionsWith(movie.ID).Any(other => other.ID == collection.ID) && MaySee(movie)),
            .. _metadataService.GetAllSeriesForSource(collection.Source)
                .Where(series => _metadataService.GetCollectionsWith(series.ID).Any(other => other.ID == collection.ID) && MaySee(series)),
        ];
    }

    /// <summary>
    /// The series and movies that have a tag.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The entries.</returns>
    private IReadOnlyList<IMetadata> EntriesWithTag(ITag tag)
    {
        if (!tag.Source.IsCore)
            return [.. _tagStore.GetEntriesWithTag(tag.ID).Select(_metadataService.GetEntry).OfType<IMetadata>().Where(MaySee)];

        return
        [
            .. _metadataService.GetAllSeriesForSource(tag.Source).Where(series => series.Tags.Any(other => other.ID == tag.ID) && MaySee(series)),
            .. _metadataService.GetAllMoviesForSource(tag.Source).Where(movie => movie.Tags.Any(other => other.ID == tag.ID) && MaySee(movie)),
        ];
    }

    /// <summary>
    /// Every tag a source has, of one kind or all.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Only the tags of this kind, or <c>null</c> for all.</param>
    /// <returns>The tags.</returns>
    private IReadOnlyList<ITag> AllTags(MetadataSource source, TagKind? kind)
    {
        if (!source.IsCore)
            return _tagStore.GetAllTags(source, kind);

        return
        [
            .. _metadataService.GetAllSeriesForSource(source).SelectMany(series => series.Tags)
                .Concat(_metadataService.GetAllMoviesForSource(source).SelectMany(movie => movie.Tags))
                .Where(tag => kind is null || tag.Kind == kind)
                .DistinctBy(tag => tag.ID)
                .Select(tag => _metadataService.GetEntry<ITag>(tag.ID) ?? tag),
        ];
    }

    #endregion

    #region Helpers | Lists

    /// <summary>
    /// Whether an entry passes a restricted filter.
    /// </summary>
    /// <param name="isRestricted">Whether the entry is restricted.</param>
    /// <param name="restricted">The filter.</param>
    /// <returns><c>true</c> when the entry is kept.</returns>
    private static bool Keeps(bool isRestricted, IncludeOnlyFilter restricted)
        => restricted is IncludeOnlyFilter.True || isRestricted == (restricted is IncludeOnlyFilter.Only);

    /// <summary>
    /// Orders entries by how well their titles match a search, dropping the
    /// ones that do not match, or by title when there is no search.
    /// </summary>
    /// <typeparam name="TMetadata">The entries' type.</typeparam>
    /// <param name="entries">The entries.</param>
    /// <param name="search">The search, if any.</param>
    /// <param name="fuzzy">Whether to match titles loosely.</param>
    /// <returns>The entries in order.</returns>
    private IEnumerable<TMetadata> Search<TMetadata>(IEnumerable<TMetadata> entries, string? search, bool fuzzy)
        where TMetadata : IWithTitles, IMetadata
    {
        if (string.IsNullOrWhiteSpace(search))
            return entries.OrderBy(entry => entry.Title, StringComparer.Ordinal).ThenBy(entry => entry.ID);

        search = search.Trim();
        return entries
            .Select(entry => (Entry: entry, Score: Score(search, entry, fuzzy)))
            .Where(pair => pair.Score is not null)
            .OrderBy(pair => pair.Score!.Value.IsNotExact)
            .ThenBy(pair => pair.Score!.Value.Index)
            .ThenBy(pair => pair.Score!.Value.Distance)
            .ThenBy(pair => pair.Score!.Value.LengthDifference)
            .Select(pair => pair.Entry);
    }

    /// <summary>
    /// How well an entry's titles match a search.
    /// </summary>
    /// <param name="search">The search.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="fuzzy">Whether to match titles loosely.</param>
    /// <returns>The score, or <c>null</c> when no title matches.</returns>
    private (bool IsNotExact, int Index, double Distance, int LengthDifference)? Score(string search, IWithTitles entry, bool fuzzy)
    {
        var names = entry.Titles
            .Select(title => title.Value)
            .Append(entry.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!fuzzy)
        {
            var index = names.Select(name => name.IndexOf(search, StringComparison.OrdinalIgnoreCase)).Where(index => index >= 0).DefaultIfEmpty(-1).Min();
            return index >= 0 ? (false, index, 0d, 0) : null;
        }

        return _fuzzySearch.FuzzyScoreAnyName(search, names) is { } score ? (score.isNotExact, score.index, score.distance, score.lengthDiff) : null;
    }

    /// <summary>
    /// One page of the entries the user may see, each read again once a
    /// refresh of it ends.
    /// </summary>
    /// <typeparam name="TMetadata">The entries' type.</typeparam>
    /// <typeparam name="TModel">The models' type.</typeparam>
    /// <param name="entries">Every entry, in order.</param>
    /// <param name="map">Builds the model of an entry.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="cancellationToken">Stops the waits.</param>
    /// <returns>The page, with the total count.</returns>
    private async Task<ListResult<TModel>> Page<TMetadata, TModel>(
        IEnumerable<TMetadata> entries,
        Func<TMetadata, TModel> map,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    ) where TMetadata : class, IMetadata
    {
        var list = entries.Where(entry => MaySee(entry)).ToList();
        var pageItems = pageSize <= 0 ? list : [.. list.Skip(pageSize * (page - 1)).Take(pageSize)];
        List<TModel> models = [];
        foreach (var entry in pageItems)
            if (await Fresh(entry, cancellationToken).ConfigureAwait(false) is { } fresh)
                models.Add(map(fresh));

        return new ListResult<TModel>(list.Count, models);
    }

    /// <summary>
    /// One page of plain items.
    /// </summary>
    /// <typeparam name="TItem">The items' type.</typeparam>
    /// <param name="items">Every item, in order.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <returns>The page, with the total count.</returns>
    private static ListResult<TItem> Page<TItem>(IEnumerable<TItem> items, int page, int pageSize)
    {
        var list = items.ToList();
        return new ListResult<TItem>(list.Count, pageSize <= 0 ? list : [.. list.Skip(pageSize * (page - 1)).Take(pageSize)]);
    }

    /// <summary>
    /// Filters an entry's episodes by a search. A bare number matches anywhere
    /// in the episode number. A leading <c>E5</c> or <c>#5</c>, <c>S1</c> or
    /// <c>S1E5</c> when the episodes have seasons, or <c>Special 3</c> or
    /// <c>Specials</c> narrows to those episodes, and the rest of the search
    /// matches the titles, ignoring case.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <param name="search">The search, if any.</param>
    /// <param name="titlesOf">Reads every title of an episode, only once there is text to match.</param>
    /// <returns>The episodes that match.</returns>
    internal static IEnumerable<IEpisode> SearchEpisodes(IEnumerable<IEpisode> episodes, string? search, Func<IEpisode, IEnumerable<string>> titlesOf)
    {
        if (string.IsNullOrWhiteSpace(search))
            return episodes;

        var text = search.Trim();
        if (EpisodeNumberSearchRegex().IsMatch(text))
            return episodes.Where(episode => episode.EpisodeNumber.ToString(CultureInfo.InvariantCulture).Contains(text, StringComparison.Ordinal));

        var list = episodes as IReadOnlyCollection<IEpisode> ?? [.. episodes];
        IEnumerable<IEpisode> found = list;
        if (EpisodePrefixSearchRegex().Match(text) is { Success: true } match &&
            (!match.Groups["season"].Success || list.Any(episode => episode.SeasonNumber is not null)))
        {
            if (match.Groups["special"].Success)
                found = found.Where(episode => episode.Type is EpisodeType.Special);
            if (Number(match.Groups["specialNumber"]) is { } specialNumber)
                found = found.Where(episode => episode.EpisodeNumber == specialNumber);
            if (Number(match.Groups["season"]) is { } seasonNumber)
                found = found.Where(episode => episode.SeasonNumber == seasonNumber);
            if (Number(match.Groups["episode"]) is { } episodeNumber)
                found = found.Where(episode => episode.EpisodeNumber == episodeNumber);
            text = text[match.Length..].Trim();
        }

        return text.Length > 0
            ? found.Where(episode => titlesOf(episode).Any(title => title.Contains(text, StringComparison.OrdinalIgnoreCase)))
            : found;

        static int? Number(System.Text.RegularExpressions.Group group)
            => group.Success && int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>
    /// Orders episodes by season, specials last, then by number.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The episodes in order.</returns>
    internal static IEnumerable<IEpisode> InOrder(IEnumerable<IEpisode> episodes)
        => episodes
            .OrderBy(episode => episode.SeasonNumber is 0 ? int.MaxValue : episode.SeasonNumber ?? 0)
            .ThenBy(episode => episode.Type is EpisodeType.Episode ? 0 : 1)
            .ThenBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .ThenBy(episode => episode.ID);

    /// <summary>
    /// The days of the week episodes first aired on.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The days' names, in alphabetical order as the TMDB routes send them.</returns>
    internal static IReadOnlyList<string> DaysOfWeek(IEnumerable<IEpisode> episodes)
        => [.. episodes
            .Select(episode => episode.AirDateWithTime?.DayOfWeek ?? episode.AirDate?.DayOfWeek)
            .OfType<DayOfWeek>()
            .Distinct()
            .Select(day => day.ToString())
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Groups the episode links into a series: an AniDB episode linked to
    /// several episodes is one group, and so are the AniDB episodes sharing
    /// one episode, in AniDB's order.
    /// </summary>
    /// <param name="links">The episode links.</param>
    /// <returns>The groups.</returns>
    private List<List<IMetadataEpisodeCrossReference>> GroupEpisodeLinks(IReadOnlyList<IMetadataEpisodeCrossReference> links)
        => GroupEpisodeLinks(links, _metadataService);

    /// <summary>
    /// Groups episode links: an AniDB episode linked to several episodes is
    /// one group, and so are the AniDB episodes sharing one episode, in
    /// AniDB's order.
    /// </summary>
    /// <param name="links">The episode links.</param>
    /// <param name="metadataService">Reads the AniDB episodes, for their order.</param>
    /// <returns>The groups.</returns>
    internal static List<List<IMetadataEpisodeCrossReference>> GroupEpisodeLinks(IReadOnlyList<IMetadataEpisodeCrossReference> links, IMetadataService metadataService)
    {
        var byAnidb = links.GroupBy(link => link.AnidbEpisodeID).ToList();
        var multiple = byAnidb.Where(group => group.Count() > 1).Select(group => group.OrderBy(link => link.Ordering).ToList());
        var single = byAnidb.Where(group => group.Count() is 1).Select(group => group.First())
            .GroupBy(link => link.ProviderID?.ToString() ?? string.Empty)
            .Select(group => group.OrderBy(link => AnidbOrder(link.AnidbEpisodeID, metadataService)).ToList());
        return [.. multiple.Concat(single).OrderBy(group => AnidbOrder(group[0].AnidbEpisodeID, metadataService))];
    }

    /// <summary>
    /// Where an AniDB episode sits in its anime.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="metadataService">Reads the AniDB episode.</param>
    /// <returns>Its type, number and ID, or last place when it is not stored.</returns>
    private static (EpisodeType Type, int Number, int ID) AnidbOrder(int anidbEpisodeID, IMetadataService metadataService)
        => metadataService.GetEntry<IEpisode>(new(MetadataSource.AniDB, MetadataEntityType.Episode, anidbEpisodeID.ToString(CultureInfo.InvariantCulture))) is { } episode
            ? (episode.Type, episode.EpisodeNumber, anidbEpisodeID)
            : (EpisodeType.Other, int.MaxValue, anidbEpisodeID);

    #endregion

    #region Helpers | Reverse Lookups

    /// <summary>
    /// The AniDB anime behind some links, once each.
    /// </summary>
    /// <param name="links">The links.</param>
    /// <returns>The anime that are stored and the user may see.</returns>
    private List<AnidbAnime> AnidbAnime(IEnumerable<IMetadataCrossReference> links)
        => [.. links
            .Select(link => link.AnidbAnimeID)
            .Distinct()
            .Order()
            .Select(animeID => _metadataService.GetEntry(new(MetadataSource.AniDB, MetadataEntityType.Series, animeID.ToString(CultureInfo.InvariantCulture))))
            .OfType<AniDB_Anime>()
            .Where(MaySee)
            .Select(anime => new AnidbAnime(anime))];

    /// <summary>
    /// The AniDB episodes behind some links, once each.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episode IDs.</param>
    /// <returns>The episodes that are stored and the user may see.</returns>
    private List<AnidbEpisode> AnidbEpisodes(IEnumerable<int> anidbEpisodeIDs)
        => [.. anidbEpisodeIDs
            .Distinct()
            .Select(episodeID => _metadataService.GetEntry(new(MetadataSource.AniDB, MetadataEntityType.Episode, episodeID.ToString(CultureInfo.InvariantCulture))))
            .OfType<AniDB_Episode>()
            .Where(MaySee)
            .OrderBy(episode => episode.AnimeID)
            .ThenBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .Select(episode => new AnidbEpisode(episode))];

    /// <summary>
    /// The Shoko series of some AniDB anime the user may see, once each.
    /// </summary>
    /// <param name="anidbAnimeIDs">The AniDB anime IDs.</param>
    /// <param name="randomImages">Whether to pick the series' images at random.</param>
    /// <param name="includeDataFrom">The sources to add data from.</param>
    /// <returns>The series.</returns>
    private List<Series> ShokoSeries(IEnumerable<int> anidbAnimeIDs, bool randomImages, HashSet<MetadataSource>? includeDataFrom)
    {
        var user = User;
        return [.. anidbAnimeIDs
            .Distinct()
            .Order()
            .Select(_metadataService.GetShokoSeriesByAnidbID)
            .OfType<AnimeSeries>()
            .Where(user.AllowedSeries)
            .Select(series => new Series(series, user.JMMUserID, randomImages, includeDataFrom))];
    }

    /// <summary>
    /// The Shoko episodes of some AniDB episodes the user may see, once each.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episode IDs.</param>
    /// <param name="includeDataFrom">The sources to add data from.</param>
    /// <returns>The episodes.</returns>
    private List<Episode> ShokoEpisodes(IEnumerable<int> anidbEpisodeIDs, HashSet<MetadataSource>? includeDataFrom)
    {
        var user = User;
        return [.. ShokoEpisodeRows(anidbEpisodeIDs)
            .Where(episode => episode.AnimeSeries is { } series && user.AllowedSeries(series))
            .Select(episode => new Episode(HttpContext, episode, includeDataFrom))];
    }

    /// <summary>
    /// The Shoko episodes of some AniDB episodes, once each.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episode IDs.</param>
    /// <returns>The episodes.</returns>
    private IEnumerable<AnimeEpisode> ShokoEpisodeRows(IEnumerable<int> anidbEpisodeIDs)
        => anidbEpisodeIDs
            .Distinct()
            .Select(_metadataService.GetShokoEpisodeByAnidbID)
            .OfType<AnimeEpisode>();

    /// <summary>
    /// The files of some AniDB episodes, filtered, sorted and paged as asked.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episode IDs.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="include">Items to include that are left out by default.</param>
    /// <param name="exclude">Items to leave out.</param>
    /// <param name="includeOnly">Items to include only.</param>
    /// <param name="releaseProviders">Release providers to keep, or with <c>!</c> to leave out.</param>
    /// <param name="sortOrder">The sort order.</param>
    /// <returns>The page of files.</returns>
    private ListResult<File> ShokoFiles(
        IEnumerable<int> anidbEpisodeIDs,
        int pageSize,
        int page,
        FileNonDefaultIncludeType[]? include,
        FileExcludeTypes[]? exclude,
        FileIncludeOnlyType[]? includeOnly,
        List<string>? releaseProviders,
        List<string>? sortOrder
    )
    {
        var user = User;
        var videos = ShokoEpisodeRows(anidbEpisodeIDs)
            .Where(episode => episode.AnimeSeries is { } series && user.AllowedSeries(series))
            .SelectMany(episode => episode.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videos, user, pageSize, page, include, exclude, includeOnly, releaseProviders, sortOrder);
    }

    #endregion

    #region Helpers | Actions

    /// <summary>
    /// Refuses a purge or refresh on a source the core refreshes through its
    /// own jobs.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A problem to answer with, or <c>null</c> to go ahead.</returns>
    private ActionResult? RefuseCoreRefresh(MetadataSource source)
    {
        if (source != MetadataSource.AniDB && source != MetadataSource.Shoko && source != MetadataSource.User && source != MetadataSource.Generated)
            return null;

        ModelState.AddModelError("source", $"{source.Name} entries are not refreshed or purged through these routes.");
        return ValidationProblem(ModelState);
    }

    /// <summary>
    /// Answers <c>503 Service Unavailable</c> with a <c>Retry-After</c> while
    /// the source is paused, after queueing the work at the front when the
    /// caller did not ask to wait for it, so a caller knows nothing ran yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="queue">Queues the work at the front.</param>
    /// <param name="description">What the work is, for the log.</param>
    /// <param name="immediate">Whether the caller wanted to wait for the work.</param>
    /// <returns>The answer while paused, or <c>null</c> to go ahead.</returns>
    private async Task<ActionResult?> QueueWhenPaused(MetadataSource source, Func<Task> queue, string description, bool immediate)
    {
        var status = _refreshService.GetPauseStatus(source);
        if (!status.IsPaused)
            return null;

        var seconds = MetadataPauseResponses.RetryAfterSeconds(status);
        if (immediate)
        {
            _logger.LogInformation("{Source} is paused. {Work} was asked for at once and was refused; retry in about {Seconds} second(s).", source.Name, description, seconds);
        }
        else
        {
            _logger.LogInformation("{Source} is paused. {Work} was queued and starts in about {Seconds} second(s).", source.Name, description, seconds);
            await queue().ConfigureAwait(false);
        }

        return MetadataPauseResponses.Paused(Response, source, status);
    }

    /// <summary>
    /// Queues a refresh of an entry, or runs it and waits for it.
    /// </summary>
    /// <param name="entry">The series or movie.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>200 when it ran, 204 when it was queued, or 503 while the source is paused.</returns>
    private async Task<ActionResult> Refresh(IMetadata entry, MetadataRefreshBody body, CancellationToken cancellationToken)
    {
        // Answered from what is stored, so a paused source is no reason to refuse it.
        if (body.Immediate && body.QuickRefresh && _refreshService.IsRefreshing(entry.ID) && entry is ISeries { Episodes.Count: > 0 })
            return Ok();

        var quick = body.Immediate && body.QuickRefresh;
        var force = !quick && body.Force;
        var options = body.ToOptions(quick);
        if (await QueueWhenPaused(entry.Source, () => _refreshService.RefreshEntry(entry.ID, force, options, prioritize: true, cancellationToken: cancellationToken), "A refresh", body.Immediate).ConfigureAwait(false) is { } paused)
            return paused;

        await _refreshService.RefreshEntry(entry.ID, force, options, immediate: body.Immediate, cancellationToken: cancellationToken).ConfigureAwait(false);
        return body.Immediate ? Ok() : NoContent();
    }

    /// <summary>
    /// Queues a refresh of an entry that is not stored yet, which only a
    /// person asking for it may fetch.
    /// </summary>
    /// <param name="entryID">The series or movie.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>200 when it ran, 204 when it was queued, or 503 while the source is paused.</returns>
    private async Task<ActionResult> RefreshMissing(MetadataGuid entryID, MetadataRefreshBody body, CancellationToken cancellationToken)
    {
        var options = body.ToOptions(body.Immediate && body.QuickRefresh);
        if (await QueueWhenPaused(entryID.Source, () => _refreshService.RefreshEntry(entryID, body.Force, options, prioritize: true, cancellationToken: cancellationToken), "A refresh", body.Immediate).ConfigureAwait(false) is { } paused)
            return paused;

        await _refreshService.RefreshEntry(entryID, body.Force, options, immediate: body.Immediate, cancellationToken: cancellationToken).ConfigureAwait(false);
        return body.Immediate ? Ok() : NoContent();
    }

    /// <summary>
    /// Queues the download of an entry's images, or runs it and waits for it.
    /// </summary>
    /// <param name="entry">The series or movie.</param>
    /// <param name="body">How to download them.</param>
    /// <param name="cancellationToken">Cancels a download waited on.</param>
    /// <returns>200 when it ran, 204 when it was queued, or 503 while the source is paused.</returns>
    private async Task<ActionResult> DownloadImages(IMetadata entry, MetadataDownloadImagesBody body, CancellationToken cancellationToken)
    {
        if (await QueueWhenPaused(entry.Source, () => _refreshService.DownloadImages(entry.ID, body.Force, prioritize: true, cancellationToken: cancellationToken), "An image download", body.Immediate).ConfigureAwait(false) is { } paused)
            return paused;

        await _refreshService.DownloadImages(entry.ID, body.Force, immediate: body.Immediate, cancellationToken: cancellationToken).ConfigureAwait(false);
        return body.Immediate ? Ok() : NoContent();
    }

    #endregion
}
