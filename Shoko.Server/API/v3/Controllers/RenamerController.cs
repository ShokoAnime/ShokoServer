using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Settings.Configuration;

namespace Shoko.Server.API.v3.Controllers
{

    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class RenamerController : BaseController
    {
        private IWritableOptions<PluginSettings> pluginSettings;

        public RenamerController(IWritableOptions<PluginSettings> pluginSettings)
        {
            this.pluginSettings = pluginSettings;
        }

        [HttpGet]
        public ActionResult<List<RenamerInfo>> Index()
        {
            return RenameFileHelper.Renamers.Select(r =>
            {
                return new RenamerInfo
                {
                    Description = r.Value.description,
                    Id = r.Key,
                    Enabled = !pluginSettings.Value.EnabledRenamers.ContainsKey(r.Key) || pluginSettings.Value.EnabledRenamers[r.Key], 
                    Priority = pluginSettings.Value.Priority.Contains(r.Key) ? pluginSettings.Value.Priority.IndexOf(r.Key) : int.MaxValue,
                };
            }).ToList();
        }

        [HttpDelete("{renamerId}")]
        public ActionResult Disable(string renamerId)
        {
            pluginSettings.Update(s => s.EnabledRenamers[renamerId] = false);

            return Ok();
        }

        [HttpPatch("{renamerId}")]
        public ActionResult SetPriority(string renamerId, [FromBody] int priority)
        {
            pluginSettings.Update(settings =>
            {
                if (settings.EnabledRenamers.TryGetValue(renamerId, out bool isEnabled) && !isEnabled)
                    settings.EnabledRenamers[renamerId] = true;

                settings.RenamerPriorities[renamerId] = priority;
            });

            return Ok();
        }
    }
}
