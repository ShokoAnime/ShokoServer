using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class DownloadTvDBImageJob : BaseJob, IImageDownloadJob
{
    private const string FailedToDownloadNoID = "Image failed to download: Can\'t find valid {ImageType} with ID: {ImageID}";
    private const string FailedToDownloadNoImpl = "Image failed to download: No implementation found for {ImageType}";

    private readonly ImageHttpClientFactory _clientFactory;
    private string _animeTitle;

    public int AnimeID { get; set; }
    public int ImageID { get; set; }
    public bool ForceDownload { get; set; }

    public ImageEntityType ImageType { get; set; }

    public override void PostInit()
    {
        _animeTitle = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override string Name => "Download TvDB Image";
    public override QueueStateStruct Description => new()
    {
        message = "Downloading {0} for {1}: {2}",
        queueState = QueueStateEnum.DownloadImage,
        extraParams = new[] { ImageType.ToString().Replace("_", " "), _animeTitle, ImageID.ToString()}
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime} -> Image Type: {ImageType} | ImageID: {EntityID}", nameof(DownloadTvDBImageJob), _animeTitle, ImageType, ImageID);

        var imageType = ImageType.ToString().Replace("_", " ");
        string downloadUrl = null;
        string filePath = null;
        switch (ImageType)
        {
            case ImageEntityType.TvDB_Episode:
                var ep = RepoFactory.TvDB_Episode.GetByID(ImageID);
                if (string.IsNullOrEmpty(ep?.Filename))
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    return;
                }

                downloadUrl = string.Format(Constants.URLS.TvDB_Episode_Images, ep.Filename);
                filePath = ep.GetFullImagePath();
                break;

            case ImageEntityType.TvDB_FanArt:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(ImageID);
                if (string.IsNullOrEmpty(fanart?.BannerPath))
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    RemoveImageRecord();
                    return;
                }

                downloadUrl = string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath);
                filePath = fanart.GetFullImagePath();
                break;

            case ImageEntityType.TvDB_Cover:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(ImageID);
                if (string.IsNullOrEmpty(poster?.BannerPath))
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    RemoveImageRecord();
                    return;
                }

                downloadUrl = string.Format(Constants.URLS.TvDB_Images, poster.BannerPath);
                filePath = poster.GetFullImagePath();
                break;

            case ImageEntityType.TvDB_Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(ImageID);
                if (string.IsNullOrEmpty(wideBanner?.BannerPath))
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    RemoveImageRecord();
                    return;
                }

                downloadUrl = string.Format(Constants.URLS.TvDB_Images, wideBanner.BannerPath);
                filePath = wideBanner.GetFullImagePath();
                break;
        }

        if (downloadUrl == null || filePath == null)
        {
            _logger.LogWarning(FailedToDownloadNoImpl, imageType);
            return;
        }

        try
        {
            // If this has any issues, it will throw an exception, so the catch below will handle it.
            var result = await DownloadNow(downloadUrl, filePath);
            switch (result)
            {
                case ImageDownloadResult.Success:
                    _logger.LogInformation("Image downloaded for {Anime}: {FilePath} from {DownloadUrl}", _animeTitle, filePath, downloadUrl);
                    break;
                case ImageDownloadResult.Cached:
                    _logger.LogDebug("Image already in cache for {Anime}: {FilePath} from {DownloadUrl}", _animeTitle, filePath, downloadUrl);
                    break;
                case ImageDownloadResult.Failure:
                    _logger.LogWarning("Image failed to download for {Anime}: {FilePath} from {DownloadUrl}", _animeTitle, filePath, downloadUrl);
                    break;
                case ImageDownloadResult.RemovedResource:
                    _logger.LogWarning("Image failed to download for {Anime} and the local entry has been removed: {FilePath} from {DownloadUrl}", _animeTitle,
                        filePath, downloadUrl);
                    break;
                case ImageDownloadResult.InvalidResource:
                    _logger.LogWarning("Image failed to download for {Anime} and the local entry could not be removed: {FilePath} from {DownloadUrl}",
                        _animeTitle, filePath, downloadUrl);
                    break;
            }
        }
        catch (WebException e)
        {
            _logger.LogWarning("Error processing {Job} for {Anime}: {Url} ({EntityID}) - {Message}", nameof(DownloadAniDBImageJob), _animeTitle, downloadUrl,
                ImageID, e.Message);
        }
    }
    
    private async Task<ImageDownloadResult> DownloadNow(string downloadUrl, string filePath)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<WebException>()
            .OrResult(ImageDownloadResult.InvalidResource)
            .OrResult(ImageDownloadResult.RemovedResource)
            .WaitAndRetryAsync(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5) }, (result, _) =>
            {
                switch (result.Exception)
                {
                    // if the server is just having issues, we can try again later
                    case HttpRequestException httpRequestException when IsRetryableError(httpRequestException.StatusCode):
                    case WebException:
                        return;
                    default:
                        // else it's a situation where the image will never work
                        RemoveImageRecord();
                        break;
                }
            });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            // Abort if the download URL or final destination is not available.
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(filePath))
                return ImageDownloadResult.Failure;

            var imageValid = File.Exists(filePath) && Misc.IsImageValid(filePath);
            if (imageValid && !ForceDownload)
                return ImageDownloadResult.Cached;

            var tempPath = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(filePath));

            try
            {
                // Download the image using custom HttpClient factory.
                using var client = _clientFactory.CreateClient("TvDBClient");
                var bytes = await client.GetByteArrayAsync(downloadUrl);

                // Validate the downloaded image.
                if (bytes.Length < 4)
                    throw new WebException("The image download stream returned less than 4 bytes (a valid image has 2-4 bytes in the header)");

                if (Misc.GetImageFormat(bytes) == null)
                    throw new WebException("The image download stream returned an invalid image");

                // Write the image data to the temp file.
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    fs.Write(bytes, 0, bytes.Length);

                // Ensure directory structure exists.
                var dirPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                // Delete existing file if re-downloading.
                if (File.Exists(filePath))
                    File.Delete(filePath);

                // Move the temp file to its final destination.
                File.Move(tempPath, filePath);

                return ImageDownloadResult.Success;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Image download failed.", ex);
            }
        });
    }

    private static bool IsRetryableError(HttpStatusCode? statusCode)
    {
        return statusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.InternalServerError;
    }

    private void RemoveImageRecord()
    {
        switch (ImageType)
        {
            case ImageEntityType.TvDB_FanArt:
                {
                    var fanart = RepoFactory.TvDB_ImageFanart.GetByID(ImageID);
                    if (fanart == null)
                        return;

                    RepoFactory.TvDB_ImageFanart.Delete(fanart);
                    return;
                }
            case ImageEntityType.TvDB_Cover:
                {
                    var poster = RepoFactory.TvDB_ImagePoster.GetByID(ImageID);
                    if (poster == null)
                        return;

                    RepoFactory.TvDB_ImagePoster.Delete(poster);
                    return;
                }
            case ImageEntityType.TvDB_Banner:
                {
                    var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(ImageID);
                    if (wideBanner == null)
                        return;

                    RepoFactory.TvDB_ImageWideBanner.Delete(wideBanner);
                    break;
                }
        }
    }

    public DownloadTvDBImageJob(ImageHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    protected DownloadTvDBImageJob() { }
}
