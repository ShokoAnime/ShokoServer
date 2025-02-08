using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;

namespace Shoko.Server.API.v3.Models.Common;

public record YearlySeason(int year, AnimeSeason animeSeason) : IComparable<YearlySeason>
{
    public int Year { get; } = year;

    [JsonConverter(typeof(StringEnumConverter))]
    public AnimeSeason AnimeSeason { get; } = animeSeason;

    public int CompareTo(YearlySeason other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        if (ReferenceEquals(null, this))
        {
            return -1;
        }

        var yearComparison = Year.CompareTo(other.Year);
        if (yearComparison != 0)
        {
            return yearComparison;
        }

        return AnimeSeason.CompareTo(other.AnimeSeason);
    }
}
