using System;
using System.Text.Json.Serialization;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Models.TMDB;

[Serializable]
public class TMDB_ContentRating : IEquatable<TMDB_ContentRating>
{
    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    public string CountryCode { get; init; }

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
    public string Rating { get; init; }

    public override string ToString()
    {
        return $"{CountryCode},{Rating}";
    }

    public static TMDB_ContentRating FromString(string str)
    {
        var (countryCode, rating) = str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new TMDB_ContentRating { CountryCode = countryCode, Rating = rating};
    }

    public bool Equals(TMDB_ContentRating? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return CountryCode == other.CountryCode && Rating == other.Rating;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((TMDB_ContentRating)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CountryCode, Rating);
    }
}
