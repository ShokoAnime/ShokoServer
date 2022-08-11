using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Shoko.Server.API.v3.Controllers
{

    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class RenamerController : BaseController
    {
        /// <summary>
        /// Get a list of all <see cref="Models.Shoko.Renamer"/>s.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<Models.Shoko.Renamer>> GetAllRenamers()
        {
            return RenameFileHelper.Renamers
                .Select(p => new Models.Shoko.Renamer(p.Key, p.Value))
                .ToList();
        }

        /// <summary>
        /// Get a list of all the  setting descriptors for a renamer to render the settings UI.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Renamer/{renamerID}/SettingDescriptors")]
        public ActionResult<List<Models.Shoko.Renamer.SettingDescriptior>> GetRenamerSettings()
        {
            return null;
        }
    }
}
