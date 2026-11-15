using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Shoko.Server.Utilities;

/// <summary>
///   Reads a whole number that may be written as a float, such as the
///   <c>100.0</c> MySQL and MariaDB write for a number a JSON function
///   computed, and writes it as a plain integer.
/// </summary>
public sealed class WholeNumberJsonConverter : JsonConverter<int>
{
    /// <inheritdoc />
    public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
        => reader.TokenType switch
        {
            JsonToken.Integer => Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture),
            JsonToken.Float => (int)Math.Round(Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture), MidpointRounding.AwayFromZero),
            JsonToken.String when double.TryParse((string?)reader.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                => (int)Math.Round(number, MidpointRounding.AwayFromZero),
            JsonToken.Null => existingValue,
            _ => throw new JsonSerializationException($"Expected a number, got {reader.TokenType}."),
        };

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer)
        => writer.WriteValue(value);
}
