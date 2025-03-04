using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Server.API.Annotations;
using Shoko.Server.Settings;

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
[Authorize("admin")]
public class PluginController(ISettingsProvider settingsProvider, IPluginManager pluginManager) : BaseController(settingsProvider)
{
    [HttpGet]
    public ActionResult<List<PluginInfo>> GetPlugins()
        => pluginManager.GetPluginInfos()
            .Select(pluginInfo => new PluginInfo(pluginInfo))
            .ToList();

    [HttpGet("{pluginID:guid}")]
    public ActionResult<PluginInfo> GetPlugin([FromRoute] Guid pluginID)
        => pluginManager.GetPluginInfo(pluginID) is { } pluginInfo
            ? new PluginInfo(pluginInfo)
            : NotFound();
}
