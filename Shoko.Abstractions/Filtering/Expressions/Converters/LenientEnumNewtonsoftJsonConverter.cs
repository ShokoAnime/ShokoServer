using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Filtering.Expressions.Converters;

/// <summary>
///   Reads an enum parameter of a saved filter from its name, ignoring case
///   and underscores, or from its number, and writes its name, for
///   Newtonsoft.Json. A value it can't place becomes the enum's default, so a
///   filter saved by an older version, with names since renamed or removed,
///   still loads.
/// </summary>
internal sealed class LenientEnumNewtonsoftJsonConverter : JsonConverter
{
    /// <inheritdoc/>
    public override bool CanConvert(Type objectType)
        => (Nullable.GetUnderlyingType(objectType) ?? objectType).IsEnum;

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var underlyingType = Nullable.GetUnderlyingType(objectType);
        var enumType = underlyingType ?? objectType;
        var fallback = underlyingType is null ? Activator.CreateInstance(enumType) : null;
        object? value;
        switch (reader.TokenType)
        {
            case JsonToken.Integer:
                value = Enum.ToObject(enumType, Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture));
                break;

            case JsonToken.String:
                var text = ((string?)reader.Value ?? string.Empty).Replace("_", string.Empty);
                value = Enum.TryParse(enumType, text, ignoreCase: true, out var parsed) ? parsed : null;
                break;

            default:
                reader.Skip();
                return fallback;
        }

        return value is not null && Enum.IsDefined(enumType, value) ? value : fallback;
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(value.ToString());
    }
}
