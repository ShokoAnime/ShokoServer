using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.API.v3.Models.Airing.Input;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Settings;

using AiringScheduleDto = Shoko.Server.API.v3.Models.Airing.AiringSchedule;
using ConfigurationInfoDto = Shoko.Server.API.v3.Models.Configuration.ConfigurationInfo;
using EpisodeAiringDto = Shoko.Server.API.v3.Models.Airing.EpisodeAiring;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for reading airing schedules, their episode airings
/// and the shared channel registry, and for managing the airing schedule
/// providers. Interacts with the <see cref="IAiringScheduleService"/>.
/// </summary>
/// <remarks>
/// Schedules, airings and channels are owned by the providers that fetch them,
/// so everything but the provider settings and the server's own preference
/// lists is read-only here.
/// </remarks>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="pluginManager">Plugin manager.</param>
/// <param name="airingScheduleService">Airing schedule service.</param>
/// <param name="configurationProvider">Airing schedule service settings provider.</param>
/// <param name="anidbAnimes">AniDB anime repository.</param>
/// <param name="animeSeries">Shoko series repository.</param>
/// <param name="animeEpisodes">Shoko episode repository.</param>
/// <param name="applicationPaths">Finds the providers' icons on disk.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class AiringScheduleController(
    ISettingsProvider settingsProvider,
    IPluginManager pluginManager,
    IAiringScheduleService airingScheduleService,
    ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider,
    AniDB_AnimeRepository anidbAnimes,
    AnimeSeriesRepository animeSeries,
    AnimeEpisodeRepository animeEpisodes,
    IApplicationPaths applicationPaths
) : BaseController(settingsProvider)
{
    #region Constants

    internal const string AiringNotFoundWithAiringID = "No EpisodeAiring entry for the given airingID";

    internal const string AiringForbiddenForUser = "Accessing EpisodeAiring is not allowed for the current user";

    internal const string ScheduleNotFoundWithScheduleID = "No AiringSchedule entry for the given scheduleID";

    internal const string ProviderNotFoundWithProviderID = "No AiringScheduleProvider entry for the given providerID";

    internal const string ProviderIconNotFound = "The airing schedule provider was not found or has no icon.";

    internal const string ChannelNotFoundWithChannelID = "No AiringChannel entry for the given channelID";

    internal const string ChannelNotFoundWithName = "No AiringChannel entry for the given name";

    internal const string SeriesNotFoundWithSeriesID = "No Series entry for the given seriesID";

    internal const string SeriesForbiddenForUser = "Accessing Series is not allowed for the current user";

    internal const string EpisodeNotFoundWithEpisodeID = "No Episode entry for the given episodeID";

    internal const string EpisodeForbiddenForUser = "Accessing Episode is not allowed for the current user";

    /// <summary>
    /// How long a caller may ask a refresh to wait, in seconds. A longer
    /// timeout is clamped to this rather than rejected.
    /// </summary>
    internal const int MaxRefreshTimeoutSeconds = 300;

    #endregion

    #region Airings

    /// <summary>
    /// Get the episode airings in the given time-frame, for a calendar.
    /// </summary>
    /// <remarks>
    /// The range is compared as instants, <paramref name="from"/> inclusive and
    /// <paramref name="to"/> exclusive, and both must name their offset. An
    /// airing delayed out of the range is still returned by its original slot
    /// unless <paramref name="includeDelayedOriginalSlots"/> is turned off, so a
    /// client can draw the gap it left behind.
    /// </remarks>
    /// <param name="from">Start of the range, with an offset. Defaults to the start of today in UTC, or now for <paramref name="nextOnly"/>.</param>
    /// <param name="to">End of the range, exclusive, with an offset. Defaults to a week after the start.</param>
    /// <param name="kind">Only include airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="language">Only include airings whose schedule has a track in one of these languages.</param>
    /// <param name="channel">Only include airings on one of these channels.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="type">Only include airings of episodes of these episode types.</param>
    /// <param name="inCollection">Filter on whether the series is in the collection, which means it has a shoko series. Defaults to only those in it.</param>
    /// <param name="includeMissing">Include airings of series in the collection with no local files.</param>
    /// <param name="includeRestricted">Include airings of restricted (H) series.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedules' own lines.</param>
    /// <param name="includeDelayedOriginalSlots">Also match a delayed airing by the slot it was moved out of.</param>
    /// <param name="includeDateOnly">Include a date-only entry for each AniDB episode with an air date in the range and no airing at all.</param>
    /// <param name="preferredOnly">Only return one airing per episode, using the server's preference.</param>
    /// <param name="nextOnly">Only return the next airing at or after the start of the range, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The airings in the time-frame, in airing order.</returns>
    [HttpGet("Airing")]
    public ActionResult<List<EpisodeAiringDto>> GetAirings(
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? from = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? to = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.Only,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool includeDelayedOriginalSlots = true,
        [FromQuery] bool includeDateOnly = false,
        [FromQuery] bool preferredOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (!TryGetRange(from, to, nextOnly, out var start, out var end))
            return ValidationProblem(ModelState);

        var options = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : [AiringKind.Original],
            Languages = language is { Count: > 0 } ? language : null,
            ChannelIDs = channel is { Count: > 0 } ? channel : null,
            EpisodeTypes = type is { Count: > 0 } ? type : null,
            InCollection = ToInclusion(inCollection),
            IncludeMissing = ToInclusion(includeMissing),
            IncludeRestricted = ToInclusion(includeRestricted),
            User = HttpContext.GetUser(),
            IncludeEstimates = includeEstimates,
            IncludeDelayedOriginalSlots = includeDelayedOriginalSlots,
            IncludeDateOnly = includeDateOnly,
            PreferredOnly = preferredOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            EntityAnchor = entityAnchor,
        };
        var context = new AiringReadCache(this);
        return airingScheduleService.GetAiringsInRange(start, end, options)
            .Select(airing => context.ToDto(airing, include))
            .ToList();
    }

    /// <summary>
    /// Get a single episode airing.
    /// </summary>
    /// <param name="airingID">The ID of the airing.</param>
    /// <param name="include">Extra display data to resolve for the airing.</param>
    /// <returns>The airing.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Airing/{airingID:guid}")]
    public ActionResult<EpisodeAiringDto> GetAiringByID(
        [FromRoute] Guid airingID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (airingScheduleService.GetAiringByID(airingID) is not { } airing)
            return NotFound(AiringNotFoundWithAiringID);

        var context = new AiringReadCache(this);
        if (!context.IsAllowed(airing))
            return Forbid(AiringForbiddenForUser);

        return context.ToDto(airing, include);
    }

    /// <summary>
    /// Get every airing linked to the given airing, the link head first.
    /// </summary>
    /// <remarks>
    /// A link set is one slot covering several episodes, e.g. a double bill.
    /// An airing that is not linked returns an empty list.
    /// </remarks>
    /// <param name="airingID">The ID of the airing.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The link set, the head first.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Airing/{airingID:guid}/Linked")]
    public ActionResult<List<EpisodeAiringDto>> GetLinkedAiringsByAiringID(
        [FromRoute] Guid airingID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (airingScheduleService.GetAiringByID(airingID) is not { } airing)
            return NotFound(AiringNotFoundWithAiringID);

        var context = new AiringReadCache(this);
        if (!context.IsAllowed(airing))
            return Forbid(AiringForbiddenForUser);

        return airingScheduleService.GetLinkedAirings(airingID)
            .Select(member => context.ToDto(member, include))
            .ToList();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Get the airing schedule service's own configuration: the preference
    /// lists, the cleanup and the sweep budget. Read and edit it through the
    /// configuration endpoints by its ID.
    /// </summary>
    /// <returns>The configuration's info.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Configuration")]
    public ActionResult<ConfigurationInfoDto> GetConfiguration()
        => new ConfigurationInfoDto(airingScheduleService.ConfigurationInfo);

    #endregion

    #region Schedules

    /// <summary>
    /// Get a single airing schedule.
    /// </summary>
    /// <param name="scheduleID">The ID of the schedule.</param>
    /// <returns>The schedule.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("{scheduleID:guid}")]
    public ActionResult<AiringScheduleDto> GetScheduleByID([FromRoute] Guid scheduleID)
    {
        if (airingScheduleService.GetScheduleByID(scheduleID) is not { } schedule)
            return NotFound(ScheduleNotFoundWithScheduleID);

        return new AiringScheduleDto(schedule);
    }

    /// <summary>
    /// Get every episode airing on a schedule.
    /// </summary>
    /// <param name="scheduleID">The ID of the schedule.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedule's own line.</param>
    /// <param name="nextOnly">Only return the next airing from now, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The schedule's airings, in airing order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("{scheduleID:guid}/Airing")]
    public ActionResult<List<EpisodeAiringDto>> GetAiringsByScheduleID(
        [FromRoute] Guid scheduleID,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (airingScheduleService.GetScheduleByID(scheduleID) is null)
            return NotFound(ScheduleNotFoundWithScheduleID);

        var options = new EpisodeAiringFilteringOptions
        {
            User = HttpContext.GetUser(),
            IncludeEstimates = includeEstimates,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            EntityAnchor = entityAnchor,
        };
        var context = new AiringReadCache(this);
        return airingScheduleService.GetAiringsForSchedule(scheduleID, options)
            .OrderBy(GetListSortTime)
            .ThenBy(airing => airing.ID)
            .Select(airing => context.ToDto(airing, include))
            .ToList();
    }

    #endregion

    #region Providers

    /// <summary>
    /// Get all airing schedule providers available, with their current enabled
    /// kinds and priority states.
    /// </summary>
    /// <param name="pluginID">Optional. Plugin ID to get airing schedule providers for.</param>
    /// <returns>A list of <see cref="AiringScheduleProvider"/>.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider")]
    public ActionResult<List<AiringScheduleProvider>> GetAvailableProviders([FromQuery] Guid? pluginID = null)
        => pluginID.HasValue
            ? pluginManager.GetPluginInfo(pluginID.Value) is { IsActive: true } pluginInfo
                ? airingScheduleService.GetProviderInfo(pluginInfo.Plugin)
                    .Select(providerInfo => new AiringScheduleProvider(providerInfo))
                    .ToList()
                : []
            : airingScheduleService.GetAvailableProviders()
                .Select(providerInfo => new AiringScheduleProvider(providerInfo))
                .ToList();

    /// <summary>
    /// Update the enabled kinds and/or priority of one or more airing schedule
    /// providers in the same request.
    /// </summary>
    /// <param name="body">The providers to update.</param>
    /// <returns>Nothing on success.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [ProducesResponseType(200)]
    [HttpPost("Provider")]
    public ActionResult UpdateMultipleProviders([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<UpdateMultipleProvidersBody> body)
    {
        var providerInfoDict = airingScheduleService.GetAvailableProviders().ToDictionary(provider => provider.ID);
        var changedProviders = new List<AiringScheduleProviderInfo>();
        foreach (var provider in body)
        {
            if (!providerInfoDict.TryGetValue(provider.ID, out var providerInfo))
                continue;

            if (ApplyProviderChanges(providerInfo, provider.Priority, provider.EnabledKinds, provider.SweepInterval))
                changedProviders.Add(providerInfo);
        }

        if (changedProviders.Count > 0)
            airingScheduleService.UpdateProviders([.. changedProviders]);

        return Ok();
    }

    /// <summary>
    /// Get a specific airing schedule provider, with its current enabled kinds
    /// and priority state.
    /// </summary>
    /// <param name="providerID">The ID of the provider to get.</param>
    /// <returns>An <see cref="AiringScheduleProvider"/>.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Provider/{providerID:guid}")]
    public ActionResult<AiringScheduleProvider> GetProviderByID([FromRoute] Guid providerID)
    {
        if (airingScheduleService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound(ProviderNotFoundWithProviderID);

        return new AiringScheduleProvider(providerInfo);
    }

    /// <summary>
    /// Get an airing schedule provider's icon: its own, else its plugin's.
    /// </summary>
    /// <remarks>
    /// An SVG or a PNG, sent so that an SVG opened on its own runs no script.
    /// </remarks>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>
    /// The icon, <c>304 Not Modified</c> when the client's copy has the same
    /// ETag, or <c>404 Not Found</c> when the provider is unknown or has none.
    /// </returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider/{providerID:guid}/Icon")]
    public ActionResult GetProviderIcon([FromRoute] Guid providerID)
        => PackageIcon(airingScheduleService.GetProviderInfo(providerID)?.Icon, applicationPaths, ProviderIconNotFound);

    /// <summary>
    /// Update the enabled kinds and/or priority of a specific airing schedule
    /// provider.
    /// </summary>
    /// <param name="providerID">The ID of the provider to update.</param>
    /// <param name="body">The changes to apply.</param>
    /// <returns>The updated <see cref="AiringScheduleProvider"/>.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpPut("Provider/{providerID:guid}")]
    public ActionResult<AiringScheduleProvider> UpdateProviderByID(
        [FromRoute] Guid providerID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateSingleProviderBody body
    )
    {
        if (airingScheduleService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound(ProviderNotFoundWithProviderID);

        if (ApplyProviderChanges(providerInfo, body.Priority, body.EnabledKinds, body.SweepInterval))
            airingScheduleService.UpdateProviders(providerInfo);

        return GetProviderByID(providerID);
    }

    /// <summary>
    /// Apply the wanted priority and enabled kinds to a provider info.
    /// </summary>
    /// <param name="providerInfo">The provider info to change, which is a copy of the registered one.</param>
    /// <param name="priority">The wanted priority, or <c>null</c> to leave it alone.</param>
    /// <param name="enabledKinds">The wanted kinds, or <c>null</c> to leave them alone.</param>
    /// <param name="sweepInterval">The wanted sweep interval, or <c>null</c> to leave it alone.</param>
    /// <returns>Whether anything changed.</returns>
    private static bool ApplyProviderChanges(AiringScheduleProviderInfo providerInfo, int? priority, IReadOnlyList<AiringKind>? enabledKinds, TimeSpan? sweepInterval)
    {
        var changed = false;
        if (enabledKinds is not null)
        {
            // Kinds the provider doesn't declare are dropped by the service, so compare against what it will actually keep.
            var wantedKinds = enabledKinds.Where(providerInfo.Provider.AvailableKinds.Contains).ToHashSet();
            if (!wantedKinds.SetEquals(providerInfo.EnabledKinds))
            {
                providerInfo.EnabledKinds = wantedKinds;
                changed = true;
            }
        }

        if (priority.HasValue && priority.Value != providerInfo.Priority)
        {
            providerInfo.Priority = priority.Value;
            changed = true;
        }

        // The floor is the service's, so an impatient value is clamped there rather than rejected.
        if (sweepInterval.HasValue && sweepInterval.Value != providerInfo.SweepInterval)
        {
            providerInfo.SweepInterval = sweepInterval.Value;
            changed = true;
        }

        return changed;
    }

    #endregion

    #region Channels

    /// <summary>
    /// Get every known channel.
    /// </summary>
    /// <param name="type">Optional. Only return channels of this type.</param>
    /// <returns>The channels, by name.</returns>
    [HttpGet("Channel")]
    public ActionResult<List<AiringChannel>> GetChannels([FromQuery] AiringChannelType? type = null)
        => airingScheduleService.GetAllChannels(type)
            .Select(channel => new AiringChannel(channel))
            .ToList();

    /// <summary>
    /// Get a channel by one of its names.
    /// </summary>
    /// <remarks>
    /// The lookup normalises the name the same way the registry does, so
    /// <c>TOKYO MX</c>, <c> tokyo  mx </c> and <c>ＴＯＫＹＯ　ＭＸ</c> all find
    /// the same channel.
    /// </remarks>
    /// <param name="name">The name or alias to look up.</param>
    /// <param name="type">Optional. Only look among channels of this type. Every type is searched when omitted.</param>
    /// <param name="useAliases">Whether a channel's aliases count as its names.</param>
    /// <returns>The channel.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Channel/ByName")]
    public ActionResult<AiringChannel> GetChannelByName(
        [FromQuery, Required] string name,
        [FromQuery] AiringChannelType? type = null,
        [FromQuery] bool useAliases = true
    )
    {
        var types = type.HasValue ? new[] { type.Value } : Enum.GetValues<AiringChannelType>();
        foreach (var channelType in types)
            if (airingScheduleService.GetChannelByName(name, channelType, useAliases) is { } channel)
                return new AiringChannel(channel);

        return NotFound(ChannelNotFoundWithName);
    }

    /// <summary>
    /// Get a single channel.
    /// </summary>
    /// <param name="channelID">The ID of the channel.</param>
    /// <returns>The channel.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Channel/{channelID:guid}")]
    public ActionResult<AiringChannel> GetChannelByID([FromRoute] Guid channelID)
    {
        if (airingScheduleService.GetChannelByID(channelID) is not { } channel)
            return NotFound(ChannelNotFoundWithChannelID);

        return new AiringChannel(channel);
    }

    /// <summary>
    /// Get what airs on a channel in the given time-frame.
    /// </summary>
    /// <remarks>
    /// The same read as <c>GET /api/v3/AiringSchedule/Airing</c>, narrowed to
    /// one channel, and filtered the same way. <paramref name="kind"/> is the
    /// one deliberate difference, defaulting to every kind rather than to
    /// <see cref="AiringKind.Original"/>: a caller that names a channel has
    /// already narrowed the read to it, and a streaming channel carries no
    /// <see cref="AiringKind.Original"/> track at all.
    /// </remarks>
    /// <param name="channelID">The ID of the channel.</param>
    /// <param name="from">Start of the range, with an offset. Defaults to the start of today in UTC, or now for <paramref name="nextOnly"/>.</param>
    /// <param name="to">End of the range, exclusive, with an offset. Defaults to a week after the start.</param>
    /// <param name="kind">Only include airings whose schedule has a track of these kinds. Defaults to every kind.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="type">Only include airings of episodes of these episode types.</param>
    /// <param name="inCollection">Filter on whether the series is in the collection, which means it has a shoko series. Defaults to only those in it.</param>
    /// <param name="includeMissing">Include airings of series in the collection with no local files.</param>
    /// <param name="includeRestricted">Include airings of restricted (H) series.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedules' own lines.</param>
    /// <param name="includeDelayedOriginalSlots">Also match a delayed airing by the slot it was moved out of.</param>
    /// <param name="preferredOnly">Only return one airing per episode, using the server's preference.</param>
    /// <param name="nextOnly">Only return the next airing at or after the start of the range, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The channel's airings in the time-frame, in airing order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Channel/{channelID:guid}/Airing")]
    public ActionResult<List<EpisodeAiringDto>> GetAiringsByChannelID(
        [FromRoute] Guid channelID,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? from = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? to = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.Only,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool includeDelayedOriginalSlots = true,
        [FromQuery] bool preferredOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (airingScheduleService.GetChannelByID(channelID) is null)
            return NotFound(ChannelNotFoundWithChannelID);

        if (!TryGetRange(from, to, nextOnly, out var start, out var end))
            return ValidationProblem(ModelState);

        var options = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : null,
            ChannelIDs = new HashSet<Guid> { channelID },
            EpisodeTypes = type is { Count: > 0 } ? type : null,
            InCollection = ToInclusion(inCollection),
            IncludeMissing = ToInclusion(includeMissing),
            IncludeRestricted = ToInclusion(includeRestricted),
            User = HttpContext.GetUser(),
            IncludeEstimates = includeEstimates,
            IncludeDelayedOriginalSlots = includeDelayedOriginalSlots,
            PreferredOnly = preferredOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            EntityAnchor = entityAnchor,
        };
        var context = new AiringReadCache(this);
        return airingScheduleService.GetAiringsInRange(start, end, options)
            .Select(airing => context.ToDto(airing, include))
            .ToList();
    }

    #endregion

    #region Preferences

    /// <summary>
    /// Get the server's preferred channel order, best first.
    /// </summary>
    /// <remarks>
    /// An empty list means no channel preference at all, and every read that
    /// doesn't bring its own preference falls back to this one.
    /// </remarks>
    /// <returns>The preferred channels, best first.</returns>
    [HttpGet("Channel/Priority")]
    public ActionResult<List<Guid>> GetChannelPriority()
        => configurationProvider.Load().PreferredChannels;

    /// <summary>
    /// Set the server's preferred channel order, best first.
    /// </summary>
    /// <param name="body">The preferred channels, best first. An empty list clears the preference.</param>
    /// <returns>The stored preference.</returns>
    [Authorize(Roles = "admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPut("Channel/Priority")]
    public ActionResult<List<Guid>> SetChannelPriority([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] List<Guid> body)
    {
        var channels = body.Distinct().ToList();
        var unknownChannels = channels.Where(channelID => airingScheduleService.GetChannelByID(channelID) is null).ToList();
        if (unknownChannels.Count > 0)
        {
            ModelState.AddModelError(nameof(body), $"Unknown channels; {string.Join(", ", unknownChannels)}");
            return ValidationProblem(ModelState);
        }

        var config = configurationProvider.Load();
        config.PreferredChannels = channels;
        configurationProvider.Save(config);
        return channels;
    }

    /// <summary>
    /// Get the server's preferred track order, best first.
    /// </summary>
    /// <remarks>
    /// An empty list means no track preference at all, and every read that
    /// doesn't bring its own preference falls back to this one.
    /// </remarks>
    /// <returns>The preferred tracks, best first.</returns>
    [HttpGet("Track/Priority")]
    public ActionResult<List<TrackPreference>> GetTrackPriority()
        => configurationProvider.Load().PreferredTracks
            .Select(preference => new TrackPreference(preference))
            .ToList();

    /// <summary>
    /// Set the server's preferred track order, best first.
    /// </summary>
    /// <param name="body">The preferred tracks, best first. An empty list clears the preference.</param>
    /// <returns>The stored preference.</returns>
    [Authorize(Roles = "admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPut("Track/Priority")]
    public ActionResult<List<TrackPreference>> SetTrackPriority([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] List<TrackPreference> body)
    {
        var tracks = body
            .Select(preference => preference.ToPreference())
            .Distinct()
            .ToList();
        var config = configurationProvider.Load();
        config.PreferredTracks = tracks;
        configurationProvider.Save(config);
        return tracks
            .Select(preference => new TrackPreference(preference))
            .ToList();
    }

    #endregion

    #region Time Zones

    /// <summary>
    /// Get the time zones a schedule may use, normalised to IANA ids on every
    /// host.
    /// </summary>
    /// <returns>The available time zones, by ID.</returns>
    [HttpGet("TimeZone")]
    public ActionResult<List<AiringTimeZone>> GetAvailableTimeZones()
        => airingScheduleService.GetAvailableTimeZones()
            .Select(zone => new AiringTimeZone(zone))
            .ToList();

    #endregion

    #region Series

    /// <summary>
    /// Get every airing schedule covering a shoko series.
    /// </summary>
    /// <param name="seriesID">The ID of the shoko series.</param>
    /// <param name="kind">Only include schedules with a track of this kind.</param>
    /// <param name="channel">Only include schedules on one of these channels.</param>
    /// <param name="includeSeasonSchedules">Include the schedules narrowed to one of the series' seasons.</param>
    /// <param name="linkedEntitySchedules">
    ///   Set to <c>false</c> for only the series' own schedules, <c>true</c> to also walk its linked entities, or leave it out to let the server decide.
    /// </param>
    /// <param name="entityAnchor">Which entities the schedules are anchored to. <c>Shoko</c> drops the schedules that resolve to no shoko series.</param>
    /// <returns>The series' schedules.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [HttpGet("~/api/v{version:apiVersion}/Series/{seriesID}/AiringSchedule"), Tags("Series")]
    public ActionResult<List<AiringScheduleDto>> GetSchedulesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] AiringKind? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery] bool includeSeasonSchedules = true,
        [FromQuery] bool? linkedEntitySchedules = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto
    )
    {
        if (animeSeries.GetByID(seriesID) is not { } series)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var options = new AiringScheduleFilteringOptions
        {
            Kind = kind,
            ChannelIDs = channel is { Count: > 0 } ? channel : null,
            IncludeSeasonSchedules = includeSeasonSchedules,
            LinkedEntitySchedules = linkedEntitySchedules,
            EntityAnchor = entityAnchor,
        };
        return airingScheduleService.GetSchedulesForSeries(series, options)
            .Select(schedule => new AiringScheduleDto(schedule))
            .ToList();
    }

    /// <summary>
    /// Get every episode airing for a shoko series.
    /// </summary>
    /// <param name="seriesID">The ID of the shoko series.</param>
    /// <param name="kind">Only include airings whose schedule has a track of these kinds.</param>
    /// <param name="language">Only include airings whose schedule has a track in one of these languages.</param>
    /// <param name="channel">Only include airings on one of these channels.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="includeDateOnly">Include a date-only entry for each AniDB episode with an air date and no airing at all.</param>
    /// <param name="nextOnly">Only return the next airing from now, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="linkedEntityAirings">
    ///   Set to <c>false</c> for only the series' own airings, <c>true</c> to also walk its linked entities, or leave it out to let the server decide.
    /// </param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The series' airings, in airing order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [HttpGet("~/api/v{version:apiVersion}/Series/{seriesID}/AiringSchedule/Airing"), Tags("Series")]
    public ActionResult<List<EpisodeAiringDto>> GetAiringsBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery] bool includeDateOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] bool? linkedEntityAirings = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (animeSeries.GetByID(seriesID) is not { } series)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        var options = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : null,
            Languages = language is { Count: > 0 } ? language : null,
            ChannelIDs = channel is { Count: > 0 } ? channel : null,
            IncludeDateOnly = includeDateOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            LinkedEntityAirings = linkedEntityAirings,
            EntityAnchor = entityAnchor,
        };
        var context = new AiringReadCache(this);
        return airingScheduleService.GetAiringsForSeries(series, options)
            .OrderBy(GetListSortTime)
            .ThenBy(airing => airing.ID)
            .Select(airing => context.ToDto(airing, include))
            .ToList();
    }

    /// <summary>
    /// Refresh the airing schedules for a shoko series with every enabled
    /// provider.
    /// </summary>
    /// <param name="seriesID">The ID of the shoko series.</param>
    /// <param name="wait">Wait for the providers to finish instead of returning as soon as the work is queued.</param>
    /// <param name="timeout">How long to wait, in seconds. Clamped to at most <see cref="MaxRefreshTimeoutSeconds"/>.</param>
    /// <returns>What each provider did and the series' schedules afterwards, or nothing when the caller didn't wait.</returns>
    /// <remarks>Refreshing is an administrator operation; reading a schedule is not.</remarks>
    [ProducesResponseType(200)]
    [ProducesResponseType(202)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [Authorize(Roles = "admin")]
    [HttpPost("~/api/v{version:apiVersion}/Series/{seriesID}/AiringSchedule/Refresh"), Tags("Series")]
    public async Task<ActionResult<AiringRefreshResult>> RefreshSchedulesBySeriesID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool wait = false,
        [FromQuery] int timeout = 60
    )
    {
        if (animeSeries.GetByID(seriesID) is not { } series)
            return NotFound(SeriesNotFoundWithSeriesID);

        if (!User.AllowedSeries(series))
            return Forbid(SeriesForbiddenForUser);

        if (!wait)
        {
            await airingScheduleService.ScheduleRefresh(series);
            return Accepted();
        }

        return await RefreshAndWait(
            token => airingScheduleService.RefreshAsync(series, token),
            () => airingScheduleService.GetSchedulesForSeries(series),
            timeout
        );
    }

    #endregion

    #region Episodes

    /// <summary>
    /// Get every episode airing for a shoko episode.
    /// </summary>
    /// <param name="episodeID">The ID of the shoko episode.</param>
    /// <param name="kind">Only include airings whose schedule has a track of these kinds.</param>
    /// <param name="language">Only include airings whose schedule has a track in one of these languages.</param>
    /// <param name="channel">Only include airings on one of these channels.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="includeDateOnly">Include a date-only entry when the episode has an AniDB air date and no airing at all.</param>
    /// <param name="nextOnly">Only return the next airing from now, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="includeDisabled">
    ///   Include airings hidden because their provider is disabled, or because none of their schedule's tracks are of an enabled kind.
    /// </param>
    /// <param name="linkedEntityAirings">
    ///   Set to <c>false</c> for only the episode's own airings, <c>true</c> to also walk its linked entities, or leave it out to let the server decide.
    /// </param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The episode's airings, best first.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [HttpGet("~/api/v{version:apiVersion}/Episode/{episodeID}/AiringSchedule/Airing"), Tags("Episode")]
    public ActionResult<List<EpisodeAiringDto>> GetAiringsByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery] bool includeDateOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool? linkedEntityAirings = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null
    )
    {
        if (animeEpisodes.GetByID(episodeID) is not { } episode)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        if (episode.AnimeSeries is not { } series)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        var options = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : null,
            Languages = language is { Count: > 0 } ? language : null,
            ChannelIDs = channel is { Count: > 0 } ? channel : null,
            IncludeDisabled = includeDisabled,
            IncludeDateOnly = includeDateOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            LinkedEntityAirings = linkedEntityAirings,
            EntityAnchor = entityAnchor,
        };
        var context = new AiringReadCache(this);
        return airingScheduleService.GetAiringsForEpisode(episode, options)
            .Select(airing => context.ToDto(airing, include))
            .ToList();
    }

    /// <summary>
    /// Refresh the airing schedules covering a shoko episode with every enabled
    /// provider.
    /// </summary>
    /// <param name="episodeID">The ID of the shoko episode.</param>
    /// <param name="wait">Wait for the providers to finish instead of returning as soon as the work is queued.</param>
    /// <param name="timeout">How long to wait, in seconds. Clamped to at most <see cref="MaxRefreshTimeoutSeconds"/>.</param>
    /// <returns>What each provider did and the series' schedules afterwards, or nothing when the caller didn't wait.</returns>
    /// <remarks>Refreshing is an administrator operation; reading a schedule is not.</remarks>
    [ProducesResponseType(200)]
    [ProducesResponseType(202)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [Authorize(Roles = "admin")]
    [HttpPost("~/api/v{version:apiVersion}/Episode/{episodeID}/AiringSchedule/Refresh"), Tags("Episode")]
    public async Task<ActionResult<AiringRefreshResult>> RefreshSchedulesByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery] bool wait = false,
        [FromQuery] int timeout = 60
    )
    {
        if (animeEpisodes.GetByID(episodeID) is not { } episode)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        if (episode.AnimeSeries is not { } series)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (!wait)
        {
            await airingScheduleService.ScheduleRefresh(episode);
            return Accepted();
        }

        return await RefreshAndWait(
            token => airingScheduleService.RefreshAsync(episode, token),
            () => airingScheduleService.GetSchedulesForSeries(series),
            timeout
        );
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Wait for every enabled provider to finish a refresh, or report them all
    /// as timed out when the wait runs out first.
    /// </summary>
    /// <param name="refresh">How to start and await the refresh.</param>
    /// <param name="schedules">How to read the entity's schedules back when the wait timed out.</param>
    /// <param name="timeout">How long to wait, in seconds, before the clamp.</param>
    /// <returns>What each provider did, and the entity's schedules.</returns>
    private async Task<AiringRefreshResult> RefreshAndWait(
        Func<CancellationToken, Task<AiringScheduleRefreshResult>> refresh,
        Func<IReadOnlyList<IAiringSchedule>> schedules,
        int timeout
    )
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        source.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeout, 1, MaxRefreshTimeoutSeconds)));
        try
        {
            return new AiringRefreshResult(await refresh(source.Token));
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            // The queued work carries on without us, so report the wait rather than a failure.
            var providers = airingScheduleService.GetAvailableProviders(onlyEnabled: true)
                .Select(info => new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.TimedOut, null))
                .ToList();
            return new AiringRefreshResult(providers, schedules());
        }
    }

    /// <summary>
    /// The time an entity read orders an airing by: its slot, or the start of
    /// a date-only entry's day in UTC.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The time to order by.</returns>
    private static DateTime GetListSortTime(IEpisodeAiring airing)
        => airing.AiredAt ?? airing.OriginalAiredAt ?? airing.AirDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? DateTime.MaxValue;

    /// <summary>
    /// The range a range read runs over: the caller's, or the defaults, which
    /// are the start of today in UTC, or now for a next-only read, and a week
    /// after the start.
    /// </summary>
    /// <param name="from">The start the caller asked for.</param>
    /// <param name="to">The end the caller asked for.</param>
    /// <param name="nextOnly">Whether the read is next-only.</param>
    /// <param name="start">The start of the range.</param>
    /// <param name="end">The exclusive end of the range.</param>
    /// <returns><c>false</c> with a model error when the end is before the start.</returns>
    private bool TryGetRange(DateTimeOffset? from, DateTimeOffset? to, bool nextOnly, out DateTimeOffset start, out DateTimeOffset end)
    {
        var now = DateTimeOffset.UtcNow;
        start = from ?? (nextOnly ? now : new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero));
        end = to ?? AddClamped(start, TimeSpan.FromDays(7));
        if (end >= start)
            return true;

        ModelState.AddModelError(nameof(to), "The end of the range is before its start.");
        return false;
    }

    /// <summary>
    /// Add <paramref name="offset"/> to <paramref name="value"/>, clamped to
    /// <see cref="DateTimeOffset.MaxValue"/> instead of throwing when the
    /// result would fall outside it.
    /// </summary>
    /// <param name="value">The point in time to add to.</param>
    /// <param name="offset">The time to add.</param>
    /// <returns>The shifted point in time.</returns>
    private static DateTimeOffset AddClamped(DateTimeOffset value, TimeSpan offset)
    {
        try
        {
            return value + offset;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.MaxValue;
        }
    }

    /// <summary>
    /// The service's counterpart of an APIv3 include filter.
    /// </summary>
    /// <param name="filter">The APIv3 filter.</param>
    /// <returns>The same filter, for the service.</returns>
    private static InclusionFilter ToInclusion(IncludeOnlyFilter filter)
        => filter switch
        {
            IncludeOnlyFilter.True => InclusionFilter.True,
            IncludeOnlyFilter.Only => InclusionFilter.Only,
            _ => InclusionFilter.False,
        };

    /// <summary>
    /// The series behind an airing: the shoko series where there is one, else
    /// whatever the airing or its schedule resolved to.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The series, or <c>null</c> when none could be resolved.</returns>
    private static ISeries? GetSeriesFor(IEpisodeAiring airing)
        => (ISeries?)airing.ShokoEpisode?.Series ?? airing.AnidbEpisode?.Series ?? airing.Episode?.Series ?? airing.Schedule?.Series;

    /// <summary>
    /// Whether the user may see an AniDB anime. An anime that is not cached
    /// has nothing to hide behind.
    /// </summary>
    /// <param name="animeID">The ID of the AniDB anime.</param>
    /// <returns><c>true</c> when the user may see it.</returns>
    private bool IsAllowedAnime(int animeID)
        => anidbAnimes.GetByAnimeID(animeID) is not { } anime || User.AllowedAnime(anime);

    /// <summary>
    /// The per-request resolution cache behind an airing read. Display data is
    /// resolved once per distinct series, since a calendar week is many
    /// episodes of few series.
    /// </summary>
    /// <param name="controller">The controller running the read.</param>
    private sealed class AiringReadCache(AiringScheduleController controller)
    {
        private readonly Dictionary<MetadataGuid, AiringSeries> _seriesDtos = [];

        private readonly Dictionary<MetadataGuid, Image?> _posters = [];

        /// <summary>
        /// Whether the user may see the series behind an airing. An airing
        /// whose series resolves to no AniDB anime has nothing to hide behind.
        /// </summary>
        /// <param name="airing">The airing.</param>
        /// <returns><c>true</c> when the user may see it.</returns>
        public bool IsAllowed(IEpisodeAiring airing)
        {
            var anidbAnimeID = GetSeriesFor(airing) switch
            {
                IShokoSeries shokoSeries => shokoSeries.AnidbAnimeID,
                IAnidbAnime anidbSeries => anidbSeries.AnidbID,
                _ => (int?)null,
            };
            return anidbAnimeID is not { } animeID || controller.IsAllowedAnime(animeID);
        }

        /// <summary>
        /// Build the wire model for an airing, resolving only the display data
        /// the caller asked for.
        /// </summary>
        /// <param name="airing">The airing.</param>
        /// <param name="include">The display data to resolve.</param>
        /// <returns>The airing.</returns>
        public EpisodeAiringDto ToDto(IEpisodeAiring airing, IReadOnlySet<AiringDataToInclude>? include)
        {
            if (include is not { Count: > 0 })
                return new(airing);

            var series = GetSeriesFor(airing);
            var episode = (IEpisode?)airing.ShokoEpisode ?? airing.AnidbEpisode ?? airing.Episode;
            var title = include.Contains(AiringDataToInclude.EpisodeTitle) ? episode?.Title : null;
            var seriesDto = include.Contains(AiringDataToInclude.Series) ? GetSeriesDto(series) : null;
            var poster = include.Contains(AiringDataToInclude.Poster) ? GetPoster(series) : null;
            var thumbnail = include.Contains(AiringDataToInclude.Thumbnail)
                ? (episode?.BackdropImage ?? series?.BackdropImage) is { } image ? new Image(image) : null
                : null;
            return new(airing, seriesDto, title, poster, thumbnail);
        }

        /// <summary>
        /// The wire model for a series, built once per read.
        /// </summary>
        /// <param name="series">The series.</param>
        /// <returns>The series, or <c>null</c> when none could be resolved.</returns>
        private AiringSeries? GetSeriesDto(ISeries? series)
        {
            if (series is null)
                return null;

            if (_seriesDtos.TryGetValue(series.ID, out var dto))
                return dto;

            return _seriesDtos[series.ID] = new AiringSeries(series);
        }

        /// <summary>
        /// The series' primary image, resolved once per read.
        /// </summary>
        /// <param name="series">The series.</param>
        /// <returns>The poster, or <c>null</c> when the series has none.</returns>
        private Image? GetPoster(ISeries? series)
        {
            if (series is null)
                return null;

            if (_posters.TryGetValue(series.ID, out var poster))
                return poster;

            return _posters[series.ID] = series.PrimaryImage is { } image ? new Image(image) : null;
        }
    }

    #endregion
}
