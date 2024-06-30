using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class DownloadTMDBImageJob : BaseJob, IImageDownloadJob
{
    private const string FailedToDownloadNoID = "Image failed to download: Can\'t find valid {ImageType} with ID: {ImageID}";
    private const string FailedToDownloadNoImpl = "Image failed to download: No implementation found for {ImageType}";

    private readonly ImageHttpClientFactory _clientFactory;
    public string Anime { get; set; }
    public int ImageID { get; set; }
    public bool ForceDownload { get; set; }

    public ImageEntityType ImageType { get; set; }

    public override string TypeName => "Download TMDB Image";
    public override string Title => "Downloading TMDB Image";
    public override Dictionary<string, object> Details => Anime == null ? new()
    {
        { "Type", ImageType.ToString().Replace("_", " ") },
        { "ImageID", ImageID }
    } : new()
    {
        { "Anime", Anime },
        { "Type", ImageType.ToString().Replace("_", " ") },
        { "ImageID", ImageID }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime} -> Image Type: {ImageType} | ImageID: {EntityID}", nameof(DownloadTMDBImageJob), Anime, ImageType, ImageID);

        var imageType = ImageType.ToString().Replace("_", " ");
        string downloadUrl = null;
        string filePath = null;
        switch (ImageType)
        {
            case ImageEntityType.MovieDB_Poster:
                var moviePoster = RepoFactory.MovieDB_Poster.GetByID(ImageID);
                if (string.IsNullOrEmpty(moviePoster?.URL))
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    RemoveImageRecord();
                    return;
                }

                downloadUrl = string.Format(Constants.URLS.MovieDB_Images, moviePoster.URL);
                filePath = moviePoster.GetFullImagePath();
                break;

            case ImageEntityType.MovieDB_FanArt:
                var movieFanart = RepoFactory.MovieDB_Fanart.GetByID(ImageID);
                if (string.IsNullOrEmpty(movieFanart?.URL))
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    RemoveImageRecord();
                    return;
                }

                downloadUrl = string.Format(Constants.URLS.MovieDB_Images, movieFanart.URL);
                filePath = movieFanart.GetFullImagePath();
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
                    _logger.LogInformation("Image downloaded for {Anime}: {FilePath} from {DownloadUrl}", Anime, filePath, downloadUrl);
                    break;
                case ImageDownloadResult.Cached:
                    _logger.LogDebug("Image already in cache for {Anime}: {FilePath} from {DownloadUrl}", Anime, filePath, downloadUrl);
                    break;
                case ImageDownloadResult.Failure:
                    _logger.LogWarning("Image failed to download for {Anime}: {FilePath} from {DownloadUrl}", Anime, filePath, downloadUrl);
                    break;
                case ImageDownloadResult.RemovedResource:
                    _logger.LogWarning("Image failed to download for {Anime} and the local entry has been removed: {FilePath} from {DownloadUrl}", Anime,
                        filePath, downloadUrl);
                    break;
                case ImageDownloadResult.InvalidResource:
                    _logger.LogWarning("Image failed to download for {Anime} and the local entry could not be removed: {FilePath} from {DownloadUrl}",
                        Anime, filePath, downloadUrl);
                    break;
            }
        }
        catch (WebException e)
        {
            _logger.LogWarning("Error processing {Job} for {Anime}: {Url} ({EntityID}) - {Message}", nameof(DownloadTMDBImageJob), Anime, downloadUrl,
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
                var client = _clientFactory.CreateClient("TMDBClient");
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
            case ImageEntityType.MovieDB_FanArt:
                {
                    var fanart = RepoFactory.MovieDB_Fanart.GetByID(ImageID);
                    if (fanart == null)
                        return;
                    RepoFactory.MovieDB_Fanart.Delete(fanart);
                    return;
                }
            case ImageEntityType.MovieDB_Poster:
                {
                    var poster = RepoFactory.MovieDB_Poster.GetByID(ImageID);
                    if (poster == null)
                        return;
                    RepoFactory.MovieDB_Poster.Delete(poster);
                    return;
                }
        }
    }

    public DownloadTMDBImageJob(ImageHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    protected DownloadTMDBImageJob() { }
}
