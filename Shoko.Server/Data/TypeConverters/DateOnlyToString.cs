using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shoko.Server.Data.TypeConverters;

public class DateOnlyToString() : ValueConverter<DateOnly?, string>(l => l == null ? null : l.Value.ToString("yyyy-MM-dd"), s => GetDateOnly(s))
{
    private static DateOnly? GetDateOnly(string s)
    {
        return DateOnly.TryParse(s, out var d) ? d : DateTime.TryParse(s, out var d1) ? new DateOnly(d1.Year, d1.Month, d1.Day) : null;
    }
}
