using System;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="MetadataSource"/> from its value or an alias, or any
///   other valid value as an unregistered source, and writes
///   its value, for Newtonsoft.Json. Dictionary keys go through
///   <see cref="MetadataSourceTypeConverter"/> instead.
/// </summary>
public class MetadataSourceNewtonsoftJsonConverter : JsonConverter<MetadataSource>
{
    /// <inheritdoc/>
    public override MetadataSource? ReadJson(JsonReader reader, Type objectType, MetadataSource? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
            return null;
        if (reader.TokenType is not JsonToken.String)
            throw new JsonSerializationException($"Expected a string for a metadata source, got {reader.TokenType}.");

        var text = (string?)reader.Value;
        return MetadataSource.TryParse(text, out var source)
            ? source
            : throw new JsonSerializationException($"\"{text}\" is not a valid metadata source.");
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, MetadataSource? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(GetText(value));
    }

    /// <summary>
    ///   Gets the text written for a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>Its <see cref="MetadataSource.Value"/>.</returns>
    protected virtual string GetText(MetadataSource source)
        => source.Value;
}
