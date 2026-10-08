using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.Settings;

using MetadataSearchResult = Shoko.Server.API.v3.Models.Metadata.MetadataSearchResult;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The metadata providers and what each source offers as a whole: its suspension
/// status, searching and looking up its entries, exporting and importing its
/// links, and the actions that run over all of its entries.
/// </summary>
/// <remarks>
/// <c>{source}</c> is a registered source's value, an alias or an old spelling,
/// ignoring case; any other answers <c>404</c>. Reading is open to every user;
/// changes and provider searches are for admins. A route calling a provider at
/// once answers with a problem body: <c>503</c> while it is not configured,
/// <c>503</c> with <c>Retry-After</c> while suspended, <c>502</c> with one while unreachable.
/// </remarks>
/// <param name="settingsProvider">The settings.</param>
/// <param name="providerManager">Lists and sets up the providers.</param>
/// <param name="suspensionService">Tells whether a source is suspended.</param>
/// <param name="linkingService">Searches and looks entries up.</param>
/// <param name="metadataService">Reads stored entries.</param>
/// <param name="transferService">Exports and imports links.</param>
/// <param name="models">Builds the stored entries' search results.</param>
/// <param name="sourceActions">Runs the source-wide actions.</param>
/// <param name="applicationPaths">Finds the sources' icons on disk.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Metadata")]
[ApiV3]
[Authorize]
[NotSupportedAsBadRequest]
public class MetadataProviderController(
    ISettingsProvider settingsProvider,
    IMetadataProviderManager providerManager,
    ISuspensionService suspensionService,
    IMetadataLinkingService linkingService,
    IMetadataService metadataService,
    IMetadataCrossReferenceTransferService transferService,
    MetadataModelBuilder models,
    MetadataSourceActions sourceActions,
    IApplicationPaths applicationPaths
) : BaseController(settingsProvider)
{
    #region Constants

    internal const string ProviderNotFound = "A metadata provider by the given `providerID` was not found.";

    internal const string EntryNotFoundOnSource = "The source has no entry by the given `id`.";

    internal const string SourceIconNotFound = "The source has no icon.";

    internal const string ProviderIconNotFound = "The metadata provider was not found or has no icon.";

    #endregion

    #region Providers

    /// <summary>
    /// Get every metadata provider, or the ones of one plugin.
    /// </summary>
    /// <param name="pluginID">Only the providers of this plugin.</param>
    /// <returns>The providers.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider")]
    public ActionResult<List<MetadataProvider>> GetProviders([FromQuery] Guid? pluginID = null)
        => providerManager.MetadataProviders
            .Where(info => pluginID is not { } id || info.PluginInfo.ID == id)
            .OrderBy(info => info.Source)
            .ThenBy(info => info.Name, StringComparer.Ordinal)
            .Select(ToModel)
            .ToList();

    /// <summary>
    /// Get one metadata provider.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>The provider.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider/{providerID:guid}")]
    public ActionResult<MetadataProvider> GetProvider([FromRoute] Guid providerID)
        => providerManager.GetProviderInfo(providerID) is { } info ? ToModel(info) : NotFound(ProviderNotFound);

    /// <summary>
    /// Get a metadata provider's icon: its own, else its source's.
    /// </summary>
    /// <remarks>
    /// An SVG or a PNG, sent so that an SVG opened on its own runs no script.
    /// </remarks>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>
    /// The icon, <c>304 Not Modified</c> when the client's copy has the same
    /// ETag, or <c>404 Not Found</c> when the provider is unknown or has none.
    /// </returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider/{providerID:guid}/Icon")]
    public ActionResult GetProviderIcon([FromRoute] Guid providerID)
        => PackageIcon(
            providerManager.GetProviderInfo(providerID) is { } info ? info.Icon ?? providerManager.GetSourceIcon(info.Source) : null,
            applicationPaths,
            ProviderIconNotFound
        );

    /// <summary>
    /// Get every source series or movies can be linked to, one row per
    /// source, with the kinds its providers link and which of them are on.
    /// </summary>
    /// <returns>The sources, in source order.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Source")]
    public ActionResult<List<MetadataLinkSource>> GetLinkSources()
        => providerManager.MetadataProviders
            .Where(info => MetadataSourceActions.IsLinkTarget(info.Source))
            .GroupBy(info => info.Source)
            .Select(group => new MetadataLinkSource(
                group.Key,
                group.Any(info => Links(info, MetadataEntityType.Series)),
                group.Any(info => Links(info, MetadataEntityType.Movie)),
                LinkingProvider(group.Key, MetadataEntityType.Series) is not null,
                LinkingProvider(group.Key, MetadataEntityType.Movie) is not null,
                new(group.Key, SourceSuspension.For(suspensionService, group.Key), providerManager.MetadataProviders),
                providerManager.GetSourceIcon(group.Key) is not null,
                group.First().PluginInfo.ID
            ))
            .Where(row => row.SupportsSeries || row.SupportsMovies)
            .OrderBy(row => row.Source)
            .ToList();

    /// <summary>
    /// Get a source's icon: its series provider's, else its movie provider's.
    /// </summary>
    /// <remarks>
    /// An SVG or a PNG, sent so that an SVG opened on its own runs no script.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <returns>
    /// The icon, <c>304 Not Modified</c> when the client's copy has the same
    /// ETag, or <c>404 Not Found</c> when the source has none.
    /// </returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Source/{source:metadata-source}/Icon")]
    public ActionResult GetSourceIcon([FromRoute] MetadataSource source)
        => PackageIcon(providerManager.GetSourceIcon(source), applicationPaths, SourceIconNotFound);

    /// <summary>
    /// Get the order of the providers claiming each kind of entry on a
    /// source, one row per kind, kinds no provider claims left out.
    /// </summary>
    /// <remarks>
    /// The first enabled provider of a kind answers for it. The rest stand
    /// by: the next enabled one takes over when it is turned off or removed.
    /// A suspended or unconfigured provider is not skipped.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <returns>The kinds, in kind order.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Source/{source:metadata-source}/Providers")]
    public ActionResult<List<MetadataProviderOrder>> GetProviderOrders([FromRoute] MetadataSource source)
        => ProviderOrders(source);

    /// <summary>
    /// Set the order of the providers claiming one or more kinds of entries
    /// on a source, and which of them are enabled. Kinds left out are kept.
    /// </summary>
    /// <remarks>
    /// Agrees with <c>PUT Provider/{providerID}</c>, which moves a provider
    /// to the front of each kind it turns on and turns it off for the rest.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="body">The kinds to change, each with its providers in order.</param>
    /// <returns>Every kind's order as it is now.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpPut("Source/{source:metadata-source}/Providers")]
    public ActionResult<List<MetadataProviderOrder>> UpdateProviderOrders(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] List<MetadataProviderOrderBody> body
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        var orders = new Dictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>>();
        foreach (var order in body)
        {
            var entityType = order.EntityType;
            if (orders.ContainsKey(entityType))
                return ValidationProblem($"The kind {entityType.Value} is given twice.", nameof(body));

            var claimants = providerManager.GetProviderOrder(source, entityType).Select(slot => slot.ProviderID).ToHashSet();
            if (claimants.Count is 0)
                return ValidationProblem($"No provider answers for {entityType.Value} on {source.Name}.", nameof(body));

            if (order.Providers.FirstOrDefault(provider => !claimants.Contains(provider.ProviderID)) is { } stranger)
                return ValidationProblem($"The provider {stranger.ProviderID} does not answer for {entityType.Value} on {source.Name}.", nameof(body));

            if (order.Providers.GroupBy(provider => provider.ProviderID).FirstOrDefault(group => group.Count() > 1) is { } twice)
                return ValidationProblem($"The provider {twice.Key} is given twice for {entityType.Value}.", nameof(body));

            orders[entityType] = [.. order.Providers
                .OrderBy(provider => provider.Priority)
                .Select(provider => new MetadataProviderAssignment(provider.ProviderID, provider.IsEnabled))];
        }

        providerManager.SetProviderOrder(source, orders);
        return ProviderOrders(source);
    }

    /// <summary>
    /// Change how a metadata provider is set up: the kinds of entries it is
    /// on for, and for its source whether it is the auto-linker and whether
    /// the source links new anime on its own.
    /// </summary>
    /// <remarks>
    /// The provider answers for exactly the kinds given: it moves to the
    /// front of each one's order, and is turned off, standing by included,
    /// for the rest. <c>Source/{source}/Providers</c> sets the order itself.
    /// </remarks>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="body">What to change; left-out fields are kept.</param>
    /// <returns>The provider as it is set up now.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpPut("Provider/{providerID:guid}")]
    public ActionResult<MetadataProvider> UpdateProvider([FromRoute] Guid providerID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataProviderUpdateBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (providerManager.GetProviderInfo(providerID) is not { } info)
            return NotFound(ProviderNotFound);

        if (body.EnabledEntityTypes is { } enabled && enabled.FirstOrDefault(entityType => !info.AvailableEntityTypes.Contains(entityType)) is { } unavailable)
            return ValidationProblem($"{info.Name} cannot answer for a {unavailable.Value}.", nameof(body.EnabledEntityTypes));

        if (body.IsAutoLinker is true && !info.SupportsAutoLinking)
            return ValidationProblem($"{info.Name} cannot work out what an anime is.", nameof(body.IsAutoLinker));

        if (body.EnabledEntityTypes is { } entityTypes)
            providerManager.SetProviderEnabled(providerID, entityTypes);
        if (body.IsAutoLinker is true)
            providerManager.SetProviderAutoLinker(info.Source, providerID);
        else if (body.IsAutoLinker is false && info.IsAutoLinker)
            providerManager.SetProviderAutoLinker(info.Source, null);
        if (body.AutoLink is { } autoLink)
            providerManager.SetProviderAutoLink(info.Source, autoLink);
        if (body.AutoLinkRestricted is { } autoLinkRestricted)
            providerManager.SetProviderAutoLinkRestricted(info.Source, autoLinkRestricted);

        return GetProvider(providerID);
    }

    /// <summary>
    /// Get whether a source's providers are configured, and whether it is
    /// suspended and for how long.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The status.</returns>
    [HttpGet("{source:metadata-source}/Status")]
    public ActionResult<MetadataSourceStatus> GetStatus([FromRoute] MetadataSource source)
        => new MetadataSourceStatus(source, SourceSuspension.For(suspensionService, source), providerManager.MetadataProviders);

    /// <summary>
    /// The order of every kind of entry a provider claims on a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The kinds, in kind order.</returns>
    private List<MetadataProviderOrder> ProviderOrders(MetadataSource source)
        => providerManager.MetadataProviders
            .Where(info => info.Source == source)
            .SelectMany(info => info.AvailableEntityTypes)
            .Distinct()
            .Order()
            .Select(kind => new MetadataProviderOrder(kind, providerManager.GetProviderOrder(source, kind)
                .Select(slot => (Info: providerManager.GetProviderInfo(slot.ProviderID), slot.IsEnabled))
                .Where(slot => slot.Info is not null)
                .Select(slot => (slot.Info!, slot.IsEnabled))))
            .ToList();

    /// <summary>
    /// A provider as the routes send it.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <returns>The model.</returns>
    private MetadataProvider ToModel(MetadataProviderInfo info)
        => new(info, SourceSuspension.For(suspensionService, info.Source), providerManager.MetadataProviders, providerManager.GetSourceIcon(info.Source) is not null);

    #endregion

    #region Search

    /// <summary>
    /// Search a source's provider for series or movies.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="query">What to search for.</param>
    /// <param name="kind">Search for <c>series</c> (the default) or <c>movie</c>.</param>
    /// <param name="includeRestricted">Include entries restricted to adults.</param>
    /// <param name="year">Only entries that first aired or were released in this year.</param>
    /// <param name="season">Only series that aired in this season of the year.</param>
    /// <param name="seasonYear">Only series that aired in a season of this year.</param>
    /// <param name="type">Only series of these types.</param>
    /// <param name="pageSize">The page size; 0 for the total alone.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>
    /// The page of what the provider found, or <c>503 Service Unavailable</c>
    /// while the provider is not configured or the source is suspended.
    /// </returns>
    [Authorize("admin")]
    [HttpGet("{source:metadata-source}/Search")]
    public async Task<ActionResult<ListResult<MetadataSearchResult>>> Search(
        [FromRoute] MetadataSource source,
        [FromQuery, Required] string query,
        [FromQuery] MetadataEntityType? kind = null,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery] YearlySeason? season = null,
        [FromQuery, Range(0, int.MaxValue)] int seasonYear = 0,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        kind ??= MetadataEntityType.Series;
        if (kind != MetadataEntityType.Series && kind != MetadataEntityType.Movie)
            return ValidationProblem("Only series and movies can be searched for.", nameof(kind));

        var provider = LinkingProvider(source, kind);
        if (MetadataPauseResponses.Refuse(Response, source, provider, SourceSuspension.For(suspensionService, source)) is { } refused)
            return refused;

        var options = new MetadataSearchOptions
        {
            Query = query,
            IncludeRestricted = includeRestricted,
            Year = year > 0 ? year : null,
            Season = season,
            SeasonYear = seasonYear > 0 ? seasonYear : null,
            Types = type is { Count: > 0 } ? [.. type] : null,
            Page = page,
            PageSize = pageSize,
        };
        IReadOnlyList<Abstractions.Metadata.Search.MetadataSearchResult> results;
        int total;
        if (kind == MetadataEntityType.Series)
            (results, total) = await linkingService.SearchSeries(source, options, cancellationToken).ConfigureAwait(false);
        else
            (results, total) = await linkingService.SearchMovies(source, options, cancellationToken).ConfigureAwait(false);

        return new ListResult<MetadataSearchResult>(total, results.Select(result => Remote(result, metadataService.GetEntry(result.ID) is not null)));
    }

    /// <summary>
    /// Get one series of a source: the stored copy when there is one,
    /// otherwise looked up from its provider.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series, with a <c>/</c> sent as <c>%2F</c>.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The series, or <c>404 Not Found</c> when the source has none by that ID.</returns>
    [HttpGet("{source:metadata-source}/Search/Series/{id}")]
    public async Task<ActionResult<MetadataSearchResult>> LookupSeries([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (MetadataEntryController.ToGuid(source, MetadataEntityType.Series, id) is not { } guid)
            return NotFound(EntryNotFoundOnSource);

        if (Stored(guid) is { } stored)
            return stored;

        if (RefuseLookup(source, MetadataEntityType.Series) is { } refused)
            return refused;

        return await linkingService.LookupSeries(guid, cancellationToken).ConfigureAwait(false) is { } found
            ? Remote(found, false)
            : NotFound(EntryNotFoundOnSource);
    }

    /// <summary>
    /// Get one movie of a source: the stored copy when there is one,
    /// otherwise looked up from its provider.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie, with a <c>/</c> sent as <c>%2F</c>.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The movie, or <c>404 Not Found</c> when the source has none by that ID.</returns>
    [HttpGet("{source:metadata-source}/Search/Movie/{id}")]
    public async Task<ActionResult<MetadataSearchResult>> LookupMovie([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (MetadataEntryController.ToGuid(source, MetadataEntityType.Movie, id) is not { } guid)
            return NotFound(EntryNotFoundOnSource);

        if (Stored(guid) is { } stored)
            return stored;

        if (RefuseLookup(source, MetadataEntityType.Movie) is { } refused)
            return refused;

        return await linkingService.LookupMovie(guid, cancellationToken).ConfigureAwait(false) is { } found
            ? Remote(found, false)
            : NotFound(EntryNotFoundOnSource);
    }

    /// <summary>
    /// Get several series of a source at once, the stored copies first and
    /// the rest looked up from its provider.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="body">The series.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The series, in the order asked for, or a validation problem naming the ones not found.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Search/Series/Bulk")]
    public Task<ActionResult<List<MetadataSearchResult>>> LookupSeriesInBulk(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataBulkLookupBody body,
        CancellationToken cancellationToken = default
    )
        => LookupInBulk(source, MetadataEntityType.Series, body, async (guid, token) => await linkingService.LookupSeries(guid, token).ConfigureAwait(false), cancellationToken);

    /// <summary>
    /// Get several movies of a source at once, the stored copies first and
    /// the rest looked up from its provider.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="body">The movies.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The movies, in the order asked for, or a validation problem naming the ones not found.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Search/Movie/Bulk")]
    public Task<ActionResult<List<MetadataSearchResult>>> LookupMoviesInBulk(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataBulkLookupBody body,
        CancellationToken cancellationToken = default
    )
        => LookupInBulk(source, MetadataEntityType.Movie, body, async (guid, token) => await linkingService.LookupMovie(guid, token).ConfigureAwait(false), cancellationToken);

    /// <summary>
    /// Looks several entries up, stored copies first.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Series or movies.</param>
    /// <param name="body">The entries.</param>
    /// <param name="lookup">Looks one entry up from the provider.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The entries, in the order asked for, or a problem.</returns>
    private async Task<ActionResult<List<MetadataSearchResult>>> LookupInBulk(
        MetadataSource source,
        MetadataEntityType kind,
        MetadataBulkLookupBody body,
        Func<MetadataGuid, CancellationToken, Task<Abstractions.Metadata.Search.MetadataSearchResult?>> lookup,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        var asked = body.IDs.Select(text => (Text: text, ID: MetadataEntryController.FromBody(source, kind, text))).ToList();
        var found = new Dictionary<MetadataGuid, MetadataSearchResult>();
        foreach (var guid in asked.Select(pair => pair.ID).OfType<MetadataGuid>().Distinct())
            if (Stored(guid) is { } stored)
                found[guid] = stored;

        // Refused before the first lookup rather than part of the way through.
        var remaining = asked.Select(pair => pair.ID).OfType<MetadataGuid>().Distinct().Where(guid => !found.ContainsKey(guid)).ToList();
        if (remaining.Count > 0 && RefuseLookup(source, kind) is { } refused)
            return refused;

        foreach (var guid in remaining)
            if (await lookup(guid, cancellationToken).ConfigureAwait(false) is { } result)
                found[guid] = Remote(result, false);

        foreach (var (text, guid) in asked)
            if (guid is null || !found.ContainsKey(guid))
                ModelState.AddModelError(nameof(body.IDs), $"The {kind.Value} '{text}' was not found on {source.Name}.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return asked.Select(pair => found[pair.ID!]).ToList();
    }

    /// <summary>
    /// A series or movie as the provider found it.
    /// </summary>
    /// <param name="result">The provider's answer.</param>
    /// <param name="isLocal">Whether the entry is stored already.</param>
    /// <returns>The result.</returns>
    private MetadataSearchResult Remote(Abstractions.Metadata.Search.MetadataSearchResult result, bool isLocal)
        => new(result, isLocal, metadataService.GetSiteUrl(result.ID));

    /// <summary>
    /// A stored series or movie as a search result, when it is stored and the
    /// user may see it.
    /// </summary>
    /// <param name="guid">The entry.</param>
    /// <returns>The result, or <c>null</c>.</returns>
    private MetadataSearchResult? Stored(MetadataGuid guid)
    {
        var entry = metadataService.GetEntry(guid);
        if (entry is null || !MetadataEntryController.MaySee(entry, () => User))
            return null;

        return entry switch
        {
            ISeries series => new(models.SearchResult(series), true, metadataService.GetSiteUrl(series)),
            IMovie movie => new(models.SearchResult(movie), true, metadataService.GetSiteUrl(movie)),
            _ => null,
        };
    }

    /// <summary>
    /// Refuses a lookup the source's provider cannot answer now: with
    /// <c>400 Bad Request</c> when it does not look entries up at all, or
    /// <c>503 Service Unavailable</c> while it is not configured or the source
    /// is suspended.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Series or movies.</param>
    /// <returns>The refusal, or <c>null</c> to go ahead.</returns>
    private ActionResult? RefuseLookup(MetadataSource source, MetadataEntityType kind)
    {
        if (LinkingProvider(source, kind) is not { SupportsLookup: true } provider)
            return ValidationProblem($"{source.Name} does not look a {kind.Value} up by its ID.", "source");

        return MetadataPauseResponses.Refuse(Response, source, provider, SourceSuspension.For(suspensionService, source));
    }

    /// <summary>
    /// The enabled provider of a source the linking service asks to search
    /// for and look up series or movies: the first that links movies, or the
    /// first that takes series links.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Series or movies.</param>
    /// <returns>The provider, or <c>null</c> when none is enabled.</returns>
    private MetadataProviderInfo? LinkingProvider(MetadataSource source, MetadataEntityType kind)
        => providerManager.GetAvailableProviders(kind, source).FirstOrDefault(info => Links(info, kind));

    /// <summary>
    /// Whether a provider takes links of a kind: movie links, or series links.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="kind">Series or movies.</param>
    /// <returns>Whether it takes them.</returns>
    private static bool Links(MetadataProviderInfo info, MetadataEntityType kind)
        => kind == MetadataEntityType.Movie
            ? info.Provider is IMetadataMovieLinkingProvider
            : info.Provider is IMetadataSeriesLinkingProvider series && series.LinkableEntityTypes.Contains(MetadataEntityType.Series);

    #endregion

    #region Cross-References

    /// <summary>
    /// Export a source's links as a CSV file, every section or the chosen
    /// ones, optionally filtered.
    /// </summary>
    /// <remarks>
    /// The file has up to three sections, each opening with a header: movie
    /// links (four columns: AniDB anime, AniDB episode, movie, rating), series
    /// links (three columns: AniDB anime, series, rating) and episode links
    /// (five columns: AniDB anime, AniDB episode, series, episode, rating).
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="body">What to export; everything when left out.</param>
    /// <returns>The file.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/CrossReferences/Export")]
    public ActionResult ExportCrossReferences([FromRoute] MetadataSource source, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataExportBody? body = null)
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        body ??= new();
        var text = transferService.Export(source, body.ToOptions());
        return File(Encoding.UTF8.GetBytes(text), "text/csv", $"anidb_{source.Value}_xrefs.csv");
    }

    /// <summary>
    /// Import a CSV file of a source's links, as the export writes them.
    /// </summary>
    /// <remarks>
    /// Sections are told apart by how many columns their headers have, not by
    /// the headers' names, so a file another tool wrote in the same layout
    /// for the same source imports as well. A <c>0</c> or an empty field is a
    /// link to nothing.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="file">The CSV file, in a form field named <c>file</c>.</param>
    /// <param name="removeExisting">Whether to drop the other links of every AniDB episode the file names.</param>
    /// <param name="addMissingSeries">Whether to fetch the series the file links and nothing stores yet.</param>
    /// <param name="addMissingMovies">Whether to fetch the movies the file links and nothing stores yet.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>What the import did, or a validation problem naming the lines that could not be read.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/CrossReferences/Import")]
    public async Task<ActionResult<MetadataImportSummary>> ImportCrossReferences(
        [FromRoute] MetadataSource source,
        IFormFile file,
        [FromQuery] bool removeExisting = true,
        [FromQuery] bool addMissingSeries = true,
        [FromQuery] bool addMissingMovies = true,
        CancellationToken cancellationToken = default
    )
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        if (file is null || file.Length == 0)
            ModelState.AddModelError("Body", "Body cannot be empty.");
        else if (file.Name != "file")
            ModelState.AddModelError("Body", "Invalid field name for import file");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        using var reader = new StreamReader(file!.OpenReadStream(), Encoding.UTF8, true);
        var result = await transferService.Import(
            source,
            reader,
            new() { RemoveExisting = removeExisting, AddMissingSeries = addMissingSeries, AddMissingMovies = addMissingMovies },
            cancellationToken
        ).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("Body", error.Message);
            return ValidationProblem(ModelState);
        }

        if (result.LinkCount is 0)
            return ValidationProblem("File contained no lines to import.", "Body");

        return new MetadataImportSummary(result);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh every series and movie of a source that is linked to an anime.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Only the <c>series</c> or only the <c>movie</c> entries; both when left out.</param>
    /// <param name="force">Refresh entries however recently they were.</param>
    /// <param name="downloadImages">Also download the images.</param>
    /// <returns><c>202 Accepted</c>; the refreshes are queued in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/RefreshAllLinked")]
    public ActionResult RefreshAllLinked([FromRoute] MetadataSource source, [FromQuery] MetadataEntityType? kind = null, [FromQuery] bool force = false, [FromQuery] bool downloadImages = true)
    {
        if ((RefuseNonTarget(source) ?? RefuseWithoutProvider(source) ?? RefuseKind(kind)) is { } refused)
            return refused;

        sourceActions.Start($"Refreshing every linked {source.Name} entry", () => sourceActions.RefreshAllLinked(source, kind, force, downloadImages));
        return Accepted();
    }

    /// <summary>
    /// Download the wanted images of every stored entry of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="force">Download the images that are there already too.</param>
    /// <returns><c>202 Accepted</c>; the downloads are queued in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/DownloadAllImages")]
    public ActionResult DownloadAllImages([FromRoute] MetadataSource source, [FromQuery] bool force = false)
    {
        if ((RefuseNonTarget(source) ?? RefuseWithoutProvider(source)) is { } refused)
            return refused;

        sourceActions.Start($"Downloading every {source.Name} image", () => sourceActions.DownloadAllImages(source, force));
        return Accepted();
    }

    /// <summary>
    /// Search for a match for every anime the source is not linked on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="force">
    /// Also search the anime left alone, and search while the source does not
    /// auto-link. An anime already linked on the source is never searched
    /// here, so no link is replaced.
    /// </param>
    /// <returns>
    /// <c>202 Accepted</c>; the searches are queued in the background. <c>503
    /// Service Unavailable</c> while the source's auto-linker is not
    /// configured.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/AutoSearchAll")]
    public ActionResult AutoSearchAll([FromRoute] MetadataSource source, [FromQuery] bool force = false)
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        if (providerManager.MetadataProviders.FirstOrDefault(info => info.Source == source && info.IsAutoLinker && info.Enabled) is not { } autoLinker)
            return ValidationProblem($"No enabled provider works out what an anime is from {source.Name}.", "source");

        if (!autoLinker.Provider.IsConfigured)
            return MetadataPauseResponses.NotConfigured(autoLinker);

        sourceActions.Start($"Searching {source.Name} for every anime", () => sourceActions.AutoSearchAll(source, force));
        return Accepted();
    }

    /// <summary>
    /// Purge the stored series and movies of a source that nothing links to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Only the <c>series</c> or only the <c>movie</c> entries; both when left out.</param>
    /// <param name="olderThan">Only the ones last refreshed before this.</param>
    /// <returns><c>202 Accepted</c>; the purges are queued in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/PurgeUnused")]
    public ActionResult PurgeUnused([FromRoute] MetadataSource source, [FromQuery] MetadataEntityType? kind = null, [FromQuery] DateTime? olderThan = null)
    {
        if ((RefuseNonTarget(source) ?? RefuseKind(kind)) is { } refused)
            return refused;

        sourceActions.Start($"Purging the unused {source.Name} entries", () => sourceActions.PurgeUnused(source, kind, olderThan));
        return Accepted();
    }

    /// <summary>
    /// Purge the people, tags, studios and networks of a source that no
    /// stored entry uses any more.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="olderThan">Only what was left unused before this.</param>
    /// <returns><c>202 Accepted</c>; the purge runs in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/PurgeOrphaned")]
    public ActionResult PurgeOrphaned([FromRoute] MetadataSource source, [FromQuery] DateTime? olderThan = null)
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        sourceActions.Start($"Purging the orphaned {source.Name} entries", () => sourceActions.PurgeOrphaned(source, olderThan));
        return Accepted();
    }

    /// <summary>
    /// Purge every stored collection of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><c>202 Accepted</c>; the purge runs in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/PurgeCollections")]
    public ActionResult PurgeCollections([FromRoute] MetadataSource source)
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        sourceActions.Start($"Purging the {source.Name} collections", () => sourceActions.PurgeCollections(source));
        return Accepted();
    }

    /// <summary>
    /// Purge the images of a source that nothing links to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><c>202 Accepted</c>; the purge is queued in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/PurgeUnusedImages")]
    public ActionResult PurgeUnusedImages([FromRoute] MetadataSource source)
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        sourceActions.Start($"Purging the unused {source.Name} images", () => sourceActions.PurgeUnusedImages(source));
        return Accepted();
    }

    /// <summary>
    /// Remove every link of a source, and choose whether it may auto-link the
    /// anime again.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="series">Remove the series links, with their episode links.</param>
    /// <param name="movies">Remove the movie links.</param>
    /// <param name="purge">Also purge what the links pointed at.</param>
    /// <param name="resetAutoLinkingState">
    /// <c>false</c> to let the source auto-link every anime again, <c>true</c>
    /// to keep it from auto-linking any; left out, nothing changes.
    /// </param>
    /// <returns><c>202 Accepted</c>; the links are removed in the background.</returns>
    [Authorize("admin")]
    [HttpPost("{source:metadata-source}/Action/RemoveAllLinks")]
    public ActionResult RemoveAllLinks(
        [FromRoute] MetadataSource source,
        [FromQuery] bool series = true,
        [FromQuery] bool movies = true,
        [FromQuery] bool purge = false,
        [FromQuery] bool? resetAutoLinkingState = null
    )
    {
        if (RefuseNonTarget(source) is { } refused)
            return refused;

        sourceActions.Start($"Removing every {source.Name} link", () => sourceActions.RemoveAllLinks(source, series, movies, purge, resetAutoLinkingState));
        return Accepted();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Refuses a source nothing is linked to, as the hub or one of the
    /// server's own.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A problem to answer with, or <c>null</c> to go ahead.</returns>
    private ActionResult? RefuseNonTarget(MetadataSource source)
        => MetadataSourceActions.IsLinkTarget(source) ? null : ValidationProblem($"Nothing is linked to {source.Name}, so it has no such action.", "source");

    /// <summary>
    /// Refuses a source no enabled provider answers for.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A problem to answer with, or <c>null</c> to go ahead.</returns>
    private ActionResult? RefuseWithoutProvider(MetadataSource source)
        => providerManager.MetadataProviders.Any(info => info.Source == source && info.Enabled)
            ? null
            : ValidationProblem($"No enabled provider answers for {source.Name}.", "source");

    /// <summary>
    /// Refuses a kind of entry other than series and movies.
    /// </summary>
    /// <param name="kind">The kind, if one was given.</param>
    /// <returns>A problem to answer with, or <c>null</c> to go ahead.</returns>
    private ActionResult? RefuseKind(MetadataEntityType? kind)
        => kind is null || kind == MetadataEntityType.Series || kind == MetadataEntityType.Movie
            ? null
            : ValidationProblem("Only series and movies can be chosen.", nameof(kind));

    #endregion
}
