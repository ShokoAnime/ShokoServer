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
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
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

        public ImageEntityType EntityTypeEnum => (ImageEntityType) EntityType;

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

        public QueueStateStruct PrettyDescription
        {
            get
            {
                string type;
                switch (EntityTypeEnum)
                {
                    case ImageEntityType.TvDB_Episode:
                        type = Resources.Command_ValidateAllImages_TvDBEpisodes;
                        break;
                    case ImageEntityType.TvDB_FanArt:
                        type = Resources.Command_ValidateAllImages_TvDBFanarts;
                        break;
                    case ImageEntityType.TvDB_Cover:
                        type = Resources.Command_ValidateAllImages_TvDBPosters;
                        break;
                    case ImageEntityType.TvDB_Banner:
                        type = Resources.Command_ValidateAllImages_TvDBBanners;
                        break;
                    case ImageEntityType.AniDB_Cover:
                        type = Resources.Command_ValidateAllImages_AniDBPosters;
                        break;
                    case ImageEntityType.AniDB_Character:
                        type = Resources.Command_ValidateAllImages_AniDBCharacters;
                        break;
                    case ImageEntityType.AniDB_Creator:
                        type = Resources.Command_ValidateAllImages_AniDBSeiyuus;
                        break;
                    default:
                        type = "";
                        break;
                }
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.DownloadImage,
                    extraParams = new[] { type, EntityID.ToString() }
                };
            }
        }

        public CommandRequest_DownloadImage()
        {
        }

        public CommandRequest_DownloadImage(int entityID, ImageEntityType entityType, bool forced)
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
                    case ImageEntityType.TvDB_Episode:
                        TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(EntityID);
                        if (string.IsNullOrEmpty(ep?.Filename))
                        {
                            logger.Warn($"TvDB Episode image failed to download: Can't get episode with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, ep, ForceDownload);
                        break;

                    case ImageEntityType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(fanart?.BannerPath))
                        {
                            logger.Warn($"TvDB Fanart image failed to download: Can't find valid fanart with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, fanart, ForceDownload);
                        break;

                    case ImageEntityType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(poster?.BannerPath))
                        {
                            logger.Warn($"TvDB Poster image failed to download: Can't find valid poster with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, poster, ForceDownload);
                        break;

                    case ImageEntityType.TvDB_Banner:
                        TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (string.IsNullOrEmpty(wideBanner?.BannerPath))
                        {
                            logger.Warn($"TvDB Banner image failed to download: Can't find valid banner with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, wideBanner, ForceDownload);
                        break;

                    case ImageEntityType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(moviePoster?.URL))
                        {
                            logger.Warn($"MovieDB Poster image failed to download: Can't find valid poster with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, moviePoster, ForceDownload);
                        break;

                    case ImageEntityType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(movieFanart?.URL))
                        {
                            logger.Warn($"MovieDB Fanart image failed to download: Can't find valid fanart with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, movieFanart, ForceDownload);
                        break;

                    case ImageEntityType.AniDB_Cover:
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(EntityID);
                        if (anime == null)
                        {
                            logger.Warn($"AniDB poster image failed to download: Can't find AniDB_Anime with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, anime, ForceDownload);
                        break;

                    case ImageEntityType.AniDB_Character:
                        AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(EntityID);
                        if (chr == null)
                        {
                            logger.Warn($"AniDB Character image failed to download: Can't find AniDB Character with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, chr, ForceDownload);
                        break;

                    case ImageEntityType.AniDB_Creator:
                        AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(EntityID);
                        if (creator == null)
                        {
                            logger.Warn($"AniDB Seiyuu image failed to download: Can't find Seiyuu with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, creator, ForceDownload);
                        break;
                }

                if (req == null)
                {
                    logger.Warn($"Image failed to download: No implementation found for {EntityTypeEnum}");
                    return;
                }

                List<string> fileNames = new List<string>();
                List<string> downloadURLs = new List<string>();

                string fileNameTemp = GetFileName(req, false);
                string downloadURLTemp = GetFileURL(req, false);

                fileNames.Add(fileNameTemp);
                downloadURLs.Add(downloadURLTemp);

                if (req.ImageType == ImageEntityType.TvDB_FanArt)
                {
                    fileNameTemp = GetFileName(req, true);
                    downloadURLTemp = GetFileURL(req, true);

                    fileNames.Add(fileNameTemp);
                    downloadURLs.Add(downloadURLTemp);
                }

                for (int i = 0; i < fileNames.Count; i++)
                {
                    try
                    {
                        string fileName = fileNames[i];
                        downloadURL = downloadURLs[i];

                        bool downloadImage = true;
                        bool fileExists = File.Exists(fileName);

                        if (fileExists && !req.ForceDownload) downloadImage = false;

                        if (!downloadImage) continue;

                        string tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));

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
                        logger.Info($"Image downloaded: {fileName} from {downloadURL}");
                    }
                    catch (WebException e)
                    {
                        logger.Warn("Error processing CommandRequest_DownloadImage: {0} ({1}) - {2}", downloadURL,
                            EntityID,
                            e.Message);
                        // Remove the record if the image doesn't exist or can't download
                        RemoveImageRecord();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn("Error processing CommandRequest_DownloadImage: {0} ({1}) - {2}", downloadURL, EntityID,
                    ex.Message);
            }
        }

        private void RemoveImageRecord()
        {
            switch (EntityTypeEnum)
                {
                    case ImageEntityType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (fanart == null) return;
                        RepoFactory.TvDB_ImageFanart.Delete(fanart);
                        break;

                    case ImageEntityType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (poster == null) return;
                        RepoFactory.TvDB_ImagePoster.Delete(poster);
                        break;

                    case ImageEntityType.TvDB_Banner:
                        TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (wideBanner == null) return;
                        RepoFactory.TvDB_ImageWideBanner.Delete(wideBanner);
                        break;

                    case ImageEntityType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                        if (moviePoster == null) return;
                        RepoFactory.MovieDB_Poster.Delete(moviePoster);
                        break;

                    case ImageEntityType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                        if (movieFanart == null) return;
                        RepoFactory.MovieDB_Fanart.Delete(movieFanart);
                        break;

                    case ImageEntityType.Trakt_Poster:
                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(EntityID);
                        if (traktPoster == null) return;
                        RepoFactory.Trakt_ImagePoster.Delete(traktPoster);
                        break;

                    case ImageEntityType.Trakt_Fanart:
                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(EntityID);
                        if (traktFanart == null) return;
                        RepoFactory.Trakt_ImageFanart.Delete(traktFanart);
                        break;
                }
        }

        private void RecursivelyRetryDownload(string downloadURL, ref string tempFilePath, int count, int maxretry)
        {
            try
            {
                // download image
                if (downloadURL.Length <= 0) return;
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("user-agent", "JMM");
                    //OnImageDownloadEvent(new ImageDownloadEventArgs("", req, ImageDownloadEventType.Started));
                    //BaseConfig.MyAnimeLog.Write("ProcessImages: Download: {0}  *** to ***  {1}", req.URL, fullName);

                    byte[] bytes = client.DownloadData(downloadURL);
                    if (bytes.Length < 4)
                        throw new WebException(
                            "The image download stream returned less than 4 bytes (a valid image has 2-4 bytes in the header)");

                    ImageFormatEnum imageFormat = Misc.GetImageFormat(bytes);
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

                    if (extension.Length <= 0) return;
                    string newFile = Path.ChangeExtension(tempFilePath, extension);
                    if(newFile == null) return;

                    if (File.Exists(newFile)) File.Delete(newFile);
                    using (var fs = new FileStream(newFile, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(bytes, 0, bytes.Length);
                    }
                    tempFilePath = newFile;
                }
            }
            catch (WebException)
            {
                if (count + 1 >= maxretry) throw;
                Thread.Sleep(500);
                RecursivelyRetryDownload(downloadURL, ref tempFilePath, count + 1, maxretry);
            }
        }

        public static string GetFileURL(ImageDownloadRequest req, bool thumbNailOnly)
        {
            switch (req.ImageType)
            {
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = req.ImageData as SVR_AniDB_Anime;
                    return string.Format(Constants.URLS.AniDB_Images, anime.Picname);

                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode ep = req.ImageData as TvDB_Episode;
                    return string.Format(Constants.URLS.TvDB_Images, ep.Filename);

                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;
                    if (thumbNailOnly)
                        return string.Format(Constants.URLS.TvDB_Images, fanart.ThumbnailPath);
                    else
                        return string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath);

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster poster = req.ImageData as TvDB_ImagePoster;
                    return string.Format(Constants.URLS.TvDB_Images, poster.BannerPath);

                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = req.ImageData as TvDB_ImageWideBanner;
                    return string.Format(Constants.URLS.TvDB_Images, wideBanner.BannerPath);

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster moviePoster = req.ImageData as MovieDB_Poster;
                    return string.Format(Constants.URLS.MovieDB_Images, moviePoster.URL);

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = req.ImageData as MovieDB_Fanart;
                    return string.Format(Constants.URLS.MovieDB_Images, movieFanart.URL);

                case ImageEntityType.Trakt_Poster:
                    Trakt_ImagePoster traktPoster = req.ImageData as Trakt_ImagePoster;
                    return traktPoster.ImageURL;

                case ImageEntityType.Trakt_Fanart:
                    Trakt_ImageFanart traktFanart = req.ImageData as Trakt_ImageFanart;
                    return traktFanart.ImageURL;

                case ImageEntityType.Trakt_Friend:
                    Trakt_Friend traktFriend = req.ImageData as Trakt_Friend;
                    return traktFriend.Avatar;

                case ImageEntityType.Trakt_Episode:
                    Trakt_Episode traktEp = req.ImageData as Trakt_Episode;
                    return traktEp.EpisodeImage;

                case ImageEntityType.AniDB_Character:
                    AniDB_Character chr = req.ImageData as AniDB_Character;
                    return string.Format(Constants.URLS.AniDB_Images, chr.PicName);

                case ImageEntityType.AniDB_Creator:
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
                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = req.ImageData as SVR_AniDB_Anime;
                    return anime.PosterPath;

                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode ep = req.ImageData as TvDB_Episode;
                    return ep.GetFullImagePath();

                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;
                    if (thumbNailOnly)
                        return fanart.GetFullThumbnailPath();
                    else
                        return fanart.GetFullImagePath();

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster poster = req.ImageData as TvDB_ImagePoster;
                    return poster.GetFullImagePath();

                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = req.ImageData as TvDB_ImageWideBanner;
                    return wideBanner.GetFullImagePath();

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster moviePoster = req.ImageData as MovieDB_Poster;
                    return moviePoster.GetFullImagePath();

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = req.ImageData as MovieDB_Fanart;
                    return movieFanart.GetFullImagePath();

                case ImageEntityType.Trakt_Poster:
                    Trakt_ImagePoster traktPoster = req.ImageData as Trakt_ImagePoster;
                    return traktPoster.GetFullImagePath();

                case ImageEntityType.Trakt_Fanart:
                    Trakt_ImageFanart traktFanart = req.ImageData as Trakt_ImageFanart;
                    return traktFanart.GetFullImagePath();

                case ImageEntityType.Trakt_Friend:
                    Trakt_Friend traktFriend = req.ImageData as Trakt_Friend;
                    return traktFriend.GetFullImagePath();

                case ImageEntityType.Trakt_Episode:
                    Trakt_Episode traktEp = req.ImageData as Trakt_Episode;
                    return traktEp.GetFullImagePath();

                case ImageEntityType.AniDB_Character:
                    AniDB_Character chr = req.ImageData as AniDB_Character;
                    return chr.GetPosterPath();

                case ImageEntityType.AniDB_Creator:
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