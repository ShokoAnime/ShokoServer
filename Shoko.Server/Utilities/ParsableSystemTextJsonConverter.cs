using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Server.Utilities;

/// <summary>
///   Writes a type parsable from text for System.Text.Json as a JSON string,
///   and reads it back from one.
/// </summary>
/// <remarks>
///   See <see cref="ParsableTypes"/> for which types it takes and how the
///   text is written and read. A nullable one is wrapped by System.Text.Json
///   itself.
/// </remarks>
public sealed class ParsableSystemTextJsonConverter : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => ParsableTypes.IsConverted(typeToConvert, isNewtonsoftJson: false, out var parsableType) && parsableType == typeToConvert;

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(ParsableConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class ParsableConverter<T> : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException($"Expected the text of a {typeof(T).Name}, got {reader.TokenType}.");

            try
            {
                return (T)ParsableTypes.FromText(typeof(T), reader.GetString()!);
            }
            catch (FormatException ex)
            {
                throw new JsonException(ex.Message, ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(ParsableTypes.ToText(value!));
    }
}
