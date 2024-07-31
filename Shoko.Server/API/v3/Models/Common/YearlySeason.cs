using System;
using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Server.API.v3.Models.Common;

public record YearlySeason(int Year, AnimeSeason AnimeSeason) : IComparable<YearlySeason>
{
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
