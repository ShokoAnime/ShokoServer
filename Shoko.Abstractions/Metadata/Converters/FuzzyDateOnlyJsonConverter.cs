using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="FuzzyDateOnly"/> from its ISO 8601 text form, as
///   <see cref="FuzzyDateOnly.TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/>
///   does, and writes the same form, for System.Text.Json. Also handles
///   dictionary keys.
/// </summary>
public sealed class FuzzyDateOnlyJsonConverter : JsonConverter<FuzzyDateOnly>
{
    /// <inheritdoc/>
    public override FuzzyDateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
            throw new JsonException($"Expected a string for a fuzzy date, got {reader.TokenType}.");

        return ReadText(reader.GetString());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FuzzyDateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    /// <inheritdoc/>
    public override FuzzyDateOnly ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadText(reader.GetString());

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, FuzzyDateOnly value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    private static FuzzyDateOnly ReadText(string? text)
        => FuzzyDateOnly.TryParse(text, out var date)
            ? date
            : throw new JsonException($"\"{text}\" is not a valid fuzzy date.");
}
