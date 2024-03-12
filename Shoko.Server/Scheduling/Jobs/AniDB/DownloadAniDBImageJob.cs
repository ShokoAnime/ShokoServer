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
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class DownloadAniDBImageJob : BaseJob, IImageDownloadJob
{
    private const string FailedToDownloadNoID = "Image failed to download: Can\'t find valid {ImageType} with ID: {ImageID}";
    private const string FailedToDownloadNoImpl = "Image failed to download: No implementation found for {ImageType}";

    private readonly AniDBTitleHelper _titleHelper;
    private readonly IUDPConnectionHandler _handler;
    private readonly ImageHttpClientFactory _clientFactory;
    private string _animeTitle;

    public int AnimeID { get; set; }
    public int ImageID { get; set; }
    public bool ForceDownload { get; set; }

    public ImageEntityType ImageType { get; set; }

    public override void PostInit()
    {
        _animeTitle = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override string TypeName => "Download AniDB Image";

    public override string Title => "Downloading AniDB Image";
    public override Dictionary<string, object> Details => ImageType switch
    {
        ImageEntityType.AniDB_Cover => new()
        {
            { "Type", ImageType.ToString().Replace("_", " ") },
            { "Anime", _animeTitle },
        },
        _ => new()
        {
            { "Anime", _animeTitle },
            { "Type", ImageType.ToString().Replace("_", " ") },
            { "ImageID", ImageID }
        }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime} -> Cover: {ImageType} | ImageID: {EntityID}", nameof(DownloadAniDBImageJob), _animeTitle, ImageType, ImageID == 0 ? AnimeID : ImageID);

        var imageType = ImageType.ToString().Replace("_", " ");
        string downloadUrl = null;
        string filePath = null;
        switch (ImageType)
        {
            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null)
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, AnimeID);
                    return;
                }

                downloadUrl = string.Format(_handler.ImageServerUrl, anime.Picname);
                filePath = anime.PosterPath;
                break;

            case ImageEntityType.AniDB_Character:
                var chr = RepoFactory.AniDB_Character.GetByCharID(ImageID);
                if (chr == null)
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    return;
                }

                downloadUrl = string.Format(_handler.ImageServerUrl, chr.PicName);
                filePath = chr.GetPosterPath();
                break;

            case ImageEntityType.AniDB_Creator:
                var va = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(ImageID);
                if (va == null)
                {
                    _logger.LogWarning(FailedToDownloadNoID, imageType, ImageID);
                    return;
                }

                downloadUrl = string.Format(_handler.ImageServerUrl, va.PicName);
                filePath = va.GetPosterPath();
                break;
        }

        if (downloadUrl == null || filePath == null)
        {
            _logger.LogWarning(FailedToDownloadNoImpl, ImageType.ToString());
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
    
    private async Task<ImageDownloadResult> DownloadNow(string downloadUrl, string filePath, int maxRetries = 5)
    {
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
            // Abort if the download URL or final destination is not available.
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(filePath))
                return ImageDownloadResult.Failure;

            var imageValid = File.Exists(filePath) && Misc.IsImageValid(filePath);
            if (imageValid && !ForceDownload)
                return ImageDownloadResult.Cached;

            var tempPath = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(filePath));

            try
            {
                // Rate limit anidb image requests.
                AniDbImageRateLimiter.Instance.EnsureRate();

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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Image download failed.", ex);
            }
        });
    }

    public DownloadAniDBImageJob(IUDPConnectionHandler handler, ImageHttpClientFactory clientFactory, AniDBTitleHelper titleHelper)
    {
        _handler = handler;
        _clientFactory = clientFactory;
        _titleHelper = titleHelper;
    }

    protected DownloadAniDBImageJob() { }
}
