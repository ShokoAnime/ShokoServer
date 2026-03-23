using System;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Represents dates with partial information, allowing for year-only or year-month values
/// in addition to full dates. Values range from January 1, 0001 Anno Domini (Common Era)
/// through December 31, 9999 A.D. (C.E.) in the Gregorian calendar.
/// </summary>
public readonly struct PartialDateOnly : IComparable<PartialDateOnly>, IEquatable<PartialDateOnly>, IFormattable, IParsable<PartialDateOnly>, ISpanFormattable, ISpanParsable<PartialDateOnly>, IUtf8SpanFormattable
{
    /// <summary>
    ///   Gets the earliest possible date that can be created.
    /// </summary>
    public static PartialDateOnly MinValue { get => new(DateOnly.MinValue); }

    /// <summary>
    ///   Gets the latest possible date that can be created.
    /// </summary>
    public static PartialDateOnly MaxValue { get => new(DateOnly.MaxValue); }

    /// <summary>
    ///   Gets a partial date-only object that is set to the current date and
    ///   time on this computer, expressed as the local time.
    /// </summary>
    public static PartialDateOnly Now { get => new(DateTime.Now); }

    /// <summary>
    ///    Gets the current date.
    /// </summary>
    public static PartialDateOnly Today { get => new(DateTime.Today); }

    /// <summary>
    ///   Gets a partial date-only object that is set to the current date and
    ///   time on this computer, expressed as the Coordinated Universal Time
    ///   (UTC).
    /// </summary>
    public static PartialDateOnly UtcNow { get => new(DateTime.UtcNow); }

    /// <summary>
    ///   Gets the day of the year represented by this instance.
    /// </summary>
    public int? DayOfYear { get; }

    /// <summary>
    ///   Gets the day of the week represented by this instance.
    /// </summary>
    public DayOfWeek? DayOfWeek { get; }

    /// <summary>
    ///   Gets the number of days since January 1, 0001 in the Proleptic
    ///   Gregorian calendar represented by this instance.
    /// </summary>
    public int? DayNumber { get; }

    /// <summary>
    /// Gets or sets the day component of the date, if known. Must be valid for the given month/year when specified.
    /// </summary>
    public int? Day { get; }

    /// <summary>
    /// Gets or sets the month component of the date, if known. Must be between 1 and 12 when specified.
    /// </summary>
    public int? Month { get; }

    /// <summary>
    /// Gets or sets the year component of the date. This is required and must be between 1 and 9999.
    /// </summary>
    public int Year { get; }

    /// <summary>
    /// Gets a value indicating whether this instance represents a complete date.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Month))]
    [MemberNotNullWhen(true, nameof(Day))]
    [MemberNotNullWhen(true, nameof(DayOfWeek))]
    [MemberNotNullWhen(true, nameof(DayOfYear))]
    [MemberNotNullWhen(true, nameof(DayNumber))]
    public bool IsComplete => Month.HasValue && Day.HasValue;

    /// <summary>
    /// Creates a new instance with year, month, and day values.
    /// </summary>
    /// <param name="year">The year (1 through 9999).</param>
    /// <param name="month">The month (1 through 12).</param>
    /// <param name="day">The day (1 through the number of days in month).</param>
    public PartialDateOnly(int year, int? month = null, int? day = null)
    {
        if (year is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1 and 9999.");

        if (month.HasValue && month.Value is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");

        if (day.HasValue)
        {
            if (!month.HasValue)
                throw new ArgumentException("Day cannot be specified without a month.", nameof(day));

            var daysInMonth = GetDaysInMonth(year, month.Value);
            if (day.Value < 1 || day.Value > daysInMonth)
                throw new ArgumentOutOfRangeException(nameof(day), $"Day must be between 1 and {daysInMonth} for the specified month.");
        }

        Year = year;
        Month = month;
        Day = day;
        if (IsComplete)
        {
            var dateOnly = new DateOnly(Year, Month.Value, Day.Value);
            DayOfWeek = dateOnly.DayOfWeek;
            DayOfYear = dateOnly.DayOfYear;
            DayNumber = dateOnly.DayNumber;
        }
    }

    /// <summary>
    /// Creates a new instance from a DateOnly value.
    /// </summary>
    public PartialDateOnly(DateOnly date)
    {
        Year = date.Year;
        Month = date.Month;
        Day = date.Day;
    }

    /// <summary>
    /// Creates a new instance from a DateTime value.
    /// </summary>
    public PartialDateOnly(DateTime date)
    {
        Year = date.Year;
        Month = date.Month;
        Day = date.Day;
    }

    /// <summary>
    /// Creates a new instance from a DateOnly value.
    /// </summary>
    public static PartialDateOnly FromDateOnly(DateOnly dateOnly) => new(dateOnly);

    /// <summary>
    /// Creates a new instance from a DateOnly value.
    /// </summary>
    [return: NotNullIfNotNull(nameof(dateOnly))]
    public static PartialDateOnly? FromDateOnly(DateOnly? dateOnly) => dateOnly.HasValue ? new(dateOnly.Value) : null;

    /// <summary>
    /// Creates a new instance from a DateTime value.
    /// </summary>
    public static PartialDateOnly FromDateTime(DateTime dateTime) => new(dateTime);

    /// <summary>
    /// Creates a new instance from a DateTime value.
    /// </summary>
    [return: NotNullIfNotNull(nameof(dateTime))]
    public static PartialDateOnly? FromDateTime(DateTime? dateTime) => dateTime.HasValue ? new(dateTime.Value) : null;

    /// <summary>
    /// Compares this instance to another NullableDateOnly instance.
    /// </summary>
    public int CompareTo(PartialDateOnly? other)
        => other is null ? 1 : CompareTo(other.Value);

    /// <summary>
    /// Compares this instance to another NullableDateOnly instance.
    /// </summary>
    public int CompareTo(PartialDateOnly other)
    {
        // Compare year first
        var yearComparison = Year.CompareTo(other.Year);
        if (yearComparison is not 0) return yearComparison;

        // Then month
        if (Month.HasValue != other.Month.HasValue)
            return other.Month.HasValue ? 1 : -1;

        if (Month.HasValue && other.Month.HasValue)
        {
            var monthComparison = Month.Value.CompareTo(other.Month.Value);
            if (monthComparison is not 0) return monthComparison;
        }

        // Finally day
        if (Day.HasValue != other.Day.HasValue)
            return other.Day.HasValue ? 1 : -1;

        if (Day.HasValue && other.Day.HasValue)
            return Day.Value.CompareTo(other.Day.Value);

        return 0;
    }

    /// <inheritdoc/>
    public bool Equals(PartialDateOnly? other)
        => other is not null && Equals(other.Value);

    /// <inheritdoc/>
    /// <inheritdoc/>
    public bool Equals(PartialDateOnly other)
        => Year == other.Year && Month == other.Month && Day == other.Day;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => Equals(obj as PartialDateOnly?);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Year, Month, Day);

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsComplete) return $"{Year:0000}-{Month.Value:D2}-{Day.Value:D2}";
        if (Month.HasValue) return $"{Year:0000}-{Month.Value:D2}";
        return $"{Year:0000}";
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => ToString();

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // Calculate required space: year (4) + optional "-" (1) + month (2) + optional "-" (1) + day (2)
        var required = 4 + (Month.HasValue ? 3 : 0) + (Day.HasValue ? 3 : 0);
        if (destination.Length < required)
        {
            charsWritten = 0;
            return false;
        }

        // Write year as 4 digits
        var yearStr = new char[4];
        Year.ToString("D4", provider).CopyTo(yearStr);
        yearStr.CopyTo(destination);

        var offset = 4;
        if (Month.HasValue)
        {
            destination[offset++] = '-';
            Month.Value.ToString("D2", provider).CopyTo(destination.Slice(offset, 2));
            offset += 2;

            if (Day.HasValue)
            {
                destination[offset++] = '-';
                Day.Value.ToString("D2", provider).CopyTo(destination.Slice(offset, 2));
                offset += 2;
            }
        }

        charsWritten = offset;
        return true;
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // Calculate required space: year (4) + optional "-" (1) + month (2) + optional "-" (1) + day (2)
        var required = 4 + (Month.HasValue ? 3 : 0) + (Day.HasValue ? 3 : 0);
        if (utf8Destination.Length < required)
        {
            bytesWritten = 0;
            return false;
        }

        // Write year as 4 digits (ASCII values)
        var y1 = (byte)('0' + Year / 1000);
        var y2 = (byte)('0' + Year % 1000 / 100);
        var y3 = (byte)('0' + Year % 100 / 10);
        var y4 = (byte)('0' + Year % 10);

        utf8Destination[0] = y1;
        utf8Destination[1] = y2;
        utf8Destination[2] = y3;
        utf8Destination[3] = y4;

        var offset = 4;
        if (Month.HasValue)
        {
            utf8Destination[offset++] = (byte)'-';
            utf8Destination[offset++] = (byte)('0' + Month.Value / 10);
            utf8Destination[offset++] = (byte)('0' + Month.Value % 10);

            if (Day.HasValue)
            {
                utf8Destination[offset++] = (byte)'-';
                utf8Destination[offset++] = (byte)('0' + Day.Value / 10);
                utf8Destination[offset++] = (byte)('0' + Day.Value % 10);
            }
        }

        bytesWritten = offset;
        return true;
    }

    /// <summary>
    /// Converts a string to a NullableDateOnly instance.
    /// </summary>
    public static bool TryParse(string? s, out PartialDateOnly result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = new PartialDateOnly(1);
            return false;
        }

        if (s.Length > 10 && DateTime.TryParse(s, out var datetime))
        {
            result = new(datetime);
            return true;
        }

        try
        {
            var trimmed = s.Trim();

            // Try full date: yyyy-MM-dd
            if (trimmed.Length == 10 && trimmed[4] == '-' && trimmed[7] == '-')
            {
                var year = int.Parse(trimmed.Substring(0, 4));
                var month = int.Parse(trimmed.Substring(5, 2));
                var day = int.Parse(trimmed.Substring(8, 2));
                result = new PartialDateOnly(year, month, day);
                return true;
            }

            // Try year-month: yyyy-MM
            if (trimmed.Length == 7 && trimmed[4] == '-')
            {
                var year = int.Parse(trimmed.Substring(0, 4));
                var month = int.Parse(trimmed.Substring(5, 2));
                result = new PartialDateOnly(year, month);
                return true;
            }

            // Try year only: yyyy
            if (trimmed.Length == 4 && int.TryParse(trimmed, out var yearOnly))
            {
                result = new PartialDateOnly(yearOnly);
                return true;
            }
        }
        catch
        {
            result = new PartialDateOnly(1);
            return false;
        }

        result = new PartialDateOnly(1);
        return false;
    }

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out PartialDateOnly result)
    {
        return TryParse(s, out result);
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out PartialDateOnly result)
    {
        return TryParse(s.ToString(), out result);
    }

    /// <summary>
    /// Converts a string to a NullableDateOnly instance.
    /// </summary>
    public static PartialDateOnly Parse(string s)
    {
        if (TryParse(s, out var result))
            return result;

        throw new FormatException($"The string '{s}' is not a valid partial ISO 8601 date format.");
    }

    /// <inheritdoc/>
    public static PartialDateOnly Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"The string '{s}' is not a valid partial ISO 8601 date format.");
    }

    /// <inheritdoc/>
    public static PartialDateOnly Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"The string '{s}' is not a valid partial ISO 8601 date format.");
    }

    /// <summary>
    ///   Converts this instance to a DateOnly value.
    /// </summary>
    public DateOnly ToDateOnly()
    {
        return new DateOnly(Year, Month ?? 1, Day ?? 1);
    }

    /// <summary>
    ///   Converts this instance to a DateTime value.
    /// </summary>
    /// <param name="timeOnly">Optional. TimeOnly to set.</param>
    /// <param name="dateTimeKind">Optional. DateTimeKind to set.</param>
    /// <returns></returns>
    public DateTime ToDateTime(TimeOnly? timeOnly = null, DateTimeKind dateTimeKind = DateTimeKind.Unspecified)
    {
        return ToDateOnly().ToDateTime(timeOnly ?? TimeOnly.MinValue, dateTimeKind);
    }

    /// <summary>
    /// Attempts to convert this instance to a DateOnly value.
    /// </summary>
    public bool TryConvertToDateOnly(out DateOnly date)
    {
        if (IsComplete)
        {
            date = new DateOnly(Year, Month.Value, Day.Value);
            return true;
        }

        date = default;
        return false;
    }

    /// <summary>
    /// Creates a NullableDateOnly from ISO 8601 partial date format (e.g., "2024", "2024-05", "2024-05-15").
    /// </summary>
    public static bool TryParseIso8601(string? s, out PartialDateOnly result)
        => TryParse(s, out result);

    /// <summary>
    /// Creates a NullableDateOnly from ISO 8601 partial date format (e.g., "2024", "2024-05", "2024-05-15").
    /// </summary>
    public static PartialDateOnly ParseIso8601(string s)
        => Parse(s);

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly? left, PartialDateOnly? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly? left, PartialDateOnly? right)
        => !(left == right);

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly? left, PartialDateOnly? right)
    {
        if (left is null)
            return right is not null;

        return left.Value.CompareTo(right) < 0;
    }

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly? left, PartialDateOnly? right)
    {
        if (right is null)
            return left is not null;

        return right.Value.CompareTo(left) < 0;
    }

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly? left, PartialDateOnly? right)
        => !(left > right);

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly? left, PartialDateOnly? right)
        => !(left < right);

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateTime right)
        => left == FromDateTime(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateTime right)
        => !(left == FromDateTime(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateTime right)
        => left < FromDateTime(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateTime right)
        => left > FromDateTime(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateTime right)
        => !(left > FromDateTime(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateTime right)
        => !(left < FromDateTime(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly? left, DateTime right)
        => left == FromDateTime(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly? left, DateTime right)
        => !(left == FromDateTime(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly? left, DateTime right)
        => left < FromDateTime(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly? left, DateTime right)
        => left > FromDateTime(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly? left, DateTime right)
        => !(left > FromDateTime(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly? left, DateTime right)
        => !(left < FromDateTime(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateTime? right)
        => left == FromDateTime(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateTime? right)
        => !(left == FromDateTime(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateTime? right)
        => left < FromDateTime(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateTime? right)
        => left > FromDateTime(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateTime? right)
        => !(left > FromDateTime(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateTime? right)
        => !(left < FromDateTime(right));

    /// <inheritdoc />
    public static bool operator ==(DateTime left, PartialDateOnly right)
        => FromDateTime(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateTime left, PartialDateOnly right)
        => !(FromDateTime(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateTime left, PartialDateOnly right)
        => FromDateTime(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateTime left, PartialDateOnly right)
        => FromDateTime(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateTime left, PartialDateOnly right)
        => !(FromDateTime(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateTime left, PartialDateOnly right)
        => !(FromDateTime(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateTime? left, PartialDateOnly right)
        => FromDateTime(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateTime? left, PartialDateOnly right)
        => !(FromDateTime(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateTime? left, PartialDateOnly right)
        => FromDateTime(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateTime? left, PartialDateOnly right)
        => FromDateTime(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateTime? left, PartialDateOnly right)
        => !(FromDateTime(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateTime? left, PartialDateOnly right)
        => !(FromDateTime(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateTime left, PartialDateOnly? right)
        => FromDateTime(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateTime left, PartialDateOnly? right)
        => !(FromDateTime(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateTime left, PartialDateOnly? right)
        => FromDateTime(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateTime left, PartialDateOnly? right)
        => FromDateTime(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateTime left, PartialDateOnly? right)
        => !(FromDateTime(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateTime left, PartialDateOnly? right)
        => !(FromDateTime(left) < right);

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateOnly right)
        => left == FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateOnly right)
        => !(left == FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateOnly right)
        => left < FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateOnly right)
        => left > FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateOnly right)
        => !(left > FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateOnly right)
        => !(left < FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly? left, DateOnly right)
        => left == FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly? left, DateOnly right)
        => !(left == FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly? left, DateOnly right)
        => left < FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly? left, DateOnly right)
        => left > FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly? left, DateOnly right)
        => !(left > FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly? left, DateOnly right)
        => !(left < FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateOnly? right)
        => left == FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateOnly? right)
        => !(left == FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateOnly? right)
        => left < FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateOnly? right)
        => left > FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateOnly? right)
        => !(left > FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateOnly? right)
        => !(left < FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator ==(DateOnly left, PartialDateOnly right)
        => FromDateOnly(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateOnly left, PartialDateOnly right)
        => !(FromDateOnly(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateOnly left, PartialDateOnly right)
        => FromDateOnly(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateOnly left, PartialDateOnly right)
        => FromDateOnly(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateOnly left, PartialDateOnly right)
        => !(FromDateOnly(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateOnly left, PartialDateOnly right)
        => !(FromDateOnly(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateOnly? left, PartialDateOnly right)
        => FromDateOnly(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateOnly? left, PartialDateOnly right)
        => !(FromDateOnly(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateOnly? left, PartialDateOnly right)
        => FromDateOnly(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateOnly? left, PartialDateOnly right)
        => FromDateOnly(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateOnly? left, PartialDateOnly right)
        => !(FromDateOnly(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateOnly? left, PartialDateOnly right)
        => !(FromDateOnly(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateOnly left, PartialDateOnly? right)
        => FromDateOnly(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateOnly left, PartialDateOnly? right)
        => !(FromDateOnly(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateOnly left, PartialDateOnly? right)
        => FromDateOnly(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateOnly left, PartialDateOnly? right)
        => FromDateOnly(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateOnly left, PartialDateOnly? right)
        => !(FromDateOnly(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateOnly left, PartialDateOnly? right)
        => !(FromDateOnly(left) < right);

    /// <inheritdoc />
    public static TimeSpan operator -(PartialDateOnly left, PartialDateOnly right)
        => left.ToDateTime() - right.ToDateTime();

    /// <inheritdoc />
    public static TimeSpan operator -(DateTime left, PartialDateOnly right)
        => left - right.ToDateTime();

    /// <inheritdoc />
    public static TimeSpan operator -(PartialDateOnly left, DateTime right)
        => left.ToDateTime() - right;

    private static int GetDaysInMonth(int year, int month)
        => month switch
        {
            2 => IsLeapYear(year) ? 29 : 28,
            4 or 6 or 9 or 11 => 30,
            _ => 31
        };

    private static bool IsLeapYear(int year)
        => (year % 4 is 0 && year % 100 is not 0) || (year % 400 is 0);
}
