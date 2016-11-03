using JMMContracts.PlexAndKodi;
namespace JMMServer.API
{
    public static class URLHelper
    {
        public static string ConstructUnsortUrl(bool without_host = false)
        {
            return URLHelper.ProperURL("api/getmetadata/" + (int)JMMType.GroupUnsort + "/0", ServerSettings.JMMServerPort);
        }

        public static string ConstructGroupIdUrl(string gid)
        {
            return URLHelper.ProperURL("/api/getmetadata/" + (int)JMMType.Group + "/" + gid, ServerSettings.JMMServerPort);      
        }

        public static string ConstructSerieIdUrl(string sid)
        {

            return URLHelper.ProperURL("/api/getmetadata/" + (int)JMMType.Serie + "/" + sid, ServerSettings.JMMServerPort);
        }

        public static string ContructVideoUrl(string vid, JMMType type)
        {
            return URLHelper.ProperURL("/api/getmetadata/" + (int)type + "/" + vid, ServerSettings.JMMServerPort);
        }

        public static string ConstructFilterIdUrl(int groupfilter_id)
        {
            return URLHelper.ProperURL("api/getmetadata/" + (int)JMMType.GroupFilter + "/" + groupfilter_id, ServerSettings.JMMServerPort);    
        }

        public static string ConstructFiltersUrl()
        {
            return URLHelper.ProperURL("/api/filters/get", ServerSettings.JMMServerPort);
        }

        public static string ConstructSearchUrl(string limit, string query, bool searchTag)
        {
            if (searchTag)
            {
                return URLHelper.ProperURL("/api/searchTag/" + limit + "/" + System.Net.WebUtility.UrlEncode(query), ServerSettings.JMMServerPort);
            }
            else
            {
                return URLHelper.ProperURL("/api/search/" + limit + "/" + System.Net.WebUtility.UrlEncode(query), ServerSettings.JMMServerPort);
            }
        }

        public static string ConstructPlaylistUrl()
        {
            return URLHelper.ProperURL("/api/GetMetaData/" + (int)JMMType.Playlist + "/0", ServerSettings.JMMServerPort);
        }

        public static string ConstructPlaylistIdUrl(int pid)
        {
            return URLHelper.ProperURL("/api/GetMetaData/" + (int)JMMType.Playlist + "/" + pid, ServerSettings.JMMServerPort);
        }

        public static string ConstructSupportImageLink(string name)
        {
            return URLHelper.ProperURL("api/image/support/"+ name, ServerSettings.JMMServerPort);
        }

        public static string ConvertRestImageToNewUrl(string url)
        {
            string link = url;
            if (link.Contains("{SCHEME}://{HOST}:"))
            {
                link = link.Replace("{SCHEME}://{HOST}:", "");
                link = link.Substring(link.IndexOf("/"));
                link = link.ToLower().Replace("jmmserverrest/", "");
            }
            return link;
        }


        private static string ProperURL(string path, string port, bool without_host = false, bool external_ip = false)
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
