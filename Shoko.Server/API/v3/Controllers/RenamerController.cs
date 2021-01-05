using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.API.v3.Controllers
{

    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class RenamerController : BaseController
    {
        [HttpGet]
        public ActionResult<List<RenamerInfo>> Index()
        {
            return RenameFileHelper.Renamers.Select(r =>
            {
                return new RenamerInfo
                {
                    Description = r.Value.description,
                    Id = r.Key,
                    Enabled = ServerSettings.Instance.Plugins.EnabledRenamers.ContainsKey(r.Key) ? ServerSettings.Instance.Plugins.EnabledRenamers[r.Key] : true, 
                    Priority = ServerSettings.Instance.Plugins.Priority.Contains(r.Key) ? ServerSettings.Instance.Plugins.Priority.IndexOf(r.Key) : int.MaxValue,
                };
            }).ToList();
        }

        [HttpDelete("{renamerId}")]
        public ActionResult Disable(string renamerId)
        {
            ServerSettings.Instance.Plugins.EnabledRenamers[renamerId] = false;
            ServerSettings.Instance.SaveSettings();
            return Ok();
        }

        [HttpPatch("{renamerId}")]
        public ActionResult SetPriority(string renamerId, [FromBody] int priority)
        {
            if (ServerSettings.Instance.Plugins.EnabledRenamers.TryGetValue(renamerId, out bool isEnabled) && !isEnabled)
                ServerSettings.Instance.Plugins.EnabledRenamers[renamerId] = true;

            ServerSettings.Instance.Plugins.RenamerPriorities[renamerId] = priority;
            ServerSettings.Instance.SaveSettings();

            return Ok();
        }
    }
}
