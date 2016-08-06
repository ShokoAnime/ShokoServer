using Nancy;

namespace JMMServer.API
{
    public class APIv2_unauth_Module: Nancy.NancyModule
    {
        public APIv2_unauth_Module()
        {
            Get["/"] = _ => { return Response.AsRedirect("/webui/index.html"); };
            Get["/api/version"] = x => { return GetVersion(); };
        }

        /// <summary>
        /// Return current version of JMMServer
        /// </summary>
        /// <returns></returns>
        private object GetVersion()
        {
            return "{\"version\":\"" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString() + "\", \"api\":" + APIv2_core_Module. version.ToString() + "}";
        }
    }
}
