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
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.API.v3.Models.Airing.Input;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;

using AiringCalendarDayDto = Shoko.Server.API.v3.Models.Airing.AiringCalendarDay;
using AiringScheduleDto = Shoko.Server.API.v3.Models.Airing.AiringSchedule;
using ConfigurationInfoDto = Shoko.Server.API.v3.Models.Configuration.ConfigurationInfo;
using EpisodeAiringDto = Shoko.Server.API.v3.Models.Airing.EpisodeAiring;
using FilterBody = Shoko.Server.API.v3.Models.Shoko.Filter.Input.CreateOrUpdateFilterBody;
using SeasonSectionDto = Shoko.Server.API.v3.Models.Airing.SeasonSection;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for reading airing schedules, their episode airings
/// and the shared channel registry, and for managing the airing schedule
/// providers. Interacts with the <see cref="IAiringScheduleService"/>.
/// </summary>
/// <remarks>
/// Schedules, airings and channels are owned by the providers that fetch them,
/// so everything but the provider settings, the server's own preference lists
/// and the channels' aliases, merges and hidden state is read-only here.
/// </remarks>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="pluginManager">Plugin manager.</param>
/// <param name="airingScheduleService">Airing schedule service.</param>
/// <param name="configurationProvider">Airing schedule service settings provider.</param>
/// <param name="anidbAnimes">AniDB anime repository.</param>
/// <param name="animeSeries">Shoko series repository.</param>
/// <param name="animeEpisodes">Shoko episode repository.</param>
/// <param name="applicationPaths">Finds the providers' icons on disk.</param>
/// <param name="anidbCatalog">Lists the cached AniDB anime of the seasons.</param>
/// <param name="seasonAnimeBuilder">Builds the season view's anime.</param>
/// <param name="airingCalendarService">Sorts and groups the calendar views.</param>
/// <param name="filterPresets">The stored filters the reads can be narrowed by.</param>
/// <param name="filterFactory">Builds the filters sent with a request.</param>
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
    IApplicationPaths applicationPaths,
    AnidbAnimeCatalog anidbCatalog,
    SeasonAnimeBuilder seasonAnimeBuilder,
    IAiringCalendarService airingCalendarService,
    FilterPresetRepository filterPresets,
    FilterFactory filterFactory
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
    /// <param name="channel">Only include airings on one of these channels, hidden or not. Without it, the hidden channels are left out.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="type">Only include airings of episodes of these episode types.</param>
    /// <param name="episodeKind">Only include airings of these kinds of showing. Leave out <c>Rerun</c> and <c>DetectedRerun</c> for no reruns.</param>
    /// <param name="inCollection">Filter on whether the series is in the collection, which means it has a shoko series. Defaults to only those in it.</param>
    /// <param name="includeRestricted">Include airings of restricted (H) series.</param>
    /// <param name="filterID">Only include airings of the Shoko series passing this stored filter, for the current user. Series not in the collection are left out.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedules' own lines.</param>
    /// <param name="includeDelayedOriginalSlots">Also match a delayed airing by the slot it was moved out of.</param>
    /// <param name="includeDateOnly">Include a date-only entry for each AniDB episode with an air date in the range and no airing at all.</param>
    /// <param name="preferredOnly">Only return one airing per episode, using the server's preference.</param>
    /// <param name="nextOnly">Only return the next airing at or after the start of the range, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. The default range counts from it.</param>
    /// <returns>The airings in the time-frame, in airing order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpGet("Airing")]
    public ActionResult<List<EpisodeAiringDto>> GetAirings(
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? from = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? to = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.Only,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool includeDelayedOriginalSlots = true,
        [FromQuery] bool includeDateOnly = false,
        [FromQuery] bool preferredOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        if (!TryGetRange(from, to, nextOnly, at, out var start, out var end))
            return ValidationProblem(ModelState);

        var options = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : [AiringKind.Original],
            Languages = language is { Count: > 0 } ? language : null,
            ChannelIDs = channel is { Count: > 0 } ? channel : null,
            EpisodeTypes = type is { Count: > 0 } ? type : null,
            EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : null,
            InCollection = inCollection.InclusionFilter,
            IncludeRestricted = includeRestricted.InclusionFilter,
            Filter = filter,
            User = HttpContext.GetUser(),
            IncludeEstimates = includeEstimates,
            IncludeDelayedOriginalSlots = includeDelayedOriginalSlots,
            IncludeDateOnly = includeDateOnly,
            PreferredOnly = preferredOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            EntityAnchor = entityAnchor,
            At = at?.UtcDateTime,
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

    #region Calendar

    /// <summary>
    /// Get the episode airings in the given time-frame by local day, each
    /// day's airings of one episode grouped under a lead, for a calendar.
    /// </summary>
    /// <remarks>
    /// The same read as <c>GET /api/v3/AiringSchedule/Airing</c>, placed on the
    /// days of <paramref name="timeZone"/>. A timed airing goes at its slot
    /// when that is in the range, else at the slot it was moved out of; a
    /// date-only entry on its AniDB air date. Each day lists the date-only
    /// entries first, then the rest by time, one entry per episode: the lead
    /// is the preferred airing, else the first timed one, and the others
    /// follow in the day's order. Days with nothing on them are left out.
    /// </remarks>
    /// <param name="from">Start of the range, with an offset. Defaults to the start of today in <paramref name="timeZone"/>.</param>
    /// <param name="to">End of the range, exclusive, with an offset. Defaults to a week after the start.</param>
    /// <param name="timeZone">The time zone the days are in, as an IANA or Windows ID, or a fixed <c>±HH:MM</c> offset. Defaults to UTC.</param>
    /// <param name="everyChannel">Keep each episode's other airings, or only its lead.</param>
    /// <param name="kind">Only include airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="language">Only include airings whose schedule has a track in one of these languages.</param>
    /// <param name="channel">Only include airings on one of these channels, hidden or not. Without it, the hidden channels are left out.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="type">Only include airings of episodes of these episode types.</param>
    /// <param name="episodeKind">Only include airings of these kinds of showing. Leave out <c>Rerun</c> and <c>DetectedRerun</c> for no reruns.</param>
    /// <param name="inCollection">Filter on whether the series is in the collection, which means it has a shoko series. Defaults to only those in it.</param>
    /// <param name="includeRestricted">Include airings of restricted (H) series.</param>
    /// <param name="filterID">Only include airings of the Shoko series passing this stored filter, for the current user. Series not in the collection are left out.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedules' own lines.</param>
    /// <param name="includeDelayedOriginalSlots">Also match a delayed airing by the slot it was moved out of.</param>
    /// <param name="includeDateOnly">Include a date-only entry for each AniDB episode with an air date in the range and no airing at all.</param>
    /// <param name="preferredOnly">Only return one airing per episode, using the server's preference.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. The default range counts from it.</param>
    /// <returns>The days with airings, in order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpGet("Calendar")]
    public ActionResult<List<AiringCalendarDayDto>> GetCalendar(
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? from = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? to = null,
        [FromQuery] string? timeZone = null,
        [FromQuery] bool everyChannel = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.Only,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool includeDelayedOriginalSlots = true,
        [FromQuery] bool includeDateOnly = true,
        [FromQuery] bool preferredOnly = false,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        return ReadCalendar(
            from,
            to,
            at,
            timeZone,
            everyChannel,
            new()
            {
                ProviderIDs = provider is { Count: > 0 } ? provider : null,
                Kinds = kind is { Count: > 0 } ? kind : [AiringKind.Original],
                Languages = language is { Count: > 0 } ? language : null,
                ChannelIDs = channel is { Count: > 0 } ? channel : null,
                EpisodeTypes = type is { Count: > 0 } ? type : null,
                EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : null,
                InCollection = inCollection.InclusionFilter,
                IncludeRestricted = includeRestricted.InclusionFilter,
                Filter = filter,
                User = HttpContext.GetUser(),
                IncludeEstimates = includeEstimates,
                IncludeDelayedOriginalSlots = includeDelayedOriginalSlots,
                IncludeDateOnly = includeDateOnly,
                PreferredOnly = preferredOnly,
                EntityAnchor = entityAnchor,
                At = at?.UtcDateTime,
            },
            include
        );
    }

    /// <summary>
    /// Get the days of <c>GET /api/v3/AiringSchedule/Calendar</c> for only the
    /// Shoko series passing the filter sent in the body.
    /// </summary>
    /// <remarks>
    /// The filter is evaluated for the current user, and series not in the
    /// collection are left out. The days stay in time order.
    /// </remarks>
    /// <param name="body">The filter, as <c>POST /api/v3/Filter/Preview/Series</c> takes it.</param>
    /// <param name="from">Start of the range, with an offset. Defaults to the start of today in <paramref name="timeZone"/>.</param>
    /// <param name="to">End of the range, exclusive, with an offset. Defaults to a week after the start.</param>
    /// <param name="timeZone">The time zone the days are in, as an IANA or Windows ID, or a fixed <c>±HH:MM</c> offset. Defaults to UTC.</param>
    /// <param name="everyChannel">Keep each episode's other airings, or only its lead.</param>
    /// <param name="kind">Only include airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="language">Only include airings whose schedule has a track in one of these languages.</param>
    /// <param name="channel">Only include airings on one of these channels, hidden or not. Without it, the hidden channels are left out.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="type">Only include airings of episodes of these episode types.</param>
    /// <param name="episodeKind">Only include airings of these kinds of showing. Leave out <c>Rerun</c> and <c>DetectedRerun</c> for no reruns.</param>
    /// <param name="inCollection">Filter on whether the series is in the collection, which means it has a shoko series. Defaults to only those in it.</param>
    /// <param name="includeRestricted">Include airings of restricted (H) series.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedules' own lines.</param>
    /// <param name="includeDelayedOriginalSlots">Also match a delayed airing by the slot it was moved out of.</param>
    /// <param name="includeDateOnly">Include a date-only entry for each AniDB episode with an air date in the range and no airing at all.</param>
    /// <param name="preferredOnly">Only return one airing per episode, using the server's preference.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. The default range counts from it.</param>
    /// <returns>The days with airings, in order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("Calendar")]
    public ActionResult<List<AiringCalendarDayDto>> GetCalendarWithFilter(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] FilterBody body,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? from = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? to = null,
        [FromQuery] string? timeZone = null,
        [FromQuery] bool everyChannel = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.Only,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool includeDelayedOriginalSlots = true,
        [FromQuery] bool includeDateOnly = true,
        [FromQuery] bool preferredOnly = false,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (GetBodyFilter(body) is not { } filter)
            return ValidationProblem(ModelState);

        return ReadCalendar(
            from,
            to,
            at,
            timeZone,
            everyChannel,
            new()
            {
                ProviderIDs = provider is { Count: > 0 } ? provider : null,
                Kinds = kind is { Count: > 0 } ? kind : [AiringKind.Original],
                Languages = language is { Count: > 0 } ? language : null,
                ChannelIDs = channel is { Count: > 0 } ? channel : null,
                EpisodeTypes = type is { Count: > 0 } ? type : null,
                EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : null,
                InCollection = inCollection.InclusionFilter,
                IncludeRestricted = includeRestricted.InclusionFilter,
                Filter = filter,
                User = HttpContext.GetUser(),
                IncludeEstimates = includeEstimates,
                IncludeDelayedOriginalSlots = includeDelayedOriginalSlots,
                IncludeDateOnly = includeDateOnly,
                PreferredOnly = preferredOnly,
                EntityAnchor = entityAnchor,
                At = at?.UtcDateTime,
            },
            include
        );
    }

    /// <summary>
    /// Reads the calendar days of a range for the calendar routes.
    /// </summary>
    /// <param name="from">Start of the range, or <c>null</c> for the start of today in the zone.</param>
    /// <param name="to">End of the range, exclusive, or <c>null</c> for a week after the start.</param>
    /// <param name="at">The time the read is as of, or <c>null</c> for now.</param>
    /// <param name="timeZone">The time zone the days are in, or <c>null</c> for UTC.</param>
    /// <param name="everyChannel">Whether to keep each episode's other airings.</param>
    /// <param name="options">The airing filters.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <returns>The days with airings, in order.</returns>
    private ActionResult<List<AiringCalendarDayDto>> ReadCalendar(
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset? at,
        string? timeZone,
        bool everyChannel,
        EpisodeAiringFilteringOptions options,
        IReadOnlySet<AiringDataToInclude>? include
    )
    {
        var zone = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            if (!airingScheduleService.TryGetTimeZone(timeZone, out var requestedZone))
            {
                ModelState.AddModelError(nameof(timeZone), $"Unknown time zone: {timeZone}");
                return ValidationProblem(ModelState);
            }

            zone = requestedZone;
        }

        var now = TimeZoneInfo.ConvertTime(at ?? DateTimeOffset.UtcNow, zone);
        var start = from ?? new DateTimeOffset(now.Date, zone.GetUtcOffset(now.Date));
        var end = to ?? AddClamped(start, TimeSpan.FromDays(7));
        if (end < start)
        {
            ModelState.AddModelError(nameof(to), "The end of the range is before its start.");
            return ValidationProblem(ModelState);
        }

        var context = new AiringReadCache(this);
        return airingCalendarService.GetCalendarDays(start, end, zone, options, everyChannel)
            .Select(day => new AiringCalendarDayDto
            {
                Date = day.Date,
                Episodes =
                [
                    .. day.Episodes.Select(episode => new AiringCalendarDayDto.Episode
                    {
                        Lead = ToCalendarEntry(episode.Lead, context, include),
                        Others = [.. episode.Others.Select(entry => ToCalendarEntry(entry, context, include))],
                    }),
                ],
            })
            .ToList();
    }

    /// <summary>
    /// The wire model for an airing placed on a calendar day.
    /// </summary>
    /// <param name="entry">The placed airing.</param>
    /// <param name="context">The read's resolution cache.</param>
    /// <param name="include">The display data to resolve.</param>
    /// <returns>The entry.</returns>
    private static AiringCalendarDayDto.Entry ToCalendarEntry(
        AiringCalendarEntry entry,
        AiringReadCache context,
        IReadOnlySet<AiringDataToInclude>? include
    )
        => new()
        {
            Airing = context.ToDto(entry.Airing, include),
            Time = entry.Time,
            IsAllDay = entry.IsAllDay,
            MovedFrom = entry.MovedFrom,
            MovedTo = entry.MovedTo,
        };

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
    /// <param name="nextOnly">Only return the next airing from now, or <paramref name="at"/>, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. <paramref name="nextOnly"/> counts from it.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
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
            At = at?.UtcDateTime,
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
    /// the same channel. Without a country, a channel with no country answers
    /// first, then one in any country. With a country and no match in it, the
    /// one channel without a country that matches answers.
    /// </remarks>
    /// <param name="name">The name or alias to look up.</param>
    /// <param name="type">Optional. Only look among channels of this type. Every type is searched when omitted.</param>
    /// <param name="countryCode">Optional. Only look among channels of this country, as an ISO 3166-1 alpha-2 code.</param>
    /// <param name="useAliases">Whether a channel's aliases count as its names.</param>
    /// <returns>The channel.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpGet("Channel/ByName")]
    public ActionResult<AiringChannel> GetChannelByName(
        [FromQuery, Required] string name,
        [FromQuery] AiringChannelType? type = null,
        [FromQuery] string? countryCode = null,
        [FromQuery] bool useAliases = true
    )
    {
        string? country;
        try
        {
            country = IAiringScheduleService.NormalizeCountryCode(countryCode);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(countryCode), ex.Message);
            return ValidationProblem(ModelState);
        }

        var types = type.HasValue ? new[] { type.Value } : Enum.GetValues<AiringChannelType>();
        foreach (var channelType in types)
            if (airingScheduleService.GetChannelByName(name, channelType, country, useAliases) is { } channel)
                return new AiringChannel(channel);

        if (country is null && FindChannelInAnyCountry(name, types, useAliases) is { } regional)
            return new AiringChannel(regional);

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
    /// <param name="episodeKind">Only include airings of these kinds of showing. Leave out <c>Rerun</c> and <c>DetectedRerun</c> for no reruns.</param>
    /// <param name="inCollection">Filter on whether the series is in the collection, which means it has a shoko series. Defaults to only those in it.</param>
    /// <param name="includeRestricted">Include airings of restricted (H) series.</param>
    /// <param name="filterID">Only include airings of the Shoko series passing this stored filter, for the current user. Series not in the collection are left out.</param>
    /// <param name="includeEstimates">Include the airings estimated from the schedules' own lines.</param>
    /// <param name="includeDelayedOriginalSlots">Also match a delayed airing by the slot it was moved out of.</param>
    /// <param name="preferredOnly">Only return one airing per episode, using the server's preference.</param>
    /// <param name="nextOnly">Only return the next airing at or after the start of the range, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. The default range counts from it.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.Only,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery] bool includeDelayedOriginalSlots = true,
        [FromQuery] bool preferredOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (airingScheduleService.GetChannelByID(channelID) is null)
            return NotFound(ChannelNotFoundWithChannelID);

        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        if (!TryGetRange(from, to, nextOnly, at, out var start, out var end))
            return ValidationProblem(ModelState);

        var options = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : null,
            ChannelIDs = new HashSet<Guid> { channelID },
            EpisodeTypes = type is { Count: > 0 } ? type : null,
            EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : null,
            InCollection = inCollection.InclusionFilter,
            IncludeRestricted = includeRestricted.InclusionFilter,
            Filter = filter,
            User = HttpContext.GetUser(),
            IncludeEstimates = includeEstimates,
            IncludeDelayedOriginalSlots = includeDelayedOriginalSlots,
            PreferredOnly = preferredOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            EntityAnchor = entityAnchor,
            At = at?.UtcDateTime,
        };
        var context = new AiringReadCache(this);
        return airingScheduleService.GetAiringsInRange(start, end, options)
            .Select(airing => context.ToDto(airing, include))
            .ToList();
    }

    /// <summary>
    /// Merge other channels into a channel.
    /// </summary>
    /// <remarks>
    /// The merged channels' schedules move to this one without changing any
    /// schedule or airing ID, their names and aliases become its aliases, and
    /// they are deleted. In the preferred channels this one takes the best
    /// position any of them had, and it keeps its own hidden state and
    /// country. A TV station without a country takes the one every merged
    /// channel with a country agrees on, and with it a new ID. A provider
    /// naming a merged channel later is handed this one.
    /// </remarks>
    /// <param name="channelID">The ID of the channel to keep.</param>
    /// <param name="body">The channels to merge into it, all of its type.</param>
    /// <returns>The merged channel.</returns>
    [Authorize(Roles = "admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPost("Channel/{channelID:guid}/Merge")]
    public ActionResult<AiringChannel> MergeChannels(
        [FromRoute] Guid channelID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MergeChannelsBody body
    )
    {
        if (airingScheduleService.GetChannelByID(channelID) is not { } target)
            return NotFound(ChannelNotFoundWithChannelID);

        var sources = new List<IAiringChannel>();
        foreach (var sourceID in body.SourceIDs.Distinct())
        {
            if (airingScheduleService.GetChannelByID(sourceID) is not { } source)
                ModelState.AddModelError(nameof(body.SourceIDs), $"Unknown channel: {sourceID}");
            else if (source.ChannelID == target.ChannelID)
                ModelState.AddModelError(nameof(body.SourceIDs), "A channel can't be merged into itself.");
            else if (source.Type != target.Type)
                ModelState.AddModelError(nameof(body.SourceIDs), $"The channel {sourceID} is a {source.Type} channel, not a {target.Type} channel.");
            else
                sources.Add(source);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return new AiringChannel(airingScheduleService.MergeChannels(target, sources));
    }

    /// <summary>
    /// Replace a channel's aliases.
    /// </summary>
    /// <remarks>
    /// An alias equal to the channel's own name, or a repeat, is dropped. An
    /// alias another channel of the same type and country already answers to
    /// is rejected, and nothing is changed.
    /// </remarks>
    /// <param name="channelID">The ID of the channel.</param>
    /// <param name="body">The full list of aliases. An empty list removes them all.</param>
    /// <returns>The channel.</returns>
    [Authorize(Roles = "admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("Channel/{channelID:guid}/Aliases")]
    public ActionResult<AiringChannel> SetChannelAliases(
        [FromRoute] Guid channelID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] List<string> body
    )
    {
        if (airingScheduleService.GetChannelByID(channelID) is not { } channel)
            return NotFound(ChannelNotFoundWithChannelID);

        try
        {
            return new AiringChannel(airingScheduleService.SetChannelAliases(channel, body));
        }
        catch (ChannelAliasConflictException ex)
        {
            ModelState.AddModelError(nameof(body), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Get the IDs of the channels the server hides.
    /// </summary>
    /// <remarks>
    /// An airing read naming no channels leaves the airings of a hidden
    /// channel out.
    /// </remarks>
    /// <returns>The hidden channels.</returns>
    [HttpGet("Channel/Hidden")]
    public ActionResult<List<Guid>> GetHiddenChannels()
        => airingScheduleService.HiddenChannelIDs.Order().ToList();

    /// <summary>
    /// Set the channels the server hides, showing every other one.
    /// </summary>
    /// <param name="body">The IDs of the channels to hide. An empty list shows them all.</param>
    /// <returns>The hidden channels.</returns>
    [Authorize(Roles = "admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPut("Channel/Hidden")]
    public ActionResult<List<Guid>> SetHiddenChannels([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] List<Guid> body)
    {
        var hidden = body.ToHashSet();
        var unknownChannels = hidden.Where(channelID => airingScheduleService.GetChannelByID(channelID) is null).ToList();
        if (unknownChannels.Count > 0)
        {
            ModelState.AddModelError(nameof(body), $"Unknown channels; {string.Join(", ", unknownChannels)}");
            return ValidationProblem(ModelState);
        }

        foreach (var channel in airingScheduleService.GetAllChannels())
            airingScheduleService.SetChannelHidden(channel, hidden.Contains(channel.ChannelID));
        return GetHiddenChannels();
    }

    /// <summary>
    /// Finds a channel in any country that answers to a name, own names
    /// before aliases.
    /// </summary>
    /// <param name="name">The name or alias to look up.</param>
    /// <param name="types">The types to look among.</param>
    /// <param name="useAliases">Whether a channel's aliases count as its names.</param>
    /// <returns>The channel, or <c>null</c> when none answers.</returns>
    private IAiringChannel? FindChannelInAnyCountry(string name, IReadOnlyList<AiringChannelType> types, bool useAliases)
    {
        var normalizedName = IAiringScheduleService.NormalizeChannelName(name);
        if (normalizedName.Length is 0)
            return null;

        var channels = airingScheduleService.GetAllChannels()
            .Where(channel => types.Contains(channel.Type))
            .ToList();
        return channels.FirstOrDefault(channel => IAiringScheduleService.NormalizeChannelName(channel.Name) == normalizedName)
            ?? (useAliases
                ? channels.FirstOrDefault(channel => channel.Aliases.Any(alias => IAiringScheduleService.NormalizeChannelName(alias) == normalizedName))
                : null);
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

    #region Seasons

    /// <summary>
    /// Get every yearly season the cached AniDB anime available to the current
    /// user are in, with how many anime are in each, newest first. Upcoming
    /// seasons stop at the one after the season under way, which is always
    /// listed, and flagged.
    /// </summary>
    /// <remarks>
    /// An anime is in a season by the rule of <c>/api/v3/AiringSchedule/Season/{year}/{season}</c>.
    /// With <paramref name="channel"/>, a season only counts the anime with a
    /// stored airing on those channels in it, or, for the season under way and
    /// the next, one still to come, and its images are picked among them. Old
    /// seasons may have no airings left, so they count fewer anime or none.
    /// </remarks>
    /// <param name="type">Only count anime of these types, comma-separated.</param>
    /// <param name="channel">Only count anime airing on one of these channels, hidden or not, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to count the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to count restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="filterID">Only count the anime of the Shoko series passing this stored filter, for the current user. Anime not in the collection are left out.</param>
    /// <param name="fromYear">Optional. Leave out the seasons of earlier years. The current season is always listed.</param>
    /// <param name="include">The extra details to add, comma-separated.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. It decides the season under way.</param>
    /// <returns>The seasons.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Season")]
    public ActionResult<List<AiringSeason>> GetSeasons(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery, Range(1, 9999)] int? fromYear = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        return ListSeasons(GetSeasonListOptions(type, channel, inCollection, includeRestricted, filter, at), fromYear, include);
    }

    /// <summary>
    /// Get the seasons of <c>GET /api/v3/AiringSchedule/Season</c>, counting
    /// only the anime of the Shoko series passing the filter sent in the body.
    /// </summary>
    /// <remarks>
    /// The filter is evaluated once for the current user, and anime not in
    /// the collection are left out.
    /// </remarks>
    /// <param name="body">The filter, as <c>POST /api/v3/Filter/Preview/Series</c> takes it.</param>
    /// <param name="type">Only count anime of these types, comma-separated.</param>
    /// <param name="channel">Only count anime airing on one of these channels, hidden or not, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to count the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to count restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="fromYear">Optional. Leave out the seasons of earlier years. The current season is always listed.</param>
    /// <param name="include">The extra details to add, comma-separated.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. It decides the season under way.</param>
    /// <returns>The seasons.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("Season")]
    public ActionResult<List<AiringSeason>> GetSeasonsWithFilter(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] FilterBody body,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, 9999)] int? fromYear = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (GetBodyFilter(body) is not { } filter)
            return ValidationProblem(ModelState);

        return ListSeasons(GetSeasonListOptions(type, channel, inCollection, includeRestricted, filter, at), fromYear, include);
    }

    /// <summary>
    /// Get the seasons of <c>/api/v3/AiringSchedule/Season</c> grouped by
    /// year, newest first, each year's seasons from winter to fall.
    /// </summary>
    /// <remarks>
    /// A year whose seasons hold no anime at all is left out, the current one
    /// included; a year's empty seasons are kept.
    /// </remarks>
    /// <param name="type">Only count anime of these types, comma-separated.</param>
    /// <param name="channel">Only count anime airing on one of these channels, hidden or not, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to count the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to count restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="filterID">Only count the anime of the Shoko series passing this stored filter, for the current user. Anime not in the collection are left out.</param>
    /// <param name="fromYear">Optional. Leave out the earlier years. The year of the current season is always listed when it has anime.</param>
    /// <param name="include">The extra details to add, comma-separated.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. It decides the season under way.</param>
    /// <returns>The years.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Season/ByYear")]
    public ActionResult<List<AiringSeasonYear>> GetSeasonsByYear(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery, Range(1, 9999)] int? fromYear = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        return ListSeasonsByYear(GetSeasonListOptions(type, channel, inCollection, includeRestricted, filter, at), fromYear, include);
    }

    /// <summary>
    /// Get the years of <c>GET /api/v3/AiringSchedule/Season/ByYear</c>,
    /// counting only the anime of the Shoko series passing the filter sent in
    /// the body.
    /// </summary>
    /// <remarks>
    /// The filter is evaluated once for the current user, and anime not in
    /// the collection are left out.
    /// </remarks>
    /// <param name="body">The filter, as <c>POST /api/v3/Filter/Preview/Series</c> takes it.</param>
    /// <param name="type">Only count anime of these types, comma-separated.</param>
    /// <param name="channel">Only count anime airing on one of these channels, hidden or not, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to count the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to count restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="fromYear">Optional. Leave out the earlier years. The year of the current season is always listed when it has anime.</param>
    /// <param name="include">The extra details to add, comma-separated.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. It decides the season under way.</param>
    /// <returns>The years.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("Season/ByYear")]
    public ActionResult<List<AiringSeasonYear>> GetSeasonsByYearWithFilter(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] FilterBody body,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, 9999)] int? fromYear = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringSeason.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (GetBodyFilter(body) is not { } filter)
            return ValidationProblem(ModelState);

        return ListSeasonsByYear(GetSeasonListOptions(type, channel, inCollection, includeRestricted, filter, at), fromYear, include);
    }

    /// <summary>
    /// Get the cached AniDB anime of a yearly season available to the current
    /// user, in the collection or not, each with what a season view card
    /// shows and its next airing.
    /// </summary>
    /// <remarks>
    /// An anime starts in the season of its first regular episode, where
    /// seasons are whole weeks starting with the week of their first day less
    /// a lead-in of four weeks for TV and one for the rest; a lone premiere up
    /// to two weeks before a season with the run going on in it starts there,
    /// and five or more episodes released together with nothing for four
    /// weeks take no lead-in. After that, it is in every calendar quarter
    /// holding one of its dated regular episodes, up to the fourth from the
    /// end once it has an end date, and without dated regular episodes in its
    /// start season alone. A season after the one
    /// following the season under way has no anime. The next airings are read
    /// through each anime's series, or the anime itself outside the
    /// collection, and count from now, or <paramref name="at"/>. With <paramref name="channel"/>, only
    /// the anime with a stored airing on those channels in the season, or one
    /// still to come, are listed, and their airings come from those channels
    /// alone. Old seasons may have no airings left, so they list fewer anime
    /// or none.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season: <c>Winter</c>, <c>Spring</c>, <c>Summer</c> or <c>Fall</c>, in any case.</param>
    /// <param name="type">Only anime of these types, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to include the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to include restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="filterID">
    ///   Only the anime of the Shoko series passing this stored filter, for the current user. Anime not in the collection are left out, and
    ///   with a sorting expression the anime come in the filter's order.
    /// </param>
    /// <param name="kind">Only take airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="channel">
    ///   Only list anime airing on one of these channels, hidden or not, and only take their airings there. Without it, the hidden channels are
    ///   left out.
    /// </param>
    /// <param name="provider">Only take airings from one of these airing schedule providers.</param>
    /// <param name="episodeKind">Only take airings of these kinds of showing. Defaults to <c>Normal</c> and <c>Advance</c>, leaving out reruns.</param>
    /// <param name="includeEstimates">Take the airings estimated from the schedules' own lines.</param>
    /// <param name="at">
    ///   The time to read as of, with an offset, instead of now. It decides the season under way, the next airings and whether an anime has
    ///   finished.
    /// </param>
    /// <returns>The anime, by air date.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Season/{year}/{season}")]
    public ActionResult<List<SeasonAnime>> GetSeasonAnime(
        [FromRoute, Range(1, 9999)] int year,
        [FromRoute] YearlySeason season,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        var (animeOptions, airingOptions) = GetSeasonOptions(
            type,
            inCollection,
            includeRestricted,
            filter,
            kind,
            channel,
            provider,
            episodeKind,
            includeEstimates,
            at
        );
        return seasonAnimeBuilder.Build(airingCalendarService.GetSeasonAnime(year, season, animeOptions, airingOptions));
    }

    /// <summary>
    /// Get the anime of <c>GET /api/v3/AiringSchedule/Season/{year}/{season}</c>
    /// for only the Shoko series passing the filter sent in the body.
    /// </summary>
    /// <remarks>
    /// The filter is evaluated once for the current user, and anime not in
    /// the collection are left out. With a sorting expression, the anime come
    /// in the filter's order.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season: <c>Winter</c>, <c>Spring</c>, <c>Summer</c> or <c>Fall</c>, in any case.</param>
    /// <param name="body">The filter, as <c>POST /api/v3/Filter/Preview/Series</c> takes it.</param>
    /// <param name="type">Only anime of these types, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to include the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to include restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="kind">Only take airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="channel">
    ///   Only list anime airing on one of these channels, hidden or not, and only take their airings there. Without it, the hidden channels are
    ///   left out.
    /// </param>
    /// <param name="provider">Only take airings from one of these airing schedule providers.</param>
    /// <param name="episodeKind">Only take airings of these kinds of showing. Defaults to <c>Normal</c> and <c>Advance</c>, leaving out reruns.</param>
    /// <param name="includeEstimates">Take the airings estimated from the schedules' own lines.</param>
    /// <param name="at">
    ///   The time to read as of, with an offset, instead of now. It decides the season under way, the next airings and whether an anime has
    ///   finished.
    /// </param>
    /// <returns>The anime, by air date or in the filter's order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("Season/{year}/{season}")]
    public ActionResult<List<SeasonAnime>> GetSeasonAnimeWithFilter(
        [FromRoute, Range(1, 9999)] int year,
        [FromRoute] YearlySeason season,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] FilterBody body,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (GetBodyFilter(body) is not { } filter)
            return ValidationProblem(ModelState);

        var (animeOptions, airingOptions) = GetSeasonOptions(
            type,
            inCollection,
            includeRestricted,
            filter,
            kind,
            channel,
            provider,
            episodeKind,
            includeEstimates,
            at
        );
        return seasonAnimeBuilder.Build(airingCalendarService.GetSeasonAnime(year, season, animeOptions, airingOptions));
    }

    /// <summary>
    /// Get the anime of <c>/api/v3/AiringSchedule/Season/{year}/{season}</c>
    /// in the default sections, each sorted by next airing.
    /// </summary>
    /// <remarks>
    /// The default layout is new full-length TV and web series (<c>new</c>),
    /// new half-length ones (<c>new-half</c>), continuing ones
    /// (<c>continuing</c>), movies (<c>movies</c>) and the rest
    /// (<c>other</c>). Each anime goes to the first section that takes it, and
    /// empty sections are left out.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season: <c>Winter</c>, <c>Spring</c>, <c>Summer</c> or <c>Fall</c>, in any case.</param>
    /// <param name="type">Only anime of these types, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to include the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to include restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="filterID">
    ///   Only the anime of the Shoko series passing this stored filter, for the current user. Anime not in the collection are left out, and
    ///   with a sorting expression each section keeps the filter's order.
    /// </param>
    /// <param name="kind">Only take airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="channel">
    ///   Only list anime airing on one of these channels, hidden or not, and only take their airings there. Without it, the hidden channels are
    ///   left out.
    /// </param>
    /// <param name="provider">Only take airings from one of these airing schedule providers.</param>
    /// <param name="episodeKind">Only take airings of these kinds of showing. Defaults to <c>Normal</c> and <c>Advance</c>, leaving out reruns.</param>
    /// <param name="includeEstimates">Take the airings estimated from the schedules' own lines.</param>
    /// <param name="at">
    ///   The time to read as of, with an offset, instead of now. It decides the season under way, the next airings and whether an anime has
    ///   finished.
    /// </param>
    /// <returns>The non-empty sections, in order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("Season/{year}/{season}/Sections")]
    public ActionResult<List<SeasonSectionDto>> GetSeasonSections(
        [FromRoute, Range(1, 9999)] int year,
        [FromRoute] YearlySeason season,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        if (!TryGetStoredFilter(filterID, out var filter))
            return NotFound(FilterController.FilterNotFound);

        var (animeOptions, airingOptions) = GetSeasonOptions(
            type,
            inCollection,
            includeRestricted,
            filter,
            kind,
            channel,
            provider,
            episodeKind,
            includeEstimates,
            at
        );
        return BuildSections(airingCalendarService.GetSeasonSections(year, season, null, animeOptions, airingOptions));
    }

    /// <summary>
    /// Get the anime of <c>/api/v3/AiringSchedule/Season/{year}/{season}</c>
    /// in a custom layout's sections, or the default ones, each sorted by next
    /// airing, optionally for only the Shoko series passing a filter.
    /// </summary>
    /// <remarks>
    /// Each anime goes to the first section that takes it, an anime no
    /// section takes is left out, and empty sections are left out. A section
    /// without <c>Types</c> takes every type, so it is the rest group. The
    /// filter, sent in the body or stored, is evaluated once for the current
    /// user; anime not in the collection are left out, and with a sorting
    /// expression each section keeps the filter's order.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season: <c>Winter</c>, <c>Spring</c>, <c>Summer</c> or <c>Fall</c>, in any case.</param>
    /// <param name="body">The layout and the filter, each optional.</param>
    /// <param name="type">Only anime of these types, comma-separated.</param>
    /// <param name="inCollection">
    ///   Whether to include the anime with a Shoko series: <c>true</c> for every anime, <c>only</c> for those with one, <c>false</c> for those
    ///   without.
    /// </param>
    /// <param name="includeRestricted">Whether to include restricted anime. The user's own restrictions apply on top.</param>
    /// <param name="filterID">Only the anime of the Shoko series passing this stored filter. Not together with a filter in the body.</param>
    /// <param name="kind">Only take airings whose schedule has a track of these kinds. Defaults to <see cref="AiringKind.Original"/>.</param>
    /// <param name="channel">
    ///   Only list anime airing on one of these channels, hidden or not, and only take their airings there. Without it, the hidden channels are
    ///   left out.
    /// </param>
    /// <param name="provider">Only take airings from one of these airing schedule providers.</param>
    /// <param name="episodeKind">Only take airings of these kinds of showing. Defaults to <c>Normal</c> and <c>Advance</c>, leaving out reruns.</param>
    /// <param name="includeEstimates">Take the airings estimated from the schedules' own lines.</param>
    /// <param name="at">
    ///   The time to read as of, with an offset, instead of now. It decides the season under way, the next airings and whether an anime has
    ///   finished.
    /// </param>
    /// <returns>The non-empty sections, in the layout's order.</returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPost("Season/{year}/{season}/Sections")]
    public ActionResult<List<SeasonSectionDto>> GetSeasonSectionsWithLayout(
        [FromRoute, Range(1, 9999)] int year,
        [FromRoute] YearlySeason season,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SeasonSectionsBody body,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery] IncludeOnlyFilter inCollection = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery, Range(1, int.MaxValue)] int? filterID = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringKind>? kind = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? channel = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<Guid>? provider = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] bool includeEstimates = true,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
    )
    {
        var duplicates = (body.Sections ?? [])
            .GroupBy(section => section.ID, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
            ModelState.AddModelError(nameof(body.Sections), $"Section IDs must be unique; {string.Join(", ", duplicates)}");

        if (body.Filter is not null && filterID is not null)
            ModelState.AddModelError(nameof(filterID), "Send either a filter in the body or a filterID, not both.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        IFilter? filter;
        if (body.Filter is { } filterBody)
        {
            if (GetBodyFilter(filterBody) is not { } bodyFilter)
                return ValidationProblem(ModelState);

            filter = bodyFilter;
        }
        else if (!TryGetStoredFilter(filterID, out filter))
        {
            return NotFound(FilterController.FilterNotFound);
        }

        var (animeOptions, airingOptions) = GetSeasonOptions(
            type,
            inCollection,
            includeRestricted,
            filter,
            kind,
            channel,
            provider,
            episodeKind,
            includeEstimates,
            at
        );
        var sections = airingCalendarService.GetSeasonSections(
            year,
            season,
            body.Sections is { } layout ? [.. layout.Select(section => section.ToDefinition())] : null,
            animeOptions,
            airingOptions
        );
        return BuildSections(sections);
    }

    /// <summary>
    /// The seasons the season list routes answer with.
    /// </summary>
    /// <param name="options">The anime filters.</param>
    /// <param name="fromYear">The earliest year to keep, the current season aside.</param>
    /// <param name="include">The extra details to add.</param>
    /// <returns>The seasons.</returns>
    private List<AiringSeason> ListSeasons(
        AnidbAnimeListOptions options,
        int? fromYear,
        IReadOnlySet<AiringSeason.IncludeDetails>? include
    )
        => anidbCatalog.GetSeasons(options, includeImages: include?.Contains(AiringSeason.IncludeDetails.Images) ?? false)
            .Where(season => fromYear is null || season.IsCurrent || season.Year >= fromYear)
            .Select(season => new AiringSeason(season))
            .ToList();

    /// <summary>
    /// The years the seasons-by-year routes answer with.
    /// </summary>
    /// <param name="options">The anime filters.</param>
    /// <param name="fromYear">The earliest year to keep, the current season's year aside.</param>
    /// <param name="include">The extra details to add.</param>
    /// <returns>The years.</returns>
    private List<AiringSeasonYear> ListSeasonsByYear(
        AnidbAnimeListOptions options,
        int? fromYear,
        IReadOnlySet<AiringSeason.IncludeDetails>? include
    )
        => airingCalendarService.GetSeasonsByYear(options, includeImages: include?.Contains(AiringSeason.IncludeDetails.Images) ?? false)
            .Where(year => fromYear is null || year.Year >= fromYear || year.Seasons.Any(season => season.IsCurrent))
            .Select(year => new AiringSeasonYear(year))
            .ToList();

    /// <summary>
    /// The filters a season list read takes.
    /// </summary>
    /// <param name="type">Only anime of these types.</param>
    /// <param name="channel">Only anime airing on one of these channels.</param>
    /// <param name="inCollection">Whether to keep the anime with a Shoko series.</param>
    /// <param name="includeRestricted">Whether to keep the restricted anime.</param>
    /// <param name="filter">Only the anime of the Shoko series this filter passes, if any.</param>
    /// <param name="at">The time the read is as of, or <c>null</c> for now.</param>
    /// <returns>The anime filters.</returns>
    private AnidbAnimeListOptions GetSeasonListOptions(
        HashSet<AnimeType>? type,
        HashSet<Guid>? channel,
        IncludeOnlyFilter inCollection,
        IncludeOnlyFilter includeRestricted,
        IFilter? filter,
        DateTimeOffset? at
    )
        => new()
        {
            Types = type,
            ChannelIDs = channel is { Count: > 0 } ? channel : null,
            InCollection = inCollection.InclusionFilter,
            IncludeRestricted = includeRestricted.InclusionFilter,
            Filter = filter,
            User = User,
            At = at?.UtcDateTime,
        };

    /// <summary>
    /// The filters a season's anime read takes, with the season routes'
    /// defaults: original tracks, no reruns, and date-only entries.
    /// </summary>
    /// <param name="type">Only anime of these types.</param>
    /// <param name="inCollection">Whether to keep the anime with a Shoko series.</param>
    /// <param name="includeRestricted">Whether to keep the restricted anime.</param>
    /// <param name="filter">Only the anime of the Shoko series this filter passes, if any.</param>
    /// <param name="kind">Only airings whose schedule has a track of these kinds.</param>
    /// <param name="channel">Only anime airing on one of these channels, and only their airings there.</param>
    /// <param name="provider">Only airings from one of these providers.</param>
    /// <param name="episodeKind">Only airings of these kinds of showing.</param>
    /// <param name="includeEstimates">Whether to take the estimated airings.</param>
    /// <param name="at">The time the read is as of, or <c>null</c> for now.</param>
    /// <returns>The anime and airing filters.</returns>
    private (AnidbAnimeListOptions Anime, EpisodeAiringFilteringOptions Airing) GetSeasonOptions(
        HashSet<AnimeType>? type,
        IncludeOnlyFilter inCollection,
        IncludeOnlyFilter includeRestricted,
        IFilter? filter,
        HashSet<AiringKind>? kind,
        HashSet<Guid>? channel,
        HashSet<Guid>? provider,
        HashSet<EpisodeAiringKind>? episodeKind,
        bool includeEstimates,
        DateTimeOffset? at
    )
    {
        var animeOptions = GetSeasonListOptions(type, channel, inCollection, includeRestricted, filter, at);
        var airingOptions = new EpisodeAiringFilteringOptions
        {
            ProviderIDs = provider is { Count: > 0 } ? provider : null,
            Kinds = kind is { Count: > 0 } ? kind : [AiringKind.Original],
            ChannelIDs = animeOptions.ChannelIDs,
            EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : [EpisodeAiringKind.Normal, EpisodeAiringKind.Advance],
            IncludeEstimates = includeEstimates,
            IncludeDateOnly = true,
            At = animeOptions.At,
        };
        return (animeOptions, airingOptions);
    }

    /// <summary>
    /// The wire models for a season's sections, built in one batch.
    /// </summary>
    /// <param name="sections">The sections.</param>
    /// <returns>The sections.</returns>
    private List<SeasonSectionDto> BuildSections(IReadOnlyList<Shoko.Abstractions.Metadata.Airing.SeasonSection> sections)
    {
        var models = seasonAnimeBuilder.Build([.. sections.SelectMany(section => section.Anime)]);
        var offset = 0;
        var result = new List<SeasonSectionDto>(sections.Count);
        foreach (var section in sections)
        {
            result.Add(new()
            {
                ID = section.Definition.ID,
                Title = section.Definition.Title,
                Anime = models.GetRange(offset, section.Anime.Count),
            });
            offset += section.Anime.Count;
        }

        return result;
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
    /// <param name="channel">Only include airings on one of these channels, hidden or not. Without it, the hidden channels are left out.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="episodeKind">Only include airings of these kinds of showing. Leave out <c>Rerun</c> and <c>DetectedRerun</c> for no reruns.</param>
    /// <param name="includeDateOnly">Include a date-only entry for each AniDB episode with an air date and no airing at all.</param>
    /// <param name="nextOnly">Only return the next airing from now, or <paramref name="at"/>, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="linkedEntityAirings">
    ///   Set to <c>false</c> for only the series' own airings, <c>true</c> to also walk its linked entities, or leave it out to let the server decide.
    /// </param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. <paramref name="nextOnly"/> counts from it.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] bool includeDateOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] bool? linkedEntityAirings = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
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
            EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : null,
            IncludeDateOnly = includeDateOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            LinkedEntityAirings = linkedEntityAirings,
            EntityAnchor = entityAnchor,
            At = at?.UtcDateTime,
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
    /// <param name="channel">Only include airings on one of these channels, hidden or not. Without it, the hidden channels are left out.</param>
    /// <param name="provider">Only include airings from one of these airing schedule providers.</param>
    /// <param name="episodeKind">Only include airings of these kinds of showing. Leave out <c>Rerun</c> and <c>DetectedRerun</c> for no reruns.</param>
    /// <param name="includeDateOnly">Include a date-only entry when the episode has an AniDB air date and no airing at all.</param>
    /// <param name="nextOnly">Only return the next airing from now, or <paramref name="at"/>, per <paramref name="nextPer"/>.</param>
    /// <param name="nextPer">What <paramref name="nextOnly"/> keeps one airing per. Defaults to <see cref="AiringNextGrouping.Series"/>.</param>
    /// <param name="includeDisabled">
    ///   Include airings hidden because their provider is disabled, or because none of their schedule's tracks are of an enabled kind.
    /// </param>
    /// <param name="linkedEntityAirings">
    ///   Set to <c>false</c> for only the episode's own airings, <c>true</c> to also walk its linked entities, or leave it out to let the server decide.
    /// </param>
    /// <param name="entityAnchor">Which entities the airings are anchored to. <c>Shoko</c> drops the airings that resolve to no shoko episode.</param>
    /// <param name="include">Extra display data to resolve for each airing.</param>
    /// <param name="at">The time to read as of, with an offset, instead of now. <paramref name="nextOnly"/> counts from it.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeAiringKind>? episodeKind = null,
        [FromQuery] bool includeDateOnly = false,
        [FromQuery] bool nextOnly = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringNextGrouping>? nextPer = null,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool? linkedEntityAirings = null,
        [FromQuery] AiringEntityAnchor entityAnchor = AiringEntityAnchor.Auto,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AiringDataToInclude>? include = null,
        [FromQuery, ModelBinder(typeof(DateTimeOffsetModelBinder))] DateTimeOffset? at = null
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
            EpisodeKinds = episodeKind is { Count: > 0 } ? episodeKind : null,
            IncludeDisabled = includeDisabled,
            IncludeDateOnly = includeDateOnly,
            NextOnly = nextOnly,
            NextPer = nextPer is { Count: > 0 } ? nextPer : null,
            LinkedEntityAirings = linkedEntityAirings,
            EntityAnchor = entityAnchor,
            At = at?.UtcDateTime,
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
    /// after the start. Now is the time the read is as of, when given.
    /// </summary>
    /// <param name="from">The start the caller asked for.</param>
    /// <param name="to">The end the caller asked for.</param>
    /// <param name="nextOnly">Whether the read is next-only.</param>
    /// <param name="at">The time the read is as of, or <c>null</c> for now.</param>
    /// <param name="start">The start of the range.</param>
    /// <param name="end">The exclusive end of the range.</param>
    /// <returns><c>false</c> with a model error when the end is before the start.</returns>
    private bool TryGetRange(
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool nextOnly,
        DateTimeOffset? at,
        out DateTimeOffset start,
        out DateTimeOffset end
    )
    {
        var now = (at ?? DateTimeOffset.UtcNow).ToUniversalTime();
        start = from ?? (nextOnly ? now : new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero));
        end = to ?? AddClamped(start, TimeSpan.FromDays(7));
        if (end >= start)
            return true;

        ModelState.AddModelError(nameof(to), "The end of the range is before its start.");
        return false;
    }

    /// <summary>
    /// The stored filter a read is narrowed by.
    /// </summary>
    /// <param name="filterID">The ID of the stored filter, or <c>null</c> for none.</param>
    /// <param name="filter">The filter, or <c>null</c> when none was asked for.</param>
    /// <returns><c>false</c> when no stored filter has the ID.</returns>
    private bool TryGetStoredFilter(int? filterID, out IFilter? filter)
    {
        filter = filterID is { } id ? filterPresets.GetByID(id) : null;
        return filterID is null || filter is not null;
    }

    /// <summary>
    /// The filter sent in a request's body.
    /// </summary>
    /// <param name="body">The filter's body.</param>
    /// <returns>The filter, or <c>null</c> with model errors when it is invalid.</returns>
    private IFilter? GetBodyFilter(FilterBody body)
    {
        var filter = filterFactory.GetFilterPreset(body, ModelState);
        return ModelState.IsValid ? filter : null;
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
