using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_DownloadImage : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_DownloadImage()
        {
        }

        public CommandRequest_DownloadImage(int entityID, JMMImageType entityType, bool forced)
        {
            EntityID = entityID;
            EntityType = (int)entityType;
            ForceDownload = forced;
            CommandType = (int)CommandRequestType.ImageDownload;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int EntityID { get; set; }
        public int EntityType { get; set; }
        public bool ForceDownload { get; set; }

        public JMMImageType EntityTypeEnum
        {
            get { return (JMMImageType)EntityType; }
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority2; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_DownloadImage, EntityID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_DownloadImage: {0}", EntityID);
            var downloadURL = "";
            try
            {
                ImageDownloadRequest req = null;
                switch (EntityTypeEnum)
                {
                    case JMMImageType.AniDB_Cover:
                        var repAnime = new AniDB_AnimeRepository();
                        var anime = repAnime.GetByID(EntityID);
                        if (anime == null) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, anime, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Episode:

                        var repTvEp = new TvDB_EpisodeRepository();
                        var ep = repTvEp.GetByID(EntityID);
                        if (ep == null) return;
                        if (string.IsNullOrEmpty(ep.Filename)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, ep, ForceDownload);
                        break;

                    case JMMImageType.TvDB_FanArt:

                        var repFanart = new TvDB_ImageFanartRepository();
                        var fanart = repFanart.GetByID(EntityID);
                        if (fanart == null) return;
                        if (string.IsNullOrEmpty(fanart.BannerPath)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, fanart, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Cover:

                        var repPoster = new TvDB_ImagePosterRepository();
                        var poster = repPoster.GetByID(EntityID);
                        if (poster == null) return;
                        if (string.IsNullOrEmpty(poster.BannerPath)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, poster, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Banner:

                        var repBanners = new TvDB_ImageWideBannerRepository();
                        var wideBanner = repBanners.GetByID(EntityID);
                        if (wideBanner == null) return;
                        if (string.IsNullOrEmpty(wideBanner.BannerPath)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, wideBanner, ForceDownload);
                        break;

                    case JMMImageType.MovieDB_Poster:

                        var repMoviePosters = new MovieDB_PosterRepository();
                        var moviePoster = repMoviePosters.GetByID(EntityID);
                        if (moviePoster == null) return;
                        if (string.IsNullOrEmpty(moviePoster.URL)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, moviePoster, ForceDownload);
                        break;

                    case JMMImageType.MovieDB_FanArt:

                        var repMovieFanart = new MovieDB_FanartRepository();
                        var movieFanart = repMovieFanart.GetByID(EntityID);
                        if (movieFanart == null) return;
                        if (string.IsNullOrEmpty(movieFanart.URL)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, movieFanart, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Poster:

                        var repTraktPosters = new Trakt_ImagePosterRepository();
                        var traktPoster = repTraktPosters.GetByID(EntityID);
                        if (traktPoster == null) return;
                        if (string.IsNullOrEmpty(traktPoster.ImageURL)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, traktPoster, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Fanart:

                        var repTraktFanarts = new Trakt_ImageFanartRepository();
                        var traktFanart = repTraktFanarts.GetByID(EntityID);
                        if (traktFanart == null) return;
                        if (string.IsNullOrEmpty(traktFanart.ImageURL)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, traktFanart, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Friend:

                        var repFriends = new Trakt_FriendRepository();
                        var friend = repFriends.GetByID(EntityID);
                        if (friend == null) return;
                        if (string.IsNullOrEmpty(friend.Avatar)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, friend, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Episode:

                        var repTraktEpisodes = new Trakt_EpisodeRepository();
                        var traktEp = repTraktEpisodes.GetByID(EntityID);
                        if (traktEp == null) return;
                        if (string.IsNullOrEmpty(traktEp.EpisodeImage)) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, traktEp, ForceDownload);
                        break;

                    case JMMImageType.AniDB_Character:
                        var repChars = new AniDB_CharacterRepository();
                        var chr = repChars.GetByID(EntityID);
                        if (chr == null) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, chr, ForceDownload);
                        break;

                    case JMMImageType.AniDB_Creator:
                        var repCreator = new AniDB_SeiyuuRepository();
                        var creator = repCreator.GetByID(EntityID);
                        if (creator == null) return;

                        req = new ImageDownloadRequest(EntityTypeEnum, creator, ForceDownload);
                        break;
                }

                if (req == null) return;

                var fileNames = new List<string>();
                var downloadURLs = new List<string>();

                var fileNameTemp = GetFileName(req, false);
                var downloadURLTemp = GetFileURL(req, false);

                fileNames.Add(fileNameTemp);
                downloadURLs.Add(downloadURLTemp);

                if (req.ImageType == JMMImageType.TvDB_FanArt)
                {
                    fileNameTemp = GetFileName(req, true);
                    downloadURLTemp = GetFileURL(req, true);

                    fileNames.Add(fileNameTemp);
                    downloadURLs.Add(downloadURLTemp);
                }

                for (var i = 0; i < fileNames.Count; i++)
                {
                    var fileName = fileNames[i];
                    downloadURL = downloadURLs[i];

                    var downloadImage = true;
                    var fileExists = File.Exists(fileName);

                    if (fileExists)
                    {
                        if (!req.ForceDownload)
                            downloadImage = false;
                        else
                            downloadImage = true;
                    }
                    else
                        downloadImage = true;

                    if (downloadImage)
                    {
                        var tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));
                        if (File.Exists(tempName)) File.Delete(tempName);


                        try
                        {
                            if (fileExists) File.Delete(fileName);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                            var msg = string.Format(Resources.Command_DeleteError, fileName, ex.Message);
                            logger.Warn(msg);
                            return;
                        }


                        // download image
                        using (var client = new WebClient())
                        {
                            client.Headers.Add("user-agent", "JMM");
                            //OnImageDownloadEvent(new ImageDownloadEventArgs("", req, ImageDownloadEventType.Started));
                            //BaseConfig.MyAnimeLog.Write("ProcessImages: Download: {0}  *** to ***  {1}", req.URL, fullName);
                            if (downloadURL.Length > 0)
                            {
                                client.DownloadFile(downloadURL, tempName);

                                var extension = "";
                                var contentType = client.ResponseHeaders["Content-type"].ToLower();
                                if (contentType.IndexOf("gif") >= 0) extension = ".gif";
                                if (contentType.IndexOf("jpg") >= 0) extension = ".jpg";
                                if (contentType.IndexOf("jpeg") >= 0) extension = ".jpg";
                                if (contentType.IndexOf("bmp") >= 0) extension = ".bmp";
                                if (contentType.IndexOf("png") >= 0) extension = ".png";
                                if (extension.Length > 0)
                                {
                                    var newFile = Path.ChangeExtension(tempName, extension);
                                    if (!newFile.ToLower().Equals(tempName.ToLower()))
                                    {
                                        try
                                        {
                                            File.Delete(newFile);
                                        }
                                        catch
                                        {
                                            //BaseConfig.MyAnimeLog.Write("DownloadedImage:Download() Delete failed:{0}", newFile);
                                        }
                                        File.Move(tempName, newFile);
                                        tempName = newFile;
                                    }
                                }
                            }
                        }

                        // move the file to it's final location
                        // check that the final folder exists
                        var fullPath = Path.GetDirectoryName(fileName);
                        if (!Directory.Exists(fullPath))
                            Directory.CreateDirectory(fullPath);


                        File.Move(tempName, fileName);
                        logger.Info("Image downloaded: {0}", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn("Error processing CommandRequest_DownloadImage: {0} ({1}) - {2}", downloadURL, EntityID,
                    ex.Message);
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                EntityID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityID"));
                EntityType = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityType"));
                ForceDownload = bool.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "ForceDownload"));
            }

            return true;
        }

        public static string GetFileURL(ImageDownloadRequest req, bool thumbNailOnly)
        {
            switch (req.ImageType)
            {
                case JMMImageType.AniDB_Cover:
                    var anime = req.ImageData as AniDB_Anime;
                    return string.Format(Constants.URLS.AniDB_Images, anime.Picname);

                case JMMImageType.TvDB_Episode:
                    var ep = req.ImageData as TvDB_Episode;
                    return string.Format(Constants.URLS.TvDB_Images, ep.Filename);

                case JMMImageType.TvDB_FanArt:
                    var fanart = req.ImageData as TvDB_ImageFanart;

                    if (thumbNailOnly)
                        return string.Format(Constants.URLS.TvDB_Images, fanart.ThumbnailPath);
                    return string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath);

                case JMMImageType.TvDB_Cover:
                    var poster = req.ImageData as TvDB_ImagePoster;
                    return string.Format(Constants.URLS.TvDB_Images, poster.BannerPath);

                case JMMImageType.TvDB_Banner:
                    var wideBanner = req.ImageData as TvDB_ImageWideBanner;
                    return string.Format(Constants.URLS.TvDB_Images, wideBanner.BannerPath);

                case JMMImageType.MovieDB_Poster:
                    var moviePoster = req.ImageData as MovieDB_Poster;
                    return string.Format(Constants.URLS.MovieDB_Images, moviePoster.URL);

                case JMMImageType.MovieDB_FanArt:

                    var movieFanart = req.ImageData as MovieDB_Fanart;
                    return string.Format(Constants.URLS.MovieDB_Images, movieFanart.URL);

                case JMMImageType.Trakt_Poster:
                    var traktPoster = req.ImageData as Trakt_ImagePoster;
                    return traktPoster.ImageURL;

                case JMMImageType.Trakt_Fanart:
                    var traktFanart = req.ImageData as Trakt_ImageFanart;
                    return traktFanart.ImageURL;

                case JMMImageType.Trakt_Friend:
                    var traktFriend = req.ImageData as Trakt_Friend;
                    return traktFriend.Avatar;

                case JMMImageType.Trakt_Episode:
                    var traktEp = req.ImageData as Trakt_Episode;
                    return traktEp.EpisodeImage;

                case JMMImageType.AniDB_Character:
                    var chr = req.ImageData as AniDB_Character;
                    return string.Format(Constants.URLS.AniDB_Images, chr.PicName);

                case JMMImageType.AniDB_Creator:
                    var creator = req.ImageData as AniDB_Seiyuu;
                    return string.Format(Constants.URLS.AniDB_Images, creator.PicName);

                default:
                    return "";
            }
        }

        private string GetFileName(ImageDownloadRequest req, bool thumbNailOnly)
        {
            switch (req.ImageType)
            {
                case JMMImageType.AniDB_Cover:

                    var anime = req.ImageData as AniDB_Anime;
                    return anime.PosterPath;

                case JMMImageType.TvDB_Episode:

                    var ep = req.ImageData as TvDB_Episode;
                    return ep.FullImagePath;

                case JMMImageType.TvDB_FanArt:

                    var fanart = req.ImageData as TvDB_ImageFanart;
                    if (thumbNailOnly)
                        return fanart.FullThumbnailPath;
                    return fanart.FullImagePath;

                case JMMImageType.TvDB_Cover:

                    var poster = req.ImageData as TvDB_ImagePoster;
                    return poster.FullImagePath;

                case JMMImageType.TvDB_Banner:

                    var wideBanner = req.ImageData as TvDB_ImageWideBanner;
                    return wideBanner.FullImagePath;

                case JMMImageType.MovieDB_Poster:

                    var moviePoster = req.ImageData as MovieDB_Poster;
                    return moviePoster.FullImagePath;

                case JMMImageType.MovieDB_FanArt:

                    var movieFanart = req.ImageData as MovieDB_Fanart;
                    return movieFanart.FullImagePath;

                case JMMImageType.Trakt_Poster:
                    var traktPoster = req.ImageData as Trakt_ImagePoster;
                    return traktPoster.FullImagePath;

                case JMMImageType.Trakt_Fanart:
                    var traktFanart = req.ImageData as Trakt_ImageFanart;
                    return traktFanart.FullImagePath;

                case JMMImageType.Trakt_Friend:
                    var traktFriend = req.ImageData as Trakt_Friend;
                    return traktFriend.FullImagePath;

                case JMMImageType.Trakt_Episode:
                    var traktEp = req.ImageData as Trakt_Episode;
                    return traktEp.FullImagePath;

                case JMMImageType.AniDB_Character:
                    var chr = req.ImageData as AniDB_Character;
                    return chr.PosterPath;

                case JMMImageType.AniDB_Creator:
                    var creator = req.ImageData as AniDB_Seiyuu;
                    return creator.PosterPath;

                default:
                    return "";
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_DownloadImage_{0}_{1}", EntityID, EntityType);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}