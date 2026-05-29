using System;
using Shoko.Abstractions.Extensions;

#nullable enable
namespace Shoko.Server.Models.TMDB;

[Serializable]
public class TMDB_ProductionCountry
{
    /// <summary>
    /// ISO-3166 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// English country name.
    /// </summary>
    public string CountryName { get; set; }

    public TMDB_ProductionCountry()
    {
        CountryCode = string.Empty;
        CountryName = string.Empty;
    }

    public TMDB_ProductionCountry(string countryCode, string countryName)
    {
        CountryCode = countryCode;
        CountryName = countryName;
    }

    public override string ToString()
    {
        return $"{CountryCode},{CountryName}";
    }

    public static TMDB_ProductionCountry FromString(string str)
    {
        var (countryCode, countryName) = str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new(countryCode, countryName);
    }

}
