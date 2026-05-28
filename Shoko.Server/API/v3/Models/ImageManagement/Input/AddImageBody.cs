using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;

#nullable enable
namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for adding a new image from a remote source.
/// </summary>
public class AddImageBody
{
    /// <summary>
    ///   Provider source (AniDB, TMDB, AniList, etc.).
    /// </summary>
    [Required]
    public DataSource Source { get; set; }

    /// <summary>
    ///   Resource identifier for the image source's template URL.
    /// </summary>
    [Required]
    public string ResourceID { get; set; } = string.Empty;

    /// <summary>
    ///   Image width in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    ///   Image height in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    ///   ISO 639-1 alpha-2 language code for the main language used
    ///   for the text in the image.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    ///   ISO 3166-1 alpha-2 country code for region-specific images.
    /// </summary>
    public string? CountryCode { get; set; }

    public ImageData ToImageData() => new()
    {
        Source = Source,
        ResourceID = ResourceID,
        Width = Width,
        Height = Height,
        LanguageCode = LanguageCode,
        CountryCode = CountryCode,
    };
}
