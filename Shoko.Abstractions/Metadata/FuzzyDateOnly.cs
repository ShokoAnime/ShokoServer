using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Shoko.Abstractions.Metadata.Converters;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A date where the year, the month and the day may each be unknown, such
///   as a birthday known only by its month and day. A day needs a month, and
///   at least one part is set. Written in ISO 8601 as <c>yyyy</c>,
///   <c>yyyy-MM</c>, <c>yyyy-MM-dd</c>, <c>--MM</c> or <c>--MM-dd</c>. The
///   default value has no parts, is not a valid date and writes as nothing.
/// </summary>
[TypeConverter(typeof(FuzzyDateOnlyTypeConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(FuzzyDateOnlyJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(FuzzyDateOnlyNewtonsoftJsonConverter))]
public readonly partial struct FuzzyDateOnly : IComparable<FuzzyDateOnly>, IComparable, IEquatable<FuzzyDateOnly>, IFormattable, IParsable<FuzzyDateOnly>,
    ISpanFormattable, ISpanParsable<FuzzyDateOnly>, IUtf8SpanFormattable
{
    #region Constants

    /// <summary>
    ///   The most characters the text form can have.
    /// </summary>
    public const int MaxLength = 10;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates a date from the parts that are known.
    /// </summary>
    /// <param name="year">The year, from 1 to 9999, or <c>null</c> if unknown.</param>
    /// <param name="month">The month, from 1 to 12, or <c>null</c> if unknown.</param>
    /// <param name="day">
    ///   The day, which needs a month. Without a year, 29 February is valid.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   No part is set, or a day is set without a month.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A part is out of range.</exception>
    public FuzzyDateOnly(int? year, int? month = null, int? day = null)
    {
        if (year is null && month is null && day is null)
            throw new ArgumentException("At least one of the year, the month or the day must be set.", nameof(year));
        if (year is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1 and 9999.");
        if (month is < 1 or > 12)
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
    }

    /// <summary>
    ///   Creates a date from a date that has a year.
    /// </summary>
    /// <param name="date">The date.</param>
    public FuzzyDateOnly(PartialDateOnly date)
    {
        Year = date.Year;
        Month = date.Month;
        Day = date.Day;
    }

    /// <summary>
    ///   Creates a complete date.
    /// </summary>
    /// <param name="date">The date.</param>
    public FuzzyDateOnly(DateOnly date)
    {
        Year = date.Year;
        Month = date.Month;
        Day = date.Day;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   The year, from 1 to 9999, or <c>null</c> if unknown.
    /// </summary>
    public int? Year { get; }

    /// <summary>
    ///   The month, from 1 to 12, or <c>null</c> if unknown.
    /// </summary>
    public int? Month { get; }

    /// <summary>
    ///   The day of the month, or <c>null</c> if unknown. Set only with a
    ///   month.
    /// </summary>
    public int? Day { get; }

    /// <summary>
    ///   Whether the year is known.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Year))]
    public bool HasYear => Year.HasValue;

    /// <summary>
    ///   Whether the year, the month and the day are all known.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Year))]
    [MemberNotNullWhen(true, nameof(Month))]
    [MemberNotNullWhen(true, nameof(Day))]
    public bool IsComplete => Year.HasValue && Month.HasValue && Day.HasValue;

    #endregion

    #region Conversions

    /// <summary>
    ///   Turns a date that has a year into a fuzzy date with the same parts.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The fuzzy date.</returns>
    public static implicit operator FuzzyDateOnly(PartialDateOnly date) => new(date);

    /// <summary>
    ///   Gets the date as a <see cref="PartialDateOnly"/>, which needs a year.
    /// </summary>
    /// <param name="date">The date, or the default value when there is no year.</param>
    /// <returns>Whether the year is known.</returns>
    public bool TryGetPartialDate(out PartialDateOnly date)
    {
        if (Year is { } year)
        {
            date = new(year, Month, Day);
            return true;
        }

        date = default;
        return false;
    }

    /// <summary>
    ///   Gets the date as a <see cref="DateOnly"/>, which needs every part.
    /// </summary>
    /// <param name="date">The date, or the default value when a part is missing.</param>
    /// <returns>Whether the year, the month and the day are all known.</returns>
    public bool TryGetDateOnly(out DateOnly date)
    {
        if (IsComplete)
        {
            date = new(Year.Value, Month.Value, Day.Value);
            return true;
        }

        date = default;
        return false;
    }

    #endregion

    #region Comparison

    /// <summary>
    ///   Compares two dates. Dates with a year come first, by year, then
    ///   month, then day, with a missing part before a known one. Dates
    ///   without a year follow, by month and then day.
    /// </summary>
    /// <param name="other">The date to compare with.</param>
    /// <returns>
    ///   Less than zero if this date sorts first, zero if they are equal, and
    ///   more than zero if it sorts last.
    /// </returns>
    public int CompareTo(FuzzyDateOnly other)
    {
        if (Year.HasValue != other.Year.HasValue)
            return Year.HasValue ? -1 : 1;

        var comparison = ComparePart(Year, other.Year);
        if (comparison is not 0)
            return comparison;

        comparison = ComparePart(Month, other.Month);
        if (comparison is not 0)
            return comparison;

        return ComparePart(Day, other.Day);
    }

    /// <summary>
    ///   Compares this date with another object, which must be a
    ///   <see cref="FuzzyDateOnly"/> or <c>null</c>. <c>null</c> sorts first.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>
    ///   Less than zero if this date sorts first, zero if they are equal, and
    ///   more than zero if it sorts last.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="obj"/> is not a fuzzy date.</exception>
    public int CompareTo(object? obj)
        => obj switch
        {
            null => 1,
            FuzzyDateOnly other => CompareTo(other),
            _ => throw new ArgumentException($"Object must be of type {nameof(FuzzyDateOnly)}.", nameof(obj)),
        };

    private static int ComparePart(int? left, int? right)
    {
        if (left.HasValue != right.HasValue)
            return left.HasValue ? 1 : -1;

        return left.HasValue ? left.Value.CompareTo(right!.Value) : 0;
    }

    #endregion

    #region Equality

    /// <summary>
    ///   Checks whether two dates have the same parts.
    /// </summary>
    /// <param name="other">The date to compare with.</param>
    /// <returns>Whether the year, the month and the day all match.</returns>
    public bool Equals(FuzzyDateOnly other)
        => Year == other.Year && Month == other.Month && Day == other.Day;

    /// <summary>
    ///   Checks whether an object is a date with the same parts.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>Whether the object is a fuzzy date equal to this one.</returns>
    public override bool Equals(object? obj)
        => obj is FuzzyDateOnly other && Equals(other);

    /// <summary>
    ///   Gets a hash code from the parts.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
        => HashCode.Combine(Year, Month, Day);

    #endregion

    #region Formatting

    /// <summary>
    ///   Writes the date in its ISO 8601 form: <c>yyyy</c>, <c>yyyy-MM</c>,
    ///   <c>yyyy-MM-dd</c>, <c>--MM</c> or <c>--MM-dd</c>.
    /// </summary>
    /// <returns>The text form, or an empty string for the default value.</returns>
    public override string ToString()
    {
        Span<char> buffer = stackalloc char[MaxLength];
        var length = Write(buffer);
        return new string(buffer[..length]);
    }

    /// <summary>
    ///   Writes the date in its ISO 8601 form. The format and the provider
    ///   are ignored, as there is only one form.
    /// </summary>
    /// <param name="format">Ignored.</param>
    /// <param name="formatProvider">Ignored.</param>
    /// <returns>The text form, or an empty string for the default value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => ToString();

    /// <summary>
    ///   Writes the date in its ISO 8601 form into a span of characters.
    /// </summary>
    /// <param name="destination">Where to write.</param>
    /// <param name="charsWritten">How many characters were written.</param>
    /// <param name="format">Ignored.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>Whether the span was long enough.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        Span<char> buffer = stackalloc char[MaxLength];
        var length = Write(buffer);
        if (destination.Length < length)
        {
            charsWritten = 0;
            return false;
        }

        buffer[..length].CopyTo(destination);
        charsWritten = length;
        return true;
    }

    /// <summary>
    ///   Writes the date in its ISO 8601 form into a span of UTF-8 bytes.
    /// </summary>
    /// <param name="utf8Destination">Where to write.</param>
    /// <param name="bytesWritten">How many bytes were written.</param>
    /// <param name="format">Ignored.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>Whether the span was long enough.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        Span<char> buffer = stackalloc char[MaxLength];
        var length = Write(buffer);
        if (utf8Destination.Length < length)
        {
            bytesWritten = 0;
            return false;
        }

        for (var index = 0; index < length; index++)
            utf8Destination[index] = (byte)buffer[index];
        bytesWritten = length;
        return true;
    }

    private int Write(Span<char> buffer)
    {
        var offset = 0;
        if (Year is { } year)
        {
            WriteDigits(buffer, ref offset, year, 4);
        }
        else if (Month.HasValue)
        {
            buffer[offset++] = '-';
        }

        if (Month is { } month)
        {
            buffer[offset++] = '-';
            WriteDigits(buffer, ref offset, month, 2);
            if (Day is { } day)
            {
                buffer[offset++] = '-';
                WriteDigits(buffer, ref offset, day, 2);
            }
        }

        return offset;
    }

    private static void WriteDigits(Span<char> buffer, ref int offset, int value, int digits)
    {
        for (var index = digits - 1; index >= 0; index--)
        {
            buffer[offset + index] = (char)('0' + value % 10);
            value /= 10;
        }

        offset += digits;
    }

    #endregion

    #region Parsing

    /// <summary>
    ///   Reads a date from its ISO 8601 form: <c>yyyy</c>, <c>yyyy-MM</c>,
    ///   <c>yyyy-MM-dd</c>, <c>--MM</c> or <c>--MM-dd</c>. A longer text is
    ///   read as a date and time, and keeps the date as written, whatever its
    ///   offset. Surrounding white space is ignored.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="result">The date, or the default value if it could not be read.</param>
    /// <returns>Whether the text held a valid date.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out FuzzyDateOnly result)
    {
        result = default;
        var text = s.Trim();
        if (text.IsEmpty)
            return false;

        if (text.Length > MaxLength)
        {
            // The date is kept as written, never moved by its offset into
            // the local time zone.
            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
                return false;

            result = new(DateOnly.FromDateTime(dateTime.DateTime));
            return true;
        }

        int? year = null, month = null, day = null;
        if (text.StartsWith("--"))
        {
            // --MM or --MM-dd
            if (text.Length is not (4 or 7) || !TryReadDigits(text[2..4], out var yearlessMonth))
                return false;

            month = yearlessMonth;
            if (text.Length is 7)
            {
                if (text[4] is not '-' || !TryReadDigits(text[5..7], out var yearlessDay))
                    return false;

                day = yearlessDay;
            }
        }
        else
        {
            // yyyy, yyyy-MM or yyyy-MM-dd
            if (text.Length is not (4 or 7 or 10) || !TryReadDigits(text[..4], out var fullYear))
                return false;

            year = fullYear;
            if (text.Length >= 7)
            {
                if (text[4] is not '-' || !TryReadDigits(text[5..7], out var datedMonth))
                    return false;

                month = datedMonth;
            }

            if (text.Length is 10)
            {
                if (text[7] is not '-' || !TryReadDigits(text[8..10], out var datedDay))
                    return false;

                day = datedDay;
            }
        }

        if (!IsValid(year, month, day))
            return false;

        result = new(year, month, day);
        return true;
    }

    /// <summary>
    ///   Reads a date from its ISO 8601 form, as
    ///   <see cref="TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/> does.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="result">The date, or the default value if it could not be read.</param>
    /// <returns>Whether the text held a valid date.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out FuzzyDateOnly result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    ///   Reads a date from its ISO 8601 form, as
    ///   <see cref="TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/> does.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The date, or the default value if it could not be read.</param>
    /// <returns>Whether the text held a valid date.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FuzzyDateOnly result)
        => TryParse(s, out result);

    /// <summary>
    ///   Reads a date from its ISO 8601 form, as
    ///   <see cref="TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/> does.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The date, or the default value if it could not be read.</param>
    /// <returns>Whether the text held a valid date.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FuzzyDateOnly result)
        => TryParse(s, out result);

    /// <summary>
    ///   Reads a date from its ISO 8601 form, as
    ///   <see cref="TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/> does.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">The text is not a valid date.</exception>
    public static FuzzyDateOnly Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    ///   Reads a date from its ISO 8601 form, as
    ///   <see cref="TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/> does.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">The text is not a valid date.</exception>
    public static FuzzyDateOnly Parse(string s, IFormatProvider? provider)
        => Parse(s);

    /// <summary>
    ///   Reads a date from its ISO 8601 form, as
    ///   <see cref="TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/> does.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The date.</returns>
    /// <exception cref="FormatException">The text is not a valid date.</exception>
    public static FuzzyDateOnly Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => Parse(s);

    private static FuzzyDateOnly Parse(ReadOnlySpan<char> s)
        => TryParse(s, out var result)
            ? result
            : throw new FormatException($"The string '{s}' is not a valid fuzzy ISO 8601 date.");

    private static bool TryReadDigits(ReadOnlySpan<char> text, out int value)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    #endregion

    #region Validation

    /// <summary>
    ///   Checks whether the parts make a valid fuzzy date, as the constructor
    ///   requires.
    /// </summary>
    /// <param name="year">The year, or <c>null</c> if unknown.</param>
    /// <param name="month">The month, or <c>null</c> if unknown.</param>
    /// <param name="day">The day, or <c>null</c> if unknown.</param>
    /// <returns>Whether a date can be made from the parts.</returns>
    public static bool IsValid(int? year, int? month, int? day)
    {
        if (year is null && month is null && day is null)
            return false;
        if (year is < 1 or > 9999 || month is < 1 or > 12)
            return false;
        if (day is null)
            return true;

        return month is { } knownMonth && day >= 1 && day <= GetDaysInMonth(year, knownMonth);
    }

    private static int GetDaysInMonth(int? year, int month)
        => month switch
        {
            // Without a year, 29 February could be any leap year's.
            2 => year is { } knownYear && !DateTime.IsLeapYear(knownYear) ? 28 : 29,
            4 or 6 or 9 or 11 => 30,
            _ => 31,
        };

    #endregion
}
