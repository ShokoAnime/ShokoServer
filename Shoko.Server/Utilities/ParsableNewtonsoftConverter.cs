using System;
using Newtonsoft.Json;

namespace Shoko.Server.Utilities;

/// <summary>
///   Writes a type parsable from text for Newtonsoft as a JSON string, and
///   reads it back from one.
/// </summary>
/// <remarks>
///   See <see cref="ParsableTypes"/> for which types it takes and how the
///   text is written and read.
/// </remarks>
public sealed class ParsableNewtonsoftConverter : JsonConverter
{
    /// <summary>
    ///   The shared instance.
    /// </summary>
    public static ParsableNewtonsoftConverter Instance { get; } = new();

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
        => ParsableTypes.IsConverted(objectType, isNewtonsoftJson: true, out _);

    /// <summary>
    ///   Writes the value as its text, or <c>null</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="serializer">Ignored.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(ParsableTypes.ToText(value));
    }

    /// <summary>
    ///   Reads the value from its text.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The parsable type, or a nullable one.</param>
    /// <param name="existingValue">Ignored.</param>
    /// <param name="serializer">Ignored.</param>
    /// <returns>The value, or <c>null</c> for a JSON null.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The token is not a string, the text does not parse, or a null was
    ///   read for a value type that cannot hold one.
    /// </exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        ParsableTypes.IsConverted(objectType, isNewtonsoftJson: true, out var parsableType);
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                if (objectType.IsValueType && Nullable.GetUnderlyingType(objectType) is null)
                    throw new JsonSerializationException($"Cannot convert null to {parsableType.Name}.");
                return null;
            case JsonToken.String:
                try
                {
                    return ParsableTypes.FromText(parsableType, (string)reader.Value!);
                }
                catch (FormatException ex)
                {
                    throw new JsonSerializationException(ex.Message, ex);
                }
            default:
                throw new JsonSerializationException($"Expected the text of a {parsableType.Name}, got {reader.TokenType}.");
        }
    }
}
