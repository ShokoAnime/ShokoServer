using System;
using System.ComponentModel;
using System.Globalization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Converts a <see cref="PartialDateOnly"/> to and from its ISO 8601 text
///   form, and from a <see cref="DateOnly"/> or a <see cref="DateTime"/>.
/// </summary>
public sealed class PartialDateOnlyTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(DateOnly) || sourceType == typeof(DateTime)
            || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            string text => PartialDateOnly.TryParse(text, out var date) ? date : throw new FormatException($"\"{text}\" is not a valid partial date."),
            DateOnly dateOnly => new PartialDateOnly(dateOnly),
            DateTime dateTime => new PartialDateOnly(dateTime),
            _ => base.ConvertFrom(context, culture, value),
        };

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is PartialDateOnly date && destinationType == typeof(string)
            ? date.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
