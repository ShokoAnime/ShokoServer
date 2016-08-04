namespace JMMServer.API
{
    public class APIv2_unauth_Module: Nancy.NancyModule
    {
        public APIv2_unauth_Module(): base("/api")
        {
            Get["/"] = _ => { return @"<html><body><h1>JMMServer is running</h1></body></html>"; };
            Get["/version"] = x => { return GetVersion(); };
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
