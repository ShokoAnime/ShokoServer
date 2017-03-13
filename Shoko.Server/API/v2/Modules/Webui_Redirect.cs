using Nancy;

namespace Shoko.Server.API.v2.Modules
{
    public class Webui_Redirect : Nancy.NancyModule
    {
        public Webui_Redirect()
        {
            Get["/"] = _ => { return Response.AsRedirect("/webui/index.html"); };
        }
    }
}