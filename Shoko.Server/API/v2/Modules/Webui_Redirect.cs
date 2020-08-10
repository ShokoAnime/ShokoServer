using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.v2.Modules
{
    [Route("/")]
    [ApiVersionNeutral]
    [InitFriendly]
    [DatabaseBlockedExempt]
    public class Webui_Redirect : Controller
    {
        [HttpGet]
        public ActionResult Index() => Redirect("/webui/index.html");
    }
}