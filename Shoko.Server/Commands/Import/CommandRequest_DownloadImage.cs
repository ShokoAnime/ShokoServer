using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Server.Repositories.Direct;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_DownloadImage : CommandRequestImplementation, ICommandRequest
    {
        public int EntityID { get; set; }
        public int EntityType { get; set; }
        public bool ForceDownload { get; set; }

        public JMMImageType EntityTypeEnum => (JMMImageType) EntityType;

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

        public QueueStateStruct PrettyDescription => new QueueStateStruct()
        {
            queueState = QueueStateEnum.DownloadImage,
            extraParams = new string[] {EntityID.ToString()}
        };

        public CommandRequest_DownloadImage()
        {
        }

        public CommandRequest_DownloadImage(int entityID, JMMImageType entityType, bool forced)
        {
            this.EntityID = entityID;
            this.EntityType = (int) entityType;
            this.ForceDownload = forced;
            this.CommandType = (int) CommandRequestType.ImageDownload;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_DownloadImage: {0}", EntityID);
            string downloadURL = "";
            try
            {
                ImageDownloadRequest req = null;
                switch (EntityTypeEnum)
                {
                    case JMMImageType.AniDB_Cover:
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByID(EntityID);
                        if (anime == null) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, anime, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Episode:
                        TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(EntityID);
                        if (string.IsNullOrEmpty(ep?.Filename)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, ep, ForceDownload);
                        break;

                    case JMMImageType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(fanart?.BannerPath)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, fanart, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(poster?.BannerPath)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, poster, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Banner:
                        TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (string.IsNullOrEmpty(wideBanner?.BannerPath)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, wideBanner, ForceDownload);
                        break;

                    case JMMImageType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(moviePoster?.URL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, moviePoster, ForceDownload);
                        break;

                    case JMMImageType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(movieFanart?.URL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, movieFanart, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Poster:
                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(traktPoster?.ImageURL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, traktPoster, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Fanart:
                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(traktFanart?.ImageURL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, traktFanart, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Friend:
                        Trakt_Friend friend = RepoFactory.Trakt_Friend.GetByID(EntityID);
                        if (string.IsNullOrEmpty(friend?.Avatar)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, friend, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Episode:
                        Trakt_Episode traktEp = RepoFactory.Trakt_Episode.GetByID(EntityID);
                        if (string.IsNullOrEmpty(traktEp?.EpisodeImage)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, traktEp, ForceDownload);
                        break;

                    case JMMImageType.AniDB_Character:
                        AniDB_Character chr = RepoFactory.AniDB_Character.GetByID(EntityID);
                        if (chr == null) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, chr, ForceDownload);
                        break;

                    case JMMImageType.AniDB_Creator:
                        AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetByID(EntityID);
                        if (creator == null) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, creator, ForceDownload);
                        break;
                }

                if (req == null) return;

                List<string> fileNames = new List<string>();
                List<string> downloadURLs = new List<string>();

                string fileNameTemp = GetFileName(req, false);
                string downloadURLTemp = GetFileURL(req, false);

                fileNames.Add(fileNameTemp);
                downloadURLs.Add(downloadURLTemp);

                if (req.ImageType == JMMImageType.TvDB_FanArt)
                {
                    fileNameTemp = GetFileName(req, true);
                    downloadURLTemp = GetFileURL(req, true);

                    fileNames.Add(fileNameTemp);
                    downloadURLs.Add(downloadURLTemp);
                }

                for (int i = 0; i < fileNames.Count; i++)
                {
                    string fileName = fileNames[i];
                    downloadURL = downloadURLs[i];

                    bool downloadImage = true;
                    bool fileExists = File.Exists(fileName);

                    if (fileExists && !req.ForceDownload) downloadImage = false;

                    if (downloadImage)
                    {
                        string tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));
                        if (File.Exists(tempName)) File.Delete(tempName);


                        try
                        {
                            if (fileExists) File.Delete(fileName);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                            string msg = string.Format(Commons.Properties.Resources.Command_DeleteError, fileName,
                                ex.Message);
                            logger.Warn(msg);
                            return;
                        }

                        // If this has any issues, it will throw an exception, so the catch below will handle it
                        RecursivelyRetryDownload(downloadURL, ref tempName, 0, 5);

                        // move the file to it's final location
                        // check that the final folder exists
                        string fullPath = Path.GetDirectoryName(fileName);
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
                return;
            }
        }

        private void RecursivelyRetryDownload(string downloadURL, ref string tempFilePath, int count, int maxretry)
        {
            try
            {
                // download image
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("user-agent", "JMM");
                    //OnImageDownloadEvent(new ImageDownloadEventArgs("", req, ImageDownloadEventType.Started));
                    //BaseConfig.MyAnimeLog.Write("ProcessImages: Download: {0}  *** to ***  {1}", req.URL, fullName);
                    if (downloadURL.Length > 0)
                    {
                        byte[] bytes = client.DownloadData(downloadURL);
                        if (bytes.Length < 4)
                            throw new WebException(
                                "The image download stream returned less than 4 bytes (a valid image has 2-4 bytes in the header)");

                        ImageFormatEnum imageFormat = GetImageFormat(bytes);
                        string extension;
                        switch (imageFormat)
                        {
                            case ImageFormatEnum.bmp:
                                extension = ".bmp";
                                break;
                            case ImageFormatEnum.gif:
                                extension = ".gif";
                                break;
                            case ImageFormatEnum.jpeg:
                                extension = ".jpeg";
                                break;
                            case ImageFormatEnum.png:
                                extension = ".png";
                                break;
                            case ImageFormatEnum.tiff:
                                extension = ".tiff";
                                break;
                            default: throw new WebException("The image download stream returned an invalid image");
                        }

                        if (extension.Length > 0)
                        {
                            string newFile = Path.ChangeExtension(tempFilePath, extension);
                            if(newFile == null) return;

                            using (var fs = new FileStream(newFile, FileMode.Create, FileAccess.Write))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                            }
                            tempFilePath = newFile;
                        }
                    }
                }
            }
            catch (WebException)
            {
                if (count + 1 >= maxretry) throw;
                Thread.Sleep(500);
                RecursivelyRetryDownload(downloadURL, ref tempFilePath, count + 1, maxretry);
            }
        }

        public static ImageFormatEnum GetImageFormat(byte[] bytes)
        {
            // see http://www.mikekunz.com/image_file_header.html
            var bmp    = Encoding.ASCII.GetBytes("BM");     // BMP
            var gif    = Encoding.ASCII.GetBytes("GIF");    // GIF
            var png    = new byte[] { 137, 80, 78, 71 };    // PNG
            var tiff   = new byte[] { 73, 73, 42 };         // TIFF
            var tiff2  = new byte[] { 77, 77, 42 };         // TIFF
            var jpeg   = new byte[] { 255, 216, 255, 224 }; // jpeg
            var jpeg2  = new byte[] { 255, 216, 255, 225 }; // jpeg canon

            if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
                return ImageFormatEnum.bmp;

            if (gif.SequenceEqual(bytes.Take(gif.Length)))
                return ImageFormatEnum.gif;

            if (png.SequenceEqual(bytes.Take(png.Length)))
                return ImageFormatEnum.png;

            if (tiff.SequenceEqual(bytes.Take(tiff.Length)))
                return ImageFormatEnum.tiff;

            if (tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
                return ImageFormatEnum.tiff;

            if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
                return ImageFormatEnum.jpeg;

            if (jpeg2.SequenceEqual(bytes.Take(jpeg2.Length)))
                return ImageFormatEnum.jpeg;

            return ImageFormatEnum.unknown;
        }

        public static string GetFileURL(ImageDownloadRequest req, bool thumbNailOnly)
        {
            switch (req.ImageType)
            {
                case JMMImageType.AniDB_Cover:
                    SVR_AniDB_Anime anime = req.ImageData as SVR_AniDB_Anime;
                    return string.Format(Constants.URLS.AniDB_Images, anime.Picname);

                case JMMImageType.TvDB_Episode:
                    TvDB_Episode ep = req.ImageData as TvDB_Episode;
                    return string.Format(Constants.URLS.TvDB_Images, ep.Filename);

                case JMMImageType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;
                    if (thumbNailOnly)
                        return string.Format(Constants.URLS.TvDB_Images, fanart.ThumbnailPath);
                    else
                        return string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath);

                case JMMImageType.TvDB_Cover:
                    TvDB_ImagePoster poster = req.ImageData as TvDB_ImagePoster;
                    return string.Format(Constants.URLS.TvDB_Images, poster.BannerPath);

                case JMMImageType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = req.ImageData as TvDB_ImageWideBanner;
                    return string.Format(Constants.URLS.TvDB_Images, wideBanner.BannerPath);

                case JMMImageType.MovieDB_Poster:
                    MovieDB_Poster moviePoster = req.ImageData as MovieDB_Poster;
                    return string.Format(Constants.URLS.MovieDB_Images, moviePoster.URL);

                case JMMImageType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = req.ImageData as MovieDB_Fanart;
                    return string.Format(Constants.URLS.MovieDB_Images, movieFanart.URL);

                case JMMImageType.Trakt_Poster:
                    Trakt_ImagePoster traktPoster = req.ImageData as Trakt_ImagePoster;
                    return traktPoster.ImageURL;

                case JMMImageType.Trakt_Fanart:
                    Trakt_ImageFanart traktFanart = req.ImageData as Trakt_ImageFanart;
                    return traktFanart.ImageURL;

                case JMMImageType.Trakt_Friend:
                    Trakt_Friend traktFriend = req.ImageData as Trakt_Friend;
                    return traktFriend.Avatar;

                case JMMImageType.Trakt_Episode:
                    Trakt_Episode traktEp = req.ImageData as Trakt_Episode;
                    return traktEp.EpisodeImage;

                case JMMImageType.AniDB_Character:
                    AniDB_Character chr = req.ImageData as AniDB_Character;
                    return string.Format(Constants.URLS.AniDB_Images, chr.PicName);

                case JMMImageType.AniDB_Creator:
                    AniDB_Seiyuu creator = req.ImageData as AniDB_Seiyuu;
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
                    SVR_AniDB_Anime anime = req.ImageData as SVR_AniDB_Anime;
                    return anime.PosterPath;

                case JMMImageType.TvDB_Episode:
                    TvDB_Episode ep = req.ImageData as TvDB_Episode;
                    return ep.GetFullImagePath();

                case JMMImageType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;
                    if (thumbNailOnly)
                        return fanart.GetFullThumbnailPath();
                    else
                        return fanart.GetFullImagePath();

                case JMMImageType.TvDB_Cover:
                    TvDB_ImagePoster poster = req.ImageData as TvDB_ImagePoster;
                    return poster.GetFullImagePath();

                case JMMImageType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = req.ImageData as TvDB_ImageWideBanner;
                    return wideBanner.GetFullImagePath();

                case JMMImageType.MovieDB_Poster:
                    MovieDB_Poster moviePoster = req.ImageData as MovieDB_Poster;
                    return moviePoster.GetFullImagePath();

                case JMMImageType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = req.ImageData as MovieDB_Fanart;
                    return movieFanart.GetFullImagePath();

                case JMMImageType.Trakt_Poster:
                    Trakt_ImagePoster traktPoster = req.ImageData as Trakt_ImagePoster;
                    return traktPoster.GetFullImagePath();

                case JMMImageType.Trakt_Fanart:
                    Trakt_ImageFanart traktFanart = req.ImageData as Trakt_ImageFanart;
                    return traktFanart.GetFullImagePath();

                case JMMImageType.Trakt_Friend:
                    Trakt_Friend traktFriend = req.ImageData as Trakt_Friend;
                    return traktFriend.GetFullImagePath();

                case JMMImageType.Trakt_Episode:
                    Trakt_Episode traktEp = req.ImageData as Trakt_Episode;
                    return traktEp.GetFullImagePath();

                case JMMImageType.AniDB_Character:
                    AniDB_Character chr = req.ImageData as AniDB_Character;
                    return chr.GetPosterPath();

                case JMMImageType.AniDB_Creator:
                    AniDB_Seiyuu creator = req.ImageData as AniDB_Seiyuu;
                    return creator.GetPosterPath();

                default:
                    return "";
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = $"CommandRequest_DownloadImage_{EntityID}_{EntityType}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.EntityID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityID"));
                this.EntityType = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityType"));
                this.ForceDownload =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "ForceDownload"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}