using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Shoko.Tests;

public class IReadOnlySetConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        if (!objectType.IsGenericType) return false;
        var arguments = objectType.GetGenericArguments();
        if (arguments.Length > 1) return false;
        return typeof(IReadOnlySet<>) == objectType.GetGenericTypeDefinition();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var argument = objectType.GetGenericArguments()[0];
        var newType = typeof(HashSet<>).MakeGenericType(argument);
        return serializer.Deserialize(reader, newType);
    }
}
