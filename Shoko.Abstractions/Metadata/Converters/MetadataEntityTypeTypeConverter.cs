using System;
using System.ComponentModel;
using System.Globalization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Converts a <see cref="MetadataEntityType"/> to and from text. Reads a
///   value or alias, or any other valid value as an unregistered kind, and
///   writes the value. Newtonsoft.Json reads dictionary keys with it,
///   settings included, so it stays as lenient as parsing; the server binds
///   API input through a stricter binder of its own.
/// </summary>
public sealed class MetadataEntityTypeTypeConverter : TypeConverter
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
            ? MetadataEntityType.TryParse(text, out var entityType) ? entityType : throw new FormatException($"\"{text}\" is not a valid metadata entity type.")
            : base.ConvertFrom(context, culture, value);

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is MetadataEntityType entityType && destinationType == typeof(string)
            ? entityType.Value
            : base.ConvertTo(context, culture, value, destinationType);
}
