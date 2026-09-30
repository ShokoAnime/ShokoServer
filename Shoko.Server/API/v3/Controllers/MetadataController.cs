using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Ordering;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The entries of any source named by their full IDs, and the user state kept
/// for them.
/// </summary>
/// <param name="settingsProvider">The settings.</param>
/// <param name="orderingService">Reads and sets the hidden state of episodes.</param>
/// <param name="metadataService">Resolves entries by their full IDs.</param>
/// <param name="models">Builds the entry models.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class MetadataController(
    ISettingsProvider settingsProvider,
    IMetadataOrderingService orderingService,
    IMetadataService metadataService,
    MetadataModelBuilder models
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get any entry by its full ID, in its minimal form, with the route that
    /// serves it in full.
    /// </summary>
    /// <remarks>
    /// Resolves every kind of entry the server or a plugin knows, a plugin's
    /// own kinds included. Users and filters are never answered here, nor
    /// are AniDB and Shoko entries the user may not see.
    /// </remarks>
    /// <param name="id">The entry's full ID, e.g. <c>anilist://series/21</c>.</param>
    /// <param name="include">The extra details to include: <c>Titles</c>, <c>Overviews</c> and <c>Images</c>.</param>
    /// <returns>The entry.</returns>
    [HttpGet("Entry")]
    public ActionResult<MetadataEntry> GetEntry(
        [FromQuery, Required] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null
    )
    {
        if (!MetadataGuid.TryParse(id, null, out var guid))
            return ValidationProblem("Invalid entry ID.", nameof(id));

        if (!MetadataEntryController.IsOpenKind(guid.EntityType) || metadataService.GetEntry(guid) is not { } entry || !MetadataEntryController.MaySee(entry, () => User))
            return NotFound(MetadataEntryController.EntryNotFound);

        return models.Entry(entry, include);
    }

    /// <summary>
    /// Get whether an episode of any source is hidden.
    /// </summary>
    /// <param name="episodeID">The episode's full ID, e.g. <c>anidb://episode/1</c>.</param>
    /// <returns>The hidden state.</returns>
    [HttpGet("Episode/Hidden")]
    public ActionResult<EpisodeHiddenState> GetEpisodeHiddenState([FromQuery] string episodeID)
    {
        if (!TryGetEpisodeID(episodeID, out var id))
            return ValidationProblem("Invalid episode ID.", nameof(episodeID));

        return new EpisodeHiddenState { EpisodeID = id.ToString(), IsHidden = orderingService.IsEpisodeHidden(id) };
    }

    /// <summary>
    /// Hide or show an episode of any source. A Shoko episode's own hidden
    /// flag is set, with its series' and group's stats.
    /// </summary>
    /// <param name="body">The episode and whether to hide it.</param>
    /// <returns>The hidden state.</returns>
    [Authorize("admin")]
    [HttpPost("Episode/Hidden")]
    public ActionResult<EpisodeHiddenState> SetEpisodeHiddenState([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] EpisodeHiddenState.Input.SetHiddenBody body)
    {
        if (!TryGetEpisodeID(body.EpisodeID, out var id))
            return ValidationProblem("Invalid episode ID.", nameof(body.EpisodeID));

        try
        {
            orderingService.SetEpisodeHidden(id, body.Value);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.EpisodeID));
        }

        return new EpisodeHiddenState { EpisodeID = id.ToString(), IsHidden = body.Value };
    }

    /// <summary>
    /// Reads an episode's full ID.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="id">The episode's ID, if the text is one.</param>
    /// <returns><c>true</c> if the text names an episode.</returns>
    private static bool TryGetEpisodeID(string? text, out MetadataGuid id)
    {
        if (MetadataGuid.TryParse(text, null, out var parsed) && parsed.EntityType == MetadataEntityType.Episode)
        {
            id = parsed;
            return true;
        }

        id = null!;
        return false;
    }
}
