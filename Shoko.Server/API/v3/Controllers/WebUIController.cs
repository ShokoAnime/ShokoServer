using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
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
using FileSummaryGroupByCriteria = Shoko.Server.API.v3.Models.Shoko.WebUI.WebUISeriesFileSummary.FileSummaryGroupByCriteria;
using Input = Shoko.Server.API.v3.Models.Shoko.WebUI.Input;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The WebUI specific controller. Only WebUI should use these endpoints.
/// They may break at any time if the WebUI client needs to change something,
/// and is therefore unsafe for other clients.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public partial class WebUIController : BaseController
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    private static readonly TimeSpan _cacheTTL = TimeSpan.FromHours(1);

    private readonly ILogger<WebUIController> _logger;

    private readonly WebUIFactory _webUIFactory;

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
    /// Adds a new theme to the application from a theme URL.
    /// </summary>
    /// <param name="body">The body of the request containing the theme URL and preview flag.</param>
    /// <returns>The added theme.</returns>
    [Authorize("admin")]
    [HttpPost("Theme/AddFromURL")]
    public async Task<ActionResult<WebUITheme>> AddThemeFromUrl([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Input.WebUIAddThemeBody body)
    {
        try
        {
            var theme = await WebUIThemeProvider.InstallThemeFromUrl(body.URL, body.Preview);
            return new WebUITheme(theme, true);
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
    /// Adds a new theme to the application by uploading a theme file.
    /// </summary>
    /// <param name="file">The theme file to add.</param>
    /// <param name="preview">Flag indicating whether to enable preview mode, which just validates the file contents without installing the theme.</param>
    /// <returns>The added or previewed theme.</returns>
    [Authorize("admin")]
    [HttpPost("Theme/AddFromFile")]
    public async Task<ActionResult<WebUITheme>> AddThemeFromFile(IFormFile file, [FromForm] bool preview = false)
    {
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrEmpty(fileName))
            return ValidationProblem("File name cannot be empty or omitted.", nameof(file));

        try
        {
            // Check if the file name conforms to our specified format.
            switch (Path.GetExtension(fileName))
            {
                case ".css":
                {
                    using var fileReader = new StreamReader(file.OpenReadStream());
                    var content = await fileReader.ReadToEndAsync();
                    var theme = await WebUIThemeProvider.CreateOrUpdateThemeFromCss(content, Path.GetFileNameWithoutExtension(fileName), preview);
                    return new WebUITheme(theme, true);
                }
                case ".json":
                {
                    using var fileReader = new StreamReader(file.OpenReadStream());
                    var content = await fileReader.ReadToEndAsync();
                    var theme = await WebUIThemeProvider.InstallOrUpdateThemeFromJson(content, Path.GetFileNameWithoutExtension(fileName), preview);
                    return new WebUITheme(theme, true);
                }
                default:
                    return ValidationProblem("Unsupported file extension.", nameof(file));
            }
        }
        catch (ValidationException valEx)
        {
            return ValidationProblem(valEx.Message);
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
    /// <param name="forceRefresh">Flag indicating whether to force a refresh of the themes before retrieving the specific theme.</param>
    /// <returns>The retrieved theme.</returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Theme/{themeID}")]
    public ActionResult<WebUITheme> GetTheme([FromRoute] string themeID, [FromQuery] bool forceRefresh = false)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, forceRefresh);
        if (theme is null)
            return NotFound("A theme with the given id was not found.");

        return new WebUITheme(theme, true);
    }

    /// <summary>
    /// Retrieves the CSS representation of a specific theme by its ID.
    /// </summary>
    /// <param name="themeID">The ID of the theme to retrieve.</param>
    /// <param name="forceRefresh">Flag indicating whether to force a refresh of the themes before retrieving the specific theme.</param>
    /// <returns>The retrieved theme.</returns>
    [AllowAnonymous]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [Produces("text/css")]
    [HttpGet("Theme/{themeID}.css")]
    public ActionResult<string> GetThemeCSS([FromRoute] string themeID, [FromQuery] bool forceRefresh = false)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, forceRefresh);
        if (theme is null)
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
        if (theme is null || !WebUIThemeProvider.RemoveTheme(theme))
            return NotFound("A theme with the given id was not found.");

        return NoContent();
    }
    /// <summary>
    /// Preview the update to a theme by its ID.
    /// </summary>
    /// <param name="themeID">The ID of the theme to update.</param>
    /// <returns>The preview of the updated theme.</returns>
    [ResponseCache(Duration = 60 /* 1 minute in seconds */)]
    [HttpGet("Theme/{themeID}/Update")]
    public async Task<ActionResult<WebUITheme>> PreviewUpdatedTheme([FromRoute] string themeID)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, true);
        if (theme is null)
            return NotFound("A theme with the given id was not found.");

        try
        {
            theme = await WebUIThemeProvider.UpdateThemeOnline(theme, true);
            return new WebUITheme(theme, true);
        }
        catch (ValidationException valEx)
        {
            return ValidationProblem(valEx.Message);
        }
        catch (HttpRequestException httpEx)
        {
            return InternalError(httpEx.Message);
        }
    }

    /// <summary>
    /// Updates a theme by its ID.
    /// </summary>
    /// <param name="themeID">The ID of the theme to update.</param>
    /// <returns>The updated theme.</returns>
    [Authorize("admin")]
    [HttpPost("Theme/{themeID}/Update")]
    public async Task<ActionResult<WebUITheme>> UpdateTheme([FromRoute] string themeID)
    {
        var theme = WebUIThemeProvider.GetTheme(themeID, true);
        if (theme is null)
            return NotFound("A theme with the given id was not found.");

        try
        {
            theme = await WebUIThemeProvider.UpdateThemeOnline(theme);
            return new WebUITheme(theme, true);
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
            .Distinct()
            .Select(groupID =>
            {
                var group = RepoFactory.AnimeGroup.GetByID(groupID);
                if (group is null || !user.AllowedGroup(group))
                {
                    return null;
                }

                var series = group.MainSeries ?? group.AllSeries.FirstOrDefault();
                var anime = series?.AniDB_Anime;
                if (anime is null)
                {
                    return null;
                }

                return _webUIFactory.GetWebUIGroupExtra(group, anime, body.TagFilter, body.OrderByName,
                    body.TagLimit);
            })
            .WhereNotNull()
            .ToList();
    }

    /// <summary>
    /// Returns extra information for the series with the given ID.
    /// </summary>
    /// <param name="seriesID">The ID of the series to retrieve information for.</param>
    /// <returns>A <c>WebUISeriesExtra</c> object containing extra information for the series.</returns>
    [HttpGet("Series/{seriesID}")]
    public ActionResult<WebUISeriesExtra> GetSeries([FromRoute, Range(1, int.MaxValue)] int seriesID)
    {
        // Retrieve extra information for the specified series if it exists and the user has permissions.
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series is null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return _webUIFactory.GetWebUISeriesExtra(series);
    }

    /// <summary>
    /// Returns a summary of file information for the series with the given ID.
    /// </summary>
    /// <param name="seriesID">The ID of the series to retrieve file information for.</param>
    /// <param name="type">Filter the view to only the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="groupBy">Group the episodes in view into smaller groups based on <see cref="FileSummaryGroupByCriteria"/>s.</param>
    /// <param name="includeEpisodeDetails">Include episode details for each range.</param>
    /// <param name="includeMissingUnknownEpisodes">Include missing episodes that does not have an air date set.</param>
    /// <param name="includeMissingFutureEpisodes">Include missing episodes that will air in the future.</param>
    /// <returns>A <c>WebUISeriesFileSummary</c> object containing a summary of file information for the series.</returns>
    [HttpGet("Series/{seriesID}/FileSummary")]
    public ActionResult<WebUISeriesFileSummary> GetSeriesFileSummary(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<FileSummaryGroupByCriteria>? groupBy = null,
        [FromQuery] bool includeEpisodeDetails = false,
        [FromQuery] bool includeMissingUnknownEpisodes = false,
        [FromQuery] bool includeMissingFutureEpisodes = false)
    {
        // Retrieve a summary of file information for the specified series if it exists and the user has permissions.
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series is null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return new WebUISeriesFileSummary(series, type, includeEpisodeDetails, includeMissingFutureEpisodes, includeMissingUnknownEpisodes, groupBy);
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
    [HttpPost("Install")]
    public ActionResult InstallWebUI([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto)
    {
        var indexLocation = Path.Combine(Utils.ApplicationPath, "webui", "index.html");
        if (System.IO.File.Exists(indexLocation))
        {
            var index = System.IO.File.ReadAllText(indexLocation);
            var token = "install-web-ui";
            if (!index.Contains(token))
                return BadRequest("Unable to install web UI when a web UI is already installed.");
        }

        var result = LatestWebUIVersion(channel);
        if (result.Value is null)
            return result.Result!;

        if (result.Value.Tag is null)
            return BadRequest("Unable to install web UI because a GitHub release was not found.");

        try
        {
            WebUIHelper.GetUrlAndUpdate(result.Value.Tag);
        }
        catch (WebException ex)
        {
            if (ex.Status != WebExceptionStatus.Success)
            {
                _logger.LogError(ex, "An error occurred while trying to install the Web UI.");
                return Problem("Unable to use the GitHub API to check for an update. Check your connection and try again.", null, (int)HttpStatusCode.BadGateway, "Unable to connect to GitHub.");
            }
            throw;
        }

        return Redirect("/webui/index.html");
    }

    /// <inheritdoc cref="InstallWebUI"/>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Install")]
    [Obsolete("Post is correct, but we want legacy versions of the webui boot-strapper to be able to install. We can remove this later™.")]
    public ActionResult UpdateWebUILegacy([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto)
        => InstallWebUI(channel);

    /// <summary>
    /// Update an existing version of the web ui to the latest for the selected
    /// <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">The release channel to use.</param>
    /// <returns></returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpPost("Update")]
    public ActionResult UpdateWebUI([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto)
    {
        if (channel == ReleaseChannel.Auto)
            channel = GetCurrentWebUIReleaseChannel();
        var result = LatestWebUIVersion(channel);
        if (result.Value is null)
            return result.Result!;

        if (result.Value.Tag is null)
            return BadRequest("Unable to update web UI because a GitHub release was not found.");

        try
        {
            WebUIHelper.GetUrlAndUpdate(result.Value.Tag);
        }
        catch (WebException ex)
        {
            if (ex.Status != WebExceptionStatus.Success)
            {
                _logger.LogError(ex, "An error occurred while trying to update the Web UI.");
                return Problem("Unable to use the GitHub API to check for an update. Check your connection and try again.", null, (int)HttpStatusCode.BadGateway, "Unable to connect to GitHub.");
            }
            throw;
        }

        return NoContent();
    }

    /// <inheritdoc cref="UpdateWebUI"/>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Update")]
    [Obsolete("Post is correct, but we want old versions of the webui to be able to update. We can remove this later™.")]
    public ActionResult UpdateWebUIOld([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto)
        => UpdateWebUI(channel);

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
    public ActionResult<ComponentVersion> LatestWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto, [FromQuery] bool force = false)
    {
        try
        {
            if (channel == ReleaseChannel.Auto)
                channel = GetCurrentWebUIReleaseChannel();
            var key = $"webui:{channel}";
            if (!force && _cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
                return componentVersion!;
            switch (channel)
            {
                // Check for dev channel updates.
                case ReleaseChannel.Dev:
                {
                    var releases = WebUIHelper.DownloadApiResponse("releases?per_page=10&page=1");
                    foreach (var release in releases)
                    {
                        string tagName = release.tag_name;
                        var version = tagName[0] == 'v' ? tagName[1..] : tagName;
                        foreach (var asset in release.assets)
                        {
                            // We don't care what the zip is named, only that it is attached.
                            string fileName = asset.name;
                            if (Path.GetExtension(fileName) is ".zip")
                            {
                                var tag = WebUIHelper.DownloadApiResponse($"git/ref/tags/{tagName}");
                                string commit = tag["object"].sha;
                                DateTime releaseDate = release.published_at;
                                releaseDate = releaseDate.ToUniversalTime();
                                string description = release.body;
                                return _cache.Set(key, new ComponentVersion
                                {
                                    Version = version,
                                    Commit = commit[..7],
                                    ReleaseChannel = ReleaseChannel.Dev,
                                    ReleaseDate = releaseDate,
                                    Tag = tagName,
                                    Description = description.Trim(),
                                }, _cacheTTL);
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
                    var version = tagName[0] == 'v' ? tagName[1..] : tagName;
                    var tag = WebUIHelper.DownloadApiResponse($"git/ref/tags/{tagName}");
                    string commit = tag["object"].sha;
                    DateTime releaseDate = latestRelease.published_at;
                    releaseDate = releaseDate.ToUniversalTime();
                    string description = latestRelease.body;
                    return _cache.Set(key, new ComponentVersion
                    {
                        Version = version,
                        Commit = commit[0..7],
                        ReleaseChannel = ReleaseChannel.Stable,
                        ReleaseDate = releaseDate,
                        Tag = tagName,
                        Description = description.Trim(),
                    }, _cacheTTL);
                }
            }
        }
        catch (WebException ex)
        {
            if (ex.Status != WebExceptionStatus.Success)
                return StatusCode((int)HttpStatusCode.BadGateway, "Unable to use the GitHub API to check for an update. Check your connection.");
            throw;
        }
    }

    [GeneratedRegex(@"^[Vv]?(?<version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+))(?:-dev.(?<buildNumber>\d+))?$", RegexOptions.Compiled, "en-US")]
    private static partial Regex ServerReleaseVersionRegex();

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
    public ActionResult<ComponentVersion> LatestServerWebUIVersion([FromQuery] ReleaseChannel channel = ReleaseChannel.Auto, [FromQuery] bool force = false)
    {
        try
        {
            if (channel == ReleaseChannel.Auto)
                channel = GetCurrentServerReleaseChannel();
            var key = $"server:{channel}";
            if (!force && _cache.TryGetValue<ComponentVersion>(key, out var componentVersion))
                return componentVersion!;
            switch (channel)
            {
                // Check for dev channel updates.
                case ReleaseChannel.Dev:
                {
                    var latestTags = WebUIHelper.DownloadApiResponse($"tags?per_page=100&page=1", WebUIHelper.ServerRepoName);
                    var version = string.Empty;
                    var tagName = string.Empty;
                    var commitSha = string.Empty;
                    var regex = ServerReleaseVersionRegex();
                    foreach (var tagInfo in latestTags)
                    {
                        string localTagName = tagInfo.name;
                        if (regex.Match(localTagName) is { Success: true } regexResult)
                        {
                            tagName = localTagName;
                            commitSha = tagInfo.commit.sha;
                            version = regexResult.Groups["version"].Value;
                            if (regexResult.Groups["buildNumber"].Success)
                                version += "." + regexResult.Groups["buildNumber"].Value;
                            else
                                version += ".0";
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(commitSha))
                    {
                        return BadRequest("Unable to locate the latest release to use.");
                    }

                    var latestCommit = WebUIHelper.DownloadApiResponse($"commits/{commitSha}", WebUIHelper.ServerRepoName);
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
                        var diff = WebUIHelper.DownloadApiResponse($"compare/{commitSha}...{currentCommit}", WebUIHelper.ServerRepoName);
                        var aheadBy = (int)diff.ahead_by;
                        var behindBy = (int)diff.behind_by;
                        description = $"You are currently {aheadBy} commits ahead and {behindBy} commits behind the latest daily release.";
                    }
                    // We're on the latest daily release.
                    else
                    {
                        description = "All caught up! You are running the latest daily release.";
                    }
                    return _cache.Set(key, new ComponentVersion
                    {
                        Version = version,
                        Commit = commitSha,
                        ReleaseChannel = ReleaseChannel.Dev,
                        ReleaseDate = releaseDate,
                        Tag = tagName,
                        Description = description,
                    }, _cacheTTL);
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
                    return _cache.Set<ComponentVersion>(key, componentVersion, _cacheTTL);
                }
#endif

                // Check for stable channel updates.
                default:
                {
                    var latestRelease = WebUIHelper.DownloadApiResponse("releases/latest", WebUIHelper.ServerRepoName);
                    string tagName = latestRelease.tag_name;
                    var tagResponse = WebUIHelper.DownloadApiResponse($"git/ref/tags/{tagName}", WebUIHelper.ServerRepoName);
                    var version = tagName[1..] + ".0";
                    string commit = tagResponse["object"].sha;
                    DateTime releaseDate = latestRelease.published_at;
                    releaseDate = releaseDate.ToUniversalTime();
                    string description = latestRelease.body;
                    return _cache.Set(key, new ComponentVersion
                    {
                        Version = version,
                        Commit = commit,
                        ReleaseChannel = ReleaseChannel.Stable,
                        ReleaseDate = releaseDate,
                        Tag = tagName,
                        Description = description.Trim(),
                    }, _cacheTTL);
                }
            }
        }
        catch (WebException ex)
        {
            if (ex.Status != WebExceptionStatus.Success)
                return StatusCode((int)HttpStatusCode.BadGateway, "Unable to use the GitHub API to check for an update. Check your connection.");
            throw;
        }
    }

    private static ReleaseChannel GetCurrentWebUIReleaseChannel()
    {
        var webuiVersion = WebUIHelper.LoadWebUIVersionInfo();
        if (webuiVersion != null)
            return webuiVersion.Debug ? ReleaseChannel.Debug : webuiVersion.Package.Contains("-dev") ? ReleaseChannel.Dev : ReleaseChannel.Stable;
        return GetCurrentServerReleaseChannel();
    }

    private static ReleaseChannel GetCurrentServerReleaseChannel()
    {
        var extraVersionDict = Utils.GetApplicationExtraVersion();
        if (extraVersionDict.TryGetValue("channel", out var rawChannel) && Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
            return channel;
        return ReleaseChannel.Stable;
    }


    public WebUIController(ISettingsProvider settingsProvider, WebUIFactory webUIFactory, ILogger<WebUIController> logger) : base(settingsProvider)
    {
        _logger = logger;
        _webUIFactory = webUIFactory;
    }
}
