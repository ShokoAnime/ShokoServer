using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RenamerController : BaseController
{
    [HttpGet]
    public ActionResult<List<RenamerInfo>> Index()
    {
        var settings = SettingsProvider.GetSettings();
        return RenameFileHelper.Renamers.Select(r =>
        {
            return new RenamerInfo
            {
                Description = r.Value.description,
                Id = r.Key,
                Enabled =
                    !settings.Plugins.EnabledRenamers.ContainsKey(r.Key) || settings.Plugins.EnabledRenamers[r.Key],
                Priority = settings.Plugins.Priority.Contains(r.Key)
                    ? settings.Plugins.Priority.IndexOf(r.Key)
                    : int.MaxValue
            };
        }).ToList();
    }

    [HttpDelete("{renamerID}")]
    public ActionResult Disable(string renamerID)
    {
        var settings = SettingsProvider.GetSettings();
        settings.Plugins.EnabledRenamers[renamerID] = false;
        SettingsProvider.SaveSettings();
        return Ok();
    }

    [HttpPatch("{renamerID}")]
    public ActionResult SetPriority(string renamerID, [FromBody] int priority)
    {
        var settings = SettingsProvider.GetSettings();
        if (settings.Plugins.EnabledRenamers.TryGetValue(renamerID, out var isEnabled) && !isEnabled)
        {
            settings.Plugins.EnabledRenamers[renamerID] = true;
        }

        settings.Plugins.RenamerPriorities[renamerID] = priority;
        SettingsProvider.SaveSettings();

        return Ok();
    }

    public RenamerController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
