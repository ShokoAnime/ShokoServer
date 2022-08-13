using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    /// <summary>
    /// The WebUI spesific controller. Only WebUI should use these endpoints.
    /// They may break at any time if the WebUI client needs to change something,
    /// and is therefore unsafe for other clients.
    /// </summary>
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [DatabaseBlockedExempt]
    [InitFriendly]
    public class WebUIController : BaseController
    {
        [HttpPost("GroupView")]
        public ActionResult<List<WebUI.WebUIGroupExtra>> GetGroupView([FromBody] WebUI.Input.WebUIGroupViewBody body)
        {
            var user = User;
            return body.GroupIDs
                .Select(groupID =>
                {
                    var group = RepoFactory.AnimeGroup.GetByID(groupID);
                    if (group == null || !user.AllowedGroup(group))
                        return null;

                    var series = group.GetMainSeries();
                    var anime = series?.GetAnime();
                    if (series == null || anime == null)
                        return null;

                    return new WebUI.WebUIGroupExtra(HttpContext, group, series, anime);
                })
                .ToList();
        }
    }
}