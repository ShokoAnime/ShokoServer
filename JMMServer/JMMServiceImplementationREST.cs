using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.ServiceModel.Web;
using JMMContracts;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;

namespace JMMServer
{
    public class JMMServiceImplementationREST : IJMMServerREST
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public System.IO.Stream GetImage(string ImageType, string ImageID)
        {
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();
            TvDB_EpisodeRepository repEpisodes = new TvDB_EpisodeRepository();
            TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
            TvDB_ImageWideBannerRepository repWideBanners = new TvDB_ImageWideBannerRepository();

            MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
            MovieDB_FanartRepository repMovieFanart = new MovieDB_FanartRepository();

            Trakt_ImageFanartRepository repTraktFanart = new Trakt_ImageFanartRepository();
            Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
            Trakt_EpisodeRepository repTraktEpisodes = new Trakt_EpisodeRepository();
            Trakt_FriendRepository repTraktFriends = new Trakt_FriendRepository();

            JMMImageType imageType = (JMMImageType) int.Parse(ImageType);

            switch (imageType)
            {
                case JMMImageType.AniDB_Cover:

                    AniDB_Anime anime = repAnime.GetByAnimeID(int.Parse(ImageID));
                    if (anime == null) return null;

                    if (File.Exists(anime.PosterPath))
                    {
                        FileStream fs = File.OpenRead(anime.PosterPath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
                        return BlankImage();
                    }

                case JMMImageType.AniDB_Character:

                    AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();
                    AniDB_Character chr = repChar.GetByID(int.Parse(ImageID));
                    if (chr == null) return null;

                    if (File.Exists(chr.PosterPath))
                    {
                        FileStream fs = File.OpenRead(chr.PosterPath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.PosterPath);
                        return BlankImage();
                    }

                case JMMImageType.AniDB_Creator:

                    AniDB_SeiyuuRepository repCreator = new AniDB_SeiyuuRepository();
                    AniDB_Seiyuu creator = repCreator.GetByID(int.Parse(ImageID));
                    if (creator == null) return null;

                    if (File.Exists(creator.PosterPath))
                    {
                        FileStream fs = File.OpenRead(creator.PosterPath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find AniDB_Creator image: {0}", creator.PosterPath);
                        return BlankImage();
                    }

                case JMMImageType.TvDB_Cover:

                    TvDB_ImagePoster poster = repPosters.GetByID(int.Parse(ImageID));
                    if (poster == null) return null;

                    if (File.Exists(poster.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(poster.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Cover image: {0}", poster.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.TvDB_Banner:

                    TvDB_ImageWideBanner wideBanner = repWideBanners.GetByID(int.Parse(ImageID));
                    if (wideBanner == null) return null;

                    if (File.Exists(wideBanner.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(wideBanner.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.TvDB_Episode:

                    TvDB_Episode ep = repEpisodes.GetByID(int.Parse(ImageID));
                    if (ep == null) return null;

                    if (File.Exists(ep.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(ep.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_Episode image: {0}", ep.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.TvDB_FanArt:

                    TvDB_ImageFanart fanart = repFanart.GetByID(int.Parse(ImageID));
                    if (fanart == null) return null;

                    if (File.Exists(fanart.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(fanart.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.MovieDB_Poster:

                    MovieDB_Poster mPoster = repMoviePosters.GetByID(int.Parse(ImageID));
                    if (mPoster == null) return null;

                    // now find only the original size
                    mPoster = repMoviePosters.GetByOnlineID(mPoster.URL);
                    if (mPoster == null) return null;

                    if (File.Exists(mPoster.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(mPoster.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.MovieDB_FanArt:

                    MovieDB_Fanart mFanart = repMovieFanart.GetByID(int.Parse(ImageID));
                    if (mFanart == null) return null;

                    mFanart = repMovieFanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null) return null;

                    if (File.Exists(mFanart.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(mFanart.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.Trakt_Fanart:

                    Trakt_ImageFanart tFanart = repTraktFanart.GetByID(int.Parse(ImageID));
                    if (tFanart == null) return null;

                    if (File.Exists(tFanart.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(tFanart.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.Trakt_Poster:

                    Trakt_ImagePoster tPoster = repTraktPosters.GetByID(int.Parse(ImageID));
                    if (tPoster == null) return null;

                    if (File.Exists(tPoster.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(tPoster.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.FullImagePath);
                        return BlankImage();
                    }

                case JMMImageType.Trakt_Episode:
                case JMMImageType.Trakt_WatchedEpisode:

                    Trakt_Episode tEpisode = repTraktEpisodes.GetByID(int.Parse(ImageID));
                    if (tEpisode == null) return null;

                    if (File.Exists(tEpisode.FullImagePath))
                    {
                        FileStream fs = File.OpenRead(tEpisode.FullImagePath);
                        if (WebOperationContext.Current != null)
                        {
                            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                        }
                        return fs;
                    }
                    else
                    {
                        logger.Trace("Could not find Trakt_Episode image: {0}", tEpisode.FullImagePath);
                        return BlankImage();
                    }

                default:

                    return BlankImage();
            }
        }

        public System.IO.Stream BlankImage()
        {
            byte[] dta = Resources.blank;
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
            }
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        //mpiva WCF STREAMING IS CHUNKED SO IT DO NOT WORK FINE WITH PLEX, commented for now
        /*
	    public System.IO.Stream GetStream(string cmd, string arg, string opt)
	    {

            string fullname;
            if (cmd == "videolocal")
            {
                int sid = 0;
                int.TryParse(arg, out sid);
                if (sid == 0)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                    WebOperationContext.Current.OutgoingResponse.StatusDescription = "Videolocal with Missing Id.";
                    return new MemoryStream();
                }
                VideoLocalRepository rep = new VideoLocalRepository();
                VideoLocal loc = rep.GetByID(sid);
                if (loc == null)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                    WebOperationContext.Current.OutgoingResponse.StatusDescription = "Videolocal with Id " + sid + " not found.";
                    return new MemoryStream();

                }
                fullname = loc.FullServerPath;
            }
            else if (cmd == "file")
            {
                fullname = Base64DecodeUrl(arg);

            }
            else
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                WebOperationContext.Current.OutgoingResponse.StatusDescription = "Not know command";
                return new MemoryStream();
            }

            bool range = false;

            try
            {
                if (!File.Exists(fullname))
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                    WebOperationContext.Current.OutgoingResponse.StatusDescription = "File '" + fullname + "' not found.";
                    return new MemoryStream();
                }

            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                WebOperationContext.Current.OutgoingResponse.StatusDescription = "Unable to access File '" + fullname + "'.";
                return new MemoryStream();
            }
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Accept-Ranges", "bytes");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");

            string rangevalue = null;
            if (WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("Range"))
                rangevalue = WebOperationContext.Current.IncomingRequest.Headers["Range"].Replace("bytes=", string.Empty).Trim();
            if (WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("range"))
                rangevalue = WebOperationContext.Current.IncomingRequest.Headers["range"].Replace("bytes=", string.Empty).Trim();
            if (WebOperationContext.Current.IncomingRequest.Method == "OPTIONS")
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS, DELETE, PUT, HEAD");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Max-Age", "1209600");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers",
                    "accept, x-plex-token, x-plex-client-identifier, x-plex-username, x-plex-product, x-plex-device, x-plex-platform, x-plex-platform-version, x-plex-version, x-plex-device-name");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain";
                return new MemoryStream();
            }
            WebOperationContext.Current.OutgoingResponse.ContentType = GetMime(fullname);
	        Stream org;
            if (WebOperationContext.Current.IncomingRequest.Method != "HEAD")
            {

                org = new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long totalsize = org.Length;
                long start = 0;
                long end = 0;
                if (!string.IsNullOrEmpty(rangevalue))
                {
                    range = true;
                    string[] split = rangevalue.Split('-');
                    if (split.Length == 2)
                    {
                        if (string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                        {
                            long e = long.Parse(split[1]);
                            start = totalsize - e;
                            end = totalsize - 1;
                        }
                        else if (!string.IsNullOrEmpty(split[0]) && string.IsNullOrEmpty(split[1]))
                        {
                            start = long.Parse(split[0]);
                            end = totalsize - 1;
                        }
                        else if (!string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                        {
                            start = long.Parse(split[0]);
                            end = long.Parse(split[1]);
                            if (start > totalsize - 1)
                                start = totalsize - 1;
                            if (end > totalsize - 1)
                                end = totalsize - 1;
                        }
                        else
                        {
                            start = 0;
                            end = totalsize - 1;
                        }
                    }
                }

                if (range)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.PartialContent;
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Content-Range", "bytes " + start + "-" + end + "/" + totalsize);
                    org = new SubStream(org, start, end - start + 1);
                    WebOperationContext.Current.OutgoingResponse.ContentLength = end - start + 1;
                }
                else
                {
                    WebOperationContext.Current.OutgoingResponse.ContentLength = totalsize;
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;
                }
                return org;
            }
            WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;
            WebOperationContext.Current.OutgoingResponse.ContentLength = new FileInfo(fullname).Length;
            return new MemoryStream();
	    }
        public static string Base64DecodeUrl(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData.Replace("-", "+").Replace("_", "/").Replace(",", "="));
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        private static string GetMime(string fullname)
        {
            string ext = Path.GetExtension(fullname).Replace(".", string.Empty).ToLower();
            switch (ext)
            {
                case "png":
                    return "image/png";
                case "jpg":
                    return "image/jpeg";
                case "mkv":
                    return "video/x-matroska";
                case "mka":
                    return "audio/x-matroska";
                case "mk3d":
                    return "video/x-matroska-3d";
                case "avi":
                    return "video/avi";
                case "mp4":
                    return "video/mp4";
                case "mov":
                    return "video/quicktime";
                case "ogm":
                case "ogv":
                    return "video/ogg";
                case "mpg":
                case "mpeg":
                    return "video/mpeg";
                case "flv":
                    return "video/x-flv";
                case "rm":
                    return "application/vnd.rn-realmedia";
            }
            if (SubtitleHelper.Extensions.ContainsKey(ext))
                return SubtitleHelper.Extensions[ext];
            return "application/octet-stream";
        }
        */

        public System.IO.Stream GetImageUsingPath(string serverImagePath)
        {
            if (File.Exists(serverImagePath))
            {
                FileStream fs = File.OpenRead(serverImagePath);
                WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                return fs;
            }
            else
            {
                logger.Trace("Could not find image: {0}", serverImagePath);
                return null;
            }
        }

        public static Image ReSize(Image im, int width, int height)
        {
            Bitmap dest = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = width >= im.Width
                    ? InterpolationMode.HighQualityBilinear
                    : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(im, 0, 0, width, height);
            }
            return dest;
        }

        public System.IO.Stream ResizeToRatio(Image im, float newratio)
        {
            float calcwidth = im.Width;
            float calcheight = im.Height;

            float nheight = 0;
            do
            {
                nheight = calcwidth/newratio;
                if (nheight > (float) im.Height + 0.5F)
                {
                    calcwidth = calcwidth*((float) im.Height/nheight);
                }
                else
                {
                    calcheight = nheight;
                }
            } while (nheight > (float) im.Height + 0.5F);

            int newwidth = (int) Math.Round(calcwidth);
            int newheight = (int) Math.Round(calcheight);
            int x = 0;
            int y = 0;
            if (newwidth < im.Width)
                x = (im.Width - newwidth)/2;
            if (newheight < im.Height)
                y = (im.Height - newheight)/2;

            Image im2 = ReSize(im, newwidth, newheight);
            Graphics g = Graphics.FromImage(im2);
            g.DrawImage(im, new Rectangle(0, 0, im2.Width, im2.Height),
                new Rectangle(x, y, im2.Width, im2.Height), GraphicsUnit.Pixel);
            MemoryStream ms = new MemoryStream();
            im2.Save(ms, ImageFormat.Jpeg);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public System.IO.Stream GetSupportImage(string name, string ratio)
        {
            float newratio = 0F;
            if (float.TryParse(ratio, NumberStyles.Any, CultureInfo.InvariantCulture, out newratio))
            {
                if (string.IsNullOrEmpty(name))
                    return new MemoryStream();
                name = Path.GetFileNameWithoutExtension(name);
                System.Resources.ResourceManager man = Resources.ResourceManager;
                byte[] dta = (byte[]) man.GetObject(name);
                if ((dta == null) || (dta.Length == 0))
                    return new MemoryStream();
                if (WebOperationContext.Current != null)
                    WebOperationContext.Current.OutgoingResponse.ContentType = "image/png";
                //Little hack
                MemoryStream ms = new MemoryStream(dta);
                ms.Seek(0, SeekOrigin.Begin);
                if (!name.Contains("404") && newratio == 1.0F)
                {
                    return ms;
                }
                Image im = Image.FromStream(ms);
                float w = im.Width;
                float h = im.Height;
                float nw;
                float nh;


                if (w <= h)
                {
                    nw = h*newratio;
                    if (nw < w)
                    {
                        nw = w;
                        nh = w/newratio;
                    }
                    else
                    {
                        nh = h;
                    }
                }
                else
                {
                    nh = w/newratio;
                    if (nh < h)
                    {
                        nh = h;
                        nw = w*newratio;
                    }
                    else
                    {
                        nw = w;
                    }
                }
                nw = (float) Math.Round(nw);
                nh = (float) Math.Round(nh);
                Image im2 = new Bitmap((int) nw, (int) nh, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(im2))
                {
                    g.InterpolationMode = nw >= im.Width
                        ? InterpolationMode.HighQualityBilinear
                        : InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.Clear(Color.Transparent);
                    Rectangle src = new Rectangle(0, 0, im.Width, im.Height);
                    Rectangle dst = new Rectangle((int) ((nw - w)/2), (int) ((nh - h)/2), im.Width, im.Height);
                    g.DrawImage(im, dst, src, GraphicsUnit.Pixel);
                }
                MemoryStream ms2 = new MemoryStream();
                im2.Save(ms2, ImageFormat.Png);
                ms2.Seek(0, SeekOrigin.Begin);
                ms.Dispose();
                return ms2;
            }
            return new MemoryStream();
        }

        public System.IO.Stream GetThumb(string ImageType, string ImageID, string Ratio)
        {
            using (Stream m = GetImage(ImageType, ImageID))
            {
                if (m != null)
                {
                    float newratio = 0F;
                    if (float.TryParse(Ratio, NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"),
                        out newratio))
                    {
                        using (Image im = Image.FromStream(m))
                        {
                            return ResizeToRatio(im, newratio);
                        }
                    }
                }
            }
            return new MemoryStream();
        }
    }
}