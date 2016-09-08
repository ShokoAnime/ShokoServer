using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
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
            foreach (Video v in vids.Randomize())
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
            o.CopyTo(v);
            v.ReplaceSchemeHost();
            return v;
        }

	    // This should be the same as a ShallowCopy, but I did just learn the difference 2 days ago, so I might be wrong
	    public static void CopyTo<T>(this Video v, T o) where T : Video
	    {
		    o.AirDate = v.AirDate;
		    o.IsMovie = v.IsMovie;
		    o.Id = v.Id;
		    o.AnimeType = v.AnimeType;
		    o.Art = v.Art;
		    o.Url = v.Url;
		    o.Thumb = v.Thumb;
		    o.Banner = v.Banner;
		    o.ParentThumb = v.ParentThumb;
		    o.GrandparentThumb = v.GrandparentThumb;
		    o.ParentArt = v.ParentArt;
		    o.GrandparentArt = v.GrandparentArt;
		    o.RatingKey = v.RatingKey;
		    o.ParentRatingKey = v.ParentRatingKey;
		    o.GrandparentRatingKey = v.GrandparentRatingKey;
		    o.Key = v.Key;
		    o.ParentKey = v.ParentKey;
		    o.GrandparentKey = v.GrandparentKey;
		    o.Index = v.Index;
		    o.ParentIndex = v.ParentIndex;
		    o.Guid = v.Guid;
		    o.Type = v.Type;
		    o.Title = v.Title;
		    o.Title1 = v.Title1;
		    o.Title2 = v.Title2;
		    o.ParentTitle = v.ParentTitle;
		    o.GrandparentTitle = v.GrandparentTitle;
		    o.Summary = v.Summary;
		    o.Year = v.Year;
		    o.Duration = v.Duration;
		    o.EpisodeCount = v.EpisodeCount;
		    o.UpdatedAt = v.UpdatedAt;
		    o.AddedAt = v.AddedAt;
		    o.LastViewedAt = v.LastViewedAt;
		    o.OriginallyAvailableAt = v.OriginallyAvailableAt;
		    o.LeafCount = v.LeafCount;
		    o.ChildCount = v.ChildCount;
		    o.ViewedLeafCount = v.ViewedLeafCount;
		    o.OriginalTitle = v.OriginalTitle;
		    o.SourceTitle = v.SourceTitle;
		    o.Rating = v.Rating;
		    o.Season = v.Season;
		    o.ViewCount = v.ViewCount;
		    o.ViewOffset = v.ViewOffset;
		    o.PrimaryExtraKey = v.PrimaryExtraKey;
		    o.ChapterSource = v.ChapterSource;
		    o.Tagline = v.Tagline;
		    o.ContentRating = v.ContentRating;
		    o.Studio = v.Studio;
		    o.ExtraType = v.ExtraType;
		    o.EpisodeType = v.EpisodeType;
		    o.EpisodeNumber = v.EpisodeNumber;
		    o.Group = v.Group;
		    o.Medias = v.Medias;
		    o.Roles = v.Roles;
		    o.Extras = v.Extras;
		    o.Related = v.Related;
		    o.Tags = v.Tags;
		    o.Genres = v.Genres;
		    o.Titles = v.Titles;
		    o.Fanarts = v.Fanarts;
		    o.Banners = v.Banners;
	    }
    }
}