using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JMMContracts;
using JMMContracts.PlexContracts;
using JMMServer.ImageDownload;
// ReSharper disable FunctionComplexityOverflow
namespace JMMServer.Plex
{
    public static class PlexExtensions
    {
        public static string GenPoster(this ImageDetails im, string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.ImageID == 0))
                return PlexHelper.ConstructSupportImageLink(fallbackimage);
            return PlexHelper.ConstructThumbLink((int)im.ImageType, im.ImageID);
        }

        public static string GenArt(this ImageDetails im)
        {
            if (im == null)
                return null;
            return PlexHelper.ConstructImageLink((int) im.ImageType, im.ImageID);
        }

        public static string GenPoster(this MetroContract_Anime_Episode im, string fallbackimage = "plex_404.png")
        {
            if ((im == null) || (im.ImageID==0))
                return PlexHelper.ConstructSupportImageLinkTV(fallbackimage);
            return PlexHelper.ConstructTVThumbLink((int) im.ImageType, im.ImageID);
        }
        public static string GenPoster(this Contract_AniDB_Anime_DefaultImage im, string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.AnimeID == 0))
                return PlexHelper.ConstructSupportImageLink(fallbackimage);
            return PlexHelper.ConstructThumbLink((int) im.ImageType, im.AnimeID);
        }

        public static string GenArt(this Contract_AniDB_Anime_DefaultImage im)
        {
            if (im == null)
                return null;
            return PlexHelper.ConstructImageLink((int)im.ImageType, im.AnimeID);
        }

        public static string ToPlexDate(this DateTime dt)
        {
            return dt.Year.ToString("0000") + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00");
        }
        public static void CopyTo(this object s, object d)
        {
            foreach (PropertyInfo pis in s.GetType().GetProperties())
            {
                foreach (PropertyInfo pid in d.GetType().GetProperties())
                {
                    if (pid.Name == pis.Name)
                        (pid.GetSetMethod()).Invoke(d, new[] { pis.GetGetMethod().Invoke(s, null) });
                }
            };
        }
        public static string ToUnixTime(this DateTime v)
        {
            return ((long) (v.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        public static Hub Clone(this Hub o)
        {
            Hub h = new Hub();
            h.HubIdentifier = o.HubIdentifier;
            h.Key = PlexHelper.ReplaceSchemeHost(o.Key);
            h.More = o.More;
            h.Size = o.Size;
            h.Title = o.Title;
            h.Type = o.Type;
            return h;
        }

 


        public static void Add(this List<Video> l, Video m, Breadcrumbs info, bool noimage=false, bool noart=false)
        {

            info.Update(m,noart).FillInfo(m, noimage, true);
            /*
            if (m is Directory)
                m.ParentThumb = m.GrandparentThumb = null;
            m.GrandparentTitle = m.ParentTitle ?? "";
            m.ParentTitle = "";
            m.Title1 = m.Title2="";
            if (m is Video)
               m.GrandparentKey = m.ParentKey;
            m.ParentKey = null;*/
            l.Add(m);
        }

        public static void EppAdd(this List<Video> l, Video m, Breadcrumbs info, bool noimage = false)
        {
            info.FillInfo(m,noimage,true);
            m.Thumb = info.Thumb;
            m.Art = info.Art;
            m.Title = info.Title;
            l.Add(m);
        }
        public static void EppAdd(this List<Directory> l, Directory m, Breadcrumbs info, bool noimage = false)
        {
            info.FillInfo(m, noimage, true);
            m.Thumb = info.Thumb;
            m.Art = info.Art;
            m.Title = info.Title;
            l.Add(m);
        }
        public static void Add(this List<Directory> l, Directory m, Breadcrumbs info, bool noimage = false)
        {
            info.Update(m).FillInfo(m, noimage, true);
            /*
            m.ParentThumb = m.GrandparentThumb = null;
            m.GrandparentTitle = m.ParentTitle ?? "";
            m.ParentTitle = "";
            if (m is Video)
                m.GrandparentKey = m.ParentKey;
            m.ParentKey = null;
            m.Title1 = m.Title2 = "";*/
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
            v.Art = PlexHelper.ReplaceSchemeHost(o.Art);
            v.ParentArt= PlexHelper.ReplaceSchemeHost(o.ParentArt);
            v.GrandparentArt = PlexHelper.ReplaceSchemeHost(o.GrandparentArt);
            v.ChapterSource = o.ChapterSource;
            v.ContentRating = o.ContentRating;
            v.Duration = o.Duration;
            v.EpNumber = o.EpNumber;
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
            v.Key = PlexHelper.ReplaceSchemeHost(o.Key);
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
            v.Thumb = PlexHelper.ReplaceSchemeHost(o.Thumb);
            v.ParentThumb = PlexHelper.ReplaceSchemeHost(o.ParentThumb);
            v.GrandparentThumb = PlexHelper.ReplaceSchemeHost(o.GrandparentThumb);
            v.Title = o.Title;
            v.Type = o.Type;
            v.UpdatedAt = o.UpdatedAt;
            v.Url = PlexHelper.ReplaceSchemeHost(o.Url);
            v.ViewCount = o.ViewCount;
            v.ViewOffset = o.ViewOffset;
            v.ViewedLeafCount = o.ViewedLeafCount;
            v.Year = o.Year;
            return v;
        }


    }
}
