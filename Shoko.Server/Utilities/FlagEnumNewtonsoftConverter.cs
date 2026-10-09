using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Shoko.Server.Utilities;

/// <summary>
///   Writes a <c>[Flags]</c> enum for Newtonsoft as a JSON array of its
///   single-bit member names, and reads that array, the comma-separated text
///   or the number.
/// </summary>
/// <remarks>
///   See <see cref="FlagEnums"/> for which members are written and which are
///   only accepted on read.
/// </remarks>
public sealed class FlagEnumNewtonsoftConverter : JsonConverter
{
    /// <summary>
    ///   The shared instance.
    /// </summary>
    public static FlagEnumNewtonsoftConverter Instance { get; } = new();

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
        => FlagEnums.IsFlagEnum(objectType, out _);

    /// <summary>
    ///   Writes the value as an array of member names, or <c>null</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="serializer">Ignored.</param>
    /// <exception cref="JsonSerializationException">
    ///   The value holds bits no single-bit member names.
    /// </exception>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        IReadOnlyList<string> names;
        try
        {
            names = FlagEnums.GetNames(value.GetType(), value, isNewtonsoftJson: true);
        }
        catch (FormatException ex)
        {
            throw new JsonSerializationException(ex.Message, ex);
        }

        writer.WriteStartArray();
        foreach (var name in names)
            writer.WriteValue(name);
        writer.WriteEndArray();
    }

    /// <summary>
    ///   Reads an array of member names, comma-separated text or a number.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The flags enum, or a nullable one.</param>
    /// <param name="existingValue">Ignored.</param>
    /// <param name="serializer">Ignored.</param>
    /// <returns>The value, or <c>null</c> for a JSON null.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The token is of another kind, a name matches no member, the value
    ///   holds bits no single-bit member names, or a null was read for a
    ///   type that cannot hold one.
    /// </exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        FlagEnums.IsFlagEnum(objectType, out var enumType);
        try
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    if (Nullable.GetUnderlyingType(objectType) is null)
                        throw new JsonSerializationException($"Cannot convert null to {enumType.Name}.");
                    return null;
                case JsonToken.Integer:
                    return FlagEnums.FromNumber(enumType, Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture));
                case JsonToken.String:
                    return FlagEnums.FromText(enumType, (string)reader.Value!, isNewtonsoftJson: true);
                case JsonToken.StartArray:
                    var names = new List<string>();
                    while (reader.Read() && reader.TokenType is not JsonToken.EndArray)
                    {
                        if (reader.TokenType is not JsonToken.String)
                            throw new JsonSerializationException($"Expected a member name of {enumType.Name}, got {reader.TokenType}.");
                        names.Add((string)reader.Value!);
                    }

                    return FlagEnums.FromNames(enumType, names, isNewtonsoftJson: true);
                default:
                    throw new JsonSerializationException($"Expected an array of member names of {enumType.Name}, got {reader.TokenType}.");
            }
        }
        catch (FormatException ex)
        {
            throw new JsonSerializationException(ex.Message, ex);
        }
    }
}
