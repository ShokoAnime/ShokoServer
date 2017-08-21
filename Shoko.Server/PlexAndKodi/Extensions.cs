using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Server.Models;
using Shoko.Server.ImageDownload;
using Shoko.Server.Repositories;

// ReSharper disable FunctionComplexityOverflow

namespace Shoko.Server.PlexAndKodi
{
    public static class Extensions
    {
        public static string ConstructUnsortUrl(this IProvider prov, int userid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.GroupUnsort + "/0?excludetags=" +
                prov.ExcludeTags);
        }

        public static string ConstructGroupIdUrl(this IProvider prov, int userid, string gid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.Group + "/" + gid +
                "?excludetags=" + prov.ExcludeTags);
        }

        public static string ConstructSerieIdUrl(this IProvider prov, int userid, string sid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.Serie + "/" + sid +
                "?excludetags=" + prov.ExcludeTags);
        }

        public static string ContructVideoUrl(this IProvider prov, int userid, string vid, JMMType type)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) type + "/" + vid + "?excludetags=" +
                prov.ExcludeTags);
        }

        public static string ConstructFilterIdUrl(this IProvider prov, int userid, int gfid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.GroupFilter + "/" + gfid +
                "?excludetags=" + prov.ExcludeTags);
        }

        public static string ConstructFakeIosThumb(this IProvider prov, int userid, string thumburl, string arturl)
        {
            string r = Helper.Base64EncodeUrl(thumburl + "|" + arturl);
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.FakeIosThumb + "/" + r +
                "/0?excludetags=" + prov.ExcludeTags);
/*

            try
            {

                if (API.Module.apiv1.Legacy.request.Url.ToString().Contains("/api/"))
                {
                    return Helper.ServerUrl(prov.ServicePort, "/api/Metadata/" + (int)JMMType.FakeIosThumb + "/" + r + "/0");
                }
                else
                {
                    return Helper.ServerUrl(prov.ServicePort, prov.ServiceAddress + "/Metadata/" + userid + "/" + (int)JMMType.FakeIosThumb + "/" + r + "/0");
                }
            }
            catch { return Helper.ServerUrl(prov.ServicePort, prov.ServiceAddress + "/Metadata/" + userid + "/" + (int)JMMType.FakeIosThumb + "/" + r + "/0"); }
            */
        }

        public static string ConstructFiltersUrl(this IProvider prov, int userid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Filters/" + userid + "?excludetags=" + prov.ExcludeTags);
        }

        public static string ConstructSearchUrl(this IProvider prov, string userid, int limit, string query,
            bool searchTag)
        {
            if (searchTag)
            {
                return prov.ServerUrl(prov.ServicePort,
                    prov.ServiceAddress + "/SearchTag/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" +
                    WebUtility.UrlEncode(query) + "?excludetags=" + prov.ExcludeTags);
            }
            else
            {
                return prov.ServerUrl(prov.ServicePort,
                    prov.ServiceAddress + "/Search/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" +
                    WebUtility.UrlEncode(query) + "?excludetags=" + prov.ExcludeTags);
            }
        }

        public static string ConstructPlaylistUrl(this IProvider prov, int userid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.Playlist + "/0?excludetags=" +
                prov.ExcludeTags);
        }

        public static string ConstructPlaylistIdUrl(this IProvider prov, int userid, int pid)
        {
            return prov.ServerUrl(prov.ServicePort,
                prov.ServiceAddress + "/Metadata/" + userid + "/" + (int) JMMType.Playlist + "/" + pid +
                "?excludetags=" + prov.ExcludeTags);
        }

        public static string GenPoster(this ImageDetails im, IProvider prov, string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.ImageID == 0))
                return prov.ConstructSupportImageLink(fallbackimage);
            return prov.ConstructThumbLink((int) im.ImageType, im.ImageID);
        }

        public static string GenArt(this ImageDetails im, IProvider prov)
        {
            if (im == null)
                return null;
            return prov.ConstructImageLink((int) im.ImageType, im.ImageID);
        }

        public static string GenPoster(this Metro_Anime_Episode im, IProvider prov,
            string fallbackimage = "plex_404.png")
        {
            if ((im == null) || (im.ImageID == 0))
                return prov.ConstructSupportImageLinkTV(fallbackimage);
            return prov.ConstructTVThumbLink(im.ImageType, im.ImageID);
        }

        public static string GenPoster(this CL_AniDB_Anime_DefaultImage im, IProvider prov,
            string fallbackimage = "plex_404V.png")
        {
            if ((im == null) || (im.AnimeID == 0))
                return prov.ConstructSupportImageLink(fallbackimage);
            return prov.ConstructThumbLink(im.ImageType, im.AnimeID);
        }

        public static string GenPoster(this TvDB_Episode ep, IProvider prov)
        {
            if ((ep == null) || (ep.TvDB_EpisodeID == 0))
                return prov.ConstructSupportImageLink("plex_404.png");
            return prov.ConstructThumbLink((int) ImageEntityType.TvDB_Episode, ep.TvDB_EpisodeID);
        }

        public static string GenArt(this CL_AniDB_Anime_DefaultImage im, IProvider prov)
        {
            if (im == null)
                return null;
            return prov.ConstructImageLink(im.ImageType, im.AnimeID);
        }

        public static void RandomizeArt(this MediaContainer m, IProvider prov, List<Video> vids)
        {
            foreach (Video v in vids.Randomize())
            {
                if (v.Art != null)
                {
                    m.Art = prov.ReplaceSchemeHost(v.Art);
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
            switch ((Shoko.Models.PlexAndKodi.AnimeTypes) Enum.Parse(typeof(Shoko.Models.PlexAndKodi.AnimeTypes),
                v.AnimeType, true))
            {
                case Shoko.Models.PlexAndKodi.AnimeTypes.AnimeGroup:
                    v.Key = prov.ConstructGroupIdUrl(userid, v.Id);
                    break;
                case Shoko.Models.PlexAndKodi.AnimeTypes.AnimeSerie:
                    v.Key = prov.ConstructSerieIdUrl(userid, v.Id);
                    break;
                case Shoko.Models.PlexAndKodi.AnimeTypes.AnimeEpisode:
                case Shoko.Models.PlexAndKodi.AnimeTypes.AnimeFile:
                    Helper.AddLinksToAnimeEpisodeVideo(prov, v, userid);
                    AddResumePosition(v, prov, userid);
                    break;
            }
        }

        public static void AddResumePosition(this Video v, IProvider prov, int userid)
        {
            switch (
                (Shoko.Models.PlexAndKodi.AnimeTypes)
                Enum.Parse(typeof(Shoko.Models.PlexAndKodi.AnimeTypes), v.AnimeType, true))
            {
                case Shoko.Models.PlexAndKodi.AnimeTypes.AnimeEpisode:
                    if (v.Medias != null)
                    {
                        VideoLocal_User vl = v.Medias.Select(a => RepoFactory.VideoLocal.GetByID(int.Parse(a.Id)))
                            .Where(a => a != null)
                            .Select(a => a.GetUserRecord(userid))
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
                case Shoko.Models.PlexAndKodi.AnimeTypes.AnimeFile:
                    int vid = int.Parse(v.Id); //This suxx, but adding regeneration at videolocal_user is worst.
                    VideoLocal_User vl2 = RepoFactory.VideoLocal.GetByID(vid)?.GetUserRecord(userid);
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
            m.ReplaceSchemeHost(prov);
            info?.Update(m, noart).FillInfo(prov, m, noimage, true);
            l.Add(m);
        }

        public static void EppAdd(this List<Video> l, IProvider prov, Video m, BreadCrumbs info, bool noimage = false)
        {
            m.ReplaceSchemeHost(prov);
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
            m.ReplaceSchemeHost(prov);
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
            m.ReplaceSchemeHost(prov);
            info?.Update(m).FillInfo(prov, m, noimage, true);
            l.Add(m);
        }

        public static void ReplaceSchemeHost(this Video o, IProvider prov)
        {
            o.Url = prov.ReplaceSchemeHost(o.Url);
            o.Thumb = prov.ReplaceSchemeHost(o.Thumb);
            o.ParentThumb = prov.ReplaceSchemeHost(o.ParentThumb);
            o.GrandparentThumb = prov.ReplaceSchemeHost(o.GrandparentThumb);
            o.Art = prov.ReplaceSchemeHost(o.Art);
            o.ParentArt = prov.ReplaceSchemeHost(o.ParentArt);
            o.GrandparentArt = prov.ReplaceSchemeHost(o.GrandparentArt);
            if (o.Roles != null)
            {
                foreach (RoleTag r in o.Roles)
                {
                    if (!string.IsNullOrEmpty(r.RolePicture))
                        r.RolePicture = prov.ReplaceSchemeHost(r.RolePicture);
                    if (!string.IsNullOrEmpty(r.TagPicture))
                        r.TagPicture = prov.ReplaceSchemeHost(r.TagPicture);
                }
            }
        }

        public static T Clone<T>(this Video o, IProvider prov) where T : Video, new()
        {
            T v = new T();
            o.CopyTo(v);
            v.ReplaceSchemeHost(prov);
            return v;
        }

        public static Stream CopyTo(this Stream s, Stream o)
        {
            o.Title = s.Title;
            o.Language = s.Language;
            o.Key = s.Key;
            o.Duration = s.Duration;
            o.Height = s.Height;
            o.Width = s.Width;
            o.Bitrate = s.Bitrate;
            o.SubIndex = s.SubIndex;
            o.Id = s.Id;
            o.ScanType = s.ScanType;
            o.RefFrames = s.RefFrames;
            o.Profile = s.Profile;
            o.Level = s.Level;
            o.HeaderStripping = s.HeaderStripping;
            o.HasScalingMatrix = s.HasScalingMatrix;
            o.FrameRateMode = s.FrameRateMode;
            o.File = s.File;
            o.FrameRate = s.FrameRate;
            o.ColorSpace = s.ColorSpace;
            o.CodecID = s.CodecID;
            o.ChromaSubsampling = s.ChromaSubsampling;
            o.Cabac = s.Cabac;
            o.BitDepth = s.BitDepth;
            o.Index = s.Index;
            o.idx = s.idx;
            o.Codec = s.Codec;
            o.StreamType = s.StreamType;
            o.Orientation = s.Orientation;
            o.QPel = s.QPel;
            o.GMC = s.GMC;
            o.BVOP = s.BVOP;
            o.SamplingRate = s.SamplingRate;
            o.LanguageCode = s.LanguageCode;
            o.Channels = s.Channels;
            o.Selected = s.Selected;
            o.DialogNorm = s.DialogNorm;
            o.BitrateMode = s.BitrateMode;
            o.Format = s.Format;
            o.Default = s.Default;
            o.Forced = s.Forced;
            o.PixelAspectRatio = s.PixelAspectRatio;
            o.PA = s.PA;
            return o;
        }

        public static Part CopyTo(this Part s, Part o)
        {
            o.Accessible = s.Accessible;
            o.Exists = s.Exists;
            o.Streams = new List<Stream>();
            o.Streams = s.Streams?.Select(a => a.CopyTo(new Stream())).ToList();
            o.Size = s.Size;
            o.Duration = s.Duration;
            o.Key = s.Key;
            o.LocalKey = s.LocalKey;
            o.Container = s.Container;
            o.Id = s.Id;
            o.File = s.File;
            o.OptimizedForStreaming = s.OptimizedForStreaming;
            o.Extension = s.Extension;
            o.Has64bitOffsets = s.Has64bitOffsets;
            return o;
        }

        public static Media CopyTo(this Media v, Media o)
        {
            o.Parts = v.Parts?.Select(a => a.CopyTo(new Part())).ToList();
            o.Duration = v.Duration;
            o.VideoFrameRate = v.VideoFrameRate;
            o.Container = v.Container;
            o.VideoCodec = v.VideoCodec;
            o.AudioCodec = v.AudioCodec;
            o.AudioChannels = v.AudioChannels;
            o.AspectRatio = v.AspectRatio;
            o.Height = v.Height;
            o.Width = v.Width;
            o.Bitrate = v.Bitrate;
            o.Id = v.Id;
            o.VideoResolution = v.VideoResolution;
            o.OptimizedForStreaming = v.OptimizedForStreaming;
            return o;
        }

        public static Tag CopyTo(this Tag v, Tag o)
        {
            o.Value = v.Value;
            o.Role = v.Role;
            return o;
        }

        public static RoleTag CopyTo(this RoleTag v, RoleTag o)
        {
            o.Value = v.Value;
            o.Role = v.Role;
            o.RoleDescription = v.RoleDescription;
            o.RolePicture = v.RolePicture;
            o.TagPicture = v.TagPicture;
            return o;
        }

        public static Extras CopyTo(this Extras v, Extras o)
        {
            o.Size = v.Size;
            o.Videos = v.Videos?.Select(a => a.CopyTo(new Video())).ToList();
            return o;
        }

        public static AnimeTitle CopyTo(this AnimeTitle v, AnimeTitle o)
        {
            o.Type = v.Type;
            o.Language = v.Language;
            o.Title = v.Title;
            return o;
        }

        public static Hub CopyTo(this Hub v, Hub o)
        {
            o.Key = v.Key;
            o.Type = v.Type;
            o.HubIdentifier = v.HubIdentifier;
            o.Size = v.Size;
            o.Title = v.Title;
            o.More = v.More;
            return o;
        }

        public static Contract_ImageDetails CopyTo(this Contract_ImageDetails v, Contract_ImageDetails o)
        {
            o.ImageID = v.ImageID;
            o.ImageType = v.ImageType;
            return o;
        }

        // This should be the same as a ShallowCopy, but I did just learn the difference 2 days ago, so I might be wrong
        public static Video CopyTo<T>(this Video v, T o) where T : Video
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
            o.UserRating = v.UserRating;
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
            o.Group = v.Group; //We use contract group as reference so, we dont need to copy it.
            o.Medias = v.Medias?.Select(a => a.CopyTo(new Media())).ToList();
            o.Roles = v.Roles?.Select(a => a.CopyTo(new RoleTag())).ToList();
            o.Extras = v.Extras?.CopyTo(new Extras());
            o.Related = v.Related?.Select(a => a.CopyTo(new Hub())).ToList();
            o.Tags = v.Tags?.Select(a => a.CopyTo(new Tag())).ToList();
            o.Genres = v.Genres?.Select(a => a.CopyTo(new Tag())).ToList();
            o.Titles = v.Titles?.Select(a => a.CopyTo(new AnimeTitle())).ToList();
            o.Fanarts = v.Fanarts?.Select(a => a.CopyTo(new Contract_ImageDetails())).ToList();
            o.Banners = v.Banners?.Select(a => a.CopyTo(new Contract_ImageDetails())).ToList();
            return o;
        }
    }
}