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
    /// its type.
    /// </summary>
    InvalidResource = 4,

    /// <summary>
    /// The image was not downloaded because the resource has been removed or is
    /// no longer available, and thus have also been removed from the local
    /// database.
    /// </summary>
    RemovedResource = 5,
}
