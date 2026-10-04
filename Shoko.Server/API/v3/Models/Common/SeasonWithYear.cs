using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.Common;

// The type converter is for query strings; JsonObject keeps the JSON an
// object, where Newtonsoft would otherwise write it as the converter's string.
[JsonObject]
[TypeConverter(typeof(SeasonWithYearTypeConverter))]
public class SeasonWithYear(int year, YearlySeason animeSeason) : IComparable<SeasonWithYear>, IEquatable<SeasonWithYear>
{
    [Required]
    public int Year { get; } = year;

    [Required, JsonConverter(typeof(StringEnumConverter))]
    public YearlySeason AnimeSeason { get; } = animeSeason;

    public int CompareTo(SeasonWithYear? other)
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

    public bool Equals(SeasonWithYear? other)
        => other is not null && Year == other.Year && AnimeSeason == other.AnimeSeason;

    public override bool Equals(object? obj)
        => obj is SeasonWithYear other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Year, AnimeSeason);

    /// <summary>
    ///   The season in its query form, <c>{Season} {Year}</c>, such as
    ///   <c>Fall 2026</c>.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
        => $"{AnimeSeason} {Year}";

    /// <summary>
    ///   Reads a season in its query form, <c>{Season} {Year}</c>, such as
    ///   <c>Fall 2026</c>, the season's name in any case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="season">The season, when the text is one.</param>
    /// <returns><c>true</c> when the text is a season.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SeasonWithYear? season)
    {
        season = null;
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 } || parts[0].Length is 0 || char.IsAsciiDigit(parts[0][0]))
            return false;

        if (!Enum.TryParse<YearlySeason>(parts[0], ignoreCase: true, out var yearlySeason) || !Enum.IsDefined(yearlySeason))
            return false;

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1900 or > 9999)
            return false;

        season = new(year, yearlySeason);
        return true;
    }
}

/// <summary>
///   Converts a <see cref="SeasonWithYear"/> to and from its query form,
///   <c>{Season} {Year}</c>, such as <c>Fall 2026</c>.
/// </summary>
public sealed class SeasonWithYearTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string text
            ? SeasonWithYear.TryParse(text, out var season) ? season : throw new FormatException($"\"{text}\" is not a season; use \"{{Season}} {{Year}}\", like \"Fall 2026\".")
            : base.ConvertFrom(context, culture, value);

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is SeasonWithYear season && destinationType == typeof(string)
            ? season.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
