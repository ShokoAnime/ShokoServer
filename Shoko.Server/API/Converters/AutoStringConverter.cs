using System;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.API.Converters;

/// <summary>
/// Automatically converts JSON values to a string.
/// </summary>
public class AutoStringConverter : JsonConverter<string>
{
    public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        switch (reader.Value)
        {
            case null:
                return null;
            case int number:
                return number.ToString(CultureInfo.InvariantCulture);
            case long number:
                return number.ToString(CultureInfo.InvariantCulture);
            case BigInteger number:
                return number.ToString(CultureInfo.InvariantCulture);
            case float number:
                return number.ToString(CultureInfo.InvariantCulture);
            case double number:
                return number.ToString(CultureInfo.InvariantCulture);
            case decimal number:
                return number.ToString(CultureInfo.InvariantCulture);
            case string str:
                return str;
            case bool boolean:
                return boolean.ToString();
            case DateTime date:
                return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            case Guid guid:
                return guid.ToString();
            case TimeSpan timeSpan:
                return timeSpan.ToString();
            default:
            {
                var token = JToken.Load(reader);
                return token.ToString(Formatting.None);
            }
        }
    }

    public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }
}
