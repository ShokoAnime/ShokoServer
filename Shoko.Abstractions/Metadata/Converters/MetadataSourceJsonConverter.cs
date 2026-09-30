using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="MetadataSource"/> from its value or an alias, or any
///   other valid value as an unregistered source, and writes
///   its value, for System.Text.Json. Also handles dictionary keys.
/// </summary>
public sealed class MetadataSourceJsonConverter : JsonConverter<MetadataSource>
{
    /// <inheritdoc/>
    public override MetadataSource? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;
        if (reader.TokenType is not JsonTokenType.String)
            throw new JsonException($"Expected a string for a metadata source, got {reader.TokenType}.");

        return MetadataSource.TryParse(reader.GetString(), out var source)
            ? source
            : throw new JsonException($"\"{reader.GetString()}\" is not a valid metadata source.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MetadataSource value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    /// <inheritdoc/>
    public override MetadataSource ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MetadataSource.TryParse(reader.GetString(), out var source)
            ? source
            : throw new JsonException($"\"{reader.GetString()}\" is not a valid metadata source.");

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, MetadataSource value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}
