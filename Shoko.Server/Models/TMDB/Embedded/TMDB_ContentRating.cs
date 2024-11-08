using System;
using System.Text.Json.Serialization;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Models.TMDB;

[Serializable]
public class TMDB_ContentRating
{
    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    public string CountryCode { get; set; }

    [JsonIgnore]
    public string LanguageCode
    {
        get => CountryCode.FromIso3166ToIso639();
    }

    [JsonIgnore]
    public TitleLanguage Language
    {
        get => LanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// content ratings (certifications) that have been added to a TV show.
    /// </summary>
    public string Rating { get; set; }

    public TMDB_ContentRating()
    {
        CountryCode = string.Empty;
        Rating = string.Empty;
    }

    public TMDB_ContentRating(string countryCode, string rating)
    {
        CountryCode = countryCode;
        Rating = rating;
    }

    public override string ToString()
    {
        return $"{CountryCode},{Rating}";
    }

    public static TMDB_ContentRating FromString(string str)
    {
        var (countryCode, rating) = str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new(countryCode, rating);
    }

}
