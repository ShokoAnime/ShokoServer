using System;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="MetadataGuid"/> from its text form, as
///   <see cref="MetadataGuid.Parse(string, IFormatProvider?)"/> does, and
///   writes the canonical text form, for Newtonsoft.Json. Dictionary keys go
///   through <see cref="MetadataGuidTypeConverter"/> instead.
/// </summary>
public class MetadataGuidNewtonsoftJsonConverter : JsonConverter<MetadataGuid>
{
    /// <inheritdoc/>
    public override MetadataGuid? ReadJson(JsonReader reader, Type objectType, MetadataGuid? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
            return null;
        if (reader.TokenType is not JsonToken.String)
            throw new JsonSerializationException($"Expected a string for a metadata identifier, got {reader.TokenType}.");

        var text = (string?)reader.Value;
        return MetadataGuid.TryParse(text, out var guid)
            ? guid
            : throw new JsonSerializationException($"\"{text}\" is not a valid metadata identifier.");
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, MetadataGuid? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(value.ToString());
    }
}
