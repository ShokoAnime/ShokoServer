using System;
using System.ComponentModel;
using System.Globalization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Converts a <see cref="FuzzyDateOnly"/> to and from its ISO 8601 text
///   form, and from a <see cref="PartialDateOnly"/> or a
///   <see cref="DateOnly"/>. Newtonsoft.Json reads dictionary keys with it.
/// </summary>
public sealed class FuzzyDateOnlyTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(PartialDateOnly) || sourceType == typeof(DateOnly)
            || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            string text => FuzzyDateOnly.TryParse(text, out var date) ? date : throw new FormatException($"\"{text}\" is not a valid fuzzy date."),
            PartialDateOnly partialDate => new FuzzyDateOnly(partialDate),
            DateOnly dateOnly => new FuzzyDateOnly(dateOnly),
            _ => base.ConvertFrom(context, culture, value),
        };

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is FuzzyDateOnly date && destinationType == typeof(string)
            ? date.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
