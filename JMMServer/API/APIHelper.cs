using JMMContracts.PlexAndKodi;
namespace JMMServer.API
{
    public static class URLHelper
    {
        #region Contructors

        public static string ConstructUnsortUrl(bool short_url = false)
        {
            return URLHelper.ProperURL("api/metadata/" + (int)JMMType.GroupUnsort + "/0",  short_url);
        }

        public static string ConstructGroupIdUrl(string gid, bool short_url = false)
        {
            return URLHelper.ProperURL("/api/metadata/" + (int)JMMType.Group + "/" + gid, short_url);      
        }

        public static string ConstructSerieIdUrl(string sid, bool short_url = false)
        {

            return URLHelper.ProperURL("/api/metadata/" + (int)JMMType.Serie + "/" + sid,  short_url);
        }

        public static string ConstructVideoUrl(string vid, JMMType type, bool short_url = false)
        {
            return URLHelper.ProperURL("/api/metadata/" + (int)type + "/" + vid, short_url);
        }

        public static string ConstructFilterIdUrl(int groupfilter_id, bool short_url = false)
        {
            return URLHelper.ProperURL("api/metadata/" + (int)JMMType.GroupFilter + "/" + groupfilter_id,  short_url);    
        }

        public static string ConstructFiltersUrl(bool short_url = false)
        {
            return URLHelper.ProperURL("/api/filters/get", short_url);
        }

        public static string ConstructSearchUrl(string limit, string query, bool searchTag, bool short_url = false)
        {
            if (searchTag)
            {
                return URLHelper.ProperURL("/api/searchTag/" + limit + "/" + System.Net.WebUtility.UrlEncode(query), short_url);
            }
            else
            {
                return URLHelper.ProperURL("/api/search/" + limit + "/" + System.Net.WebUtility.UrlEncode(query), short_url);
            }
        }

        public static string ConstructPlaylistUrl(bool short_url = false)
        {
            return URLHelper.ProperURL("/api/metadata/" + (int)JMMType.Playlist + "/0", short_url);
        }

        public static string ConstructPlaylistIdUrl(int pid, bool short_url = false)
        {
            return URLHelper.ProperURL("/api/metadata/" + (int)JMMType.Playlist + "/" + pid, short_url);
        }

        public static string ConstructSupportImageLink(string name, bool short_url = false)
        {
            return URLHelper.ProperURL("api/image/support/"+ name, short_url);
        }

        public static string ConstructImageLinkFromRest(string path, bool short_url = false)
        {
            return ProperURL(ConvertRestImageToNonRestUrl(path),short_url);
        }

        #endregion

        #region Converters

        private static string ConvertRestImageToNonRestUrl(string url)
        {
            string link = url.ToLower();
            if (link.Contains("{scheme}://{host}:"))
            {
                link = link.Replace("{scheme}://{host}:", "");
                link = link.Substring(link.IndexOf("/") + 1);
                link = link.Replace("jmmserverrest/", "");

                if (link.Contains("getimage"))
                {
                    link = link.Replace("getimage", "api/image");
                }
                else if (link.Contains("getthumb"))
                {
                    link = link.Replace("getthumb", "api/thumb");
                    if (link.Contains(","))
                    {
                        link = link.Replace(',', '.');
                    }
                }
            }
            return link;
        }

        #endregion

        private static string ProperURL(string path, bool short_url = false)
        {           
            if (!short_url)
            {
                return Module.apiv2.Core.request.Url.Scheme + "://" + Module.apiv2.Core.request.Url.HostName + ":" + Module.apiv2.Core.request.Url.Port + "/" + path;
            }
            else
            {
                return "/" + path;
            }       
        }
    }
}
