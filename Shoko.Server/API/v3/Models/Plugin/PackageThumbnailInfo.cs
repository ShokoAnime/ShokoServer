using System.ComponentModel.DataAnnotations;

using AbstractPluginThumbnailInfo = Shoko.Abstractions.Plugin.Models.PackageThumbnailInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// A plugin thumbnail definition.
/// </summary>
public class PackageThumbnailInfo(AbstractPluginThumbnailInfo thumbnailInfo)
{
    /// <summary>
    /// The mime type of the thumbnail image.
    /// </summary>
    [Required]
    public string MimeType { get; init; } = thumbnailInfo.MimeType;

    /// <summary>
    /// The width of the thumbnail image.
    /// </summary>
    [Required]
    public uint Width { get; init; } = thumbnailInfo.Width;

    /// <summary>
    /// The height of the thumbnail image.
    /// </summary>
    [Required]
    public uint Height { get; init; } = thumbnailInfo.Height;
}
