using System;

namespace Shoko.Abstractions.Metadata.Image;

/// <summary>
///   Data transfer object (DTO) for updating existing image records with
///   support for partial updates.
/// </summary>
public sealed class ImageUpdateData
{
    /// <summary>
    ///   Sets the primary image the image is linked to. You can set it to the
    ///   same image to unlink it from the previous primary image.
    /// </summary>
    public IImage? PrimaryImage { get; set; }

    /// <summary>
    ///   Used by the manager to determine if the size should be updated. This
    ///   is set to <c>true</c> when either width or height property is set.
    /// </summary>
    public bool HasSizeSet { get; private set; }

    /// <summary>
    ///   Indicates that the image has both width and height set. This is used
    ///   to check if dimension data is available before accessing width and
    ///   height properties.
    /// </summary>
    public bool HasSize { get => _width.HasValue && _height.HasValue; }

    private int? _width;

    /// <summary>
    /// Image width in pixels. Must be greater than 0 if set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the width is set to a value less than 1 that is not
    ///   <c>null</c>.
    /// </exception>
    public int? Width
    {
        get => _width;
        set
        {
            if (value is not null and not > 0)
                throw new ArgumentOutOfRangeException(nameof(Width), "Width cannot be less than 1 if set.");
            HasSizeSet = true;
            _width = value;
            if (value.HasValue && !_height.HasValue)
                _height = 1;
            else if (!value.HasValue && _height.HasValue)
                _height = null;
        }
    }

    private int? _height;

    /// <summary>
    ///   Image height in pixels. Must be greater than 0 if set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the height is set to a value less than 1 that is not
    ///   <c>null</c>.
    /// </exception>
    public int? Height
    {
        get => _height;
        set
        {
            if (value is not null and not > 0)
                throw new ArgumentOutOfRangeException(nameof(Height), "Height cannot be less than 1 if set.");
            HasSizeSet = true;
            _height = value;
            if (value.HasValue && !_width.HasValue)
                _width = 1;
            else if (!value.HasValue && _width.HasValue)
                _width = null;
        }
    }

    /// <summary>
    ///   Used by the manager to determine if the language code should be
    ///   updated. This is set to <c>true</c> when the language code property is
    ///   set.
    /// </summary>
    public bool HasLanguageCodeSet { get; private set; }

    private string? _languageCode;

    /// <summary>
    ///   ISO 639-1 alpha-2 language code for the main language used for the
    ///   text in the image, if any. Or <c>null</c> if the image doesn't contain
    ///   any text.
    /// </summary>
    public string? LanguageCode
    {
        get => _languageCode;
        set
        {
            HasLanguageCodeSet = true;
            _languageCode = value;
        }
    }

    /// <summary>
    ///   Used by the manager to determine if the country code should be
    ///   updated. This is set to <c>true</c> when the country code property is
    ///   set.
    /// </summary>
    public bool HasCountryCodeSet { get; private set; }

    private string? _countryCode;

    /// <summary>
    ///   ISO 3166-1 alpha-2 country code for region-specific images. This is
    ///   used in combination with the language code to identify region-specific
    ///   variants of images when the language alone is not enough. E.g. "pt-BR"
    ///   for Brazilian Portuguese, where "pt" is the language code and "BR" is
    ///   the country code.
    /// </summary>
    public string? CountryCode
    {
        get => _countryCode;
        set
        {
            HasCountryCodeSet = true;
            _countryCode = value;
        }
    }

    /// <summary>
    /// Whether the image is globally enabled. Disabled images should not be displayed
    /// but may still be available for administrative purposes. Set to null to leave
    /// the current value unchanged.
    /// </summary>
    public bool? IsEnabled { get; set; }
}
