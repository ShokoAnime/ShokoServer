using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Orderings;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Ordering;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The orderings of a Shoko series, and the ones users make for it on this
/// server.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/Series/{seriesID:int}/Ordering"), Tags("Series")]
[ApiV3]
[Authorize]
public class SeriesOrderingController(
    ISettingsProvider settingsProvider,
    AnimeSeriesRepository seriesRepository,
    IMetadataOrderingService orderingService,
    IMetadataOrderingTransferService transferService,
    MetadataModelBuilder models
) : BaseController(settingsProvider)
{
    /// <summary>
    /// The route ID of a series' default ordering.
    /// </summary>
    private const string DefaultOrderingID = "default";

    /// <summary>
    /// The route ID of the ordering chosen for a series.
    /// </summary>
    private const string PreferredOrderingID = "preferred";

    /// <summary>
    /// Get every ordering of a series: its default one first, then the ones
    /// plugins saved, then the ones users made.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="includeGroups">Include each ordering's groups and their episodes.</param>
    /// <returns>The orderings.</returns>
    [HttpGet]
    public ActionResult<IReadOnlyList<SeriesOrdering>> GetOrderings(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool includeGroups = false
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;

        return orderingService.GetOrderings(series).Select(ordering => new SeriesOrdering(ordering, includeGroups, models)).ToList();
    }

    /// <summary>
    /// Get one of a series' orderings, with its groups.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="orderingID">The ordering's full ID (URL-encoded), a user's ordering's local ID, <c>default</c> for the default ordering, or <c>preferred</c> for the one chosen for the series.</param>
    /// <returns>The ordering.</returns>
    [HttpGet("{orderingID}")]
    public ActionResult<SeriesOrdering> GetOrdering(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] string orderingID
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;

        if (string.Equals(orderingID, DefaultOrderingID, StringComparison.OrdinalIgnoreCase))
            return new SeriesOrdering(orderingService.GetDefaultOrdering(series), true, models);
        if (string.Equals(orderingID, PreferredOrderingID, StringComparison.OrdinalIgnoreCase))
            return new SeriesOrdering(orderingService.GetPreferredOrdering(series), true, models);

        if (FindOrdering(series, orderingID, false) is not { } ordering)
            return NotFound("No ordering of the series has the given orderingID.");

        return new SeriesOrdering(ordering, true, models);
    }

    /// <summary>
    /// Make a user's ordering of a series. At most one group may be special.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="body">The ordering, with its groups of Shoko episode IDs.</param>
    /// <returns>The new ordering.</returns>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<SeriesOrdering> CreateOrdering(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SeriesOrdering.Input.OrderingBody body
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;

        try
        {
            var data = ToData(series, body, out var problem);
            if (data is null)
                return ValidationProblem(problem, nameof(body.Groups));

            return new SeriesOrdering(orderingService.CreateLocalOrdering(data), true, models);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body));
        }
    }

    /// <summary>
    /// Replace a user's ordering of a series whole. A group naming one of the
    /// ordering's groups keeps its ID, and at most one group may be special.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="orderingID">The ordering's local ID, or its full ID (URL-encoded).</param>
    /// <param name="body">The ordering, with its groups of Shoko episode IDs.</param>
    /// <returns>The updated ordering.</returns>
    [Authorize("admin")]
    [HttpPut("{orderingID}")]
    public ActionResult<SeriesOrdering> UpdateOrdering(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] string orderingID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SeriesOrdering.Input.OrderingBody body
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;
        if (FindOrdering(series, orderingID, true) is not { ID: var id })
            return NotFound("No ordering of the series has the given orderingID.");

        try
        {
            var data = ToData(series, body, out var problem);
            if (data is null)
                return ValidationProblem(problem, nameof(body.Groups));

            return orderingService.UpdateLocalOrdering(id, data) is { } updated
                ? new SeriesOrdering(updated, true, models)
                : NotFound("No ordering of the series has the given orderingID.");
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body));
        }
    }

    /// <summary>
    /// Remove a user's ordering of a series.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="orderingID">The ordering's local ID, or its full ID (URL-encoded).</param>
    /// <returns>Nothing.</returns>
    [Authorize("admin")]
    [HttpDelete("{orderingID}")]
    public ActionResult DeleteOrdering(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] string orderingID
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;
        if (FindOrdering(series, orderingID, true) is not { ID: var id })
            return NotFound("No ordering of the series has the given orderingID.");

        orderingService.DeleteLocalOrdering(id);
        return Ok();
    }

    /// <summary>
    /// Choose the ordering to use for a series.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="body">The ordering to choose.</param>
    /// <returns>Nothing.</returns>
    [Authorize("admin")]
    [HttpPost("SetPreferred")]
    public ActionResult SetPreferredOrdering(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SeriesOrdering.Input.SetPreferredOrderingBody body
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;

        if (!TryParsePreferredOrderingID(body.OrderingID, out var orderingID))
            return ValidationProblem("Invalid ordering ID.", nameof(body.OrderingID));

        try
        {
            orderingService.SetPreferredOrdering(((ISeries)series).ID, orderingID);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.OrderingID));
        }
    }

    /// <summary>
    /// Export orderings of a series to a file another server can import. With
    /// no orderings named, every local ordering of the series is exported.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="orderingIDs">Orderings of the series to export, of any kind: full IDs, a user's ordering's local ID, or <c>default</c>.</param>
    /// <param name="includeGlobal">Also export the series' stored global orderings.</param>
    /// <param name="images">How to carry the images: <c>None</c>, <c>UrlOnly</c>, <c>EmbedMissingRemote</c> or <c>EmbedAll</c>.</param>
    /// <param name="container">The file: <c>Auto</c> (zip when embedding, else JSON), <c>Json</c> or <c>Zip</c>.</param>
    /// <param name="includePreferred">Record which ordering the series uses.</param>
    /// <returns>The file, <c>application/zip</c> or <c>application/json</c>.</returns>
    [Authorize("admin")]
    [HttpGet("Export")]
    [Produces("application/json", "application/zip")]
    public ActionResult ExportOrderings(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] List<string>? orderingIDs = null,
        [FromQuery] bool includeGlobal = false,
        [FromQuery] MetadataOrderingImageExportMode images = MetadataOrderingImageExportMode.UrlOnly,
        [FromQuery] MetadataOrderingContainer container = MetadataOrderingContainer.Auto,
        [FromQuery] bool includePreferred = true
    )
    {
        if (GetSeries(seriesID, out var error) is not { } series)
            return error!;

        var orderings = new List<MetadataGuid>();
        foreach (var text in orderingIDs ?? [])
        {
            var ordering = string.Equals(text, DefaultOrderingID, StringComparison.OrdinalIgnoreCase)
                ? orderingService.GetDefaultOrdering(series)
                : FindOrdering(series, text, false);
            if (ordering is null)
                return ValidationProblem($"No ordering of the series has the ID \"{text}\".", nameof(orderingIDs));
            orderings.Add(ordering.ID);
        }

        return OrderingController.Export(
            transferService,
            new()
            {
                OrderingIDs = orderings,
                SeriesIDs = orderings.Count is 0 ? [((ISeries)series).ID] : [],
                IncludeGlobalOrderings = includeGlobal,
                ImageMode = images,
                Container = container,
                IncludePreferred = includePreferred,
            }
        );
    }

    #region Helpers

    /// <summary>
    /// Finds a series the user may see.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="error">The response to give when there is none.</param>
    /// <returns>The series, or <c>null</c>.</returns>
    private AnimeSeries? GetSeries(int seriesID, out ActionResult? error)
    {
        error = null;
        if (seriesRepository.GetByID(seriesID) is not { } series)
        {
            error = NotFound(SeriesController.SeriesNotFoundWithSeriesID);
            return null;
        }

        if (!User.AllowedSeries(series))
        {
            error = Forbid(SeriesController.SeriesForbiddenForUser);
            return null;
        }

        return series;
    }

    /// <summary>
    /// Finds one of a series' orderings from a route value.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="orderingID">The route value.</param>
    /// <param name="localOnly">Only find a user's ordering, for the endpoints that change one.</param>
    /// <returns>The ordering, or <c>null</c> when the series has none by that ID.</returns>
    private IOrdering? FindOrdering(AnimeSeries series, string orderingID, bool localOnly)
    {
        if (ParseOrderingID(orderingID) is not { } id || (localOnly && id.Source != MetadataSource.User))
            return null;

        return orderingService.GetOrdering(id) is { } ordering && ordering.SeriesID == ((ISeries)series).ID ? ordering : null;
    }

    /// <summary>
    /// The full ID of an ordering from a route value: a full ID, which comes
    /// URL-encoded, or a user's ordering's local ID.
    /// </summary>
    /// <param name="orderingID">The route value.</param>
    /// <returns>The full ID, or <c>null</c> when the text is not a valid ID.</returns>
    internal static MetadataGuid? ParseOrderingID(string orderingID)
    {
        var text = Uri.UnescapeDataString(orderingID);
        return MetadataGuid.TryParse(text, null, out var id) ? id : LocalID(text);
    }

    /// <summary>
    /// The ordering a body chooses for a series: none, for its default one,
    /// when the text is empty or <c>default</c>, else a full ID or a user's
    /// ordering's local ID.
    /// </summary>
    /// <param name="text">The body's ordering ID.</param>
    /// <param name="orderingID">The ordering's full ID, or <c>null</c> for the default one.</param>
    /// <returns><see langword="false"/> when the text is not a valid ID.</returns>
    internal static bool TryParsePreferredOrderingID(string? text, out MetadataGuid? orderingID)
    {
        orderingID = null;
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, DefaultOrderingID, StringComparison.OrdinalIgnoreCase))
            return true;

        orderingID = MetadataGuid.TryParse(text, null, out var parsed) ? parsed : LocalID(text);
        return orderingID is not null;
    }

    /// <summary>
    /// The full ID of a user's ordering from its local ID.
    /// </summary>
    /// <param name="orderingID">The local ID.</param>
    /// <returns>The full ID, or <c>null</c> when the text is not a valid ID.</returns>
    private static MetadataGuid? LocalID(string orderingID)
    {
        var text = $"{MetadataSource.User.Value}://{MetadataEntityType.Ordering.Value}/{orderingID}";
        return MetadataGuid.TryParse(text, null, out var id) && id.Source == MetadataSource.User ? id : null;
    }

    /// <summary>
    /// Turns a body into what the ordering service takes. The service checks
    /// that each episode is the series'.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="body">The body.</param>
    /// <param name="problem">What is wrong with the body, when it can't be used.</param>
    /// <returns>The ordering, or <c>null</c> when a group ID is not a valid ID.</returns>
    private static MetadataLocalOrderingData? ToData(AnimeSeries series, SeriesOrdering.Input.OrderingBody body, out string problem)
    {
        problem = string.Empty;
        var groups = new List<MetadataLocalOrderingGroupData>();
        foreach (var group in body.Groups)
        {
            MetadataGuid? groupID = null;
            if (!string.IsNullOrEmpty(group.ID))
            {
                if (!MetadataGuid.TryParse(group.ID, null, out var parsed))
                {
                    problem = $"\"{group.ID}\" is not a valid group ID.";
                    return null;
                }

                groupID = parsed;
            }

            groups.Add(new()
            {
                ID = groupID,
                Name = group.Name,
                Overview = group.Description,
                IsSpecial = group.IsSpecial,
                Episodes = [.. group.EpisodeIDs.Select(episodeID => new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, episodeID.ToString()))],
            });
        }

        return new()
        {
            SeriesID = ((ISeries)series).ID,
            Name = body.Name,
            Overview = body.Description,
            Groups = groups,
        };
    }

    #endregion
}
