using System.ComponentModel.DataAnnotations;

using AbstractPackageImageInfo = Shoko.Abstractions.Plugin.Models.PackageImageInfo;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// A plugin image definition, used for both thumbnails and logos.
/// </summary>
public class PackageImageInfo(AbstractPackageImageInfo imageInfo)
{
    /// <summary>
    /// The mime type of the image.
    /// </summary>
    [Required]
    public string MimeType { get; init; } = imageInfo.MimeType;

    /// <summary>
    /// The width of the image.
    /// </summary>
    [Required]
    public int Width { get; init; } = imageInfo.Width;

    /// <summary>
    /// The height of the image.
    /// </summary>
    [Required]
    public int Height { get; init; } = imageInfo.Height;
}
