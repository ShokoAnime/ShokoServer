using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Plex;
using Shoko.Server.Plex;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class PlexController : BaseController
{
    private const string AlreadyAuthenticated = "User has already authenticated with plex yet. Invalidate the current token before re-authenticating.";

    private const string NotAuthenticated = "User has not authenticated with plex yet. Authenticate first before retrying.";

    private const string NoSelectedServer = "A server has not been selected yet. Select a server first.";

    private const string InvalidServerSelection = "Invalid server selection.";

    private const string InvalidLibrarySelection = "Selected libraries are invalid.";

    /// <summary>
    /// Get an OAuth2 authenticate url to authenticate the current user.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Auth/Authenticate")]
    public ActionResult<string> GetOAuthRequestUrl([FromQuery] bool force = false)
    {
        var helper = GetHelperForUser();

        if (helper.IsAuthenticated && !force)
            return BadRequest(AlreadyAuthenticated);

        return helper.LoginUrl;
    }

    /// <summary>
    /// Check if the current user is currently authenticated against the plex api.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Auth/IsAuthenticated")]
    public ActionResult<bool> IsAuthenticated()
    {
        var helper = GetHelperForUser();

        return helper.IsAuthenticated;
    }

    /// <summary>
    /// Invalidate and remove the current plex authentication token.
    /// </summary>
    /// <returns></returns>
    [HttpPost("Auth/InvalidateToken")]
    public ActionResult<bool> InvalidateToken()
    {
        var helper = GetHelperForUser();

        if (!helper.IsAuthenticated)
            return false;

        helper.InvalidateToken();

        return true;
    }

    /// <summary>
    /// Show all available server for the current user.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("Linking/AvailableServers")]
    public ActionResult<List<PlexServer>> AvailableServers(int userId)
    {
        var helper = GetHelperForUser();

        if (!helper.IsAuthenticated)
            return BadRequest(NotAuthenticated);

        var currentServerID = helper.ServerCache?.ClientIdentifier ?? "";
        return helper.GetPlexServers()
            .Select(device => new PlexServer(device, device.ClientIdentifier == currentServerID))
            .ToList();
    }

    /// <summary>
    /// Get the active server.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Linking/Server")]
    public ActionResult<PlexServer> CurrentDevice()
    {
        var helper = GetHelperForUser();

        if (!helper.IsAuthenticated)
            return BadRequest(NotAuthenticated);

        var currentServer = helper.ServerCache;
        if (currentServer == null)
            return BadRequest(NoSelectedServer);

        return new PlexServer(currentServer, true);
    }

    /// <summary>
    /// Select the active server.
    /// </summary>
    /// <param name="server"></param>
    /// <returns></returns>
    [HttpPut("Linking/Server")]
    public ActionResult<PlexServer> UseDevice(PlexServer server)
    {
        var helper = GetHelperForUser();

        if (!helper.IsAuthenticated)
            return BadRequest(NotAuthenticated);

        var previousServer = helper.ServerCache;
        var currentServer = helper.GetPlexServers()
            .FirstOrDefault(ser => ser.ClientIdentifier == server.ID);

        if (currentServer == null || !currentServer.Provides.Split(',').Contains("server"))
            return BadRequest(InvalidServerSelection);

        helper.UseServer(currentServer);

        // Reset the selected libraries if we're switching server.
        if (previousServer != null && currentServer.ClientIdentifier != previousServer.ClientIdentifier)
        {
            ServerSettings.Instance.Plex.Libraries = new();
            ServerSettings.Instance.SaveSettings();
        }

        return new PlexServer(currentServer, true);
    }

    /// <summary>
    /// Display all the libraries available in the selected server.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Linking/Server/Libraries")]
    public ActionResult<List<PlexLibrary>> GetLibraries()
    {
        var helper = GetHelperForUser();

        if (!helper.IsAuthenticated)
            return BadRequest(NotAuthenticated);

        var currentServer = helper.ServerCache;
        if (currentServer == null)
            return BadRequest(NoSelectedServer);

        var directories = helper.GetDirectories();
        return directories
            .Select(directory => new PlexLibrary(directory, currentServer))
            .ToList();
    }

    /// <summary>
    /// Select the libraries used for the plex syncing.
    /// </summary>
    /// <param name="libraries"></param>
    /// <returns></returns>
    [HttpPut("Linking/Server/Libraries")]
    public ActionResult<List<PlexLibrary>> SelectLibraries([FromBody] List<PlexLibrary> libraries)
    {
        var helper = GetHelperForUser();

        if (!helper.IsAuthenticated)
            return BadRequest(NotAuthenticated);

        var currentServer = helper.ServerCache;
        if (currentServer == null)
            return BadRequest(NoSelectedServer);

        var directories = helper.GetDirectories();
        var directorySet = directories
            .Select(directory => directory.Key)
            .ToHashSet();

        if (libraries.Any(library => !directorySet.Contains(library.ID) || library.ServerID != currentServer.ClientIdentifier))
            return BadRequest(InvalidLibrarySelection);

        ServerSettings.Instance.Plex.Libraries = libraries?.Select(s => s.ID).ToList() ?? new();
        ServerSettings.Instance.SaveSettings();

        return directories
            .Select(directory => new PlexLibrary(directory, currentServer, directorySet.Contains(directory.Key)))
            .ToList();
    }

    [NonAction]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PlexHelper GetHelperForUser()
        => PlexHelper.GetForUser(User);

}
