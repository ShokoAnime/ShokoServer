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

    public bool DownloadNow(int maxRetries = 5)
        => RecursivelyRetryDownload(0, maxRetries);

    private bool RecursivelyRetryDownload(int count, int maxRetries)
    {
        // Abort if the download url or final destination is not available.
        if (string.IsNullOrEmpty(DownloadUrl) || string.IsNullOrEmpty(FilePath))
            return false;

        var imageValid = File.Exists(FilePath) && Misc.IsImageValid(FilePath);
        if (imageValid && !ForceDownload)
            return true;

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

                return true;
            }
        }
        catch (WebException)
        {
            if (count + 1 >= maxRetries)
                throw;

            Thread.Sleep(1000);
            return RecursivelyRetryDownload(count + 1, maxRetries);
        }
    }
}
