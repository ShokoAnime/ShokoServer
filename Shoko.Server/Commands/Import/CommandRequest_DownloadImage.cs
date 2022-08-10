using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.AniDB_API;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.ImageDownload)]
    public class CommandRequest_DownloadImage : CommandRequestImplementation
    {
        public int EntityID { get; set; }
        public int EntityType { get; set; }
        public bool ForceDownload { get; set; }

        public ImageEntityType EntityTypeEnum => (ImageEntityType) EntityType;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

        public override QueueStateStruct PrettyDescription
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
                    case ImageEntityType.MovieDB_Poster:
                        type = Resources.Command_ValidateAllImages_MovieDBPosters;
                        break;
                    case ImageEntityType.MovieDB_FanArt:
                        type = Resources.Command_ValidateAllImages_MovieDBFanarts;
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
                        type = string.Empty;
                        break;
                }
                return new QueueStateStruct
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
            EntityID = entityID;
            EntityType = (int) entityType;
            ForceDownload = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_DownloadImage: {0}", EntityID);
            string downloadURL = string.Empty;

            try
            {
                ImageDownloadRequest req = null;
                switch (EntityTypeEnum)
                {
                    case ImageEntityType.TvDB_Episode:
                        TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(EntityID);
                        if (string.IsNullOrEmpty(ep?.Filename))
                        {
                            Logger.LogWarning($"TvDB Episode image failed to download: Can't get episode with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, ep, ForceDownload);
                        break;

                    case ImageEntityType.TvDB_FanArt:
                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(fanart?.BannerPath))
                        {
                            Logger.LogWarning($"TvDB Fanart image failed to download: Can't find valid fanart with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, fanart, ForceDownload);
                        break;

                    case ImageEntityType.TvDB_Cover:
                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(poster?.BannerPath))
                        {
                            Logger.LogWarning($"TvDB Poster image failed to download: Can't find valid poster with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, poster, ForceDownload);
                        break;

                    case ImageEntityType.TvDB_Banner:
                        TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (string.IsNullOrEmpty(wideBanner?.BannerPath))
                        {
                            Logger.LogWarning($"TvDB Banner image failed to download: Can't find valid banner with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, wideBanner, ForceDownload);
                        break;

                    case ImageEntityType.MovieDB_Poster:
                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(moviePoster?.URL))
                        {
                            Logger.LogWarning($"MovieDB Poster image failed to download: Can't find valid poster with ID: {EntityID}");
                            RemoveImageRecord();
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, moviePoster, ForceDownload);
                        break;

                    case ImageEntityType.MovieDB_FanArt:
                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(movieFanart?.URL))
                        {
                            Logger.LogWarning($"MovieDB Fanart image failed to download: Can't find valid fanart with ID: {EntityID}");
                            return;
                        }
                        req = new ImageDownloadRequest(EntityTypeEnum, movieFanart, ForceDownload);
                        break;

                    case ImageEntityType.AniDB_Cover:
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(EntityID);
                        if (anime == null)
                        {
                            Logger.LogWarning($"AniDB poster image failed to download: Can't find AniDB_Anime with ID: {EntityID}");
                            return;
                        }
                        AniDbImageRateLimiter.Instance.EnsureRate();
                        req = new ImageDownloadRequest(EntityTypeEnum, anime, ForceDownload);
                        break;

                    case ImageEntityType.AniDB_Character:
                        AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(EntityID);
                        if (chr == null)
                        {
                            Logger.LogWarning($"AniDB Character image failed to download: Can't find AniDB Character with ID: {EntityID}");
                            return;
                        }
                        AniDbImageRateLimiter.Instance.EnsureRate();
                        req = new ImageDownloadRequest(EntityTypeEnum, chr, ForceDownload);
                        break;

                    case ImageEntityType.AniDB_Creator:
                        AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(EntityID);
                        if (creator == null)
                        {
                            Logger.LogWarning($"AniDB Seiyuu image failed to download: Can't find Seiyuu with ID: {EntityID}");
                            return;
                        }
                        AniDbImageRateLimiter.Instance.EnsureRate();
                        req = new ImageDownloadRequest(EntityTypeEnum, creator, ForceDownload);
                        break;
                }

                if (req == null)
                {
                    Logger.LogWarning($"Image failed to download: No implementation found for {EntityTypeEnum}");
                    return;
                }

                List<string> fileNames = new List<string>();
                List<string> downloadURLs = new List<string>();

                string fileNameTemp = GetFileName(req);
                string downloadURLTemp = GetFileURL(serviceProvider, req);

                fileNames.Add(fileNameTemp);
                downloadURLs.Add(downloadURLTemp);

                for (int i = 0; i < fileNames.Count; i++)
                {
                    try
                    {
                        string fileName = fileNames[i];
                        downloadURL = downloadURLs[i];

                        bool downloadImage = true;
                        bool fileExists = File.Exists(fileName);
                        bool imageValid = fileExists && Misc.IsImageValid(fileName);

                        if (imageValid && !req.ForceDownload) downloadImage = false;

                        if (!downloadImage) continue;

                        string tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));

                        try
                        {
                            if (fileExists) File.Delete(fileName);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

                            Logger.LogWarning(Resources.Command_DeleteError, fileName, ex.Message);
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
                        Logger.LogInformation($"Image downloaded: {fileName} from {downloadURL}");
                    }
                    catch (WebException e)
                    {
                        Logger.LogWarning("Error processing CommandRequest_DownloadImage: {0} ({1}) - {2}", downloadURL,
                            EntityID,
                            e.Message);
                        // Remove the record if the image doesn't exist or can't download
                        RemoveImageRecord();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error processing CommandRequest_DownloadImage: {0} ({1}) - {2}", downloadURL, EntityID,
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
                }
        }

        private void RecursivelyRetryDownload(string downloadURL, ref string tempFilePath, int count, int maxretry)
        {
            try
            {
                // download image
                if (downloadURL.Length <= 0) return;
                
                // Ignore all certificate failures.
                ServicePointManager.Expect100Continue = true;                
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                
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

        public static string GetFileURL(IServiceProvider provider, ImageDownloadRequest req)
        {
            IUDPConnectionHandler handler;
            switch (req.ImageType)
            {
                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode ep = req.ImageData as TvDB_Episode;
                    return string.Format(Constants.URLS.TvDB_Episode_Images, ep.Filename);

                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;
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

                case ImageEntityType.AniDB_Cover:
                    SVR_AniDB_Anime anime = req.ImageData as SVR_AniDB_Anime;
                    handler = provider.GetRequiredService<IUDPConnectionHandler>();
                    return string.Format(handler.ImageServerUrl, anime.Picname);

                case ImageEntityType.AniDB_Character:
                    AniDB_Character chr = req.ImageData as AniDB_Character;
                    handler = provider.GetRequiredService<IUDPConnectionHandler>();
                    return string.Format(handler.ImageServerUrl, chr.PicName);

                case ImageEntityType.AniDB_Creator:
                    AniDB_Seiyuu creator = req.ImageData as AniDB_Seiyuu;
                    handler = provider.GetRequiredService<IUDPConnectionHandler>();
                    return string.Format(handler.ImageServerUrl, creator.PicName);

                default:
                    return string.Empty;
            }
        }

        private string GetFileName(ImageDownloadRequest req)
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

                case ImageEntityType.AniDB_Character:
                    AniDB_Character chr = req.ImageData as AniDB_Character;
                    return chr.GetPosterPath();

                case ImageEntityType.AniDB_Creator:
                    AniDB_Seiyuu creator = req.ImageData as AniDB_Seiyuu;
                    return creator.GetPosterPath();

                default:
                    return string.Empty;
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_DownloadImage_{EntityID}_{EntityType}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                EntityID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityID"));
                EntityType = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityType"));
                ForceDownload =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "ForceDownload"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
