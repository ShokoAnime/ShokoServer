using System.Collections.Generic;
using System.Threading.Tasks;
using Pri.LongPath;
using Shoko.Server.API.v2.Models.core;

namespace Shoko.Server.API.v2.Modules
{
    public class Unauth : Nancy.NancyModule
    {
        public static int version = 1;

        public Unauth()
        {
            Get["/api/version", true] = async (x,ct) => await Task.Factory.StartNew(GetVersion, ct);
        }

        /// <summary>
        /// Return current version of JMMServer
        /// </summary>
        /// <returns></returns>
        private object GetVersion()
        {
            List<ComponentVersion> list = new List<ComponentVersion>();

            ComponentVersion version = new ComponentVersion
            {
                version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(),
                name = "server"
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "auth_module",
                version = Auth.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "common_module",
                version = Common.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "core_module",
                version = Core.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "database_module",
                version = Database.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "dev_module",
                version = Dev.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "unauth_module",
                version = Unauth.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "webui_module",
                version = Webui.version.ToString()
            };
            list.Add(version);

            if (File.Exists("webui//index.ver"))
            {
                string webui_version = File.ReadAllText("webui//index.ver");
                string[] versions = webui_version.Split('>');
                if (versions.Length == 2)
                {
                    version = new ComponentVersion
                    {
                        name = "webui/" + versions[0],
                        version = versions[1]
                    };
                    list.Add(version);
                }
            }

            return list;
        }
    }
}