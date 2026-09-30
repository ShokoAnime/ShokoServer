using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="PartialDateOnly"/> from its ISO 8601 text form, as
///   <see cref="PartialDateOnly.TryParse(string?, out PartialDateOnly)"/>
///   does, and writes <c>yyyy</c>, <c>yyyy-MM</c> or <c>yyyy-MM-dd</c>, for
///   System.Text.Json. Also handles dictionary keys.
/// </summary>
public sealed class PartialDateOnlyJsonConverter : JsonConverter<PartialDateOnly>
{
    /// <inheritdoc/>
    public override PartialDateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
            throw new JsonException($"Expected a string for a partial date, got {reader.TokenType}.");

        return ReadText(reader.GetString());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PartialDateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    /// <inheritdoc/>
    public override PartialDateOnly ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadText(reader.GetString());

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, PartialDateOnly value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    private static PartialDateOnly ReadText(string? text)
        => PartialDateOnly.TryParse(text, out var date)
            ? date
            : throw new JsonException($"\"{text}\" is not a valid partial date.");
}
