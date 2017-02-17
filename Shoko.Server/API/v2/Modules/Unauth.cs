using System.Collections.Generic;
using Pri.LongPath;
using Shoko.Server.API.v2.Models.core;

namespace Shoko.Server.API.v2.Modules
{
    public class Unauth: Nancy.NancyModule
    {
        public static int version = 1;

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
            version.name = "server";
            list.Add(version);

            version = new ComponentVersion();
            version.name = "auth_module";
            version.version = Auth.version.ToString();
            list.Add(version);

            version = new ComponentVersion();
            version.name = "common_module";
            version.version = Common.version.ToString();
            list.Add(version);

            version = new ComponentVersion();
            version.name = "core_module";
            version.version = Core.version.ToString();
            list.Add(version);

            version = new ComponentVersion();
            version.name = "database_module";
            version.version = Database.version.ToString();
            list.Add(version);

            version = new ComponentVersion();
            version.name = "dev_module";
            version.version = Dev.version.ToString();
            list.Add(version);

            version = new ComponentVersion();
            version.name = "unauth_module";
            version.version = Unauth.version.ToString();
            list.Add(version);

            version = new ComponentVersion();
            version.name = "webui_module";
            version.version = Webui.version.ToString();
            list.Add(version);

            if (File.Exists("webui//index.ver"))
            {
                string webui_version = File.ReadAllText("webui//index.ver");
                string[] versions = webui_version.Split('>');
                if (versions.Length == 2)
                {
                    version = new ComponentVersion();
                    version.name = "webui/" + versions[0];
                    version.version = versions[1];
                    list.Add(version);
                }
            }

            return list;
        }

    }
}
