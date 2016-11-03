using JMMContracts.PlexAndKodi;
namespace JMMServer.API
{
    public static class URLHelper
    {
        public static string ConstructUnsortUrl(bool without_host = false)
        {
            return URLHelper.ProperURL("/api/getmetadata/" + (int)JMMType.GroupUnsort + "/0", ServerSettings.JMMServerPort);
        }

        public static string ConstructFilterIdUrl(int groupfilter_id)
        {
            return URLHelper.ProperURL("/api/getmetadata/" + (int)JMMType.GroupFilter + "/" + groupfilter_id, ServerSettings.JMMServerPort);    
        }

        public static string ProperURL(string path, string port, bool without_host = false, bool external_ip = false)
        {
            string host = "";
            
            if (!without_host)
            {
                if (external_ip)
                {
                    System.Net.IPAddress ip = FileServer.FileServer.GetExternalAddress();
                    if (ip != null)
                    {
                        host = ip.ToString();
                    }
                }
                else
                {
                    host = API.Module.apiv2.Core.request.Url.HostName;
                }

                return Module.apiv2.Core.request.Url.Scheme + "://" + host + ":" + port + "/" + path;
            }
            else
            {
                return "/" + path;
            }       
        }
    }
}
