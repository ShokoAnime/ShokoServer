using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="MetadataEntityType"/> from its value or an alias, or
///   any other valid value as an unregistered kind, and writes its value, for
///   System.Text.Json. Also handles dictionary keys.
/// </summary>
public sealed class MetadataEntityTypeJsonConverter : JsonConverter<MetadataEntityType>
{
    /// <inheritdoc/>
    public override MetadataEntityType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;
        if (reader.TokenType is not JsonTokenType.String)
            throw new JsonException($"Expected a string for a metadata entity type, got {reader.TokenType}.");

        return MetadataEntityType.TryParse(reader.GetString(), out var entityType)
            ? entityType
            : throw new JsonException($"\"{reader.GetString()}\" is not a valid metadata entity type.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MetadataEntityType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    /// <inheritdoc/>
    public override MetadataEntityType ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MetadataEntityType.TryParse(reader.GetString(), out var entityType)
            ? entityType
            : throw new JsonException($"\"{reader.GetString()}\" is not a valid metadata entity type.");

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, MetadataEntityType value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}
