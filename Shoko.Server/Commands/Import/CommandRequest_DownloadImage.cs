using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml;
using Shoko.Server.Repositories.Direct;
using Shoko.Models;
using Shoko.Models.Enums;
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

        public JMMImageType EntityTypeEnum
        {
            get { return (JMMImageType) EntityType; }
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority2; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.DownloadImage,
                    extraParams = new string[] {EntityID.ToString()}
                };
            }
        }

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
                        if (ep == null) return;
                        if (string.IsNullOrEmpty(ep.Filename)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, ep, ForceDownload);
                        break;

                    case JMMImageType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (fanart == null) return;
                        if (string.IsNullOrEmpty(fanart.BannerPath)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, fanart, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (poster == null) return;
                        if (string.IsNullOrEmpty(poster.BannerPath)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, poster, ForceDownload);
                        break;

                    case JMMImageType.TvDB_Banner:
                        TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (wideBanner == null) return;
                        if (string.IsNullOrEmpty(wideBanner.BannerPath)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, wideBanner, ForceDownload);
                        break;

                    case JMMImageType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                        if (moviePoster == null) return;
                        if (string.IsNullOrEmpty(moviePoster.URL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, moviePoster, ForceDownload);
                        break;

                    case JMMImageType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                        if (movieFanart == null) return;
                        if (string.IsNullOrEmpty(movieFanart.URL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, movieFanart, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Poster:
                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(EntityID);
                        if (traktPoster == null) return;
                        if (string.IsNullOrEmpty(traktPoster.ImageURL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, traktPoster, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Fanart:
                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(EntityID);
                        if (traktFanart == null) return;
                        if (string.IsNullOrEmpty(traktFanart.ImageURL)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, traktFanart, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Friend:
                        Trakt_Friend friend = RepoFactory.Trakt_Friend.GetByID(EntityID);
                        if (friend == null) return;
                        if (string.IsNullOrEmpty(friend.Avatar)) return;
                        req = new ImageDownloadRequest(EntityTypeEnum, friend, ForceDownload);
                        break;

                    case JMMImageType.Trakt_Episode:
                        Trakt_Episode traktEp = RepoFactory.Trakt_Episode.GetByID(EntityID);
                        if (traktEp == null) return;
                        if (string.IsNullOrEmpty(traktEp.EpisodeImage)) return;
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
                        string tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));
                        if (File.Exists(tempName)) File.Delete(tempName);


                        try
                        {
                            if (fileExists) File.Delete(fileName);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                            string msg = string.Format(Shoko.Commons.Properties.Resources.Command_DeleteError, fileName,
                                ex.Message);
                            logger.Warn(msg);
                            return;
                        }


                        // download image
                        using (WebClient client = new WebClient())
                        {
                            client.Headers.Add("user-agent", "JMM");
                            //OnImageDownloadEvent(new ImageDownloadEventArgs("", req, ImageDownloadEventType.Started));
                            //BaseConfig.MyAnimeLog.Write("ProcessImages: Download: {0}  *** to ***  {1}", req.URL, fullName);
                            if (downloadURL.Length > 0)
                            {
                                client.DownloadFile(downloadURL, tempName);

                                string extension = "";
                                string contentType = client.ResponseHeaders["Content-type"].ToLower();
                                if (contentType.IndexOf("gif") >= 0) extension = ".gif";
                                if (contentType.IndexOf("jpg") >= 0) extension = ".jpg";
                                if (contentType.IndexOf("jpeg") >= 0) extension = ".jpg";
                                if (contentType.IndexOf("bmp") >= 0) extension = ".bmp";
                                if (contentType.IndexOf("png") >= 0) extension = ".png";
                                if (extension.Length > 0)
                                {
                                    string newFile = Path.ChangeExtension(tempName, extension);
                                    if (!newFile.ToLower().Equals(tempName.ToLower()))
                                    {
                                        try
                                        {
                                            System.IO.File.Delete(newFile);
                                        }
                                        catch
                                        {
                                            //BaseConfig.MyAnimeLog.Write("DownloadedImage:Download() Delete failed:{0}", newFile);
                                        }
                                        System.IO.File.Move(tempName, newFile);
                                        tempName = newFile;
                                    }
                                }
                            }
                        }

                        // move the file to it's final location
                        // check that the final folder exists
                        string fullPath = Path.GetDirectoryName(fileName);
                        if (!Directory.Exists(fullPath))
                            Directory.CreateDirectory(fullPath);


                        System.IO.File.Move(tempName, fileName);
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
            this.CommandID = string.Format("CommandRequest_DownloadImage_{0}_{1}", EntityID, (int) EntityType);
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

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}