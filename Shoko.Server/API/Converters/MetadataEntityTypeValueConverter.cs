using System;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Converters;

namespace Shoko.Server.API.Converters;

/// <summary>
///   Writes a <see cref="MetadataEntityType"/> for the API as the old enum
///   spelled it, through <see cref="LegacyMetadataSpellings"/>, and reads the
///   value or an alias of a registered entity type only, ignoring case, or of
///   any entity type for settings.
/// </summary>
public sealed class MetadataEntityTypeValueConverter : MetadataEntityTypeNewtonsoftJsonConverter
{
    private readonly bool _registeredOnly;

    private MetadataEntityTypeValueConverter(bool registeredOnly)
    {
        _registeredOnly = registeredOnly;
    }

    /// <summary>
    ///   The shared instance, which reads registered entity types only.
    /// </summary>
    public static MetadataEntityTypeValueConverter Instance { get; } = new(registeredOnly: true);

    /// <summary>
    ///   The shared instance for settings, which also reads an entity type
    ///   whose plugin isn't loaded, as the settings file itself does.
    /// </summary>
    public static MetadataEntityTypeValueConverter Lenient { get; } = new(registeredOnly: false);

    /// <summary>
    ///   Reads an entity type, refusing text that names no registered entity
    ///   type unless this is the <see cref="Lenient"/> converter.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The type to read.</param>
    /// <param name="existingValue">Ignored.</param>
    /// <param name="hasExistingValue">Ignored.</param>
    /// <param name="serializer">Ignored.</param>
    /// <returns>The entity type, or <c>null</c> for a JSON null.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The token is not a string, or it names no entity type this converter
    ///   takes.
    /// </exception>
    public override MetadataEntityType? ReadJson(JsonReader reader, Type objectType, MetadataEntityType? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (!_registeredOnly)
            return base.ReadJson(reader, objectType, existingValue, hasExistingValue, serializer);
        if (reader.TokenType is JsonToken.Null)
            return null;
        if (reader.TokenType is not JsonToken.String)
            throw new JsonSerializationException($"Expected a string for a metadata entity type, got {reader.TokenType}.");

        return ReadRegistered((string?)reader.Value);
    }

    /// <summary>
    ///   Writes an entity type as its old spelling, or its value when the old
    ///   enum lacked it.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The entity type, or <c>null</c>.</param>
    /// <param name="serializer">Ignored.</param>
    public override void WriteJson(JsonWriter writer, MetadataEntityType? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(LegacyMetadataSpellings.Of(value));
    }

    /// <summary>
    ///   Gets a registered entity type by its value or an alias, ignoring case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The registered entity type.</returns>
    /// <exception cref="JsonSerializationException">
    ///   No entity type is registered under <paramref name="text"/>.
    /// </exception>
    internal static MetadataEntityType ReadRegistered(string? text)
        => MetadataEntityType.TryGet(text, out var entityType)
            ? entityType
            : throw new JsonSerializationException($"\"{text}\" is not a registered metadata entity type.");
}
