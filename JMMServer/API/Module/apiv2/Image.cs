

using JMMServer.API.Model.core;
using JMMServer.Entities;
using JMMServer.PlexAndKodi;
using JMMServer.Properties;
using JMMServer.Repositories;
using Nancy;
using NLog;
using System.Collections.Generic;
using System.IO;

namespace JMMServer.API.Module.apiv2
{
    public class Image : Nancy.NancyModule
    {
        public static int version = 1;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Image() : base("/api")
        {           
            Get["/image/{type}/{id}"] = x => { return GetImage((int)x.id, (int)x.type, false); };
            Get["/image/support/{name}"] = x => { return GetSupportImage(x.name); };
        }

        /// <summary>
        /// Return image with given id, type and information if its should be thumb
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <param name="thumb">thumb mode</param>
        /// <returns>image body inside stream</returns>
        private object GetImage(int id, int type, bool thumb)
        {
            Nancy.Response response = new Nancy.Response();
            string contentType = "";
            string path = ReturnImagePath(id, type, thumb);

            if (path == "")
            {
                Stream image = MissingImage();
                contentType = "image/png";
                response = Response.FromStream(image, contentType);
            }
            else
            {
                FileStream fs = Pri.LongPath.File.OpenRead(path);
                contentType = MimeTypes.GetMimeType(path);
                response = Response.FromStream(fs, contentType);
            }

            return response;
        }

        /// <summary>
        /// Return SupportImage (build-in server)
        /// </summary>
        /// <param name="name">image file name</param>
        /// <returns></returns>
        private object GetSupportImage(string name)
        {
            Nancy.Response response = new Nancy.Response();

            if (string.IsNullOrEmpty(name)) { return APIStatus.notFound404(); }
            name = Path.GetFileNameWithoutExtension(name);
            System.Resources.ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[])man.GetObject(name);
            if ((dta == null) || (dta.Length == 0)) { return APIStatus.notFound404(); }
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
      
            response = Response.FromStream(ms, "image/png");
            return response;
        }

        /// <summary>
        /// Internal function that return valid image file path on server that exist
        /// </summary>
        /// <param name="id">image id</param>
        /// <param name="type">image type</param>
        /// <param name="thumb">thumb mode</param>
        /// <returns>string</returns>
        internal string ReturnImagePath(int id, int type, bool thumb)
        {
            JMMImageType imageType = (JMMImageType)type;
            string path = "";

            switch (imageType)
            {
                // 1
                case JMMImageType.AniDB_Cover:
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(id);
                    if (anime == null) { return null; }

                    path = anime.PosterPath;

                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
                    }
                    break;

                // 2
                case JMMImageType.AniDB_Character:
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByID(id);
                    if (chr == null) { return null; }

                    path = chr.PosterPath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.PosterPath);
                    }
                    break;

                // 3
                case JMMImageType.AniDB_Creator:
                    AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetByID(id);
                    if (creator == null) { return null; }

                    path = creator.PosterPath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Creator image: {0}", creator.PosterPath);
                    }
                    break;

                // 4
                case JMMImageType.TvDB_Banner:

                    TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(id);
                    if (wideBanner == null) { return null; }

                    path = wideBanner.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.FullImagePath);
                    }
                    break;

                // 5
                case JMMImageType.TvDB_Cover:
                    TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(id);
                    if (poster == null) { return null; }

                    path = poster.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Cover image: {0}", poster.FullImagePath);
                    }
                    break;

                // 6
                case JMMImageType.TvDB_Episode:
                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(id);
                    if (ep == null) { return null; }

                    path = ep.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Episode image: {0}", ep.FullImagePath);
                    }
                    break;

                // 7
                case JMMImageType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(id);
                    if (fanart == null) { return null; }

                    if (thumb)
                    {
                        path = fanart.FullThumbnailPath;
                        if (Pri.LongPath.File.Exists(path))
                        {
                            return path;
                        }
                        else
                        {
                            logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullThumbnailPath);
                        }
                    }
                    else
                    {
                        path = fanart.FullImagePath;
                        if (Pri.LongPath.File.Exists(path))
                        {
                            return path;
                        }
                        else
                        {
                            logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullImagePath);
                        }
                    }
                    break;

                // 8
                case JMMImageType.MovieDB_FanArt:
                    MovieDB_Fanart mFanart = RepoFactory.MovieDB_Fanart.GetByID(id);
                    if (mFanart == null) { return null; }

                    mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null) { return null; }

                    path = mFanart.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.FullImagePath);
                    }
                    break;

                // 9
                case JMMImageType.MovieDB_Poster:
                    MovieDB_Poster mPoster = RepoFactory.MovieDB_Poster.GetByID(id);
                    if (mPoster == null) { return null; }

                    mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                    if (mPoster == null) { return null; }

                    path = mPoster.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.FullImagePath);
                    }
                    break;

                // 10
                case JMMImageType.Trakt_Poster:
                    Trakt_ImagePoster tPoster = RepoFactory.Trakt_ImagePoster.GetByID(id);
                    if (tPoster == null) { return null; }

                    path = tPoster.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.FullImagePath);
                    }
                    break;

                // 11
                case JMMImageType.Trakt_Fanart:

                    Trakt_ImageFanart tFanart = RepoFactory.Trakt_ImageFanart.GetByID(id);
                    if (tFanart == null) { return null; }

                    path = tFanart.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.FullImagePath);
                    }
                    break;


                // 12 + 16
                case JMMImageType.Trakt_Episode:
                case JMMImageType.Trakt_WatchedEpisode:

                    Trakt_Episode tEpisode = RepoFactory.Trakt_Episode.GetByID(id);
                    if (tEpisode == null) { return null; }

                    path = tEpisode.FullImagePath;
                    if (Pri.LongPath.File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        logger.Trace("Could not find Trakt_Episode image: {0}", tEpisode.FullImagePath);
                    }

                    break;

                // 0, 13-15, 17+
                default:
                    path = "";
                    break;
            }

            return path;
        }

        /// <summary>
        /// Internal function that return image for missing image
        /// </summary>
        /// <returns>Stream</returns>
        internal Stream MissingImage()
        {
            byte[] dta = Resources.blank;
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

    }
}