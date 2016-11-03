using Nancy;

namespace JMMServer.API.Module.apiv2
{
    public class Webui : Nancy.NancyModule
    {
        public Webui()
        {
            Get["/"] = _ => { return Response.AsRedirect("/webui/index.html"); };
        }
    }
}
