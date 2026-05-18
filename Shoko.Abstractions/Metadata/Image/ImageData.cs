using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image;

/// <summary>
///   Data transfer object (DTO) for creating new images records containing all
///   metadata needed to create an image from a remote provider.
/// </summary>
public sealed class ImageData
{
    private DataSource _source;

    /// <summary>
    ///   Provider source (AniDB, TMDB, AniList). This indicates where the image
    ///   originated from and is used for routing, display, and management
    ///   purposes.
    /// </summary>
    public required DataSource Source
    {
        get => _source;
        set => _source = value.IsLocal
            ? throw new ArgumentException(nameof(Source), "Invalid image data source.")
            : value;
    }

    /// <summary>
    ///   Resource identifier for the image source's template URL to retrieve
    ///   the image from the source, or an MD5 hash digest for locally generated
    ///   or user uploaded images.
    /// </summary>
    [MinLength(0)]
    public required string ResourceID { get; set; }

    /// <summary>
    ///   Indicates that the image has both width and height set.
    /// </summary>
    public bool HasSize { get => _width.HasValue && _height.HasValue; }

    private uint? _width;

    /// <summary>
    ///   Image width in pixels. Must be greater than 0 if set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the width is set to a value less than 1 that is not
    ///   <c>null</c>.
    /// </exception>
    public uint? Width
    {
        get => _width;
        set
        {
            if (value is not null and not > 0)
                throw new ArgumentOutOfRangeException(nameof(Width), "Width cannot be less than 1 if set.");
            _width = value;
            if (value.HasValue && !_height.HasValue)
                _height = 1;
            else if (!value.HasValue && _height.HasValue)
                _height = null;
        }
    }

    private uint? _height;

    /// <summary>
    ///   Image height in pixels. Must be greater than 0 if set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the height is set to a value less than 1 that is not
    ///   <c>null</c>.
    /// </exception>
    public uint? Height
    {
        get => _height;
        set
        {
            if (value is not null and not > 0)
                throw new ArgumentOutOfRangeException(nameof(Height), "Height cannot be less than 1 if set.");
            _height = value;
            if (value.HasValue && !_width.HasValue)
                _width = 1;
            else if (!value.HasValue && _width.HasValue)
                _width = null;
        }
    }

    /// <summary>
    ///   ISO 639-1 alpha-2 language code for the main language used for the
    ///   text in the image, if any. Or <c>null</c> if the image doesn't contain
    ///   any text.
    /// </summary>
    [MaxLength(5)]
    public string? LanguageCode { get; set; }

    /// <summary>
    ///   ISO 3166-1 alpha-2 country code for region-specific images. This is
    ///   used in combination with the language code to identify region-specific
    ///   variants of images when the language alone is not enough. E.g. "pt-BR"
    ///   for Brazilian Portuguese, where "pt" is the language code and "BR" is
    ///   the country code.
    /// </summary>
    [MaxLength(5)]
    public string? CountryCode { get; set; }
}
