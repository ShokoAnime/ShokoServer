using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AbstractPluginInfo = Shoko.Abstractions.Plugin.Models.LocalPluginInfo;
using PluginPage = Shoko.Server.API.v3.Models.Plugin.PluginPage;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
///   Lists the pages plugins offer, to every user. Apart from
///   <see cref="PluginController"/>, which is for administrators only.
/// </summary>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="pluginManager">Plugin manager.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Plugin"), Tags("Plugin")]
[ApiV3]
[Authorize]
[DatabaseBlockedExempt]
[InitFriendly]
public class PluginPageController(
    ISettingsProvider settingsProvider,
    IPluginManager pluginManager
) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Gets the pages offered by the plugins.
    /// </summary>
    /// <remarks>
    ///   Administrators get every page, of any plugin the filters let through,
    ///   along with the plugin info. Other users only get the pages of active
    ///   plugins that are offered to users, and the filters are ignored.
    /// </remarks>
    /// <param name="query">An optional search to narrow the plugins by.</param>
    /// <param name="active">Whether to include active plugins.</param>
    /// <param name="installed">Whether to include installed plugins.</param>
    /// <param name="enabled">Whether to include enabled plugins.</param>
    /// <param name="restartPending">Whether to include plugins waiting on a restart.</param>
    /// <param name="allVersions">Whether to include every version of each plugin.</param>
    /// <returns>
    ///   The pages visible to the caller.
    /// </returns>
    [HttpGet("Pages")]
    public List<PluginPage> GetPluginPages(
        [FromQuery] string? query = null,
        [FromQuery] IncludeOnlyFilter active = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter installed = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter enabled = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter restartPending = IncludeOnlyFilter.True,
        [FromQuery] bool allVersions = false
    )
    {
        return User.IsAdmin == 1
            ? GetPluginPages(PluginPageVisibility.Admin, query, active, installed, enabled, restartPending, allVersions)
            : GetPluginPages(PluginPageVisibility.Authenticated, query);
    }

    /// <summary>
    ///   Gets the pages offered to a caller of the given level.
    /// </summary>
    /// <param name="visibility">The highest level the caller may see.</param>
    /// <param name="query">An optional search to narrow the plugins by.</param>
    /// <param name="active">Whether to include active plugins.</param>
    /// <param name="installed">Whether to include installed plugins.</param>
    /// <param name="enabled">Whether to include enabled plugins.</param>
    /// <param name="restartPending">Whether to include plugins waiting on a restart.</param>
    /// <param name="allVersions">Whether to include every version of each plugin.</param>
    /// <returns>
    ///   The pages visible at that level.
    /// </returns>
    internal List<PluginPage> GetPluginPages(
        PluginPageVisibility visibility,
        string? query = null,
        IncludeOnlyFilter active = IncludeOnlyFilter.True,
        IncludeOnlyFilter installed = IncludeOnlyFilter.True,
        IncludeOnlyFilter enabled = IncludeOnlyFilter.True,
        IncludeOnlyFilter restartPending = IncludeOnlyFilter.True,
        bool allVersions = false
    )
    {
        var includePluginInfo = visibility is PluginPageVisibility.Admin;
        var enumerable = (IEnumerable<AbstractPluginInfo>)pluginManager.GetPluginInfos();
        if (!allVersions)
            enumerable = enumerable.DistinctBy(pluginInfo => pluginInfo.ID);
        // A search puts a name hit before a tag or author hit, and those
        // before a word found only in a description, then keeps load order.
        enumerable = string.IsNullOrEmpty(query)
            ? enumerable.OrderBy(pluginInfo => pluginInfo.LoadOrder)
            : enumerable
                .Search(query, [p => [p.Name], p => [.. p.Tags, p.Authors], p => [p.Description]])
                .OrderBy(result => result.Tier)
                .ThenBy(result => result.Result.LoadOrder)
                .Select(result => result.Result);
        return enumerable
            .Where(pluginInfo =>
            {
                if (!includePluginInfo && !pluginInfo.IsActive)
                    return false;
                if (active is not IncludeOnlyFilter.True)
                {
                    var shouldHideActive = active is IncludeOnlyFilter.False;
                    if (shouldHideActive == pluginInfo.IsActive)
                        return false;
                }
                if (installed is not IncludeOnlyFilter.True)
                {
                    var shouldHideInstalled = installed is IncludeOnlyFilter.False;
                    if (shouldHideInstalled == pluginInfo.IsInstalled)
                        return false;
                }
                if (enabled is not IncludeOnlyFilter.True)
                {
                    var shouldHideEnabled = enabled is IncludeOnlyFilter.False;
                    if (shouldHideEnabled == pluginInfo.IsEnabled)
                        return false;
                }
                if (restartPending is not IncludeOnlyFilter.True)
                {
                    var shouldHideRequiresRestart = restartPending is IncludeOnlyFilter.False;
                    if (shouldHideRequiresRestart == pluginInfo.RestartPending)
                        return false;
                }
                return true;
            })
            .SelectMany(pluginInfo => pluginInfo.GetPages())
            .Where(page => page.Visibility <= visibility)
            .Select(page => new PluginPage(page, includePluginInfo))
            .ToList();
    }
}
