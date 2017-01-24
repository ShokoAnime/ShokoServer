using JMMContracts.PlexAndKodi;
using JMMServer.API.Model.common;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.PlexAndKodi;
using JMMServer.Repositories;
using System;
using System.Collections.Generic;

namespace JMMServer.API
{
    public static class APIHelper
    {
        #region Contructors

        public static string ConstructUnsortUrl(bool short_url = false)
        {
            return APIHelper.ProperURL("/api/filter?id=" + (int)JMMType.GroupUnsort,  short_url);
        }

        [Obsolete]
        public static string ConstructGroupIdUrl(string gid, bool short_url = false)
        {
            return APIHelper.ProperURL("__TEST__" + (int)JMMType.Group + "/" + gid, short_url);      
        }

        [Obsolete]
        public static string ConstructSerieIdUrl(string sid, bool short_url = false)
        {

            return APIHelper.ProperURL("__TEST__" + (int)JMMType.Serie + " / " + sid,  short_url);
        }

        [Obsolete]
        public static string ConstructVideoUrl(string vid, JMMType type, bool short_url = false)
        {
            return APIHelper.ProperURL("__TEST__" + (int)type + "/" + vid, short_url);
        }

        public static string ConstructFilterIdUrl(int groupfilter_id, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/filter?id=" + groupfilter_id,  short_url);    
        }

        [Obsolete]
        public static string ConstructFiltersUrl(bool short_url = false)
        {
            return APIHelper.ProperURL("__TEST__", short_url);
        }

        [Obsolete]
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

        [Obsolete]
        public static string ConstructPlaylistUrl(bool short_url = false)
        {
            return APIHelper.ProperURL("/api/metadata/" + (int)JMMType.Playlist + "/0", short_url);
        }

        [Obsolete]
        public static string ConstructPlaylistIdUrl(int pid, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/metadata/" + (int)JMMType.Playlist + "/" + pid, short_url);
        }

        public static string ConstructSupportImageLink(string name, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/image/support/"+ name, short_url);
        }

        public static string ConstructImageLinkFromRest(string path, bool short_url = false)
        {
            return APIHelper.ProperURL(ConvertRestImageToNonRestUrl(path),short_url);
        }

        public static string ConstructImageLinkFromTypeAndId(int type, int id, bool short_url = false)
        {
            return APIHelper.ProperURL("/api/image/" + type.ToString() + "/" + id.ToString());
        }

        public static string ConstructVideoLocalStream(int userid, string vid, string name, bool autowatch)
        {
            return ProperURL(int.Parse(ServerSettings.JMMServerFilePort), "/videolocal/" + userid + "/" + (autowatch ? "1" : "0") + "/" + vid + "/" + name, false);
        }

        #endregion

        #region Converters

        private static string ConvertRestImageToNonRestUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                string link = url.ToLower();
                if (link.Contains("{scheme}://{host}:"))
                {
                    link = link.Replace("{scheme}://{host}:", "");
                    link = link.Substring(link.IndexOf("/") + 1);
                    link = link.Replace("jmmserverrest/", "");

                    if (link.Contains("getimage"))
                    {
                        link = link.Replace("getimage", "/api/image");
                    }
                    else if (link.Contains("getthumb"))
                    {
                        link = link.Replace("getthumb", "/api/thumb");
                        if (link.Contains(","))
                        {
                            link = link.Replace(',', '.');
                        }
                    }
                }
                return link;
            }
            else
            { return null; }
        }

        public static Filter FilterFromGroupFilter(GroupFilter gg, int uid)
        {
            Filter ob = new Filter();
            ob.name = gg.GroupFilterName;
            ob.id = gg.GroupFilterID;
            ob.url = APIHelper.ConstructFilterIdUrl(gg.GroupFilterID);

            if (gg.GroupsIds.ContainsKey(uid))
            {
                HashSet<int> groups = gg.GroupsIds[uid];
                if (groups.Count != 0)
                {
                    ob.size = groups.Count;
                    ob.viewed = 0;

                    foreach (int grp in groups)
                    {
                        AnimeGroup ag = RepoFactory.AnimeGroup.GetByID(grp);
                        Video v = ag.GetPlexContract(uid);
                        if (v?.Art != null && v.Thumb != null)
                        {
                            ob.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(v.Art), index = 0 });
                            ob.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(v.Thumb), index = 0 });
                            break;
                        }
                    }
                }
            }
            return ob;
        }

        public static Filter FilterFromAnimeGroup(AnimeGroup grp, int uid)
        {
            Filter ob = new Filter();
            ob.name = grp.GroupName;
            ob.id = grp.AnimeGroupID;
            ob.url = APIHelper.ConstructFilterIdUrl(grp.AnimeGroupID);
            ob.size = -1;
            ob.viewed = -1;

            foreach (AnimeSeries ser in grp.GetSeries().Randomize())
            {
                AniDB_Anime anim = ser.GetAnime();
                if (anim != null)
                {
                    ImageDetails fanart = anim.GetDefaultFanartDetailsNoBlanks();
                    ImageDetails banner = anim.GetDefaultWideBannerDetailsNoBlanks();

                    if (fanart != null)
                    {
                        ob.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromTypeAndId((int)fanart.ImageType, fanart.ImageID), index = ob.art.fanart.Count });
                        ob.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromTypeAndId((int)fanart.ImageType, fanart.ImageID), index = ob.art.thumb.Count });
                    }

                    if (banner!= null)
                    {
                        ob.art.banner.Add(new Art() { url = APIHelper.ConstructImageLinkFromTypeAndId((int)banner.ImageType, banner.ImageID), index = ob.art.banner.Count });
                    }

                    if (ob.art.fanart.Count > 0)
                    {
                        break;
                    }
                }
            }
            return ob;
        }

        #endregion

        private static string ProperURL(string path, bool short_url = false)
        {
            return ProperURL(Module.apiv2.Core.request.Url.Port, path, short_url);
        }

        private static string ProperURL(int? port, string path, bool short_url = false)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!short_url)
                {
                    return Module.apiv2.Core.request.Url.Scheme + "://" + Module.apiv2.Core.request.Url.HostName + ":" + port + path;
                }
                else
                {
                    return path;
                }
            }
            else
            {
                return "";
            }      
        }
    }
}
