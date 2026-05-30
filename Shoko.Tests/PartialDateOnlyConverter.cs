using System;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;

namespace Shoko.Tests;

public class PartialDateOnlyConverter : JsonConverter<PartialDateOnly?>
{
    public override PartialDateOnly? ReadJson(JsonReader reader, Type objectType, PartialDateOnly? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType == JsonToken.Date && reader.Value is DateTime dt)
            return new PartialDateOnly(dt.Year, dt.Month, dt.Day);

        if (reader.TokenType == JsonToken.String && reader.Value is string s && PartialDateOnly.TryParse(s, null, out var parsed))
            return parsed;

        return null;
    }

    public override void WriteJson(JsonWriter writer, PartialDateOnly? value, JsonSerializer serializer)
    {
        if (value is null) writer.WriteNull();
        else writer.WriteValue(value.Value.ToString());
    }
}
