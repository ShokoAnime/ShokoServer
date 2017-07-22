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
            if (String.IsNullOrEmpty(fanart.URL)) return "";

            //strip out the base URL
            int pos = fanart.URL.IndexOf('/', 0);
            string fname = fanart.URL.Substring(pos + 1, fanart.URL.Length - pos - 1);
            fname = fname.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
        }

        public static string GetFullImagePath(this MovieDB_Poster movie)
        {
            if (String.IsNullOrEmpty(movie.URL)) return "";

            //strip out the base URL
            int pos = movie.URL.IndexOf('/', 0);
            string fname = movie.URL.Substring(pos + 1, movie.URL.Length - pos - 1);
            fname = fname.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
        }

        public static string GetFullImagePath(this Trakt_Episode episode)
        {
            // typical EpisodeImage url
            // http://vicmackey.trakt.tv/images/episodes/3228-1-1.jpg

            // get the TraktID from the URL
            // http://trakt.tv/show/11eyes/season/1/episode/1 (11 eyes)

            if (String.IsNullOrEmpty(episode.EpisodeImage)) return "";
            if (String.IsNullOrEmpty(episode.URL)) return "";

            // on Trakt, if the episode doesn't have a proper screenshot, they will return the
            // fanart instead, we will ignore this
            int pos = episode.EpisodeImage.IndexOf(@"episodes/");
            if (pos <= 0) return "";

            int posID = episode.URL.IndexOf(@"show/");
            if (posID <= 0) return "";

            int posIDNext = episode.URL.IndexOf(@"/", posID + 6);
            if (posIDNext <= 0) return "";

            string traktID = episode.URL.Substring(posID + 5, posIDNext - posID - 5);
            traktID = traktID.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");

            string imageName = episode.EpisodeImage.Substring(pos + 9, episode.EpisodeImage.Length - pos - 9);
            imageName = imageName.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");

            string relativePath = System.IO.Path.Combine("episodes", traktID);
            relativePath = System.IO.Path.Combine(relativePath, imageName);

            return System.IO.Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
        }

        public static string GetFullImagePath(this Trakt_Friend friend)
        {
            // typical url
            // http://vicmackey.trakt.tv/images/avatars/837.jpg
            // http://gravatar.com/avatar/f894a4cbd5e8bcbb1a79010699af1183.jpg?s=140&r=pg&d=http%3A%2F%2Fvicmackey.trakt.tv%2Fimages%2Favatar-large.jpg

            if (String.IsNullOrEmpty(friend.Avatar)) return "";

            string path = ImageUtils.GetTraktImagePath_Avatars();
            return System.IO.Path.Combine(path, String.Format("{0}.jpg", friend.Username));
        }

        public static string GetFullImagePath(this Trakt_ImageFanart image)
        {
            // typical url
            // http://vicmackey.trakt.tv/images/fanart/3228.jpg

            if (String.IsNullOrEmpty(image.ImageURL)) return "";

            int pos = image.ImageURL.IndexOf(@"images/");
            if (pos <= 0) return "";

            string relativePath = image.ImageURL.Substring(pos + 7, image.ImageURL.Length - pos - 7);
            relativePath = relativePath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");

            return System.IO.Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
        }

        public static string GetFullImagePath(this Trakt_ImagePoster poster)
        {
            // typical url
            // http://vicmackey.trakt.tv/images/seasons/3228-1.jpg
            // http://vicmackey.trakt.tv/images/posters/1130.jpg

            if (String.IsNullOrEmpty(poster.ImageURL)) return "";

            int pos = poster.ImageURL.IndexOf(@"images/");
            if (pos <= 0) return "";

            string relativePath = poster.ImageURL.Substring(pos + 7, poster.ImageURL.Length - pos - 7);
            relativePath = relativePath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");

            return System.IO.Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
        }

        public static string GetFullImagePath(this TvDB_Episode episode)
        {
            if (String.IsNullOrEmpty(episode.Filename)) return "";

            string fname = episode.Filename;
            fname = episode.Filename.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImageFanart fanart)
        {
            if (String.IsNullOrEmpty(fanart.BannerPath)) return "";

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
            if (String.IsNullOrEmpty(poster.BannerPath)) return "";

            string fname = poster.BannerPath;
            fname = poster.BannerPath.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImageWideBanner banner)
        {
            if (String.IsNullOrEmpty(banner.BannerPath)) return "";

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
            if (String.IsNullOrEmpty(character.PicName)) return "";

            return System.IO.Path.Combine(ImageUtils.GetAniDBCharacterImagePath(character.CharID), character.PicName);
        }

        public static string GetPosterPath(this AniDB_Seiyuu seiyuu)
        {
            if (String.IsNullOrEmpty(seiyuu.PicName)) return "";

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