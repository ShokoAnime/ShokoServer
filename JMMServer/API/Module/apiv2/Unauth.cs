using JMMServer.API.Model.core;
using Nancy;
using System.Collections.Generic;

namespace JMMServer.API.Module.apiv2
{
    public class Unauth: Nancy.NancyModule
    {
        public Unauth()
        {
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
            version.version = version.ToString();
            version.name = "apiv2";
            list.Add(version);
            return list;
        }
    }
}
