using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Filtering.Expressions.Converters;

/// <summary>
///   Reads a string set parameter of a saved filter from a JSON array, and
///   <c>null</c> as <c>null</c>, for Newtonsoft.Json, which cannot create a
///   read-only set on its own. Writes the set as an array.
/// </summary>
internal sealed class StringSetNewtonsoftJsonConverter : JsonConverter<IReadOnlySet<string>?>
{
    /// <inheritdoc/>
    public override IReadOnlySet<string>? ReadJson(JsonReader reader, Type objectType, IReadOnlySet<string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        => reader.TokenType is JsonToken.Null ? null : serializer.Deserialize<HashSet<string>>(reader);

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, IReadOnlySet<string>? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteValue(item);
        writer.WriteEndArray();
    }
}
