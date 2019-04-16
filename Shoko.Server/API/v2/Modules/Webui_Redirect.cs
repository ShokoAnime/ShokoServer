using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Shoko.Server.API.v2.Modules
{
    [Route("/")]
    [ApiVersionNeutral]
    public class Webui_Redirect : Controller
    {
        [HttpGet]
        public ActionResult Index() => Redirect("/webui/index.html");
    }
}