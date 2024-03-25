using System;
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
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using WebException = System.Net.WebException;

namespace Shoko.Server.Providers.AniDB;

public class AniDBImageHandler
{
    private const string FailedToDownloadNoID = "Image failed to download: Can\'t find valid {ImageType} with ID: {ImageID}";
    private readonly IUDPConnectionHandler _handler;
    private readonly ImageHttpClientFactory _clientFactory;
    private readonly ILogger<AniDBImageHandler> _logger;

    public AniDBImageHandler(ILogger<AniDBImageHandler> logger, IUDPConnectionHandler handler, ImageHttpClientFactory clientFactory)
    {
        _logger = logger;
        _handler = handler;
        _clientFactory = clientFactory;
    }

    public (string downloadUrl, string filePath) GetPaths(ImageEntityType imageType, int imageID)
    {
        var prettyImageType = imageType.ToString().Replace("_", " ");
        string downloadUrl = null;
        string filePath = null;
        switch (imageType)
        {
            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageID);
                if (anime == null)
                {
                    _logger.LogWarning(FailedToDownloadNoID, prettyImageType, imageID);
                    return default;
                }

                downloadUrl = string.Format(_handler.ImageServerUrl, anime.Picname);
                filePath = anime.PosterPath;
                break;

            case ImageEntityType.AniDB_Character:
                var chr = RepoFactory.AniDB_Character.GetByCharID(imageID);
                if (chr == null)
                {
                    _logger.LogWarning(FailedToDownloadNoID, prettyImageType, imageID);
                    return default;
                }

                downloadUrl = string.Format(_handler.ImageServerUrl, chr.PicName);
                filePath = chr.GetPosterPath();
                break;

            case ImageEntityType.AniDB_Creator:
                var va = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(imageID);
                if (va == null)
                {
                    _logger.LogWarning(FailedToDownloadNoID, prettyImageType, imageID);
                    return default;
                }

                downloadUrl = string.Format(_handler.ImageServerUrl, va.PicName);
                filePath = va.GetPosterPath();
                break;
        }

        return (downloadUrl, filePath);
    }

    public async Task<ImageDownloadResult> DownloadImage(string downloadUrl, string filePath, bool force, int maxRetries = 5)
    {
        // Abort if the download URL or final destination is not available.
        if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(filePath))
            return ImageDownloadResult.Failure;

        var imageValid = IsImageCached(filePath);
        if (imageValid && !force)
            return ImageDownloadResult.Cached;

        return await DownloadImageDirectly(downloadUrl, filePath, maxRetries);
    }

    public bool IsImageCached(ImageEntityType imageType, int imageID)
    {
        var (_, filePath) = GetPaths(imageType, imageID);
        return IsImageCached(filePath);
    }

    public static bool IsImageCached(string filePath) => !string.IsNullOrEmpty(filePath) && File.Exists(filePath) && Misc.IsImageValid(filePath);

    public async Task<ImageDownloadResult> DownloadImageDirectly(string downloadUrl, string filePath, int maxRetries = 5)
    {
        // Abort if the download URL or final destination is not available.
        if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(filePath))
            return ImageDownloadResult.Failure;

        var tempPath = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(filePath));
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<WebException>()
            .RetryAsync(maxRetries, (exception, _) =>
            {
                if (exception is HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.NotFound } httpEx)
                {
                    throw new InvalidOperationException("Image download failed with invalid resource.", httpEx);
                }
            });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            // Download the image using custom HttpClient factory.
            using var client = _clientFactory.CreateClient("AniDBClient");
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
        });
    }
}
