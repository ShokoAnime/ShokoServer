using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Repositories;

// ReSharper disable FunctionComplexityOverflow

namespace JMMServer.PlexAndKodi
{
    public static class Extensions
    {
        public static string ConstructUnsortUrl(this IProvider prov, int userid)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.GroupUnsort + "/0/");
        }

        public static string ConstructGroupIdUrl(this IProvider prov, int userid, string gid)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.Group + "/" + gid);
        }

        public static string ConstructSerieIdUrl(this IProvider prov, int userid, string sid)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.Serie + "/" + sid);
        }

        public static string ContructVideoUrl(this IProvider prov, int userid, string vid, JMMType type)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) type + "/" + vid);
        }

        public static string ConstructFilterIdUrl(this IProvider prov, int userid, int gfid)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.GroupFilter + "/" + gfid);
        }

        public static string ConstructFakeIosThumb(this IProvider prov, int userid, string thumburl, string arturl)
        {
            string r = Helper.Base64EncodeUrl(thumburl + "|" + arturl);
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.FakeIosThumb + "/" + r + "/0");
        }

        public static string ConstructFiltersUrl(this IProvider prov, int userid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.ServiceAddress + "/GetFilters/" + userid);
        }

        public static string ConstructSearchUrl(this IProvider prov, string userid, string limit, string query,
            bool searchTag)
        {
            if (searchTag)
                return Helper.ServerUrl(prov.ServicePort,
                    prov.ServiceAddress + "/SearchTag/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" +
                    WebUtility.UrlEncode(query));
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Search/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" +
                WebUtility.UrlEncode(query));
        }

        public static string ConstructPlaylistUrl(this IProvider prov, int userid)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.Playlist + "/0");
        }

        public static string ConstructPlaylistIdUrl(this IProvider prov, int userid, int pid)
        {
            return Helper.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/GetMetadata/" + userid + "/" + (int) JMMType.Playlist + "/" + pid);
        }


        public static string GenPoster(this ImageDetails im, string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.ImageID == 0))
                return Helper.ConstructSupportImageLink(fallbackimage);
            return Helper.ConstructThumbLink((int) im.ImageType, im.ImageID);
        }

        public static string GenArt(this ImageDetails im)
        {
            if (im == null)
                return null;
            return Helper.ConstructImageLink((int) im.ImageType, im.ImageID);
        }

        public static string GenPoster(this MetroContract_Anime_Episode im, string fallbackimage = "plex_404.png")
        {
            if ((im == null) || (im.ImageID == 0))
                return Helper.ConstructSupportImageLinkTV(fallbackimage);
            return Helper.ConstructTVThumbLink((int) im.ImageType, im.ImageID);
        }

        public static string GenPoster(this Contract_AniDB_Anime_DefaultImage im, string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.AnimeID == 0))
                return Helper.ConstructSupportImageLink(fallbackimage);
            return Helper.ConstructThumbLink((int) im.ImageType, im.AnimeID);
        }

        public static string GenArt(this Contract_AniDB_Anime_DefaultImage im)
        {
            if (im == null)
                return null;
            return Helper.ConstructImageLink((int) im.ImageType, im.AnimeID);
        }

        public static void RandomizeArt(this MediaContainer m, List<Video> vids)
        {
            foreach (Video v in vids.Randomize(123456789))
            {
                if (v.Art != null)
                {
                    m.Art = Helper.ReplaceSchemeHost(v.Art);
                    break;
                }
            }
        }

        public static string ToPlexDate(this DateTime dt)
        {
            return dt.Year.ToString("0000") + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00");
        }


        public static string ToUnixTime(this DateTime v)
        {
            return ((long) v.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        public static void GenerateKey(this Video v, IProvider prov, int userid)
        {
            switch ((JMMContracts.PlexAndKodi.AnimeTypes)Enum.Parse(typeof(JMMContracts.PlexAndKodi.AnimeTypes),v.AnimeType, true))
            {
                case JMMContracts.PlexAndKodi.AnimeTypes.AnimeGroup:
                    v.Key = prov.ConstructGroupIdUrl(userid, v.Id);
                    break;
                case JMMContracts.PlexAndKodi.AnimeTypes.AnimeSerie:
                    v.Key = prov.ConstructSerieIdUrl(userid, v.Id);
                    break;
                case JMMContracts.PlexAndKodi.AnimeTypes.AnimeEpisode:
                case JMMContracts.PlexAndKodi.AnimeTypes.AnimeFile:
                    Helper.AddLinksToAnimeEpisodeVideo(prov, v, userid);
                    AddResumePosition(v,prov,userid);
                    break;
            }
        }

        public static void AddResumePosition(this Video v, IProvider prov, int userid)
        {
            switch (
                (JMMContracts.PlexAndKodi.AnimeTypes)
                    Enum.Parse(typeof(JMMContracts.PlexAndKodi.AnimeTypes), v.AnimeType, true))
            {
                case JMMContracts.PlexAndKodi.AnimeTypes.AnimeEpisode:
                    if (v.Medias!=null)
                    {
                        VideoLocalRepository vrepo=new VideoLocalRepository();
                        VideoLocal_User vl=v.Medias.Select(a=>vrepo.GetByID(int.Parse(a.Id))).Where(a => a != null).Select(a => a.GetUserRecord(userid))
                                .Where(a => a != null)
                                .OrderByDescending(a => a.ResumePosition)
                                .FirstOrDefault();                       
                        if (vl != null && vl.ResumePosition > 0)
                        {
                            v.ViewOffset = vl.ResumePosition.ToString();
                            if (vl.WatchedDate.HasValue)
                                v.LastViewedAt = vl.WatchedDate.Value.ToUnixTime();
                        }
                    }
                    break;
                case JMMContracts.PlexAndKodi.AnimeTypes.AnimeFile:
                    int vid = int.Parse(v.Id); //This suxx, but adding regeneration at videolocal_user is worst.
                    VideoLocal_User vl2 = new VideoLocalRepository().GetByID(vid)?.GetUserRecord(userid);
                    if (vl2 != null && vl2.ResumePosition > 0)
                    {
                        v.ViewOffset = vl2.ResumePosition.ToString();
                        if (vl2.WatchedDate.HasValue)
                            v.LastViewedAt = vl2.WatchedDate.Value.ToUnixTime();
                    }
                    break;
            }
        }

        public static void Add(this List<Video> l, IProvider prov, Video m, BreadCrumbs info, bool noimage = false,
            bool noart = false)
        {
            m.ReplaceSchemeHost();
            info?.Update(m, noart).FillInfo(prov, m, noimage, true);
            l.Add(m);
        }

        public static void EppAdd(this List<Video> l, IProvider prov, Video m, BreadCrumbs info, bool noimage = false)
        {
            m.ReplaceSchemeHost();
            if (info != null)
            {
                info.FillInfo(prov, m, noimage, true);
                m.Thumb = info.Thumb;
                m.Art = info.Art;
                m.Title = info.Title;
            }
            l.Add(m);
        }

        public static void EppAdd(this List<Directory> l, IProvider prov, Directory m, BreadCrumbs info,
            bool noimage = false)
        {
            m.ReplaceSchemeHost();
            if (info != null)
            {
                info.FillInfo(prov, m, noimage, true);
                m.Thumb = info.Thumb;
                m.Art = info.Art;
                m.Title = info.Title;
            }
            l.Add(m);
        }

        public static void Add(this List<Directory> l, IProvider prov, Directory m, BreadCrumbs info,
            bool noimage = false)
        {
            m.ReplaceSchemeHost();
            info?.Update(m).FillInfo(prov, m, noimage, true);
            l.Add(m);
        }

        public static void ReplaceSchemeHost(this Video o)
        {
            o.Url = Helper.ReplaceSchemeHost(o.Url);
            o.Thumb = Helper.ReplaceSchemeHost(o.Thumb);
            o.ParentThumb = Helper.ReplaceSchemeHost(o.ParentThumb);
            o.GrandparentThumb = Helper.ReplaceSchemeHost(o.GrandparentThumb);
            o.Art = Helper.ReplaceSchemeHost(o.Art);
            o.ParentArt = Helper.ReplaceSchemeHost(o.ParentArt);
            o.GrandparentArt = Helper.ReplaceSchemeHost(o.GrandparentArt);
            if (o.Roles != null)
            {
                foreach (RoleTag r in o.Roles)
                {
                    if (!string.IsNullOrEmpty(r.RolePicture))
                        r.RolePicture = Helper.ReplaceSchemeHost(r.RolePicture);
                    if (!string.IsNullOrEmpty(r.TagPicture))
                        r.TagPicture = Helper.ReplaceSchemeHost(r.TagPicture);
                }
            }
        }

        public static T Clone<T>(this Video o) where T : Video, new()
        {
            T v = new T();
            o.ShallowCopyTo(v);
            v.ReplaceSchemeHost();
            return v;
        }
    }
}