using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Plugin.Input;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AbstractPluginInfo = Shoko.Abstractions.Plugin.PluginInfo;
using PluginInfo = Shoko.Server.API.v3.Models.Plugin.PluginInfo;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing plugins. Interacts with the <see cref="IPluginManager"/>.
/// </summary>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="pluginManager">Plugin manager.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class PluginController(ISettingsProvider settingsProvider, IPluginManager pluginManager) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Gets a list of all registered plugins.
    /// </summary>
    /// <param name="query">
    ///   An optional query to filter plugins by name.
    /// </param>
    /// <param name="active">
    ///   Whether to include all active plugins, include only active plugins,
    ///   or exclude all active plugins.
    /// </param>
    /// <param name="installed">
    ///   Whether to include all installed plugins, include only installed
    ///   plugins, or exclude all installed plugins.
    /// </param>
    /// <param name="enabled">
    ///   Whether to include all enabled plugins, include only enabled plugins,
    ///   or exclude all enabled plugins.
    /// </param>
    /// <param name="restartPending">
    ///   Whether to include all plugins that require a restart, include only
    ///   plugins that require a restart, or exclude all plugins that require a
    ///   restart.
    /// </param>
    /// <param name="allVersions">
    ///   Whether to include all versions of plugins, or only the active or
    ///   highest version.
    /// </param>
    /// <returns>
    ///   A list of <see cref="PluginInfo"/> for all registered plugins and
    ///   versions.
    /// </returns>
    [HttpGet]
    public ActionResult<List<PluginInfo>> GetPlugins(
        [FromQuery] string? query = null,
        [FromQuery] IncludeOnlyFilter active = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter installed = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter enabled = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter restartPending = IncludeOnlyFilter.True,
        [FromQuery] bool allVersions = false
    )
    {
        var enumerable = (IEnumerable<AbstractPluginInfo>)pluginManager.GetPluginInfos();
        if (!allVersions)
            enumerable = enumerable.DistinctBy(pluginInfo => pluginInfo.ID);
        if (!string.IsNullOrEmpty(query))
            enumerable = enumerable
                .Search(query, p => [p.Name])
                .Select(pluginInfo => pluginInfo.Result)
                .OrderBy(pluginInfo => pluginInfo.LoadOrder);
        return enumerable
            .Where(pluginInfo =>
            {
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
            .Select(pluginInfo => new PluginInfo(pluginInfo))
            .ToList();
    }

    /// <summary>
    ///   Attempts to load plugin infos from the specified paths.
    /// </summary>
    /// <param name="body">
    ///   The body containing the paths to load from.
    /// </param>
    /// <returns>
    ///   The newly loaded plugins.
    /// </returns>
    [HttpPost]
    public ActionResult<List<PluginInfo>> LoadPluginInfos(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] DiscoverPluginInfosBody body
    )
    {
        var list = new List<PluginInfo>();
        foreach (var path in body.Paths)
        {
            var pluginInfo = pluginManager.LoadFromPath(path);
            if (pluginInfo is not null)
                list.Add(new PluginInfo(pluginInfo));
        }

        return list;
    }

    /// <summary>
    ///   Gets the active or highest version of a plugin by ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpGet("{pluginID}")]
    public ActionResult<PluginInfo> GetPluginByID([FromRoute] Guid pluginID)
        => pluginManager.GetPluginInfo(pluginID) is { } pluginInfo
            ? new PluginInfo(pluginInfo)
            : NotFound("Plugin not found");

    /// <summary>
    ///   Gets the thumbnail for the active or highest version of a plugin by
    ///   ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpGet("{pluginID}/Thumbnail")]
    public ActionResult GetThumbnailForPluginByID([FromRoute] Guid pluginID)
        => pluginManager.GetPluginInfo(pluginID) is { Thumbnail: { } } pluginInfo
            ? File(pluginInfo.Thumbnail.GetStream(), pluginInfo.Thumbnail.MimeType)
            : NotFound("Plugin not found");

    /// <summary>
    ///   Updates the active or highest version of a plugin by ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="body">
    ///   The body containing the fields to update. Only the IsEnabled field is
    ///   supported for now.
    /// </param>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpPut("{pluginID}")]
    public ActionResult<PluginInfo> PutPluginByID(
        [FromRoute] Guid pluginID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdatePluginInfoBody body
    )
    {
        if (pluginManager.GetPluginInfo(pluginID) is not { } pluginInfo)
            return NotFound("Plugin not found");

        if (body.IsEnabled.HasValue)
            if (body.IsEnabled.Value)
                pluginInfo = pluginManager.EnablePlugin(pluginInfo);
            else
                pluginInfo = pluginManager.DisablePlugin(pluginInfo);

        return new PluginInfo(pluginInfo);
    }

    /// <summary>
    ///   Updates the active or highest version of a plugin by ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="body">
    ///   A JSON Patch Document containing the fields to update. Only the
    ///   IsEnabled field is supported for now.
    /// </param>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpPatch("{pluginID}")]
    public ActionResult<PluginInfo> PatchPluginByID(
        [FromRoute] Guid pluginID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<UpdatePluginInfoBody> body
    )
    {
        if (pluginManager.GetPluginInfo(pluginID) is not { } pluginInfo)
            return NotFound("Plugin not found");

        var updatePluginInfoBody = new UpdatePluginInfoBody
        {
            IsEnabled = pluginInfo.IsEnabled,
        };
        body.ApplyTo(updatePluginInfoBody);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return PutPluginByID(pluginID, updatePluginInfoBody);
    }

    /// <summary>
    ///   Uninstalls the active or highest version of a plugin by ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="purgeConfiguration">
    ///   Whether to purge the plugin configuration.
    /// </param>
    /// <returns>
    ///   No content.
    /// </returns>
    [HttpDelete("{pluginID}")]
    public ActionResult UninstallPluginByID([FromRoute] Guid pluginID, [FromQuery] bool purgeConfiguration = true)
    {
        if (pluginManager.GetPluginInfo(pluginID) is not { } pluginInfo)
            return NotFound("Plugin not found");

        pluginManager.UninstallPlugin(pluginInfo, purgeConfiguration);

        return NoContent();
    }

    /// <summary>
    ///   Gets the specific version of a plugin by ID and version.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="pluginVersion">
    ///   The plugin version.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpGet("{pluginID}/{pluginVersion}")]
    public ActionResult<PluginInfo> GetPluginByIDAndVersion([FromRoute] Guid pluginID, [FromRoute] Version pluginVersion)
        => pluginManager.GetPluginInfo(pluginID, pluginVersion) is { } pluginInfo
            ? new PluginInfo(pluginInfo)
            : NotFound("Plugin not found");

    /// <summary>
    ///   Gets the thumbnail for the specific version of a plugin by ID and
    ///   version.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="pluginVersion">
    ///   The plugin version.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpGet("{pluginID}/{pluginVersion}/Thumbnail")]
    public ActionResult GetThumbnailForPluginByIDAndVersion([FromRoute] Guid pluginID, [FromRoute] Version pluginVersion)
        => pluginManager.GetPluginInfo(pluginID, pluginVersion) is { Thumbnail: { } } pluginInfo
            ? File(pluginInfo.Thumbnail.GetStream(), pluginInfo.Thumbnail.MimeType)
            : NotFound("Plugin not found");

    /// <summary>
    ///   Updates the specific version of a plugin by ID and version.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="pluginVersion">
    ///   The plugin version.
    /// </param>
    /// <param name="body">
    ///   The body containing the fields to update. Only the IsEnabled field is
    ///   supported for now.
    /// </param>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpPut("{pluginID}/{pluginVersion}")]
    public ActionResult<PluginInfo> PutPluginByIDAndVersion(
        [FromRoute] Guid pluginID,
        [FromRoute] Version pluginVersion,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdatePluginInfoBody body
    )
    {
        if (pluginManager.GetPluginInfo(pluginID, pluginVersion) is not { } pluginInfo)
            return NotFound("Plugin not found");

        if (body.IsEnabled.HasValue)
            if (body.IsEnabled.Value)
                pluginInfo = pluginManager.EnablePlugin(pluginInfo);
            else
                pluginInfo = pluginManager.DisablePlugin(pluginInfo);

        return new PluginInfo(pluginInfo);
    }

    /// <summary>
    ///   Updates the specific version of a plugin by ID and version.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="pluginVersion">
    ///   The plugin version.
    /// </param>
    /// <param name="body">
    ///   A JSON Patch Document containing the fields to update. Only the
    ///   IsEnabled field is supported for now.
    /// </param>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> if a plugin is found.
    /// </returns>
    [HttpPatch("{pluginID}/{pluginVersion}")]
    public ActionResult<PluginInfo> PatchPluginByIDAndVersion(
        [FromRoute] Guid pluginID,
        [FromRoute] Version pluginVersion,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<UpdatePluginInfoBody> body
    )
    {
        if (pluginManager.GetPluginInfo(pluginID, pluginVersion) is not { } pluginInfo)
            return NotFound("Plugin not found");

        var updatePluginInfoBody = new UpdatePluginInfoBody
        {
            IsEnabled = pluginInfo.IsEnabled,
        };
        body.ApplyTo(updatePluginInfoBody);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return PutPluginByIDAndVersion(pluginID, pluginVersion, updatePluginInfoBody);
    }

    /// <summary>
    ///   Uninstalls the specific version of a plugin by ID and version.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="pluginVersion">
    ///   The plugin version.
    /// </param>
    /// <param name="purgeConfiguration">
    ///   Whether to purge the plugin configuration.
    /// </param>
    /// <returns>
    ///   No content.
    /// </returns>
    [HttpDelete("{pluginID}/{pluginVersion}")]
    public ActionResult DeletePluginByIDAndVersion(
        [FromRoute] Guid pluginID,
        [FromRoute] Version pluginVersion,
        [FromQuery] bool purgeConfiguration = true
    )
    {
        if (pluginManager.GetPluginInfo(pluginID, pluginVersion) is not { } pluginInfo)
            return NotFound("Plugin not found");

        pluginManager.UninstallPlugin(pluginInfo, purgeConfiguration);

        return NoContent();
    }

    /// <summary>
    ///   Gets all versions of a plugin by ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <returns>
    ///   A list of <see cref="PluginInfo"/> for the different versions if a plugin is found.
    /// </returns>
    [HttpGet("{pluginID}/All")]
    public ActionResult<List<PluginInfo>> GetAllPluginsByID([FromRoute] Guid pluginID)
        => pluginManager.GetPluginInfos(pluginID) is { } pluginInfos
            ? pluginInfos
                .Select(pluginInfo => new PluginInfo(pluginInfo))
                .ToList()
            : NotFound("Plugin not found");

    /// <summary>
    ///   Uninstalls all versions of a plugin by ID.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <param name="purgeConfiguration">
    ///   Whether to purge the plugin configuration.
    /// </param>
    /// <returns>
    ///   No content.
    /// </returns>
    [HttpDelete("{pluginID}/All")]
    public ActionResult UninstallAllPluginsByID([FromRoute] Guid pluginID, [FromQuery] bool purgeConfiguration = true)
    {
        if (pluginManager.GetPluginInfos(pluginID) is not { Count: > 0 } pluginInfos)
            return NotFound("Plugin not found");

        foreach (var pluginInfo in pluginInfos)
            pluginManager.UninstallPlugin(pluginInfo, purgeConfiguration);

        return NoContent();
    }
}
