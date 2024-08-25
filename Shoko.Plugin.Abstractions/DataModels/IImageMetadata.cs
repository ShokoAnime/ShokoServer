using System;
using System.IO;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Image metadata.
/// </summary>
public interface IImageMetadata : IMetadata<int>, IEquatable<IImageMetadata>
{
    /// <summary>
    /// Image type.
    /// </summary>
    ImageEntityType ImageType { get; }

    /// <summary>
    /// MIME type for image.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Indicates the image is enabled for use. Disabled images should not be
    /// used except for administrative purposes.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Indicates that the image is the preferred image for the linked entity.
    /// </summary>
    /// <value></value>
    public bool IsPreferred { get; }

    /// <summary>
    /// Indicates the image is locked and cannot be removed by the user. It can
    /// still be disabled though.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Indicates the image is readily available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Indicates that the image is readily available from the local file system.
    /// </summary>
    public bool IsLocalAvailable { get; }

    /// <summary>
    /// Indicates that the image is readily available from the remote location.
    /// </summary>
    public bool IsRemoteAvailable { get; }

    /// <summary>
    /// Image aspect ratio.
    /// </summary>
    /// <value></value>
    double AspectRatio { get; }

    /// <summary>
    /// Width of the image, in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Height of the image, in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Language code for the language used for the text in the image, if any.
    /// Or null if the image doesn't contain any language specifics.
    /// </summary>
    string? LanguageCode { get; }

    /// <summary>
    /// The language used for any text in the image, if any.
    /// Or <see cref="TitleLanguage.None"/> if the image doesn't contain any
    /// language specifics.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    /// A full remote URL to fetch the image, if the provider uses remote
    /// images.
    /// </summary>
    string? RemoteURL { get; }

    /// <summary>
    /// Local absolute path to where the image is stored. Will be null if the
    /// image is currently not locally available.
    /// </summary>
    string? LocalPath { get; }

    /// <summary>
    /// Get a stream that reads the image contents from the local copy or remote
    /// copy of the image. Returns null if the image is currently unavailable.
    /// </summary>
    /// <param name="allowLocal">
    /// Allow retrieving the stream from the local cache.
    /// </param>
    /// <param name="allowRemote">
    /// Allow retrieving the stream from the remote cache.
    /// </param>
    /// <returns>
    /// A stream of the image content, or null. The stream will never be
    /// interrupted partway through.
    /// </returns>
    Task<Stream?> GetStream(bool allowLocal = true, bool allowRemote = true);

    /// <summary>
    /// Will attempt to download the remote copy of the image available at
    /// <see cref="RemoteURL"/> to the <see cref="LocalPath"/>.
    /// </summary>
    /// <returns>Indicates that the image is available locally.</returns>
    Task<bool> DownloadImage(bool force = false);
}
