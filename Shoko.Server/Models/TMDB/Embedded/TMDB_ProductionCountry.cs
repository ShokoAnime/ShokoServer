using System;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Models.TMDB;

[Serializable]
public class TMDB_ProductionCountry : IEquatable<TMDB_ProductionCountry>
{
    /// <summary>
    /// ISO-3166 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; init; }

    /// <summary>
    /// English country name.
    /// </summary>
    public string CountryName { get; init; }

    public override string ToString()
    {
        return $"{CountryCode},{CountryName}";
    }

    public static TMDB_ProductionCountry FromString(string str)
    {
        var (countryCode, countryName) = str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new TMDB_ProductionCountry
        {
            CountryCode = countryCode, CountryName = countryName
        };
    }

    public bool Equals(TMDB_ProductionCountry? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return CountryCode == other.CountryCode && CountryName == other.CountryName;
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

        return Equals((TMDB_ProductionCountry)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CountryCode, CountryName);
    }
}
