using System;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="FuzzyDateOnly"/>, or a nullable one, from its ISO 8601
///   text form, as
///   <see cref="FuzzyDateOnly.TryParse(ReadOnlySpan{char}, out FuzzyDateOnly)"/>
///   does, and writes the same form, for Newtonsoft.Json. Dictionary keys go
///   through <see cref="FuzzyDateOnlyTypeConverter"/> instead.
/// </summary>
public class FuzzyDateOnlyNewtonsoftJsonConverter : JsonConverter
{
    /// <summary>
    ///   Checks whether a type is a fuzzy date or a nullable one.
    /// </summary>
    /// <param name="objectType">The type.</param>
    /// <returns>Whether this converter handles it.</returns>
    public override bool CanConvert(Type objectType)
        => objectType == typeof(FuzzyDateOnly) || objectType == typeof(FuzzyDateOnly?);

    /// <summary>
    ///   Reads a fuzzy date from a string, or <c>null</c> for a nullable one.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The type to read.</param>
    /// <param name="existingValue">Ignored.</param>
    /// <param name="serializer">Ignored.</param>
    /// <returns>The date, or <c>null</c>.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The token is not a string holding a valid date, or is <c>null</c>
    ///   for a type that is not nullable.
    /// </exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
            return objectType == typeof(FuzzyDateOnly?)
                ? null
                : throw new JsonSerializationException("Expected a string for a fuzzy date, got Null.");
        if (reader.TokenType is not JsonToken.String)
            throw new JsonSerializationException($"Expected a string for a fuzzy date, got {reader.TokenType}.");

        var text = (string?)reader.Value;
        return FuzzyDateOnly.TryParse(text, out var date)
            ? date
            : throw new JsonSerializationException($"\"{text}\" is not a valid fuzzy date.");
    }

    /// <summary>
    ///   Writes a fuzzy date as its text form, or <c>null</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The date, or <c>null</c>.</param>
    /// <param name="serializer">Ignored.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is FuzzyDateOnly date)
            writer.WriteValue(date.ToString());
        else
            writer.WriteNull();
    }
}
