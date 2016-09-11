using JMMServer.API.Model;
using Nancy;
using System.Collections.Generic;

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
            List<ComponentVersion> list = new List<ComponentVersion>();
            ComponentVersion version = new ComponentVersion();
            version.version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
            version.name = "jmmserver";
            list.Add(version);
            version = new ComponentVersion();
            version.version = APIv2_core_Module.version.ToString();
            version.name = "apiv2";
            list.Add(version);
            return list;
        }
    }
}
