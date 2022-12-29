using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The WebUI spesific controller. Only WebUI should use these endpoints.
/// They may break at any time if the WebUI client needs to change something,
/// and is therefore unsafe for other clients.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class WebUIController : BaseController
{
    [HttpPost("GroupView")]
    public ActionResult<List<WebUI.WebUIGroupExtra>> GetGroupView([FromBody] WebUI.Input.WebUIGroupViewBody body)
    {
        var user = User;
        return body.GroupIDs
            .Select(groupID =>
            {
                var group = RepoFactory.AnimeGroup.GetByID(groupID);
                if (group == null || !user.AllowedGroup(group))
                {
                    return null;
                }

                var series = group.GetMainSeries();
                var anime = series?.GetAnime();
                if (series == null || anime == null)
                {
                    return null;
                }

                return new WebUI.WebUIGroupExtra(HttpContext, group, series, anime, body.TagFilter, body.OrderByName,
                    body.TagLimit);
            })
            .ToList();
    }

    /// <summary>
    /// Install a fresh copy of the web ui for the selected
    /// <paramref name="channel"/>. Will only install if it detects that no
    /// previous version is installed.
    /// 
    /// You don't need to be authenticated to use this endpoint.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <returns></returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Install")]
    public ActionResult InstallWebUI([FromQuery] ReleaseChannel channel = ReleaseChannel.Stable)
    {
        var indexLocation = Path.Combine(Utils.ApplicationPath, "webui", "index.html");
        if (System.IO.File.Exists(indexLocation))
        {
            var index = System.IO.File.ReadAllText(indexLocation);
            var token = "Web UI was not properly installed";
            if (!index.Contains(token))
                return BadRequest("If trying to update");
        }

        WebUIHelper.GetUrlAndUpdate(LatestWebUIVersion(channel).Version);
        return Redirect("/webui/index.html");
    }

    /// <summary>
    /// Update an existing version of the web ui to the latest for the selected
    /// <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <returns></returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Update")]
    public ActionResult UpdateWebUI([FromQuery] ReleaseChannel channel = ReleaseChannel.Stable)
    {
        WebUIHelper.GetUrlAndUpdate(LatestWebUIVersion(channel).Version);
        return NoContent();
    }

    /// <summary>
    /// Check for latest version for the selected <paramref name="channel"/> and
    /// return a <see cref="ComponentVersion"/> containing the version
    /// information.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <returns></returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("LatestVersion")]
    public ComponentVersion LatestWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Stable)
    {
        return new ComponentVersion { Version = WebUIHelper.WebUIGetLatestVersion(channel == ReleaseChannel.Stable) };
    }

    public WebUIController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
