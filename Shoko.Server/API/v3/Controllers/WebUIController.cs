using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.WebUI;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using WebUIGroupExtra = Shoko.Server.API.v3.Models.Shoko.WebUI.WebUIGroupExtra;
using Input = Shoko.Server.API.v3.Models.Shoko.WebUI.Input;

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
    private static IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions() {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    private static readonly TimeSpan CacheTTL = TimeSpan.FromHours(1);

    [HttpPost("GroupView")]
    public ActionResult<List<WebUIGroupExtra>> GetGroupView([FromBody] Input.WebUIGroupViewBody body)
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

                return new WebUIGroupExtra(HttpContext, group, series, anime, body.TagFilter, body.OrderByName,
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
    /// <param name="force">Bypass the cache and search for a new version online.</param>
    /// /// <returns></returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("LatestVersion")]
    public ComponentVersion LatestWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Stable, [FromQuery] bool force = false)
    {
        var key = $"webui:{channel}";
        if (!force && Cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
            return componentVersion;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var releases = WebUIHelper.DownloadApiResponse("releases?per_page=10&page=1");
                foreach (var release in releases)
                {
                    string version = release.tag_name;
                    foreach (var asset in release.assets)
                    {
                        // We don't care what the zip is named, only that it is attached.
                        // This is because we changed the signature from "latest.zip" to
                        // "Shoko-WebUI-{obj.tag_name}.zip" in the upgrade to web ui v2
                        string fileName = asset.name;
                        if (fileName == "latest.zip" || fileName == $"Shoko-WebUI-{version}.zip")
                        {
                            var tag = WebUIHelper.DownloadApiResponse($"git/ref/tags/{version}");
                            string commit = tag["object"].sha;
                            string releaseDate = release.published_at;
                            return Cache.Set<ComponentVersion>(key, new ComponentVersion
                            {
                                Version = version,
                                Commit = commit[0..7],
                                ReleaseChannel = ReleaseChannel.Dev,
                            }, CacheTTL);
                        }
                    }
                }

                // Fallback to stable.
                goto default;
            }

            // Check for stable channel updates.
            default:
            {
                var latestRelease = WebUIHelper.DownloadApiResponse("releases/latest");
                string version = latestRelease.tag_name;
                var tag = WebUIHelper.DownloadApiResponse($"git/ref/tags/{version}");
                string commit = tag["object"].sha;
                return Cache.Set<ComponentVersion>(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit[0..7],
                    ReleaseChannel = ReleaseChannel.Stable,
                    Tag = version,
                }, CacheTTL);
            }
        }
    }

    /// <summary>
    /// Check for latest version for the selected <paramref name="channel"/> and
    /// return a <see cref="ComponentVersion"/> containing the version
    /// information.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <param name="force">Bypass the cache and search for a new version online.</param>
    /// <returns></returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("LatestServerVersion")]
    public ActionResult<ComponentVersion> LatestServerWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Stable, [FromQuery] bool force = false)
    {
        var key = $"server:{channel}";
        if (!force && Cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
            return componentVersion;
        switch (channel)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var latestRelease = WebUIHelper.DownloadApiResponse("releases/latest", "shokoanime/shokoserver");
                var latestCommit = WebUIHelper.DownloadApiResponse("git/ref/heads/master", "shokoanime/shokoserver");
                string tagName = latestRelease.tag_name;
                string version = tagName[1..] + ".0";
                string commit = latestCommit["object"].sha;
                return Cache.Set<ComponentVersion>(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit,
                    ReleaseChannel = ReleaseChannel.Dev,
                }, CacheTTL);
            }

            // Check for stable channel updates.
            default:
            {
                var latestRelease = WebUIHelper.DownloadApiResponse("releases/latest", "shokoanime/shokoserver");
                string tagName = latestRelease.tag_name;
                var tagResponse = WebUIHelper.DownloadApiResponse($"git/ref/tags/{tagName}", "shokoanime/shokoserver");
                string version = tagName[1..] + ".0";
                string commit = tagResponse["object"].sha;
                return Cache.Set<ComponentVersion>(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit,
                    ReleaseChannel = ReleaseChannel.Stable,
                    Tag = tagName,
                }, CacheTTL);
            }
        }
    }

    public WebUIController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
