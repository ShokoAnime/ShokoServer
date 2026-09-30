using System;
using System.ComponentModel;
using System.Globalization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Converts a <see cref="MetadataGuid"/> to and from its text form. Reads
///   as <see cref="MetadataGuid.Parse(string, IFormatProvider?)"/> does and
///   writes the canonical text form. Newtonsoft.Json reads dictionary keys
///   with it.
/// </summary>
public sealed class MetadataGuidTypeConverter : TypeConverter
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
            ? MetadataGuid.TryParse(text, out var guid) ? guid : throw new FormatException($"\"{text}\" is not a valid metadata identifier.")
            : base.ConvertFrom(context, culture, value);

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is MetadataGuid guid && destinationType == typeof(string)
            ? guid.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
