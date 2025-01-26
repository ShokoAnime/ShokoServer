using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.v0.Controllers;

[Route("/")]
[ApiVersionNeutral]
[InitFriendly]
[DatabaseBlockedExempt]
public class IndexRedirectController : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return Redirect("/webui/index.html");
    }
}
