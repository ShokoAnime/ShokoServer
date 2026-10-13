using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Plugin.LegacyPlex.Jobs;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.Plugin.LegacyPlex.Controllers;

/// <summary>
///   The Plex route among the server's legacy APIv3 actions, kept where the
///   clients using it expect it.
/// </summary>
/// <param name="scheduler">Queues the syncs.</param>
/// <param name="plexHelpers">Lists the users with a linked Plex account.</param>
[ApiController, Tags("Plex")]
[Route("/api/v{version:apiVersion}/Action")]
[ApiVersion("3.0")]
[Authorize("admin")]
public class PlexActionController(IQueueScheduler scheduler, PlexHelperProvider plexHelpers) : Controller
{
    /// <summary>
    ///   Sync watch states with Plex for all users with a Plex token.
    /// </summary>
    /// <returns>Ok, once the syncs are queued.</returns>
    [HttpGet("PlexSyncAll")]
    public async Task<ActionResult> PlexSyncAll()
    {
        foreach (var user in plexHelpers.GetLinkedUsers())
            await scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.UserID = user.JMMUserID);
        return Ok();
    }
}
