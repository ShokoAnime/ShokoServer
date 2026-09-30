using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="MetadataGuid"/> from its text form, as
///   <see cref="MetadataGuid.Parse(string, IFormatProvider?)"/> does, and
///   writes the canonical text form, for System.Text.Json. Also handles
///   dictionary keys.
/// </summary>
public sealed class MetadataGuidJsonConverter : JsonConverter<MetadataGuid>
{
    /// <inheritdoc/>
    public override MetadataGuid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;
        if (reader.TokenType is not JsonTokenType.String)
            throw new JsonException($"Expected a string for a metadata identifier, got {reader.TokenType}.");

        return MetadataGuid.TryParse(reader.GetString(), out var guid)
            ? guid
            : throw new JsonException($"\"{reader.GetString()}\" is not a valid metadata identifier.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MetadataGuid value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    /// <inheritdoc/>
    public override MetadataGuid ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MetadataGuid.TryParse(reader.GetString(), out var guid)
            ? guid
            : throw new JsonException($"\"{reader.GetString()}\" is not a valid metadata identifier.");

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, MetadataGuid value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());
}
