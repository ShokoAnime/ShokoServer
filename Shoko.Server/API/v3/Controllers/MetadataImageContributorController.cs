using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The image contributors: plugin parts that add images from a source of
/// their own to entries of other sources, and the sources and kinds each one
/// is on for.
/// </summary>
/// <remarks>
/// Reading is open to every user; turning a contributor on or off is for
/// admins, and takes effect without a restart.
/// </remarks>
/// <param name="settingsProvider">The settings.</param>
/// <param name="contributorManager">Lists and sets up the contributors.</param>
/// <param name="applicationPaths">Finds the contributors' icons on disk.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Metadata")]
[ApiV3]
[Authorize]
public class MetadataImageContributorController(
    ISettingsProvider settingsProvider,
    IMetadataImageContributorManager contributorManager,
    IApplicationPaths applicationPaths
) : BaseController(settingsProvider)
{
    #region Constants

    internal const string ContributorNotFound = "An image contributor by the given `contributorID` was not found.";

    internal const string ContributorIconNotFound = "The image contributor was not found or has no icon.";

    #endregion

    #region Contributors

    /// <summary>
    /// Get every image contributor, or the ones of one plugin.
    /// </summary>
    /// <param name="pluginID">Only the contributors of this plugin.</param>
    /// <returns>The contributors, in plugin load order.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("ImageContributor")]
    public ActionResult<List<MetadataImageContributor>> GetImageContributors([FromQuery] Guid? pluginID = null)
        => contributorManager.ImageContributors
            .Where(info => pluginID is not { } id || info.PluginInfo.ID == id)
            .Select(info => new MetadataImageContributor(info))
            .ToList();

    /// <summary>
    /// Get one image contributor.
    /// </summary>
    /// <param name="contributorID">The contributor's ID.</param>
    /// <returns>The contributor.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("ImageContributor/{contributorID:guid}")]
    public ActionResult<MetadataImageContributor> GetImageContributor([FromRoute] Guid contributorID)
        => contributorManager.GetImageContributorInfo(contributorID) is { } info ? new MetadataImageContributor(info) : NotFound(ContributorNotFound);

    /// <summary>
    /// Get an image contributor's icon.
    /// </summary>
    /// <remarks>
    /// An SVG or a PNG, sent so that an SVG opened on its own runs no script.
    /// </remarks>
    /// <param name="contributorID">The contributor's ID.</param>
    /// <returns>
    /// The icon, <c>304 Not Modified</c> when the client's copy has the same
    /// ETag, or <c>404 Not Found</c> when there is no such contributor or it
    /// has no icon.
    /// </returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("ImageContributor/{contributorID:guid}/Icon")]
    public ActionResult GetImageContributorIcon([FromRoute] Guid contributorID)
        => PackageIcon(contributorManager.GetImageContributorInfo(contributorID)?.Icon, applicationPaths, ContributorIconNotFound);

    /// <summary>
    /// Set the sources and kinds an image contributor is on for. The images
    /// it added on a pair turned off are removed in a queued job.
    /// </summary>
    /// <param name="contributorID">The contributor's ID.</param>
    /// <param name="body">The pairs to leave on.</param>
    /// <returns>The contributor as it is set up now.</returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpPut("ImageContributor/{contributorID:guid}")]
    public ActionResult<MetadataImageContributor> UpdateImageContributor(
        [FromRoute] Guid contributorID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataImageContributorUpdateBody body
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (contributorManager.GetImageContributorInfo(contributorID) is not { } info)
            return NotFound(ContributorNotFound);

        var enabled = MetadataEntityScopeEntry.ToScope(body.Enabled);
        var unavailable = enabled.Except(info.AvailableScope);
        if (!unavailable.IsEmpty)
            return ValidationProblem($"{info.Name} cannot add images for {unavailable}.", nameof(body.Enabled));

        contributorManager.SetImageContributorEnabled(contributorID, enabled);
        return GetImageContributor(contributorID);
    }

    #endregion
}
