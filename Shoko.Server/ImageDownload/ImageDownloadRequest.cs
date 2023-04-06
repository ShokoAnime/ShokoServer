using System.Net;
using System.IO;
using System.Threading;
using System.Net.Http;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Server;
using Shoko.Commons.Utils;
using Shoko.Server.Providers.AniDB;

#nullable enable
namespace Shoko.Server.ImageDownload;

/// <summary>
/// Represents the result of an image download operation.
/// </summary>
public enum ImageDownloadResult
{
    /// <summary>
    /// The image was successfully downloaded and saved.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The image was not downloaded because it was already available in the cache.
    /// </summary>
    Cached = 2,

    /// <summary>
    /// The image could not be downloaded due to not being able to get the
    /// source or destination.
    /// </summary>
    Failure = 3,

    /// <summary>
    /// The image was not downloaded because the resource has been removed or is
    /// no longer available, but we could not remove the local entry because of
    /// it's type.
    /// </summary>
    InvalidResource = 4,

    /// <summary>
    /// The image was not downloaded because the resource has been removed or is
    /// no longer available, and thus have also been removed from the local
    /// database.
    /// </summary>
    RemovedResource = 5,
}

public class ImageDownloadRequest
{

    private object ImageData { get; }

    public bool ForceDownload { get; }

    private string ImageServerUrl { get; }

    private string? _filePath { get; set; } = null;

    public string FilePath
        => _filePath != null ? _filePath : _filePath = ImageData switch
        {
            AniDB_Character character => character.GetPosterPath(),
            AniDB_Seiyuu creator => creator.GetPosterPath(),
            MovieDB_Fanart image => image.GetFullImagePath(),
            MovieDB_Poster image => image.GetFullImagePath(),
            SVR_AniDB_Anime anime => anime.PosterPath,
            TvDB_Episode episode => episode.GetFullImagePath(),
            TvDB_ImageFanart image => image.GetFullImagePath(),
            TvDB_ImagePoster image => image.GetFullImagePath(),
            TvDB_ImageWideBanner image => image.GetFullImagePath(),
            _ => string.Empty,
        };

    private string? _downloadUrl { get; set; } = null;

    public string DownloadUrl
        => _downloadUrl != null ? _downloadUrl : _downloadUrl = ImageData switch
        {
            AniDB_Character character => string.Format(ImageServerUrl, character.PicName),
            AniDB_Seiyuu creator => string.Format(ImageServerUrl, creator.PicName),
            MovieDB_Fanart movieFanart => string.Format(Constants.URLS.MovieDB_Images, movieFanart.URL),
            MovieDB_Poster moviePoster => string.Format(Constants.URLS.MovieDB_Images, moviePoster.URL),
            SVR_AniDB_Anime anime => string.Format(ImageServerUrl, anime.Picname),
            TvDB_Episode ep => string.Format(Constants.URLS.TvDB_Episode_Images, ep.Filename),
            TvDB_ImageFanart fanart => string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath),
            TvDB_ImagePoster poster => string.Format(Constants.URLS.TvDB_Images, poster.BannerPath),
            TvDB_ImageWideBanner wideBanner => string.Format(Constants.URLS.TvDB_Images, wideBanner.BannerPath),
            _ => string.Empty
        };

    public bool IsImageValid
        => !string.IsNullOrEmpty(DownloadUrl) && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath) && Misc.IsImageValid(FilePath);

    private bool ShouldAniDBRateLimit 
        => ImageData switch
        {
            AniDB_Character => true,
            AniDB_Seiyuu => true,
            SVR_AniDB_Anime => true,
            _ => false,
        };

    public ImageDownloadRequest(object data, bool forceDownload, string? imageServerUrl = null)
    {
        ImageData = data;
        ForceDownload = forceDownload;
        ImageServerUrl = imageServerUrl ?? "";
    }

    public ImageDownloadResult DownloadNow(int maxRetries = 5)
        => RecursivelyRetryDownload(0, maxRetries);

    private ImageDownloadResult RecursivelyRetryDownload(int count, int maxRetries)
    {
        // Abort if the download url or final destination is not available.
        if (string.IsNullOrEmpty(DownloadUrl) || string.IsNullOrEmpty(FilePath))
            return ImageDownloadResult.Failure;

        var imageValid = File.Exists(FilePath) && Misc.IsImageValid(FilePath);
        if (imageValid && !ForceDownload)
            return ImageDownloadResult.Cached;

        var tempPath = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(FilePath));
        try
        {
            // Rate limit anidb image requests.
            if (ShouldAniDBRateLimit)
                AniDbImageRateLimiter.Instance.EnsureRate();

            // Ignore all certificate failures.
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            // Download the image.
            using (var client = new HttpClient())
            {
                // Download the image data.
                client.DefaultRequestHeaders.Add("user-agent", "JMM");
                var bytes = client.GetByteArrayAsync(DownloadUrl)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                if (bytes.Length < 4)
                    throw new WebException(
                        "The image download stream returned less than 4 bytes (a valid image has 2-4 bytes in the header)");

                // Check if the image format is valid.
                if (Misc.GetImageFormat(bytes) == null)
                    throw new WebException("The image download stream returned an invalid image");

                // Delete the existing (failed?) temporary file.
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                // Write the image data to the temp file.
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    fs.Write(bytes, 0, bytes.Length);

                // Make sure the directory structure exists.
                var dirPath = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                // Delete the existing file if we're re-downloading.
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }

                // Move the temp file to it's final destination.
                File.Move(tempPath, FilePath);

                return ImageDownloadResult.Success;
            }
        }
        catch (HttpRequestException ex)
        {
            // Mark the request as a failure if we received a 404 or 403.
            if (ex.StatusCode.HasValue && (ex.StatusCode.Value == HttpStatusCode.Forbidden || ex.StatusCode.Value == HttpStatusCode.NotFound))
            {
                var removed = RemoveResource();
                return removed ? ImageDownloadResult.RemovedResource : ImageDownloadResult.InvalidResource;
            }

            throw;
        }
        catch (WebException)
        {
            if (count + 1 >= maxRetries)
                throw;

            Thread.Sleep(1000);
            return RecursivelyRetryDownload(count + 1, maxRetries);
        }
    }

    private bool RemoveResource()
    {
        switch (ImageData)
        {
            case MovieDB_Fanart movieFanart:
                Repositories.RepoFactory.MovieDB_Fanart.Delete(movieFanart);
                return true;
            case MovieDB_Poster moviePoster:
                Repositories.RepoFactory.MovieDB_Poster.Delete(moviePoster);
                return true;
            case TvDB_ImageFanart tvdbFanart:
                Repositories.RepoFactory.TvDB_ImageFanart.Delete(tvdbFanart);
                return true;
            case TvDB_ImagePoster tvdbPoster:
                Repositories.RepoFactory.TvDB_ImagePoster.Delete(tvdbPoster);
                return true;
            case TvDB_ImageWideBanner tvdbWideBanner:
                Repositories.RepoFactory.TvDB_ImageWideBanner.Delete(tvdbWideBanner);
                return true;
        }

        return false;
    }
}
