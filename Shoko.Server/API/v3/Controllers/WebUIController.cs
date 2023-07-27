using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.WebUI;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using WebUITheme = Shoko.Server.API.v3.Models.Shoko.WebUI.WebUITheme;
using WebUIGroupExtra = Shoko.Server.API.v3.Models.Shoko.WebUI.WebUIGroupExtra;
using WebUISeriesExtra = Shoko.Server.API.v3.Models.Shoko.WebUI.WebUISeriesExtra;
using WebUISeriesFileSummary = Shoko.Server.API.v3.Models.Shoko.WebUI.WebUISeriesFileSummary;
using Input = Shoko.Server.API.v3.Models.Shoko.WebUI.Input;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;

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

    /// <summary>
    /// Retrieves the list of available themes.
    /// </summary>
    /// <param name="forceRefresh">Flag indicating whether to force a refresh of the themes.</param>
    /// <returns>The list of available themes.</returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Theme")]
    public ActionResult<List<WebUITheme>> GetThemes([FromQuery] bool forceRefresh = false)
    {
        return WebUIThemeProvider.GetThemes(forceRefresh).Select(definition => new WebUITheme(definition)).ToList();
    }

    /// <summary>
    /// Retrieves the CSS representation of the available themes.
    /// </summary>
    /// <param name="forceRefresh">Flag indicating whether to force a refresh of the themes.</param>
    /// <returns>The CSS representation of the themes.</returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [Produces("text/css")]
    [HttpGet("Theme.css")]
    public ActionResult<string> GetThemesCSS([FromQuery] bool forceRefresh = false)
    {
        return Content(WebUIThemeProvider.GetThemes(forceRefresh).ToCSS(), "text/css");
    }

    /// <summary>
    /// Adds a new theme to the application.
    /// </summary>
    /// <param name="body">The body of the request containing the theme URL and preview flag.</param>
    /// <returns>The added theme.</returns>
    [Authorize("admin")]
    [HttpPost("Theme")]
    public async Task<ActionResult<WebUITheme>> AddTheme([FromBody] Input.WebUIAddThemeBody body)
    {
        try
        {
            var theme = await WebUIThemeProvider.InstallTheme(body.URL, body.Preview);
            return new WebUITheme(theme);
        }
        catch (ValidationException valEx)
        {
            return ValidationProblem(valEx.Message, nameof(body.URL));
        }
        catch (HttpRequestException httpEx)
        {
            return InternalError(httpEx.Message);
        }
    }

    /// <summary>
    /// Retrieves a specific theme by its ID.
    /// </summary>
    /// <param name="themeID">The ID of the theme to retrieve.</param>
    /// <param name="forceRefresh">Flag indicating whether to force a refresh of the themes before retriving the specific theme.</param>
    /// <returns>The retrieved theme.</returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Theme/{themeID}")]
    public ActionResult<WebUITheme> GetTheme([FromRoute] string themeID, [FromQuery] bool forceRefresh = false)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, forceRefresh);
        if (theme == null)
            return NotFound("A theme with the given id was not found.");

        return new WebUITheme(theme);
    }

    /// <summary>
    /// Retrieves the CSS representation of a specific theme by its ID.
    /// </summary>
    /// <param name="themeID">The ID of the theme to retrieve.</param>
    /// <param name="forceRefresh">Flag indicating whether to force a refresh of the themes before retriving the specific theme.</param>
    /// <returns>The retrieved theme.</returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [Produces("text/css")]
    [HttpGet("Theme/{themeID}.css")]
    public ActionResult<string> GetThemeCSS([FromRoute] string themeID, [FromQuery] bool forceRefresh = false)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, forceRefresh);
        if (theme == null)
            return NotFound("A theme with the given id was not found.");

        return Content(theme.ToCSS(), "text/css");
    }

    /// <summary>
    /// Removes a theme from the application.
    /// </summary>
    /// <param name="themeID">The ID of the theme to remove.</param>
    /// <returns>The result of the removal operation.</returns>
    [Authorize("admin")]
    [HttpDelete("Theme/{themeID}")]
    public ActionResult RemoveTheme([FromRoute] string themeID)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, true);
        if (theme == null)
            return NotFound("A theme with the given id was not found.");

        WebUIThemeProvider.RemoveTheme(theme);

        return NoContent();
    }

    /// <summary>
    /// Updates a theme by its ID.
    /// </summary>
    /// <param name="themeID">The ID of the theme to update.</param>
    /// <param name="preview">Flag indicating whether to enable preview mode.</param>
    /// <returns>The updated theme.</returns>
    [Authorize("admin")]
    [HttpPost("Theme/{themeID}/Update")]
    public async Task<ActionResult<WebUITheme>> UpdateTheme([FromRoute] string themeID, [FromQuery] bool preview = false)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, true);
        if (theme == null)
            return NotFound("A theme with the given id was not found.");

        try
        {
            theme = await WebUIThemeProvider.UpdateTheme(theme, preview);
            return new WebUITheme(theme);
        }
        catch (ValidationException valEx)
        {
            return BadRequest(valEx.Message);
        }
        catch (HttpRequestException httpEx)
        {
            return InternalError(httpEx.Message);
        }
    }

    /// <summary>
    /// Returns a list of extra information for each group ID in the given body.
    /// </summary>
    /// <param name="body">The body of the request, containing the group IDs and optional filter parameters.</param>
    /// <returns>A list of <c>WebUIGroupExtra</c> objects containing extra information for each group.</returns>
    [HttpPost("GroupView")]
    public ActionResult<List<WebUIGroupExtra>> GetGroupView([FromBody] Input.WebUIGroupViewBody body)
    {
        // Check user permissions for each requested group and return extra information.
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

                return new WebUIGroupExtra(group, series, anime, body.TagFilter, body.OrderByName,
                    body.TagLimit);
            })
            .ToList();
    }

    /// <summary>
    /// Returns extra information for the series with the given ID.
    /// </summary>
    /// <param name="seriesID">The ID of the series to retrieve information for.</param>
    /// <returns>A <c>WebUISeriesExtra</c> object containing extra information for the series.</returns>
    [HttpGet("Series/{seriesID}")]
    public ActionResult<WebUISeriesExtra> GetSeries([FromRoute] int seriesID)
    {
        // Retrieve extra information for the specified series if it exists and the user has permissions.
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return new WebUISeriesExtra(HttpContext, series);
    }

    /// <summary>
    /// Returns a summary of file information for the series with the given ID.
    /// </summary>
    /// <param name="seriesID">The ID of the series to retrieve file information for.</param>
    /// <param name="type">Filter the view to only the spesified <see cref="EpisodeType"/>s.</param>
    /// <param name="includeEpisodeDetails">Include episode details for each range.</param>
    /// <returns>A <c>WebUISeriesFileSummary</c> object containing a summary of file information for the series.</returns>
    [HttpGet("Series/{seriesID}/FileSummary")]
    public ActionResult<WebUISeriesFileSummary> GetSeriesFileSummary([FromRoute] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType> type = null,
        [FromQuery] bool includeEpisodeDetails = false)
    {
        // Retrieve a summary of file information for the specified series if it exists and the user has permissions.
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return new WebUISeriesFileSummary(series, type, includeEpisodeDetails);
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

        WebUIHelper.GetUrlAndUpdate(LatestWebUIVersion(channel).Tag);
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
        WebUIHelper.GetUrlAndUpdate(LatestWebUIVersion(channel).Tag);
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
                    string tagName = release.tag_name;
                    string version = tagName[0] == 'v' ? tagName[1..] : tagName;
                    foreach (var asset in release.assets)
                    {
                        // We don't care what the zip is named, only that it is attached.
                        // This is because we changed the signature from "latest.zip" to
                        // "Shoko-WebUI-{obj.tag_name}.zip" in the upgrade to web ui v2
                        string fileName = asset.name;
                        if (fileName == "latest.zip" || fileName == $"Shoko-WebUI-{tagName}.zip")
                        {
                            var tag = WebUIHelper.DownloadApiResponse($"git/ref/tags/{tagName}");
                            string commit = tag["object"].sha;
                            DateTime releaseDate = release.published_at;
                            releaseDate = releaseDate.ToUniversalTime();
                            string description = release.body;
                            return Cache.Set<ComponentVersion>(key, new ComponentVersion
                            {
                                Version = version,
                                Commit = commit[0..7],
                                ReleaseChannel = ReleaseChannel.Dev,
                                ReleaseDate = releaseDate,
                                Tag = tagName,
                                Description = description.Trim(),
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
                string tagName = latestRelease.tag_name;
                string version = tagName[0] == 'v' ? tagName[1..] : tagName;
                var tag = WebUIHelper.DownloadApiResponse($"git/ref/tags/{version}");
                string commit = tag["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                string description = latestRelease.body;
                return Cache.Set<ComponentVersion>(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit[0..7],
                    ReleaseChannel = ReleaseChannel.Stable,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description.Trim(),
                }, CacheTTL);
            }
        }
    }

    private static ReleaseChannel GetDefaultServerReleaseChannel()
    {
        var extraVersionDict = Utils.GetApplicationExtraVersion();
        if (extraVersionDict.TryGetValue("channel", out var rawChannel))
            if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var parsedChannel))
                return parsedChannel;
        return ReleaseChannel.Stable;
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
    public ActionResult<ComponentVersion> LatestServerWebUIVersion([FromQuery] ReleaseChannel? channel = null, [FromQuery] bool force = false)
    {
        if (!channel.HasValue)
            channel = GetDefaultServerReleaseChannel();
        var key = $"server:{channel}";
        if (!force && Cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
            return componentVersion;
        switch (channel.Value)
        {
            // Check for dev channel updates.
            case ReleaseChannel.Dev:
            {
                var latestRelease = WebUIHelper.DownloadApiResponse("releases/latest", "shokoanime/shokoserver");
                var masterBranch = WebUIHelper.DownloadApiResponse("git/ref/heads/master", "shokoanime/shokoserver");
                string commitSha = masterBranch["object"].sha;
                var latestCommit = WebUIHelper.DownloadApiResponse($"commits/{commitSha}", "shokoanime/shokoserver");
                string tagName = latestRelease.tag_name;
                string version = tagName[1..] + ".0";
                DateTime releaseDate = latestCommit.commit.author.date;
                releaseDate = releaseDate.ToUniversalTime();
                string description;
                // We're on a local build.
                if (!Utils.GetApplicationExtraVersion().TryGetValue("commit", out var currentCommit))
                {
                    description = "Local build detected. Unable to determine the relativeness of the latest daily release.";
                }
                // We're not on the latest daily release.
                else if (!string.Equals(currentCommit, commitSha))
                {
                    var diff = WebUI.WebUIHelper.DownloadApiResponse($"compare/{commitSha}...{currentCommit}", "shokoanime/shokoserver");
                    var aheadBy = (int)diff.ahead_by;
                    var behindBy = (int)diff.behind_by;
                    description = $"You are currently {aheadBy} commits ahead and {behindBy} commits behind the latest daily release.";
                }
                // We're on the latest daily release.
                else {
                    description = "All caught up! You are running the latest daily release.";
                }
                return Cache.Set<ComponentVersion>(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commitSha,
                    ReleaseChannel = ReleaseChannel.Dev,
                    ReleaseDate = releaseDate,
                    Description = description,
                }, CacheTTL);
            }

#if DEBUG
            // Spoof update if debugging and requesting the latest debug version.
            case ReleaseChannel.Debug:
            {
                componentVersion = new ComponentVersion() { Version = Utils.GetApplicationVersion(), Description = "Local debug version." };
                var extraVersionDict = Utils.GetApplicationExtraVersion();
                if (extraVersionDict.TryGetValue("tag", out var tag))
                    componentVersion.Tag = tag;
                if (extraVersionDict.TryGetValue("commit", out var commit))
                    componentVersion.Commit = commit;
                if (extraVersionDict.TryGetValue("channel", out var rawChannel))
                    if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var parsedChannel))
                        componentVersion.ReleaseChannel = parsedChannel;
                    else
                        componentVersion.ReleaseChannel = ReleaseChannel.Debug;
                if (extraVersionDict.TryGetValue("date", out var dateText) && DateTime.TryParse(dateText, out var releaseDate))
                    componentVersion.ReleaseDate = releaseDate.ToUniversalTime();
                return Cache.Set<ComponentVersion>(key, componentVersion, CacheTTL);
            }
#endif

            // Check for stable channel updates.
            default:
            {
                var latestRelease = WebUIHelper.DownloadApiResponse("releases/latest", "shokoanime/shokoserver");
                string tagName = latestRelease.tag_name;
                var tagResponse = WebUIHelper.DownloadApiResponse($"git/ref/tags/{tagName}", "shokoanime/shokoserver");
                string version = tagName[1..] + ".0";
                string commit = tagResponse["object"].sha;
                DateTime releaseDate = latestRelease.published_at;
                releaseDate = releaseDate.ToUniversalTime();
                string description = latestRelease.body;
                return Cache.Set<ComponentVersion>(key, new ComponentVersion
                {
                    Version = version,
                    Commit = commit,
                    ReleaseChannel = ReleaseChannel.Stable,
                    ReleaseDate = releaseDate,
                    Tag = tagName,
                    Description = description.Trim(),
                }, CacheTTL);
            }
        }
    }

    public WebUIController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
