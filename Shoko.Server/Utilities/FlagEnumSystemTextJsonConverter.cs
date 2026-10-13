using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Server.Utilities;

/// <summary>
///   Writes a <c>[Flags]</c> enum for System.Text.Json as a JSON array of its
///   single-bit member names, and reads that array, the comma-separated text
///   or the number.
/// </summary>
/// <remarks>
///   See <see cref="FlagEnums"/> for which members are written and which are
///   only accepted on read. A nullable one is wrapped by System.Text.Json
///   itself.
/// </remarks>
public sealed class FlagEnumSystemTextJsonConverter : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => FlagEnums.IsFlagEnum(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(FlagEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class FlagEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        return (TEnum)FlagEnums.FromNumber(typeof(TEnum), reader.GetInt64());
                    case JsonTokenType.String:
                        return (TEnum)FlagEnums.FromText(typeof(TEnum), reader.GetString()!, isNewtonsoftJson: false);
                    case JsonTokenType.StartArray:
                        var names = new List<string>();
                        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
                        {
                            if (reader.TokenType is not JsonTokenType.String)
                                throw new JsonException($"Expected a member name of {typeof(TEnum).Name}, got {reader.TokenType}.");
                            names.Add(reader.GetString()!);
                        }

                        return (TEnum)FlagEnums.FromNames(typeof(TEnum), names, isNewtonsoftJson: false);
                    default:
                        throw new JsonException($"Expected an array of member names of {typeof(TEnum).Name}, got {reader.TokenType}.");
                }
            }
            catch (FormatException ex)
            {
                throw new JsonException(ex.Message, ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            IReadOnlyList<string> names;
            try
            {
                names = FlagEnums.GetNames(typeof(TEnum), value, isNewtonsoftJson: false);
            }
            catch (FormatException ex)
            {
                throw new JsonException(ex.Message, ex);
            }

            writer.WriteStartArray();
            foreach (var name in names)
                writer.WriteStringValue(name);
            writer.WriteEndArray();
        }
    }
}
