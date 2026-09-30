using System;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="MetadataEntityType"/> from its value or an alias, or
///   any other valid value as an unregistered kind, and writes its value, for
///   Newtonsoft.Json. Dictionary keys go through
///   <see cref="MetadataEntityTypeTypeConverter"/> instead.
/// </summary>
public class MetadataEntityTypeNewtonsoftJsonConverter : JsonConverter<MetadataEntityType>
{
    /// <inheritdoc/>
    public override MetadataEntityType? ReadJson(JsonReader reader, Type objectType, MetadataEntityType? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
            return null;
        if (reader.TokenType is not JsonToken.String)
            throw new JsonSerializationException($"Expected a string for a metadata entity type, got {reader.TokenType}.");

        var text = (string?)reader.Value;
        return MetadataEntityType.TryParse(text, out var entityType)
            ? entityType
            : throw new JsonSerializationException($"\"{text}\" is not a valid metadata entity type.");
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, MetadataEntityType? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(value.Value);
    }
}
