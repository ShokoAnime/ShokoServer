using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.Converters;

/// <summary>
///   Reads a dictionary keyed by <see cref="MetadataSource"/> for the API,
///   taking the value or an alias of a registered source only as a key,
///   ignoring case. Writing is left to the contract, which sends each key
///   in its old enum spelling (see <see cref="LegacyMetadataSpellings"/>).
/// </summary>
public sealed class MetadataSourceDictionaryConverter : JsonConverter
{
    /// <summary>
    ///   The shared instance.
    /// </summary>
    public static MetadataSourceDictionaryConverter Instance { get; } = new();

    /// <summary>
    ///   Whether this converter writes; it doesn't.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    ///   Checks whether a type is a dictionary this converter reads.
    /// </summary>
    /// <param name="objectType">The type.</param>
    /// <returns><c>true</c> for a <see cref="Dictionary{TKey, TValue}"/> or one of its interfaces, keyed by source.</returns>
    public override bool CanConvert(Type objectType)
        => TryGetValueType(objectType, out _);

    /// <summary>
    ///   Reads the dictionary.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The dictionary type.</param>
    /// <param name="existingValue">Ignored; a new dictionary is made.</param>
    /// <param name="serializer">The serializer, for the values.</param>
    /// <returns>The dictionary, or <c>null</c> for a JSON null.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The JSON is not an object, or a key names no registered source.
    /// </exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
            return null;
        if (reader.TokenType is not JsonToken.StartObject)
            throw new JsonSerializationException($"Expected an object for a dictionary keyed by metadata source, got {reader.TokenType}.");
        if (!TryGetValueType(objectType, out var valueType))
            throw new JsonSerializationException($"{objectType} is not a dictionary keyed by metadata source.");

        var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(MetadataSource), valueType))!;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var source = MetadataSourceValueConverter.ReadRegistered((string?)reader.Value);
                    if (!reader.Read())
                        break;

                    dictionary[source] = serializer.Deserialize(reader, valueType);
                    continue;
                case JsonToken.Comment:
                    continue;
                case JsonToken.EndObject:
                    return dictionary;
            }

            break;
        }

        throw new JsonSerializationException("Unexpected end of a dictionary keyed by metadata source.");
    }

    /// <summary>
    ///   Not supported; <see cref="CanWrite"/> is <c>false</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="serializer">The serializer.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => throw new NotSupportedException();

    /// <summary>
    ///   Gets the value type of a dictionary keyed by source.
    /// </summary>
    /// <param name="objectType">The type.</param>
    /// <param name="valueType">The value type, if the type is one.</param>
    /// <returns>
    ///   <c>true</c> for a <see cref="Dictionary{TKey, TValue}"/>,
    ///   <see cref="IDictionary{TKey, TValue}"/> or
    ///   <see cref="IReadOnlyDictionary{TKey, TValue}"/> keyed by source.
    /// </returns>
    internal static bool TryGetValueType(Type objectType, [NotNullWhen(true)] out Type? valueType)
    {
        valueType = null;
        if (!objectType.IsGenericType || objectType.GenericTypeArguments[0] != typeof(MetadataSource))
            return false;

        var definition = objectType.GetGenericTypeDefinition();
        if (definition != typeof(Dictionary<,>) && definition != typeof(IDictionary<,>) && definition != typeof(IReadOnlyDictionary<,>))
            return false;

        valueType = objectType.GenericTypeArguments[1];
        return true;
    }
}
