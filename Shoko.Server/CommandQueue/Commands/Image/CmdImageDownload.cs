using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Image
{

    public class CmdImageDownload : BaseCommand, ICommand
    {
        public int EntityID { get; set; }
        public int EntityType { get; set; }
        public bool ForceDownload { get; set; }

        [JsonIgnore]
        public ImageEntityType EntityTypeEnum => (ImageEntityType) EntityType;
        private ImageTypeInfo _info;


        public string ParallelTag
        {
            get => _info.Tag;
            set => _info.Tag = value;
        }

        public int ParallelMax
        {
            get => _info.MaxThreads;
            set => _info.MaxThreads = value;
        }


        public int Priority { get; set; } = 2;
        public string Id => $"DownloadImage_{EntityID}_{EntityType}";
        public WorkTypes WorkType => _info.WorkType;

        private class ImageTypeInfo
        {
            public string Tag;
            public string FormatDescription;
            public WorkTypes WorkType;
            public int MaxThreads;
            public Func<object> FindFunc;
            public Func<object, string> FileNameFunc;
            public Func<object, string> FileNameThumbFunc;
            public Func<object, bool> ValidateFunc;
            public Func<object, string> UrlFunc;
            public Func<bool> DeleteFunc;
            public string Error;

            public ImageTypeInfo(ImageEntityType type, int entityId)
            {

                switch (type)
                {
                    case ImageEntityType.TvDB_Episode:
                        FormatDescription = Resources.Command_ValidateAllImages_TvDBEpisodes;
                        FindFunc = ()=>Repo.Instance.TvDB_Episode.GetByID(entityId);
                        //NO DELETE
                        FileNameFunc = a => ((TvDB_Episode) a).GetFullImagePath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((TvDB_Episode) a)?.Filename ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.TvDB_Episode_Images, ((TvDB_Episode)a).Filename);
                        MaxThreads = 4;
                        WorkType = WorkTypes.Image;
                        Tag = "TvDB";
                        Error = $"TvDB Episode image failed to download: Can't get episode with ID: {entityId}";
                        break;
                    case ImageEntityType.TvDB_FanArt:
                        FormatDescription = Resources.Command_ValidateAllImages_TvDBFanarts;
                        FindFunc = ()=>Repo.Instance.TvDB_ImageFanart.GetByID(entityId);
                        DeleteFunc = () => Repo.Instance.TvDB_ImageFanart.FindAndDelete(() => (TvDB_ImageFanart)FindFunc());
                        FileNameFunc = a => ((TvDB_ImageFanart) a).GetFullImagePath();
                        FileNameThumbFunc = a => ((TvDB_ImageFanart) a).GetFullThumbnailPath();
                        ValidateFunc = a => !string.IsNullOrEmpty(((TvDB_ImageFanart)a)?.BannerPath ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.TvDB_Images, ((TvDB_ImageFanart) a).Id);
                        MaxThreads = 4;
                        WorkType = WorkTypes.Image;
                        Tag = "TvDB";
                        Error = $"TvDB Fanart image failed to download: Can't find valid fanart with ID: {entityId}";
                        break;
                    case ImageEntityType.TvDB_Cover:
                        FormatDescription = Resources.Command_ValidateAllImages_TvDBPosters;
                        FindFunc = ()=>Repo.Instance.TvDB_ImagePoster.GetByID(entityId);
                        DeleteFunc = () => Repo.Instance.TvDB_ImagePoster.FindAndDelete(() => (TvDB_ImagePoster)FindFunc());
                        FileNameFunc = a => ((TvDB_ImagePoster) a).GetFullImagePath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a=> !string.IsNullOrEmpty(((TvDB_ImagePoster)a)?.BannerPath ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.TvDB_Images, ((TvDB_ImagePoster) a).Id);
                        MaxThreads = 4;
                        WorkType = WorkTypes.Image;
                        Tag = "TvDB";
                        Error = $"TvDB Poster image failed to download: Can't find valid poster with ID: {entityId}";
                        break;
                    case ImageEntityType.TvDB_Banner:
                        FormatDescription = Resources.Command_ValidateAllImages_TvDBBanners;
                        FindFunc = ()=>Repo.Instance.TvDB_ImageWideBanner.GetByID(entityId);
                        DeleteFunc = () => Repo.Instance.TvDB_ImageWideBanner.FindAndDelete(() => (TvDB_ImageWideBanner)FindFunc());
                        FileNameFunc = a => ((TvDB_ImageWideBanner) a).GetFullImagePath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((TvDB_ImageWideBanner)a)?.BannerPath ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.TvDB_Images, ((TvDB_ImageWideBanner)a).Id);
                        MaxThreads = 4;
                        WorkType = WorkTypes.Image;
                        Tag = "TvDB";
                        Error = $"TvDB Banner image failed to download: Can't find valid banner with ID: {entityId}";
                        break;
                    case ImageEntityType.MovieDB_Poster:
                        FormatDescription = Resources.Command_ValidateAllImages_MovieDBPosters;
                        FindFunc = ()=>Repo.Instance.MovieDB_Poster.GetByID(entityId);
                        DeleteFunc = () => Repo.Instance.MovieDB_Poster.FindAndDelete(() => (MovieDB_Poster)FindFunc());
                        FileNameFunc = a => ((MovieDB_Poster)a).GetFullImagePath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((MovieDB_Poster)a)?.URL ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.MovieDB_Images, ((MovieDB_Poster)a).URL);
                        MaxThreads = 4;
                        WorkType = WorkTypes.Image;
                        Tag = "MovieDB";
                        Error = $"MovieDB Poster image failed to download: Can't find valid poster with ID: {entityId}";
                        break;
                    case ImageEntityType.MovieDB_FanArt:
                        FormatDescription = Resources.Command_ValidateAllImages_MovieDBFanarts;
                        FindFunc = ()=>Repo.Instance.MovieDB_Fanart.GetByID(entityId);
                        DeleteFunc = () => Repo.Instance.MovieDB_Fanart.FindAndDelete(() => (MovieDB_Fanart)FindFunc());
                        FileNameFunc = a => ((MovieDB_Fanart)a).GetFullImagePath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((MovieDB_Fanart)a)?.URL ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.MovieDB_Images, ((MovieDB_Fanart)a).URL);
                        MaxThreads = 4;
                        WorkType = WorkTypes.Image;
                        Tag = "MovieDB";
                        Error = $"MovieDB Fanart image failed to download: Can't find valid fanart with ID: {entityId}";
                        break;
                    case ImageEntityType.AniDB_Cover:
                        FormatDescription = Resources.Command_ValidateAllImages_AniDBPosters;
                        FindFunc = ()=>Repo.Instance.AniDB_Anime.GetByAnimeID(entityId);
                        //NO DELETE
                        FileNameFunc = a => ((SVR_AniDB_Anime) a).PosterPath;
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((AniDB_Anime)a)?.Picname ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.AniDB_Images, ((AniDB_Anime) a).Picname);
                        MaxThreads = 1;
                        WorkType = WorkTypes.Image;
                        Tag = WorkTypes.AniDB.ToString();
                        Error = $"AniDB poster image failed to download: Can't find AniDB_Anime with ID: {entityId}";
                        break;
                    case ImageEntityType.AniDB_Character:
                        FormatDescription = Resources.Command_ValidateAllImages_AniDBCharacters;
                        FindFunc = () => Repo.Instance.AniDB_Character.GetByCharID(entityId);
                        //NO DELETE
                        FileNameFunc = a => ((AniDB_Character)a).GetPosterPath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((AniDB_Character)a)?.PicName ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.AniDB_Images, ((AniDB_Character) a).PicName);
                        MaxThreads = 1;
                        WorkType = WorkTypes.Image;
                        Tag = WorkTypes.AniDB.ToString();
                        Error = $"AniDB Character image failed to download: Can't find AniDB Character with ID: {entityId}";
                        break;
                    case ImageEntityType.AniDB_Creator:
                        FormatDescription = Resources.Command_ValidateAllImages_AniDBSeiyuus;
                        FindFunc = () => Repo.Instance.AniDB_Seiyuu.GetBySeiyuuID(entityId);
                        //NO DELETE
                        FileNameFunc = a => ((AniDB_Seiyuu)a).GetPosterPath();
                        FileNameThumbFunc = FileNameFunc;
                        ValidateFunc = a => !string.IsNullOrEmpty(((AniDB_Seiyuu)a)?.PicName ?? "");
                        UrlFunc = a => string.Format(Constants.URLS.AniDB_Images, ((AniDB_Seiyuu)a).PicName);
                        MaxThreads = 1;
                        WorkType = WorkTypes.Image;
                        Tag = WorkTypes.AniDB.ToString();
                        Error = $"AniDB Seiyuu image failed to download: Can't find Seiyuu with ID: {entityId}";
                        break;
                }
            }
        }


        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.DownloadImage, ExtraParams = new[] {_info.FormatDescription, EntityID.ToString()}};



        public CmdImageDownload(string str) : base(str)
        {
            _info=new ImageTypeInfo(EntityTypeEnum,EntityID);
        }

        public CmdImageDownload(int entityID, ImageEntityType entityType, bool forced)
        {
            EntityID = entityID;
            EntityType = (int) entityType;
            ForceDownload = forced;
            _info = new ImageTypeInfo(EntityTypeEnum, EntityID);
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_DownloadImage: {0}", EntityID);
            string downloadURL = string.Empty;

            try
            {
                InitProgress(progress);
                if (_info.ValidateFunc == null)
                {
                    logger.Warn($"Image failed to download: No implementation found for {EntityTypeEnum}");
                    ReportFinishAndGetResult(progress);
                    return;
                }
                var obj = _info.FindFunc();
                if (!_info.ValidateFunc(obj))
                {
                    logger.Warn(_info.Error);
                    _info.DeleteFunc?.Invoke();
                    ReportFinishAndGetResult(progress);
                    return;
                }
                List<string> fileNames = new List<string>();
                List<string> downloadURLs = new List<string>();

                string fileNameTemp = _info.FileNameFunc(obj);
                string downloadURLTemp = _info.UrlFunc(obj);
                
                fileNames.Add(fileNameTemp);
                downloadURLs.Add(downloadURLTemp);

                if (EntityTypeEnum == ImageEntityType.TvDB_FanArt)
                {
                    fileNameTemp = _info.FileNameThumbFunc(obj);
                    downloadURLTemp = _info.UrlFunc(obj);

                    fileNames.Add(fileNameTemp);
                    downloadURLs.Add(downloadURLTemp);
                }

                for (int i = 0; i < fileNames.Count; i++)
                {
                    try
                    {
                        double val = i * 80 / (double)fileNames.Count;
                        UpdateAndReportProgress(progress,20+val);
                        string fileName = fileNames[i];
                        downloadURL = downloadURLs[i];

                        bool downloadImage = true;
                        bool fileExists = File.Exists(fileName);
                        bool imageValid = fileExists && Misc.IsImageValid(fileName);

                        if (imageValid && !ForceDownload) downloadImage = false;

                        if (!downloadImage) continue;

                        string tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));

                        try
                        {
                            if (fileExists) File.Delete(fileName);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);
                            logger.Warn(Resources.Command_DeleteError, fileName, ex.Message);
                            ReportFinishAndGetResult(progress);
                            return;
                        }

                        // If this has any issues, it will throw an exception, so the catch below will handle it
                        RecursivelyRetryDownload(downloadURL, ref tempName, 0, 5, _info.Tag == WorkTypes.AniDB.ToString());

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
                        _info.DeleteFunc?.Invoke();
                    }

                }
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing CommandRequest_DownloadImage: {downloadURL ?? ""} ({EntityTypeEnum}) - {EntityID}",ex);
            }
        }

    

        private void RecursivelyRetryDownload(string downloadURL, ref string tempFilePath, int count, int maxretry, bool AniDB)
        {
            try
            {
                if (AniDB)
                    AniDbImageRateLimiter.Instance.EnsureRate();
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
                RecursivelyRetryDownload(downloadURL, ref tempFilePath, count + 1, maxretry,AniDB);
            }
        }
    }
}
