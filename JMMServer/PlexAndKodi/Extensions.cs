using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.ImageDownload;

// ReSharper disable FunctionComplexityOverflow
namespace JMMServer.PlexAndKodi
{
    public static class Extensions
    {

        public static string ConstructUnsortUrl(this IProvider prov, int userid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.GroupUnsort + "/0/");
        }
        public static string ConstructGroupIdUrl(this IProvider prov, int userid, int gid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.Group + "/" + gid);
        }
        public static string ConstructSerieIdUrl(this IProvider prov, int userid, string sid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.Serie + "/" + sid);
        }

        public static string ContructVideoUrl(this IProvider prov, int userid, int vid, JMMType type)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)type + "/" + vid);
        }
        public static string ConstructFilterIdUrl(this IProvider prov, int userid, int gfid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.GroupFilter + "/" + gfid);
        }

        public static string ConstructFakeIosThumb(this IProvider prov, int userid, string url)
        {
            string r = Helper.Base64EncodeUrl(url);
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.FakeIosThumb + "/" + r + "/0");

        }
        public static string ConstructFiltersUrl(this IProvider prov, int userid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetFilters/" + userid);
        }
        public static string ConstructSearchUrl(this IProvider prov, string userid, string limit, string query, bool searchTag)
        {
            if (searchTag)
                return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/SearchTag/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" + WebUtility.UrlEncode(query));
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/Search/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" + WebUtility.UrlEncode(query));

        }
        public static string ConstructPlaylistUrl(this IProvider prov, int userid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.Playlist + "/0");
        }

        public static string ConstructPlaylistIdUrl(this IProvider prov, int userid, int pid)
        {
            return Helper.ServerUrl(prov.ServicePort, prov.Serviceddress + "/GetMetadata/" + userid + "/" + (int)JMMType.Playlist + "/" + pid);
        }


        public static string GenPoster(this ImageDetails im, string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.ImageID == 0))
                return Helper.ConstructSupportImageLink(fallbackimage);
            return Helper.ConstructThumbLink((int)im.ImageType, im.ImageID);
        }

        public static string GenArt(this ImageDetails im)
        {
            if (im == null)
                return null;
            return Helper.ConstructImageLink((int) im.ImageType, im.ImageID);
        }

        public static string GenPoster(this MetroContract_Anime_Episode im, string fallbackimage = "plex_404.png")
        {
            if ((im == null) || (im.ImageID==0))
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
            return Helper.ConstructImageLink((int)im.ImageType, im.AnimeID);
        }

        public static string ToPlexDate(this DateTime dt)
        {
            return dt.Year.ToString("0000") + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00");
        }


        public static string ToUnixTime(this DateTime v)
        {
            return ((long) (v.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        public static Hub Clone(this Hub o)
        {
            Hub h = new Hub();
            h.HubIdentifier = o.HubIdentifier;
            h.Key = Helper.ReplaceSchemeHost(o.Key);
            h.More = o.More;
            h.Size = o.Size;
            h.Title = o.Title;
            h.Type = o.Type;
            return h;
        }

 


        public static void Add(this List<Video> l, IProvider prov, Video m, Breadcrumbs info, bool noimage=false, bool noart=false)
        {
            info?.Update(m,noart).FillInfo(prov,m, noimage, true);
            l.Add(m);
        }

        public static void EppAdd(this List<Video> l, IProvider prov, Video m, Breadcrumbs info, bool noimage = false)
        {
            if (info != null)
            {
                info.FillInfo(prov, m, noimage, true);
                m.Thumb = info.Thumb;
                m.Art = info.Art;
                m.Title = info.Title;
            }
            l.Add(m);
        }
        public static void EppAdd(this List<Directory> l, IProvider prov, Directory m, Breadcrumbs info, bool noimage = false)
        {
            if (info != null)
            {
                info.FillInfo(prov, m, noimage, true);
                m.Thumb = info.Thumb;
                m.Art = info.Art;
                m.Title = info.Title;
            }
            l.Add(m);
        }
        public static void Add(this List<Directory> l, IProvider prov, Directory m, Breadcrumbs info, bool noimage = false)
        {
            info?.Update(m).FillInfo(prov, m, noimage, true);
            l.Add(m);
        }
        public static Video Clone(this Video o)
        {
            Video v;
            if (o is Directory)
                v = new Directory();
            else
                v = new Video();
            v.AddedAt = o.AddedAt;
            v.AirDate = o.AirDate;
            v.Art = Helper.ReplaceSchemeHost(o.Art);
            v.ParentArt= Helper.ReplaceSchemeHost(o.ParentArt);
            v.GrandparentArt = Helper.ReplaceSchemeHost(o.GrandparentArt);
            v.ChapterSource = o.ChapterSource;
            v.ContentRating = o.ContentRating;
            v.Duration = o.Duration;
            v.EpisodeNumber = o.EpisodeNumber;
            v.EpisodeCount = o.EpisodeCount;
            v.ExtraType = o.ExtraType;
            if (o.Extras != null)
            {
                v.Extras = new Extras();
                v.Extras.Size = o.Extras.Size;
                if (o.Extras.Videos != null)
                {
                    v.Extras.Videos = new List<Video>();
                    o.Extras.Videos.ForEach(a => v.Extras.Videos.Add(a.Clone()));
                }
            }
            v.Genres = o.Genres;
            v.GrandparentKey = o.GrandparentKey;
            v.GrandparentRatingKey = o.GrandparentRatingKey;
            v.GrandparentTitle = o.GrandparentTitle;
            v.Group = o.Group;
            v.Guid = o.Guid;
            v.Index = o.Index;
            v.Key = Helper.ReplaceSchemeHost(o.Key);
            v.LeafCount = o.LeafCount;
            v.Medias = o.Medias;
            v.OriginalTitle = o.OriginalTitle;
            v.OriginallyAvailableAt = o.OriginallyAvailableAt;
            v.ParentIndex = o.ParentIndex;
            v.ParentKey = o.ParentKey;
            v.ParentRatingKey = o.ParentRatingKey;
            v.ParentTitle = o.ParentTitle;
            v.PrimaryExtraKey = o.PrimaryExtraKey;
            v.Rating = o.Rating;
            v.RatingKey = o.RatingKey;
            if (o.Related != null)
            {
                v.Related = new List<Hub>();
                o.Related.ForEach(a => v.Related.Add(a.Clone()));
            }
            v.Roles = o.Roles;
            v.Season = o.Season;
            v.SourceTitle = o.SourceTitle;
            v.Summary = o.Summary;
            v.Tagline = o.Tagline;
            v.Tags = o.Tags;
            v.Thumb = Helper.ReplaceSchemeHost(o.Thumb);
            v.ParentThumb = Helper.ReplaceSchemeHost(o.ParentThumb);
            v.GrandparentThumb = Helper.ReplaceSchemeHost(o.GrandparentThumb);
            v.Title = o.Title;
            v.Type = o.Type;
            v.UpdatedAt = o.UpdatedAt;
            v.Url = Helper.ReplaceSchemeHost(o.Url);
            v.ViewCount = o.ViewCount;
            v.ViewOffset = o.ViewOffset;
            v.ViewedLeafCount = o.ViewedLeafCount;
            v.Year = o.Year;
            return v;
        }


    }
}
