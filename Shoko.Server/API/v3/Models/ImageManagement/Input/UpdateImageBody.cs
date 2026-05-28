using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Image;

#nullable enable
namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for updating an existing image. Tracks which properties were
///   explicitly set to support partial updates.
/// </summary>
public class UpdateImageBody
{
    private readonly HashSet<string> _setProperties = [];

    private int? _width;
    private int? _height;
    private string? _languageCode;
    private string? _countryCode;

    /// <summary>
    ///   Image width in pixels.
    /// </summary>
    public int? Width
    {
        get => _width;
        set
        {
            _setProperties.Add(nameof(Width));
            _width = value;
        }
    }

    /// <summary>
    ///   Image height in pixels.
    /// </summary>
    public int? Height
    {
        get => _height;
        set
        {
            _setProperties.Add(nameof(Height));
            _height = value;
        }
    }

    /// <summary>
    ///   ISO 639-1 alpha-2 language code for the main language used
    ///   for the text in the image.
    /// </summary>
    public string? LanguageCode
    {
        get => _languageCode;
        set
        {
            _setProperties.Add(nameof(LanguageCode));
            _languageCode = value;
        }
    }

    /// <summary>
    ///   ISO 3166-1 alpha-2 country code for region-specific images.
    /// </summary>
    public string? CountryCode
    {
        get => _countryCode;
        set
        {
            _setProperties.Add(nameof(CountryCode));
            _countryCode = value;
        }
    }

    /// <summary>
    ///   Whether the image is globally enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    public bool HasWidthSet => _setProperties.Contains(nameof(Width));
    public bool HasHeightSet => _setProperties.Contains(nameof(Height));
    public bool HasLanguageCodeSet => _setProperties.Contains(nameof(LanguageCode));
    public bool HasCountryCodeSet => _setProperties.Contains(nameof(CountryCode));

    public ImageUpdateData ToImageUpdateData()
    {
        var data = new ImageUpdateData
        {
            IsEnabled = IsEnabled,
        };
        if (HasWidthSet || HasHeightSet)
        {
            data.Width = Width;
            data.Height = Height;
        }
        if (HasLanguageCodeSet)
            data.LanguageCode = LanguageCode;
        if (HasCountryCodeSet)
            data.CountryCode = CountryCode;
        return data;
    }
}
