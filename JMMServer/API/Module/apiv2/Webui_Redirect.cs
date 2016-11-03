using Nancy;

namespace JMMServer.API.Module.apiv2
{
    public class Webui_Redirect : Nancy.NancyModule
    {
        public Webui_Redirect()
        {
            Get["/"] = _ => { return Response.AsRedirect("/webui/index.html"); };
        }
    }
}
