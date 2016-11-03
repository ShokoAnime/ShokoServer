using JMMContracts.PlexAndKodi;
using JMMServer.API.Model.common;
using JMMServer.Entities;
using JMMServer.Repositories;
using System.Collections.Generic;

namespace JMMServer.API
{
    public static class APIHelper
    {
        #region Contructors

        public static string ConstructUnsortUrl(bool short_url = false)
        {
            return APIHelper.ProperURL("api/metadata/" + (int)JMMType.GroupUnsort + "/0",  short_url);
        }

        public static string ConstructGroupIdUrl(string gid, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/metadata/" + (int)JMMType.Group + "/" + gid, short_url);      
        }

        public static string ConstructSerieIdUrl(string sid, bool short_url = false)
        {

            return APIHelper.ProperURL("/api/metadata/" + (int)JMMType.Serie + "/" + sid,  short_url);
        }

        public static string ConstructVideoUrl(string vid, JMMType type, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/metadata/" + (int)type + "/" + vid, short_url);
        }

        public static string ConstructFilterIdUrl(int groupfilter_id, bool short_url = false)
        {
            return APIHelper.ProperURL("api/metadata/" + (int)JMMType.GroupFilter + "/" + groupfilter_id,  short_url);    
        }

        public static string ConstructFiltersUrl(bool short_url = false)
        {
            return APIHelper.ProperURL("/api/filters/get", short_url);
        }

        public static string ConstructSearchUrl(string limit, string query, bool searchTag, bool short_url = false)
        {
            if (searchTag)
            {
                return APIHelper.ProperURL("/api/searchTag/" + limit + "/" + System.Net.WebUtility.UrlEncode(query), short_url);
            }
            else
            {
                return APIHelper.ProperURL("/api/search/" + limit + "/" + System.Net.WebUtility.UrlEncode(query), short_url);
            }
        }

        public static string ConstructPlaylistUrl(bool short_url = false)
        {
            return APIHelper.ProperURL("/api/metadata/" + (int)JMMType.Playlist + "/0", short_url);
        }

        public static string ConstructPlaylistIdUrl(int pid, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/metadata/" + (int)JMMType.Playlist + "/" + pid, short_url);
        }

        public static string ConstructSupportImageLink(string name, bool short_url = false)
        {
            return APIHelper.ProperURL("api/image/support/"+ name, short_url);
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

        public static RawFile RawFileFromVideoLocal(VideoLocal vl)
        {
            Model.common.RawFile file = new Model.common.RawFile();
            file.audio.Add("bitrate", vl.AudioBitrate);
            file.audio.Add("codec", vl.AudioCodec);

            file.video.Add("bitrate", vl.VideoBitrate);
            file.video.Add("bitdepth", vl.VideoBitDepth);
            file.video.Add("codec", vl.VideoCodec);
            file.video.Add("fps", vl.VideoFrameRate);
            file.video.Add("resolution", vl.VideoResolution);

            file.crc32 = vl.CRC32;
            file.ed2khash = vl.ED2KHash;
            file.md5 = vl.MD5;
            file.sha1 = vl.SHA1;

            file.created = vl.DateTimeCreated;
            file.updated = vl.DateTimeUpdated;
            file.duration = vl.Duration;

            file.filename = vl.FileName;
            file.size = vl.FileSize;
            file.hash = vl.Hash;
            file.hashsource = vl.HashSource;

            file.info = vl.Info;
            file.isignored = vl.IsIgnored;

            file.mediasize = vl.MediaSize;

            file.media = vl.Media;

            return file;
        }

        public static Filter FilterFromGroupFilter(GroupFilter gg, int uid)
        {
            Filter ob = new Filter();
            ob.type = "show";
            ob.name = gg.GroupFilterName;
            ob.id = gg.GroupFilterID;

            if (gg.GroupsIds.ContainsKey(uid))
            {
                HashSet<int> groups = gg.GroupsIds[uid];
                if (groups.Count != 0)
                {
                    ob.size = groups.Count;
                }
            }
            return ob;
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
