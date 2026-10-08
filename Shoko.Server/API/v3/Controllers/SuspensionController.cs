using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Suspension;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The suspension providers: the services plugins and the core talk to, and
/// why each cannot take work now.
/// </summary>
/// <remarks>
/// Reading is open to every user; lifting a suspension is for admins.
/// </remarks>
/// <param name="settingsProvider">The settings.</param>
/// <param name="suspensionService">Keeps the suspensions.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class SuspensionController(ISettingsProvider settingsProvider, ISuspensionService suspensionService) : BaseController(settingsProvider)
{
    #region Constants

    internal const string ProviderNotFound = "A suspension provider by the given `providerID` was not found.";

    internal const string NotLiftable = "The provider holds no liftable suspension of the given `kind`.";

    #endregion

    #region Statuses

    /// <summary>
    /// Get the status of every suspension provider, suspended or not.
    /// </summary>
    /// <returns>One status per provider, in plugin load order.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet]
    public ActionResult<List<SuspensionProviderStatus>> GetSuspensions()
        => suspensionService.GetAll().Select(status => new SuspensionProviderStatus(status)).ToList();

    /// <summary>
    /// Get the status of one suspension provider.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>The status.</returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("{providerID:guid}")]
    public ActionResult<SuspensionProviderStatus> GetSuspension([FromRoute] Guid providerID)
        => suspensionService.Get(providerID) is { } status ? new SuspensionProviderStatus(status) : NotFound(ProviderNotFound);

    /// <summary>
    /// Lift a liftable suspension before it ends, such as an AniDB ban.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="kind">The kind of the suspension.</param>
    /// <param name="cancellationToken">Cancels the lift.</param>
    /// <returns>
    /// The provider's status after the lift, <c>404 Not Found</c> for an
    /// unknown provider, or <c>400 Bad Request</c> when it holds no liftable
    /// suspension of that kind.
    /// </returns>
    [Authorize("admin")]
    [DatabaseBlockedExempt]
    [HttpPost("{providerID:guid}/{kind}/Lift")]
    public async Task<ActionResult<SuspensionProviderStatus>> LiftSuspension(
        [FromRoute] Guid providerID,
        [FromRoute] SuspensionKind kind,
        CancellationToken cancellationToken = default
    )
    {
        if (suspensionService.GetProviderInfo(providerID) is null)
            return NotFound(ProviderNotFound);

        if (!await suspensionService.Lift(providerID, kind, cancellationToken).ConfigureAwait(false))
            return ValidationProblem(NotLiftable, nameof(kind));

        return GetSuspension(providerID);
    }

    #endregion
}
