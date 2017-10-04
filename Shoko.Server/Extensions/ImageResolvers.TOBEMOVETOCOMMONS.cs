using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pri.LongPath;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.ImageDownload;
using Shoko.Server.Properties;
using File = System.IO.File;
using Resources = Shoko.Commons.Properties.Resources;

namespace Shoko.Server.Extensions
{
    public static class ImageResolvers
    {
        public static string GetFullImagePath(this MovieDB_Fanart fanart)
        {
            if (String.IsNullOrEmpty(fanart.URL)) return string.Empty;

            //strip out the base URL
            int pos = fanart.URL.IndexOf('/', 0);
            string fname = fanart.URL.Substring(pos + 1, fanart.URL.Length - pos - 1);
            fname = fname.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
        }

        public static string GetFullImagePath(this MovieDB_Poster movie)
        {
            if (String.IsNullOrEmpty(movie.URL)) return string.Empty;

            //strip out the base URL
            int pos = movie.URL.IndexOf('/', 0);
            string fname = movie.URL.Substring(pos + 1, movie.URL.Length - pos - 1);
            fname = fname.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_Episode episode)
        {
            if (String.IsNullOrEmpty(episode.Filename)) return string.Empty;

            string fname = episode.Filename;
            fname = episode.Filename.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImageFanart fanart)
        {
            if (String.IsNullOrEmpty(fanart.BannerPath)) return string.Empty;

            string fname = fanart.BannerPath;
            fname = fanart.BannerPath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullThumbnailPath(this TvDB_ImageFanart fanart)
        {
            string fname = fanart.ThumbnailPath;
            fname = fanart.ThumbnailPath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImagePoster poster)
        {
            if (String.IsNullOrEmpty(poster.BannerPath)) return string.Empty;

            string fname = poster.BannerPath;
            fname = poster.BannerPath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImageWideBanner banner)
        {
            if (String.IsNullOrEmpty(banner.BannerPath)) return string.Empty;

            string fname = banner.BannerPath;
            fname = banner.BannerPath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static void Valid(this TvDB_ImageFanart fanart)
        {
            if (!File.Exists(fanart.GetFullImagePath()) || !File.Exists(fanart.GetFullThumbnailPath()))
            {
                //clean leftovers
                if (File.Exists(fanart.GetFullImagePath()))
                {
                    File.Delete(fanart.GetFullImagePath());
                }
                if (File.Exists(fanart.GetFullThumbnailPath()))
                {
                    File.Delete(fanart.GetFullThumbnailPath());
                }
            }
        }

        public static string GetPosterPath(this AniDB_Character character)
        {
            if (String.IsNullOrEmpty(character.PicName)) return string.Empty;

            return System.IO.Path.Combine(ImageUtils.GetAniDBCharacterImagePath(character.CharID), character.PicName);
        }

        public static string GetPosterPath(this AniDB_Seiyuu seiyuu)
        {
            if (String.IsNullOrEmpty(seiyuu.PicName)) return string.Empty;

            return System.IO.Path.Combine(ImageUtils.GetAniDBCreatorImagePath(seiyuu.SeiyuuID), seiyuu.PicName);
        }

        //The resources need to be moved
        public static string GetAnimeTypeDescription(this AniDB_Anime anidbanime)
        {
            switch (anidbanime.GetAnimeTypeEnum())
            {
                case AnimeType.Movie:
                    return Resources.AnimeType_Movie;
                case AnimeType.Other:
                    return Resources.AnimeType_Other;
                case AnimeType.OVA:
                    return Resources.AnimeType_OVA;
                case AnimeType.TVSeries:
                    return Resources.AnimeType_TVSeries;
                case AnimeType.TVSpecial:
                    return Resources.AnimeType_TVSpecial;
                case AnimeType.Web:
                    return Resources.AnimeType_Web;
                default:
                    return Resources.AnimeType_Other;
            }
        }
    }
}