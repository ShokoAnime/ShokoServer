using System.Threading.Tasks;
using Nancy;

namespace Shoko.Server.API.v2.Modules
{
    public class Webui_Redirect : Nancy.NancyModule
    {
        public Webui_Redirect()
        {
            Get("/", async (x,ct) => await Task.Factory.StartNew(() => Response.AsRedirect("/webui/index.html"), ct));
        }
    }
}