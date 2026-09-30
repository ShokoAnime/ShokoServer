using System;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Metadata.Converters;

/// <summary>
///   Reads a <see cref="PartialDateOnly"/>, or a nullable one, from its ISO
///   8601 text form, as
///   <see cref="PartialDateOnly.TryParse(string?, out PartialDateOnly)"/>
///   does, and writes <c>yyyy</c>, <c>yyyy-MM</c> or <c>yyyy-MM-dd</c>, for
///   Newtonsoft.Json.
/// </summary>
/// <remarks>
///   A dictionary key is written as the same text but cannot be read back:
///   the struct is <see cref="IConvertible"/>, and Newtonsoft.Json tries
///   <see cref="Convert.ChangeType(object, Type)"/> on a key first.
/// </remarks>
public class PartialDateOnlyNewtonsoftJsonConverter : JsonConverter
{
    /// <summary>
    ///   Checks whether a type is a partial date or a nullable one.
    /// </summary>
    /// <param name="objectType">The type.</param>
    /// <returns>Whether this converter handles it.</returns>
    public override bool CanConvert(Type objectType)
        => objectType == typeof(PartialDateOnly) || objectType == typeof(PartialDateOnly?);

    /// <summary>
    ///   Reads a partial date from a string, or from a date the reader already
    ///   parsed, or <c>null</c> for a nullable one.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The type to read.</param>
    /// <param name="existingValue">Ignored.</param>
    /// <param name="serializer">Ignored.</param>
    /// <returns>The date, or <c>null</c>.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The token is not a date or a string holding a valid date, or is
    ///   <c>null</c> for a type that is not nullable.
    /// </exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                return objectType == typeof(PartialDateOnly?)
                    ? null
                    : throw new JsonSerializationException("Expected a string for a partial date, got Null.");

            // A reader that parses dates hands a full date over already parsed.
            case JsonToken.Date when reader.Value is DateTime dateTime:
                return new PartialDateOnly(dateTime);

            case JsonToken.Date when reader.Value is DateTimeOffset dateTimeOffset:
                return new PartialDateOnly(dateTimeOffset.DateTime);

            case JsonToken.String:
                var text = (string?)reader.Value;
                return PartialDateOnly.TryParse(text, out var date)
                    ? date
                    : throw new JsonSerializationException($"\"{text}\" is not a valid partial date.");

            default:
                throw new JsonSerializationException($"Expected a string for a partial date, got {reader.TokenType}.");
        }
    }

    /// <summary>
    ///   Writes a partial date as its text form, or <c>null</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The date, or <c>null</c>.</param>
    /// <param name="serializer">Ignored.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is PartialDateOnly date)
            writer.WriteValue(date.ToString());
        else
            writer.WriteNull();
    }
}
